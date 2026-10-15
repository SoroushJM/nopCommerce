[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Madadrang', 'MishaShop', 'Boutique')][string]$Theme,
    [Parameter(Mandatory)][string]$Image,
    [Parameter(Mandatory)][ValidateRange(1, [long]::MaxValue)][long]$ImageSize,
    [Parameter(Mandatory)][string]$ArchivePath,
    [Parameter(Mandatory)][string]$ConfigPath,
    [Parameter(Mandatory)][string]$ReceiptPath,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$SourceCommit,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$SourceHash,
    [ValidateRange(30, 1800)][int]$StartupTimeout = 180
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion -lt [version]'7.4') { throw 'PowerShell 7.4 or newer is required.' }
$Theme = if ($Theme -in @('MishaShop', 'Boutique')) { 'MishaShop' } else { 'Madadrang' }

# The bootstrap loads pinned .NET libraries, not a separate scripting runtime.
. (Join-Path $PSScriptRoot 'Import-StorefrontSsh.ps1')
Import-StorefrontSsh

function ConvertTo-ShellArgument {
    param([AllowEmptyString()][string]$Value)
    $quote = [string][char]39
    $doubleQuote = [string][char]34
    $escapedQuote = $quote + $doubleQuote + $quote + $doubleQuote + $quote
    return $quote + $Value.Replace($quote, $escapedQuote) + $quote
}

function Join-ShellArguments {
    param([string[]]$Arguments)
    return ($Arguments | ForEach-Object { ConvertTo-ShellArgument $_ }) -join ' '
}

function Resolve-PrivatePath {
    param([string]$Path)
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $ConfigPath) $Path))
}

function Get-PinnedHostKeys {
    param([string]$KnownHostsPath, [string]$Server, [int]$Port)
    $hostToken = if ($Port -eq 22) { $Server } else { '[{0}]:{1}' -f $Server, $Port }
    $pins = [Collections.Generic.List[string]]::new()
    foreach ($line in [IO.File]::ReadLines($KnownHostsPath)) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
        $fields = $line.Trim() -split '\s+'
        $marker = ''
        if ($fields[0].StartsWith('@')) { $marker = $fields[0]; $fields = $fields[1..($fields.Length - 1)] }
        if ($fields.Length -lt 3) { continue }
        $matchesHost = $false
        foreach ($entry in ($fields[0] -split ',')) {
            if ($entry -ceq $hostToken) { $matchesHost = $true }
            elseif ($entry.StartsWith('|1|')) {
                $hashed = $entry -split '\|'
                if ($hashed.Length -ne 4) { continue }
                $hmac = [Security.Cryptography.HMACSHA1]::new([Convert]::FromBase64String($hashed[2]))
                try {
                    $actual = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($hostToken))
                    if ([Convert]::ToBase64String($actual) -ceq $hashed[3]) { $matchesHost = $true }
                }
                finally { $hmac.Dispose() }
            }
        }
        if (!$matchesHost) { continue }
        if ($marker) { throw 'A host-key marker requires manual review; only explicit pinned host keys are accepted.' }
        $keyBytes = [Convert]::FromBase64String($fields[2])
        if ($keyBytes.Length -lt 8) { throw 'Invalid pinned host-key blob.' }
        $algorithmLength = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($keyBytes, 0))
        if ($algorithmLength -le 0 -or $algorithmLength -gt $keyBytes.Length - 4) { throw 'Invalid pinned key algorithm.' }
        $algorithm = [Text.Encoding]::ASCII.GetString($keyBytes, 4, $algorithmLength)
        if ($algorithm -cne $fields[1]) { throw 'Pinned host-key algorithm does not match its blob.' }
        $pins.Add([Convert]::ToBase64String($keyBytes))
    }
    if (!$pins.Count) { throw 'No explicit known_hosts key matches the configured host and port.' }
    return $pins.ToArray()
}

function Initialize-HostKeyCallback {
    # SSH.NET raises this event on its transport thread. A small .NET callback
    # avoids invoking PowerShell scriptblocks without a runspace on that thread.
    if ('StorefrontDeployment.HostKeyGuard' -as [type]) { return }
    $callback = @'
using System;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace StorefrontDeployment
{
    public sealed class HostKeyGuard
    {
        private readonly string[] allowedKeys;
        public HostKeyGuard(string[] keys)
        {
            allowedKeys = (string[])keys.Clone();
        }
        private void Validate(object sender, HostKeyEventArgs args)
        {
            args.CanTrust = false;
            string receivedKey = Convert.ToBase64String(args.HostKey);
            foreach (string allowedKey in allowedKeys)
            {
                if (String.Equals(allowedKey, receivedKey, StringComparison.Ordinal))
                {
                    args.CanTrust = true;
                    return;
                }
            }
        }
        public void Attach(BaseClient client)
        {
            client.HostKeyReceived += Validate;
        }
    }
}
'@
    # The pinned net8.0 library also runs on newer PowerShell .NET hosts.
    # Suppress only the compiler's expected assembly-version-unification warning.
    Add-Type -TypeDefinition $callback -ReferencedAssemblies ([Renci.SshNet.SshClient].Assembly.Location) -CompilerOptions '/nowarn:1701'
}

