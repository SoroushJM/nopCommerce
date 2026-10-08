param([switch]$Verify)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projects = @(
    'src/Plugins/Nop.Plugin.Misc.PersianStorefront/Nop.Plugin.Misc.PersianStorefront.csproj',
    'src/Storefront/Storefront.UI/Storefront.UI.csproj',
    'src/Storefront/Storefront.Preview/Storefront.Preview.csproj'
)

Push-Location $repoRoot
try {
    foreach ($project in $projects) {
        # IDE1006 naming fixes do not support batch Fix All. Keep the naming
        # rules in .editorconfig, but check formatting separately from renames.
        $formatArguments = @('format', $project, '--severity', 'info',
            '--exclude-diagnostics', 'IDE1006', '--verbosity', 'minimal')
        if ($Verify) {
            $formatArguments += '--verify-no-changes'
        }
        & dotnet @formatArguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet format failed for $project (exit $LASTEXITCODE)."
        }
        if (!$Verify) {
            # Code-style fixes can introduce new whitespace edits after the
            # initial formatter pass (for example, replacing unused out locals).
            & dotnet format whitespace $project --no-restore --verbosity minimal
            if ($LASTEXITCODE -ne 0) {
                throw "Whitespace formatting failed for $project (exit $LASTEXITCODE)."
            }
        }
    }
}
finally {
    Pop-Location
}
