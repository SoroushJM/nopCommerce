# SSH.NET is loaded into PowerShell's existing .NET runtime. No Python, external
# package manager, SSH client executable or remote scripting runtime is needed.
function Import-StorefrontSsh {
    [CmdletBinding()]
    param(
        [string]$CacheDirectory = (
            Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) `
                'NopStorefrontDeploy/dependencies'
        )
    )

    if ($PSVersionTable.PSVersion -lt [version]'7.4') {
        throw 'The deployment scripts require PowerShell 7.4 or newer (.NET 8+).'
    }

    # Exact versions and SHA256 values from the official NuGet packages.
    # Dependency order matters: load dependencies before SSH.NET itself.
    $packages = @(
        @{
            Name = 'microsoft.extensions.dependencyinjection.abstractions'
            Version = '8.0.2'
            Sha256 = '51F2DF1100245F10DA54F0BB7E813F277155117777D4FBBAB902214E27372606'
            Assembly = 'lib/net8.0/Microsoft.Extensions.DependencyInjection.Abstractions.dll'
        }
        @{
            Name = 'microsoft.extensions.logging.abstractions'
            Version = '8.0.3'
            Sha256 = 'E4C498D5A13051B4577A148F1D8C3470167215C507E2392069B75DC61322BB74'
            Assembly = 'lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll'
        }
        @{
            Name = 'bouncycastle.cryptography'
            Version = '2.7.0'
            Sha256 = 'F091FFCCAB4D03993E660BACE277659A79DEE0972F54D7F1F4BD46D680966241'
            Assembly = 'lib/net6.0/BouncyCastle.Cryptography.dll'
        }
        @{
            Name = 'ssh.net'
            Version = '2026.0.0'
            Sha256 = 'B2515DE616821198F5CF5530F5EA198912730BBE3504EF4C6AEE00A661FECAC2'
            Assembly = 'lib/net8.0/Renci.SshNet.dll'
        }
    )

    foreach ($package in $packages) {
        $packageName = '{0}.{1}' -f $package.Name, $package.Version
        $packageDirectory = Join-Path $CacheDirectory $packageName
        $archivePath = Join-Path $packageDirectory "$packageName.nupkg"
        New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

        if (!(Test-Path -LiteralPath $archivePath -PathType Leaf)) {
            Write-Host "Preparing deployment library: $packageName"
            $downloadUri = 'https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg' -f `
                $package.Name, $package.Version
            $downloadPath = "$archivePath.$([guid]::NewGuid().ToString('N')).download"

            try {
                Invoke-WebRequest -Uri $downloadUri -OutFile $downloadPath -TimeoutSec 120
                $downloadHash = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash
                if ($downloadHash -ne $package.Sha256) {
                    throw "Downloaded package checksum does not match: $packageName"
                }
                Move-Item -LiteralPath $downloadPath -Destination $archivePath -Force
            }
            finally {
                if (Test-Path -LiteralPath $downloadPath -PathType Leaf) {
                    Remove-Item -LiteralPath $downloadPath -Force
                }
            }
        }

        $cachedHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
        if ($cachedHash -ne $package.Sha256) {
            throw "Cached package checksum does not match: $archivePath"
        }

        # Extract only the named assembly. Do not run package build scripts or
        # trust a previously extracted DLL without checking its package again.
        $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
        try {
            $entry = $archive.GetEntry($package.Assembly)
            if ($null -eq $entry) {
                throw "Required assembly is missing from package: $packageName"
            }

            $assemblyPath = Join-Path $packageDirectory ([IO.Path]::GetFileName($package.Assembly))
            $assemblyStream = $entry.Open()
            $assemblyBytes = [IO.MemoryStream]::new()
            try {
                $assemblyStream.CopyTo($assemblyBytes)
                $expectedHash = [Convert]::ToHexString(
                    [Security.Cryptography.SHA256]::HashData($assemblyBytes.ToArray())
                )
                $assemblyMatches = (Test-Path -LiteralPath $assemblyPath -PathType Leaf) -and `
                    (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash -eq $expectedHash

                if (!$assemblyMatches) {
                    [IO.File]::WriteAllBytes($assemblyPath, $assemblyBytes.ToArray())
                }
            }
            finally {
                $assemblyStream.Dispose()
                $assemblyBytes.Dispose()
            }
        }
        finally {
            $archive.Dispose()
        }

        [void][Reflection.Assembly]::LoadFrom($assemblyPath)
    }
}