function New-ConnectionInfo {
    param([hashtable]$Settings)
    $port = if ($Settings.ContainsKey('port')) { [int]$Settings.port } else { 22 }
    if ($Settings.keyFilename) {
        $keyFile = [Renci.SshNet.PrivateKeyFile]::new((Resolve-PrivatePath $Settings.keyFilename))
        $script:privateKeyFile = $keyFile
        $authentication = [Renci.SshNet.PrivateKeyAuthenticationMethod]::new($Settings.username, @($keyFile))
    }
    else {
        $handoff = [IO.File]::ReadAllText((Resolve-PrivatePath $Settings.handoffPath))
        $passwordMatch = [regex]::Match($handoff, '(?m)^Password: (.+)$')
        if (!$passwordMatch.Success) { throw 'The private handoff does not contain the configured SSH authentication.' }
        $authentication = [Renci.SshNet.PasswordAuthenticationMethod]::new($Settings.username, $passwordMatch.Groups[1].Value.Trim())
        $handoff = $null
    }
    $connection = [Renci.SshNet.ConnectionInfo]::new($Settings.host, $port, $Settings.username, @($authentication))
    $connection.Timeout = [TimeSpan]::FromSeconds(20)
    return $connection
}

function Assert-DeploymentLock {
    if (!$script:ssh.IsConnected -or !$script:lockTask -or $script:lockTask.IsCompleted) {
        throw 'The SSH deployment lock is no longer held; refusing another remote operation.'
    }
}

