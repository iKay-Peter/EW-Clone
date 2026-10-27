param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'EasyWorshipClone.sln'

if (-not $NoRestore) {
    & dotnet restore $solution
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

& dotnet build $solution -c $Configuration --no-restore
exit $LASTEXITCODE
