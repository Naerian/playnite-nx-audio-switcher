[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$NotesPath = ".release-notes.md",
    [string]$Configuration = "Release",
    [string]$ToolboxPath = "C:\Playnite\Toolbox.exe",
    [string]$RequiredApiVersion = "6.16.0",
    [switch]$Publish,
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repository = "Naerian/playnite-nx-audio-switcher"
$addonId = "PlayniteAudioSwitcher_708b6ec4-bf96-4c0d-bd9d-fe0aa04d6bf1"
$tag = "v$Version"
$versionForFile = $Version -replace '\.', '_'
$releaseDate = Get-Date -Format "yyyy-MM-dd"
$packageName = "${addonId}_${versionForFile}.pext"
$packageUrl = "https://github.com/$repository/releases/download/$tag/$packageName"
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
$emDash = [char]0x2014

function Write-Utf8File([string]$Path, [string]$Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $utf8WithoutBom)
}

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed ($LASTEXITCODE): $Command $($Arguments -join ' ')"
    }
}

function Test-GitHubReleaseExists([string]$Repository, [string]$Tag) {
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& gh release view $Tag --repo $Repository 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -eq 0) {
        return $true
    }

    $message = (($output | ForEach-Object { $_.ToString() }) -join "`n").Trim()
    if ($message -match '(?i)release not found|HTTP\s+404|status code 404') {
        return $false
    }

    throw "Could not check whether GitHub release $Tag exists (exit $exitCode): $message"
}

function Get-ReleaseChanges([string]$Path) {
    $changes = @(
        Get-Content -LiteralPath $Path |
            ForEach-Object {
                if ($_ -match '^\s*-\s+(.+?)\s*$') {
                    $Matches[1]
                }
            }
    )
    if ($changes.Count -eq 0) {
        throw "No '- Change description' entries were found in $Path."
    }
    return $changes
}

Set-Location $root

if (-not [System.IO.Path]::IsPathRooted($NotesPath)) {
    $NotesPath = Join-Path $root $NotesPath
}

if (-not (Test-Path -LiteralPath $NotesPath)) {
    Write-Utf8File $NotesPath @"
# Write one public, English change per line. This file is ignored by Git.
- Added ...
- Fixed ...
"@
    Write-Host "Created release-notes template: $NotesPath" -ForegroundColor Yellow
    Write-Host "Edit it and run this command again."
    exit 2
}

$changes = @(Get-ReleaseChanges $NotesPath)
$extensionPath = Join-Path $root "extension.yaml"
$projectPath = Join-Path $root "PlayniteAudioSwitcher.csproj"
$changelogPath = Join-Path $root "CHANGELOG.md"
$installerPath = Join-Path $root "installer.yaml"

$currentVersionText = (
    Select-String -LiteralPath $extensionPath -Pattern '^\s*Version:\s*(.+)\s*$' |
        Select-Object -First 1
).Matches[0].Groups[1].Value.Trim()
if ([version]$Version -lt [version]$currentVersionText) {
    throw "Version $Version is older than the current manifest version $currentVersionText."
}

$localTagOutput = @(& git tag --list $tag)
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect local Git tags."
}
$localTag = ($localTagOutput -join "`n").Trim()
$tagAlreadyExists = $localTag -eq $tag
if ($tagAlreadyExists -and -not $Publish) {
    throw "Tag $tag already exists locally; choose a new version."
}

if ($Publish) {
    Invoke-Checked "gh" @("auth", "status")
    if (Test-GitHubReleaseExists $repository $tag) {
		throw "Release $tag already exists."
	}
}

# Version metadata. These replacements are safe to run again after a failed publish.
$extension = [System.IO.File]::ReadAllText($extensionPath)
$extension = [regex]::Replace($extension, '(?m)^Version:\s*[^\r\n]+', "Version: $Version")
Write-Utf8File $extensionPath $extension

$project = [System.IO.File]::ReadAllText($projectPath)
$project = [regex]::Replace($project,
    '<Version>[^<]+</Version>', "<Version>$Version</Version>")