function Invoke-RemoteCommand {
    param(
        [string[]]$Arguments,
        [string]$Purpose,
        [int]$TimeoutSeconds = 30,
        [switch]$AllowFailure
    )
    Assert-DeploymentLock
    $quotedArguments = Join-ShellArguments $Arguments
    $quotedRoot = ConvertTo-ShellArgument $script:remoteRoot
    $commandText = 'cd -- {0} && timeout --signal=TERM --kill-after=10s {1}s {2}' -f `
        $quotedRoot, $TimeoutSeconds, $quotedArguments
    $command = $script:ssh.CreateCommand($commandText)
    $command.CommandTimeout = [TimeSpan]::FromSeconds($TimeoutSeconds + 20)
    try {
        try { $output = $command.Execute() }
        catch { throw "Remote operation did not finish within its bounds: $Purpose." }
        $exitStatus = $command.ExitStatus
        if ($null -eq $exitStatus -or ($exitStatus -ne 0 -and !$AllowFailure)) {
            # Provider stdout/stderr may include environment secrets; never echo it.
            throw "Remote operation failed: $Purpose (exit $exitStatus)."
        }
        return @{ Output = $output; ExitStatus = $exitStatus }
    }
    finally { $command.Dispose() }
}

function Start-DeploymentLock {
    $lockFile = ConvertTo-ShellArgument ($script:remoteRoot + '/.deploy.lock')
    $lockBody = @"
exec 9>$lockFile
flock -n 9 || exit 73
printf 'LOCKED\n'
read -r release
"@
    $script:lockCommand = $script:ssh.CreateCommand('sh -c ' + (ConvertTo-ShellArgument $lockBody))
    $script:lockCommand.CommandTimeout = [TimeSpan]::FromHours(3)
    $script:lockTask = $script:lockCommand.ExecuteAsync()
    $script:lockInput = $script:lockCommand.CreateInputStream()
    $script:lockReader = [IO.StreamReader]::new($script:lockCommand.OutputStream)
    $ready = $script:lockReader.ReadLineAsync()
    if (!$ready.Wait([TimeSpan]::FromSeconds(20)) -or $ready.Result -cne 'LOCKED') {
        throw 'Another deployment holds the server lock, or lock acquisition failed.'
    }
    Assert-DeploymentLock
}

function Get-ComposeArguments {
    param([string]$AdditionalOverride)
    $arguments = @('docker', 'compose', '-f', "$script:remoteRoot/compose.yml", '-f', "$script:remoteRoot/images.override.yml")
    if ($AdditionalOverride) { $arguments += @('-f', $AdditionalOverride) }
    return $arguments
}

function Get-ImageInformation {
    param([string]$Reference)
    $result = Invoke-RemoteCommand -Arguments @('docker', 'image', 'inspect', $Reference) -Purpose 'read image identity'
    $images = @($result.Output | ConvertFrom-Json -AsHashtable -Depth 100)
    if ($images.Count -ne 1) { throw 'Expected exactly one image identity.' }
    return $images[0]
}

function Get-ServiceState {
    param([string]$Service)
    $arguments = (Get-ComposeArguments) + @('ps', '--all', '-q', $Service)
    $result = Invoke-RemoteCommand -Arguments $arguments -Purpose "read $Service container reference"
    $containerIds = @($result.Output.Trim() -split '\r?\n' | Where-Object { $_ })
    if ($containerIds.Count -ne 1) { throw "Expected one existing $Service container." }
    $result = Invoke-RemoteCommand -Arguments @('docker', 'inspect', $containerIds[0]) -Purpose "read $Service container metadata"
    $container = @($result.Output | ConvertFrom-Json -AsHashtable -Depth 100)[0]
    return [ordered]@{
        Id = $container.Id
        ImageId = $container.Image
        Image = $container.Config.Image
        Running = $container.State.Running
        StartedAt = $container.State.StartedAt
        RestartCount = $container.RestartCount
    }
}

function Assert-ProtectedServices {
    foreach ($service in @($script:otherStore, 'database')) {
        $actual = Get-ServiceState $service | ConvertTo-Json -Compress
        $expected = $script:before[$service] | ConvertTo-Json -Compress
        if ($actual -cne $expected) { throw "Protected service metadata changed: $service." }
    }
}

function Get-BytesHash {
    param([byte[]]$Bytes)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Read-TarJsonEntry {
    param([string]$EntryName, [long]$MaximumBytes)
    $stream = [IO.File]::OpenRead($ArchivePath)
    $reader = [System.Formats.Tar.TarReader]::new($stream, $false)
    $found = $null
    $expandedBytes = 0L
    try {
        while ($entry = $reader.GetNextEntry()) {
            if ($entry.DataStream) { $expandedBytes += $entry.Length }
            if ($entry.Name -cne $EntryName) { continue }
            if ($found -or !$entry.DataStream -or $entry.Length -gt $MaximumBytes) {
                throw 'The saved-image metadata entry is duplicated, invalid or too large.'
            }
            $memory = [IO.MemoryStream]::new()
            try {
                $entry.DataStream.CopyTo($memory)
                $bytes = $memory.ToArray()
                $document = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -AsHashtable -NoEnumerate -Depth 100
                $found = @{ Document = $document; Sha256 = Get-BytesHash $bytes }
            }
            finally { $memory.Dispose() }
        }
    }
    finally { $reader.Dispose(); $stream.Dispose() }
    if (!$found) { throw 'Required saved-image metadata was not found in the archive.' }
    $found.ExpandedBytes = $expandedBytes
    return $found
}

function Get-SavedImageIdentity {
    $manifestEntry = Read-TarJsonEntry -EntryName 'manifest.json' -MaximumBytes 1MB
    $manifest = $manifestEntry.Document
    if ($manifest.Count -ne 1 -or $manifest[0].RepoTags.Count -ne 1 -or $manifest[0].RepoTags[0] -cne $Image) {
        throw 'The archive must contain exactly the selected single image and tag.'
    }
    $configEntry = Read-TarJsonEntry -EntryName $manifest[0].Config -MaximumBytes 8MB
    $config = $configEntry.Document
    $labels = $config.config.Labels
    Assert-ImageIdentity -OperatingSystem $config.os -Architecture $config.architecture -Labels $labels
    return @{ ImageId = 'sha256:' + $configEntry.Sha256; ExpandedBytes = $manifestEntry.ExpandedBytes }
}

function Assert-ImageIdentity {
    param([string]$OperatingSystem, [string]$Architecture, [hashtable]$Labels)
    if ($OperatingSystem -cne 'linux' -or $Architecture -cne 'amd64') {
        throw 'The selected image must target linux/amd64.'
    }
    $expectedLabels = @{
        'com.nop.storefront.theme' = $Theme
        'org.opencontainers.image.revision' = $SourceCommit
        'com.nop.storefront.source-sha256' = $SourceHash
    }
    foreach ($label in $expectedLabels.Keys) {
        if (!$Labels -or $Labels[$label] -cne $expectedLabels[$label]) {
            throw 'Image source/theme labels differ from the requested deployment.'
        }
    }
}

function Read-RemoteBytes {
    param([string]$Path)
    Assert-DeploymentLock
    return ,$script:sftp.ReadAllBytes($Path)
}

function Write-RemoteBytes {
    param([string]$Path, [byte[]]$Bytes)
    Assert-DeploymentLock
    $script:sftp.WriteAllBytes($Path, $Bytes)
    # SSH.NET accepts the octal digits (600), rather than their decimal value.
    $script:sftp.ChangePermissions($Path, [short]600)
}

function Get-UpdatedOverride {
    param([byte[]]$OriginalBytes, [string]$NewImage)
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    $text = $encoding.GetString($OriginalBytes)
    $bom = $text.StartsWith([string][char]0xFEFF, [StringComparison]::Ordinal)
    if ($bom) { $text = $text.Substring(1) }
    if ($text.TrimStart().StartsWith('{')) {
        $document = $text | ConvertFrom-Json -AsHashtable -Depth 100
        $oldImage = $document.services[$script:themeSlug].image
        if ($oldImage -isnot [string]) { throw 'Override image must be a literal scalar.' }
        $document.services[$script:themeSlug].image = $NewImage
        $newText = ($document | ConvertTo-Json -Depth 100) + "`n"
    }
    else {
        # This changes one direct service image scalar. Other environment fields,
        # comments, whitespace, services and override semantics remain untouched.
        $lines = [regex]::Matches($text, '[^\r\n]+(?:\r?\n|$)|\r?\n')
        $servicesFound = $false
        $serviceIndent = -1
        $fieldIndent = -1
        $selectedService = $false
        $imageMatches = [Collections.Generic.List[object]]::new()
        foreach ($lineMatch in $lines) {
            $line = $lineMatch.Value
            if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
            if ($line -match '^services:\s*(?:#.*)?$') { $servicesFound = $true; continue }
            if (!$servicesFound) { continue }
            if ($line -notmatch '^\s') { break }
            $indentText = [regex]::Match($line, '^\s*').Value
            if ($indentText.Contains("`t")) { throw 'Tab-indented image overrides are unsupported.' }
            $indent = $indentText.Length
            $mapping = [regex]::Match($line, '^\s*([A-Za-z0-9_.-]+):\s*(?:#.*)?$')
            if ($serviceIndent -lt 0) {
                if (!$mapping.Success) { throw 'Use block-style service mappings in the image override.' }
                $serviceIndent = $indent
            }
            if ($indent -eq $serviceIndent) {
                if (!$mapping.Success) { throw 'Unsupported service mapping in the image override.' }
                $selectedService = $mapping.Groups[1].Value -ceq $script:themeSlug
                $fieldIndent = -1
                continue
            }
            if (!$selectedService) { continue }
            if ($fieldIndent -lt 0) { $fieldIndent = $indent }
            if ($indent -ne $fieldIndent -or $line -notmatch '^\s*image\s*:') { continue }
            $imageLine = [regex]::Match($line, '^(\s*image\s*:\s*)([^\r\n]*?)(\r?\n)?$')
            $scalar = $imageLine.Groups[2].Value
            $commentIndex = $scalar.IndexOf(' #', [StringComparison]::Ordinal)
            $comment = if ($commentIndex -ge 0) { $scalar.Substring($commentIndex) } else { '' }
            if ($commentIndex -ge 0) { $scalar = $scalar.Substring(0, $commentIndex) }
            $oldImage = $scalar.Trim().Trim([char]39, [char]34)
            if ($oldImage -notmatch '^[A-Za-z0-9][A-Za-z0-9._:/@+-]*$') { throw 'Image override must be a literal Docker reference.' }
            $newLine = $imageLine.Groups[1].Value + $NewImage + $comment + $imageLine.Groups[3].Value
            $imageMatches.Add(@{ Index = $lineMatch.Index; Length = $line.Length; Text = $newLine; OldImage = $oldImage })
        }
        if ($imageMatches.Count -ne 1) { throw 'Expected exactly one selected-service image scalar.' }
        $selected = $imageMatches[0]
        $oldImage = $selected.OldImage
        $newText = $text.Remove($selected.Index, $selected.Length).Insert($selected.Index, $selected.Text)
    }
    if ($bom) { $newText = [string][char]0xFEFF + $newText }
    return @{ Bytes = $encoding.GetBytes($newText); OldImage = $oldImage }
}

