param(
    [string]$Configuration = "Release",
    [string]$Version,
    [string]$ToolboxPath = "C:\Playnite\Toolbox.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "PlayniteAudioSwitcher.csproj"
$manifest = Join-Path $root "extension.yaml"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifestText = Get-Content -Raw -LiteralPath $manifest
    if ($manifestText -notmatch '(?m)^Version:\s*(.+?)\s*$') {
        throw "Could not read Version from extension.yaml"
    }

    $Version = $Matches[1].Trim().Trim("'`"")
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "Project file was not found at $project"
}

dotnet restore $project
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

dotnet clean $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed with exit code $LASTEXITCODE"
}

dotnet build $project -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $ToolboxPath)) {
    throw "Playnite Toolbox was not found at $ToolboxPath"
}

$source = Join-Path $root "bin\$Configuration"
$output = Join-Path $root "dist\v$Version"
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $ToolboxPath pack $source $output
if ($LASTEXITCODE -ne 0) {
    throw "Playnite Toolbox pack failed with exit code $LASTEXITCODE"
}

$package = Get-ChildItem -LiteralPath $output -Filter '*.pext' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $package) {
    throw "Playnite Toolbox did not create a .pext package."
}

Write-Host "Package created: $($package.FullName)"
Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256