$project = [regex]::Replace($project,
    '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>")
$project = [regex]::Replace($project,
    '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>")
Write-Utf8File $projectPath $project

if (-not (Test-Path -LiteralPath $changelogPath)) {
    Write-Utf8File $changelogPath "# Changelog`n`n"
}
$changelog = [System.IO.File]::ReadAllText($changelogPath)
if ($changelog -notmatch "(?m)^## $([regex]::Escape($Version))(?=\s|$)") {
    $changeLines = ($changes | ForEach-Object { "- $_" }) -join "`n"
    $entry = "## $Version $emDash $releaseDate`n$changeLines`n`n"
    $changelog = [regex]::Replace($changelog, '(?m)^(# Changelog\s*\r?\n)',
        { param($match) $match.Groups[1].Value + "`n" + $entry })
    Write-Utf8File $changelogPath $changelog
}

$installer = [System.IO.File]::ReadAllText($installerPath)
if ($installer -notmatch "(?m)^\s+- Version:\s*$([regex]::Escape($Version))\s*$") {
    $yamlChanges = ($changes | ForEach-Object {
        "      - '" + ($_ -replace "'", "''") + "'"
    }) -join "`n"
    $packageBlock = @"
  - Version: $Version
    RequiredApiVersion: $RequiredApiVersion
    ReleaseDate: $releaseDate
    PackageUrl: $packageUrl
    Changelog:
$yamlChanges
"@
    $installer = [regex]::Replace($installer, '(?m)^(Packages:\s*\r?\n)',
        { param($match) $match.Groups[1].Value + $packageBlock + "`n" })
    Write-Utf8File $installerPath $installer
}

Write-Host "Running release checks for Audio Switcher $Version..." -ForegroundColor Cyan
$testScripts = @()
$testDirectory = Join-Path $root "tests"
if (Test-Path -LiteralPath $testDirectory) {
    $testScripts = @(
        Get-ChildItem -LiteralPath $testDirectory -Filter "run-*.ps1" -File |
            Sort-Object Name
    )
}
foreach ($test in $testScripts) {
    Invoke-Checked "powershell.exe" @("-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", $test.FullName)
}
if ($testScripts.Count -eq 0) {
    Write-Host "No standalone test scripts were found; package.ps1 will run the Release build checks."
}

Invoke-Checked "powershell.exe" @("-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $root "package.ps1"), "-Configuration", $Configuration,
    "-Version", $Version, "-ToolboxPath", $ToolboxPath)

$packagePath = Join-Path $root "dist\$Version\$packageName"
if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Expected package was not created: $packagePath"
}

$contents = @(tar -tf $packagePath)
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect package contents."
}
foreach ($required in @(
    "PlayniteAudioSwitcher.dll",
    "extension.yaml",
    "README.md",
    "media/icon.png"
)) {
    if (-not ($contents | Where-Object { $_ -eq $required })) {
        throw "Package is missing required file: $required"
    }
}
foreach ($requiredPrefix in @("Localization/", "Icons/", "Examples/")) {
    if (-not ($contents | Where-Object { $_.StartsWith($requiredPrefix, [StringComparison]::Ordinal) })) {
        throw "Package contains no files under $requiredPrefix"
    }
}
$packagedManifest = (& tar -xOf $packagePath extension.yaml) -join "`n"
if ($LASTEXITCODE -ne 0 -or
    $packagedManifest -notmatch "(?m)^Version:\s*$([regex]::Escape($Version))\s*$") {
    throw "The packaged extension.yaml does not contain version $Version."
}

$localHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
Invoke-Checked "git" @("-c", "core.safecrlf=false", "--no-pager", "diff", "--check")

Write-Host ""
Write-Host "Prepared successfully" -ForegroundColor Green
Write-Host "Version : $Version"
Write-Host "Package : $packagePath"
Write-Host "SHA-256: $localHash"
Write-Host "Changes :"
$changes | ForEach-Object { Write-Host "  - $_" }
Write-Host ""
Invoke-Checked "git" @("--no-pager", "status", "--short")