function Set-CanonicalOverride {
    param([string]$SourcePath)
    $canonical = "$script:remoteRoot/images.override.yml"
    $temporary = "$script:remoteRoot/.images.override.$script:runId.tmp"
    $quotedCanonical = ConvertTo-ShellArgument $canonical
    $quotedTemporary = ConvertTo-ShellArgument $temporary
    $quotedSource = ConvertTo-ShellArgument $SourcePath
    $atomicReplacement = @"
set -e
cp --preserve=mode -- $quotedCanonical $quotedTemporary
cat -- $quotedSource > $quotedTemporary
mv -f -- $quotedTemporary $quotedCanonical
"@
    Invoke-RemoteCommand -Arguments @('sh', '-c', $atomicReplacement) -Purpose 'atomically update selected image override' | Out-Null
}

function Save-DeploymentReceipt {
    $json = ($script:receipt | ConvertTo-Json -Depth 100) + "`n"
    [IO.Directory]::CreateDirectory((Split-Path -Parent $ReceiptPath)) | Out-Null
    [IO.File]::WriteAllText($ReceiptPath, $json, [Text.UTF8Encoding]::new($false))
    if ($script:releasePath) {
        $temporary = "$script:releasePath/receipt.json.part"
        Write-RemoteBytes -Path $temporary -Bytes ([Text.Encoding]::UTF8.GetBytes($json))
        $persistReceipt = @{
            Arguments = @('mv', '-f', '--', $temporary, "$script:releasePath/receipt.json")
            Purpose = 'persist deployment receipt'
        }
        Invoke-RemoteCommand @persistReceipt | Out-Null
    }
}

function Send-ImageArchive {
    param([string]$Destination)
    Assert-DeploymentLock
    $source = [IO.File]::OpenRead($ArchivePath)
    $destinationStream = $null
    try {
        $destinationStream = $script:sftp.Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        $script:sftp.ChangePermissions($Destination, [short]600)
        $buffer = [byte[]]::new(1MB)
        $transferred = 0L
        $nextProgress = 64MB
        while (($count = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {
            Assert-DeploymentLock
            $destinationStream.Write($buffer, 0, $count)
            $transferred += $count
            if ($transferred -ge $nextProgress) {
                Write-Host ('Image upload: {0:N0} MiB of {1:N0} MiB' -f ($transferred / 1MB), ($source.Length / 1MB))
                $nextProgress += 64MB
            }
        }
        $destinationStream.Flush()
    }
    finally {
        if ($destinationStream) { $destinationStream.Dispose() }
        $source.Dispose()
    }
}

function Get-RemoteDiskState {
    param([string]$Path)
    $result = Invoke-RemoteCommand -Arguments @('df', '-B1', '--output=avail', '--', $Path) -Purpose 'read available disk capacity'
    $available = ($result.Output.Trim() -split '\r?\n')[-1].Trim()
    if ($available -notmatch '^\d+$') { throw 'Remote disk capacity was not numeric.' }
    $result = Invoke-RemoteCommand -Arguments @('stat', '-c', '%d', '--', $Path) -Purpose 'read filesystem identity'
    return @{ FreeBytes = [long]$available; Device = $result.Output.Trim() }
}

function Wait-SelectedContainer {
    param([string]$ExpectedImageId)
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeout)
    $stableSince = $null
    $containerId = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        $current = Get-ServiceState $script:themeSlug
        if (!$current.Running -or $current.RestartCount -ne 0) { throw 'Selected container exited or restarted during startup.' }
        if ($current.ImageId -cne $ExpectedImageId) { throw 'Selected container runs an unexpected image.' }
        if ($containerId -and $current.Id -cne $containerId) { throw 'Selected container changed during its stability window.' }
        $containerId = $current.Id
        $readListener = @{
            Arguments = @('docker', 'exec', $containerId, 'cat', '/proc/net/tcp', '/proc/net/tcp6')
            Purpose = 'read passive internal listener metadata'
            AllowFailure = $true
        }
        $result = Invoke-RemoteCommand @readListener
        $listening = $false
        if ($result.ExitStatus -eq 0) {
            foreach ($line in ($result.Output -split '\r?\n')) {
                $columns = $line.Trim() -split '\s+'
                if ($columns.Length -gt 3 -and $columns[1].EndsWith(':1F90') -and $columns[3] -ceq '0A') { $listening = $true }
            }
        }
        if ($listening) {
            if (!$stableSince) { $stableSince = [DateTime]::UtcNow }
            if (([DateTime]::UtcNow - $stableSince).TotalSeconds -ge 5) { return $current }
        }
        else { $stableSince = $null }
        Start-Sleep -Seconds 1
    }
    throw 'Startup timeout: no stable passive internal TCP 8080 listener.'
}

function Restart-SelectedService {
    param([string]$AdditionalOverride, [string]$ExpectedImageId)
    $arguments = (Get-ComposeArguments $AdditionalOverride) + @('up', '-d', '--no-build', '--no-deps', '--force-recreate', $script:themeSlug)
    Invoke-RemoteCommand -Arguments $arguments -Purpose 'replace only the selected store' -TimeoutSeconds ($StartupTimeout + 60) | Out-Null
    return Wait-SelectedContainer $ExpectedImageId
}

function Test-ImageReferenced {
    param([string]$ImageId)
    $result = Invoke-RemoteCommand -Arguments @('docker', 'ps', '-aq', '--no-trunc') -Purpose 'read all running and stopped container references'
    foreach ($containerId in ($result.Output.Trim() -split '\s+' | Where-Object { $_ })) {
        $readContainerImage = @{
            Arguments = @('docker', 'inspect', '--format', '{{.Image}}', $containerId)
            Purpose = 'guard previous image container references'
        }
        $result = Invoke-RemoteCommand @readContainerImage
        if ($result.Output.Trim() -ceq $ImageId) { return $true }
    }
    return $false
}

function Test-StorefrontImageTag {
    param([string]$Tag)
    foreach ($slug in $script:imageSlugs) {
        $prefix = $script:imageRepository + ':' + $slug + '-'
        if ($Tag.StartsWith($prefix, [StringComparison]::Ordinal)) { return $true }
    }
    return $false
}

function Remove-PreviousImage {
    $previousId = $script:before[$script:themeSlug].ImageId
    if ((Get-ServiceState $script:themeSlug).ImageId -ceq $previousId) { return @{ Status = 'skipped'; Reason = 'Previous image is still current.' } }
    if (Test-ImageReferenced $previousId) { return @{ Status = 'skipped'; Reason = 'A running or stopped container references the previous image.' } }
    $oldImage = Get-ImageInformation $previousId
    $oldLabels = $oldImage.Config.Labels
    if ($oldLabels -and $oldLabels.ContainsKey('com.nop.storefront.theme') -and $(if ($oldLabels['com.nop.storefront.theme'] -eq 'Boutique') { 'MishaShop' } else { $oldLabels['com.nop.storefront.theme'] }) -cne $Theme) {
        return @{ Status = 'skipped'; Reason = 'Previous image declares a different storefront theme.' }
    }
    $tags = @($oldImage.RepoTags | Where-Object { $_ })
    if (!$tags.Count -or @($tags | Where-Object { !(Test-StorefrontImageTag $_) }).Count) {
        return @{ Status = 'skipped'; Reason = 'Previous image has missing or foreign ownership tags.' }
    }
    if ($tags.Count -ne 1) { return @{ Status = 'skipped'; Reason = 'Previous image has multiple tags; no tag was removed.' } }
    Assert-ProtectedServices
    if ((Get-ServiceState $script:themeSlug).ImageId -ceq $previousId -or (Test-ImageReferenced $previousId)) {
        return @{ Status = 'skipped'; Reason = 'Previous image acquired a container reference before removal.' }
    }
    $freshImage = Get-ImageInformation $previousId
    if (($freshImage.RepoTags | ConvertTo-Json -Compress) -cne ($oldImage.RepoTags | ConvertTo-Json -Compress)) {
        return @{ Status = 'skipped'; Reason = 'Previous image tags changed before removal.' }
    }
    $removeImage = @{
        Arguments = @('docker', 'image', 'rm', $previousId)
        Purpose = 'remove exactly the unused previous selected-store image'
        AllowFailure = $true
    }
    $result = Invoke-RemoteCommand @removeImage
    if ($result.ExitStatus -ne 0) { return @{ Status = 'skipped'; Reason = 'Docker refused unforced image removal.' } }
    return @{ Status = 'removed'; ImageId = $previousId; Tags = $tags }
}