if (-not $Publish) {
    Write-Host "Nothing was published." -ForegroundColor Yellow
    Write-Host "Review the changes, then run:"
    Write-Host ".\release.ps1 -Version $Version -Publish"
    exit 0
}

$branchOutput = @(& git branch --show-current)
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect the current Git branch."
}
$branch = ($branchOutput -join "`n").Trim()
if ($branch -ne "main") {
    throw "Publishing is only allowed from the main branch (current: $branch)."
}

if (-not $Yes) {
    $confirmation = Read-Host "Type RELEASE $Version to commit, push and publish"
    if ($confirmation -ne "RELEASE $Version") {
        throw "Publication cancelled."
    }
}

Invoke-Checked "git" @("add", "-A")
Invoke-Checked "git" @("-c", "core.safecrlf=false", "--no-pager", "diff", "--cached", "--check")
& git diff --cached --quiet
if ($LASTEXITCODE -eq 1) {
    Invoke-Checked "git" @("commit", "-m", "Release Audio Switcher $Version")
}
elseif ($LASTEXITCODE -ne 0) {
    throw "Could not inspect staged changes."
}

Invoke-Checked "git" @("push", "origin", "main")

$headOutput = @(& git rev-parse HEAD)
if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve the release commit."
}
$headCommit = ($headOutput -join "`n").Trim()
if ($tagAlreadyExists) {
    $tagCommitOutput = @(& git rev-list -n 1 $tag)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve existing tag $tag."
    }
    $tagCommit = ($tagCommitOutput -join "`n").Trim()
    if ($tagCommit -ne $headCommit) {
        throw "Existing tag $tag does not point to the release commit."
    }
}
else {
    Invoke-Checked "git" @("tag", "-a", $tag, "-m", "Audio Switcher $tag")
}
Invoke-Checked "git" @("push", "origin", $tag)

$releaseNotesPath = Join-Path $env:TEMP "audio-switcher-$Version-release-notes.md"
$releaseBullets = ($changes | ForEach-Object { "- $_" }) -join "`n"
$releaseNotes = @"
## What's Changed

$releaseBullets

## Verification

- Release build completed with no errors or warnings.
- The Playnite package contents and hash were verified.

SHA-256: ``$localHash``
"@
Write-Utf8File $releaseNotesPath $releaseNotes

try {
    Invoke-Checked "gh" @("release", "create", $tag, $packagePath,
        "--repo", $repository,
        "--title", "Audio Switcher $tag", "--notes-file", $releaseNotesPath,
        "--latest", "--verify-tag")
}
finally {
    if (Test-Path -LiteralPath $releaseNotesPath) {
        Remove-Item -LiteralPath $releaseNotesPath -Force
    }
}

$publishedJson = & gh release view $tag --repo $repository `
    --json url,isDraft,isPrerelease,tagName,targetCommitish,assets
if ($LASTEXITCODE -ne 0) {
    throw "Could not verify the published release."
}
$published = $publishedJson | ConvertFrom-Json
$asset = $published.assets | Where-Object { $_.name -eq $packageName } | Select-Object -First 1
if (-not $asset -or $published.isDraft -or $published.isPrerelease) {
    throw "The release is missing its asset or is not a final public release."
}
$remoteHash = ($asset.digest -replace '^sha256:', '').ToUpperInvariant()
if ($remoteHash -ne $localHash) {
    throw "Public asset hash mismatch. Local=$localHash Remote=$remoteHash"
}

$installerUrl =
    "https://raw.githubusercontent.com/$repository/main/installer.yaml?release=$Version"
$publicInstaller = (Invoke-WebRequest -UseBasicParsing -Uri $installerUrl).Content
if ($publicInstaller -notmatch "(?m)^\s+- Version:\s*$([regex]::Escape($Version))\s*$" -or
    $publicInstaller.IndexOf($packageUrl, [StringComparison]::Ordinal) -lt 0) {
    throw "The public installer.yaml does not advertise $Version correctly."
}

Invoke-Checked "git" @("fetch", "--tags")
Write-Host ""
Write-Host "Release published and verified: $($published.url)" -ForegroundColor Green
Write-Host "SHA-256: $localHash"