function Remove-TransferArchives {
    $removed = [Collections.Generic.List[string]]::new()
    $warnings = [Collections.Generic.List[string]]::new()
    if ($script:releasePath) {
        foreach ($name in @('image.tar', 'image.tar.part')) {
            $candidate = "$script:releasePath/$name"
            try {
                Assert-DeploymentLock
                if (!$script:sftp.Exists($candidate)) { continue }
                $attributes = $script:sftp.GetAttributes($candidate)
                if (!$attributes.IsRegularFile -or $attributes.IsSymbolicLink) {
                    $warnings.Add('A transfer archive is not a regular release-owned file; it was retained.')
                    continue
                }
                $script:sftp.DeleteFile($candidate)
                $removed.Add($name)
            }
            catch { $warnings.Add('Release-owned transfer archive cleanup could not be confirmed.') }
        }
    }
    return @{ Removed = $removed.ToArray(); Warnings = $warnings.ToArray() }
}

$script:ssh = $null
$script:sftp = $null
$script:privateKeyFile = $null
$script:lockCommand = $null
$script:lockTask = $null
$script:lockInput = $null
$script:lockReader = $null
$script:releasePath = $null
$script:receipt = [ordered]@{
    status = 'failed'
    Phase = 'validate configuration and saved image'
    Theme = $Theme
    SourceCommit = $SourceCommit
    SourceSha256 = $SourceHash
    NoHttpChecks = $true
    NoDatabaseOrAdminCommands = $true
}
$script:themeSlug = if ($Theme -eq 'MishaShop') { 'boutique' } else { 'madadrang' }
# Compose service names keep existing state; new image tags use the primary brand name.
$script:imageSlugs = @($Theme.ToLowerInvariant(), $script:themeSlug) | Select-Object -Unique
$script:otherStore = if ($script:themeSlug -eq 'madadrang') { 'boutique' } else { 'madadrang' }
$script:runId = [Guid]::NewGuid().ToString('N')
$switched = $false
$committed = $false
$deploymentSucceeded = $false

try {
    $ConfigPath = (Resolve-Path -LiteralPath $ConfigPath).Path
    $ArchivePath = (Resolve-Path -LiteralPath $ArchivePath).Path
    $ReceiptPath = [IO.Path]::GetFullPath($ReceiptPath)
    $settings = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($required in @('host', 'username', 'knownHostsPath', 'remoteRoot')) {
        if (!$settings[$required]) { throw "Missing private configuration field: $required." }
    }
    # Missing optional keys are normalized without reading a stale password handoff
    # when a key file has been selected.
    foreach ($optional in @('keyFilename', 'handoffPath')) { if (!$settings.ContainsKey($optional)) { $settings[$optional] = $null } }
    if (!$settings.keyFilename -and !$settings.handoffPath) { throw 'Configure keyFilename or handoffPath for SSH authentication.' }
    $script:remoteRoot = ([string]$settings.remoteRoot).TrimEnd('/')
    if (!$script:remoteRoot.StartsWith('/') -or $script:remoteRoot -eq '/') {
        throw 'remoteRoot must name the existing absolute deployment directory.'
    }
    $script:imageRepository = if ($settings.ContainsKey('imageRepository')) { [string]$settings.imageRepository } else { 'nop-dual' }
    $validDockerReference = $Image -match '^[A-Za-z0-9][A-Za-z0-9._:/-]*:[A-Za-z0-9_][A-Za-z0-9_.-]*$'
    $ownedImageTag = Test-StorefrontImageTag $Image
    if (!$validDockerReference -or !$ownedImageTag) {
        throw 'The image must have an explicit unique tag owned by the selected store.'
    }
    $archiveLength = (Get-Item -LiteralPath $ArchivePath).Length
    $archiveHash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $savedImage = Get-SavedImageIdentity
    $script:receipt.ArchiveSha256 = $archiveHash
    $port = if ($settings.ContainsKey('port')) { [int]$settings.port } else { 22 }
    $pins = @(Get-PinnedHostKeys -KnownHostsPath (Resolve-PrivatePath $settings.knownHostsPath) -Server $settings.host -Port $port)
    Initialize-HostKeyCallback
    $hostKeyGuard = [StorefrontDeployment.HostKeyGuard]::new($pins)
    $connection = New-ConnectionInfo $settings
    $script:ssh = [Renci.SshNet.SshClient]::new($connection)
    $script:sftp = [Renci.SshNet.SftpClient]::new($connection)
    $hostKeyGuard.Attach($script:ssh)
    $hostKeyGuard.Attach($script:sftp)
    $script:ssh.KeepAliveInterval = [TimeSpan]::FromSeconds(15)
    $script:sftp.KeepAliveInterval = [TimeSpan]::FromSeconds(15)
    $script:sftp.OperationTimeout = [TimeSpan]::FromSeconds(1800)
    $script:receipt.Phase = 'connect using pinned SSH host keys'
    $script:ssh.Connect()
    # No remote mutation occurs until the existing advisory deployment lock is held.
    $script:receipt.Phase = 'acquire deployment lock'
    Start-DeploymentLock
    $script:sftp.Connect()
    Write-Host 'Remote lock acquired. Checking image identity, protected services and capacity.'
    $script:receipt.Phase = 'check protected services and disk capacity'
    $script:before = [ordered]@{}
    foreach ($service in @($script:themeSlug, $script:otherStore, 'database')) {
        $script:before[$service] = Get-ServiceState $service
        if (!$script:before[$service].Running) { throw 'Existing stores and database must be running before deployment.' }
    }
    $script:receipt.Before = $script:before
    $originalBytes = Read-RemoteBytes "$script:remoteRoot/images.override.yml"
    $originalHash = Get-BytesHash $originalBytes
    $updatedOverride = Get-UpdatedOverride -OriginalBytes $originalBytes -NewImage $Image
    $configuredImageId = (Get-ImageInformation $updatedOverride.OldImage).Id
    if ($configuredImageId -cne $script:before[$script:themeSlug].ImageId) {
        throw 'The current override does not match the selected container image.'
    }
    $existingImage = Invoke-RemoteCommand -Arguments @('docker', 'image', 'inspect', $Image) -Purpose 'guard unique image tag' -AllowFailure
    if ($existingImage.ExitStatus -eq 0) { throw 'The new image tag already exists remotely; build a fresh unique tag.' }
    $readDockerRoot = @{
        Arguments = @('docker', 'info', '--format', '{{.DockerRootDir}}')
        Purpose = 'read Docker filesystem location'
    }
    $dockerRootResult = Invoke-RemoteCommand @readDockerRoot
    $releaseDisk = Get-RemoteDiskState $script:remoteRoot
    $dockerDisk = Get-RemoteDiskState $dockerRootResult.Output.Trim()
    $loadRequired = 2 * [Math]::Max($ImageSize, $savedImage.ExpandedBytes) + 256MB
    $transferRequired = $archiveLength + 64MB
    $sameFilesystem = $releaseDisk.Device -ceq $dockerDisk.Device
    $requiredAtRelease = if ($sameFilesystem) { $loadRequired + $transferRequired } else { $transferRequired }
    $releaseCapacityInsufficient = $releaseDisk.FreeBytes -lt $requiredAtRelease
    $dockerCapacityInsufficient = !$sameFilesystem -and $dockerDisk.FreeBytes -lt $loadRequired
    if ($releaseCapacityInsufficient -or $dockerCapacityInsufficient) {
        throw 'Insufficient disk capacity for image transfer and load; no image was uploaded or switched.'
    }
    $releaseName = '{0}-{1}-image-{2}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'), $script:themeSlug, $script:runId.Substring(0, 8)
    $script:releasePath = "$script:remoteRoot/releases/$releaseName"
    Invoke-RemoteCommand -Arguments @('mkdir', '-m', '700', '--', $script:releasePath) -Purpose 'create a private image deployment record' | Out-Null
    $script:receipt.Release = $script:releasePath
    $script:receipt.status = 'uploading'
    $script:receipt.Phase = 'transfer and verify saved image'
    Save-DeploymentReceipt
    Send-ImageArchive "$script:releasePath/image.tar.part"
    $verifyTransfer = @{
        Arguments = @('sha256sum', '--', "$script:releasePath/image.tar.part")
        Purpose = 'verify transferred image archive'
        TimeoutSeconds = 1800
    }
    $result = Invoke-RemoteCommand @verifyTransfer
    if (($result.Output -split '\s+')[0] -cne $archiveHash) { throw 'Transferred image archive SHA256 differs.' }
    $finalizeTransfer = @{
        Arguments = @('mv', '--', "$script:releasePath/image.tar.part", "$script:releasePath/image.tar")
        Purpose = 'finalize verified image transfer'
    }
    Invoke-RemoteCommand @finalizeTransfer | Out-Null
    $recheckImageTag = @{
        Arguments = @('docker', 'image', 'inspect', $Image)
        Purpose = 'recheck unique image tag before loading'
        AllowFailure = $true
    }
    $existingImage = Invoke-RemoteCommand @recheckImageTag
    if ($existingImage.ExitStatus -eq 0) { throw 'The requested image tag appeared during upload.' }
    Write-Host 'Loading the selected image; both existing stores remain running.'
    $script:receipt.Phase = 'load and verify image identity'
    $loadImage = @{
        Arguments = @('docker', 'load', '--input', "$script:releasePath/image.tar")
        Purpose = 'load selected local image'
        TimeoutSeconds = 1800
    }
    Invoke-RemoteCommand @loadImage | Out-Null
    $loadedImage = Get-ImageInformation $Image
    if ($loadedImage.Id -cne $savedImage.ImageId) { throw 'Loaded image ID differs from the saved image configuration hash.' }
    $labels = $loadedImage.Config.Labels
    Assert-ImageIdentity -OperatingSystem $loadedImage.Os -Architecture $loadedImage.Architecture -Labels $labels
    $script:receipt.NewImage = $Image
    $script:receipt.NewImageId = $loadedImage.Id
    foreach ($service in $script:before.Keys) {
        $currentState = Get-ServiceState $service | ConvertTo-Json -Compress
        $originalState = $script:before[$service] | ConvertTo-Json -Compress
        if ($currentState -cne $originalState) { throw 'Container metadata changed during image transfer/load.' }
    }
    $currentOverrideHash = Get-BytesHash (Read-RemoteBytes "$script:remoteRoot/images.override.yml")
    if ($currentOverrideHash -cne $originalHash) { throw 'Canonical override changed during transfer/load.' }
    Write-RemoteBytes "$script:releasePath/images.override.before.yml" $originalBytes
    Write-RemoteBytes "$script:releasePath/images.override.after.yml" $updatedOverride.Bytes
    $selectedOverride = "$script:releasePath/selected.override.json"
    $selectedDocument = @{ services = @{ $script:themeSlug = @{ image = $Image } } }
    Write-RemoteBytes $selectedOverride ([Text.Encoding]::UTF8.GetBytes(($selectedDocument | ConvertTo-Json -Depth 10)))
    Write-Host "Replacing only $Theme and waiting for passive startup metadata."
    $script:receipt.Phase = 'replace selected store and observe passive startup'
    $switched = $true
    $script:receipt.Current = Restart-SelectedService -AdditionalOverride $selectedOverride -ExpectedImageId $loadedImage.Id
    Assert-ProtectedServices
    $currentOverrideHash = Get-BytesHash (Read-RemoteBytes "$script:remoteRoot/images.override.yml")
    if ($currentOverrideHash -cne $originalHash) { throw 'Canonical override changed before commit.' }
    Set-CanonicalOverride "$script:releasePath/images.override.after.yml"
    $script:receipt.status = 'started'
    Save-DeploymentReceipt
    $committed = $true
    $script:receipt.Phase = 'retire only the unused previous selected image'
    try { $script:receipt.PreviousImageCleanup = Remove-PreviousImage }
    catch {
        $script:receipt.PreviousImageCleanup = @{
            Status = 'unconfirmed'
            Reason = 'Previous-image cleanup could not be confirmed; no forced retry was made.'
        }
    }
    Assert-ProtectedServices
    $script:receipt.After = [ordered]@{}
    foreach ($service in $script:before.Keys) { $script:receipt.After[$service] = Get-ServiceState $service }
    $script:receipt.status = 'success'
    $script:receipt.Phase = 'completed'
    $deploymentSucceeded = $true
}
catch {
    # Keep arbitrary SSH, Docker and configuration exception details out of logs.
    $script:receipt.status = 'failed'
    $script:receipt.Error = 'Deployment could not be confirmed; review the private receipt and original override backup.'
    # Exception types and source locations identify the failing operation without
    # recording provider messages, command output or authentication values.
    $script:receipt.FailureType = $_.Exception.GetType().FullName
    $script:receipt.FailureLocation = $_.ScriptStackTrace
    if ($switched -and !$committed) {
        try {
            Set-CanonicalOverride "$script:releasePath/images.override.before.yml"
            $restored = Restart-SelectedService -ExpectedImageId $script:before[$script:themeSlug].ImageId
            Assert-ProtectedServices
            $script:receipt.Rollback = @{ Status = 'restored'; Current = $restored }
            $script:receipt.status = 'rolled_back'
        }
        catch {
            $script:receipt.Rollback = @{
                Status = 'failed'
                Reason = 'Manual recovery required; original override and previous image were retained.'
            }
        }
    }
}
finally {
    try {
        if ($script:releasePath) {
            $script:receipt.TransferArchiveCleanup = Remove-TransferArchives
            $script:receipt.DiskFreeBytes = (Get-RemoteDiskState $script:remoteRoot).FreeBytes
        }
        Save-DeploymentReceipt
    }
    catch {
        $deploymentSucceeded = $false
        $script:receipt.status = 'failed'
        $script:receipt.Error = 'Final receipt persistence could not be confirmed; inspect retained release metadata before retrying.'
        try { [IO.File]::WriteAllText($ReceiptPath, ($script:receipt | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false)) } catch { }
    }
    # Hold the deployment lock until archive cleanup and the final receipt finish.
    if ($script:lockInput) {
        try {
            $releaseBytes = [Text.Encoding]::UTF8.GetBytes("release`n")
            $script:lockInput.Write($releaseBytes, 0, $releaseBytes.Length)
            $script:lockInput.Dispose()
        }
        catch { }
    }
    if ($script:lockCommand) { $script:lockCommand.Dispose() }
    if ($script:lockReader) { $script:lockReader.Dispose() }
    if ($script:sftp) { $script:sftp.Dispose() }
    if ($script:ssh) { $script:ssh.Dispose() }
    if ($script:privateKeyFile) { $script:privateKeyFile.Dispose() }
}

if (!$deploymentSucceeded) { throw "Image deployment did not complete successfully. Review $ReceiptPath; no credentials were printed." }
Write-Host "Deployed $Theme image $Image from source $SourceCommit."
Write-Host "Previous image cleanup: $($script:receipt.PreviousImageCleanup.Status). Receipt: $ReceiptPath"
if ($script:receipt.PreviousImageCleanup.Status -ne 'removed') {
    Write-Warning $script:receipt.PreviousImageCleanup.Reason
}
foreach ($warning in $script:receipt.TransferArchiveCleanup.Warnings) { Write-Warning $warning }
