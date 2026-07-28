[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version = '0.4.1',
    [ValidateSet('Standard', 'Yaroro')]
    [string] $Edition = 'Standard',
    [ValidateSet('win-x64')]
    [string] $RuntimeIdentifier = 'win-x64',
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',
    [string] $InnoCompiler
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $repoRoot '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

$editionKey = $Edition.ToLowerInvariant()
$isYaroro = $Edition -eq 'Yaroro'
$windowsRoot = Join-Path $repoRoot "artifacts\windows\$editionKey"
$publishRoot = Join-Path $windowsRoot 'publish'
$desktopPublish = Join-Path $publishRoot 'desktop'
$configPublish = Join-Path $publishRoot 'config'
$updaterPublish = Join-Path $publishRoot 'updater'
$stagingRoot = Join-Path $windowsRoot 'staging'
$installerRoot = Join-Path $windowsRoot 'installer'
$desktopProject = Join-Path $repoRoot 'src\PixelCompanion.Desktop\PixelCompanion.Desktop.csproj'
$configProject = Join-Path $repoRoot 'src\PixelCompanion.Config\PixelCompanion.Config.csproj'
$updaterProject = Join-Path $repoRoot 'src\PixelCompanion.Updater\PixelCompanion.Updater.csproj'

function Reset-BuildDirectory([string] $Path) {
    $resolvedParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $Path))
    $resolvedArtifacts = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
    if (-not $resolvedParent.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset a directory outside the artifacts tree: $Path"
    }
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

Reset-BuildDirectory $publishRoot
Reset-BuildDirectory $stagingRoot
New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

& $dotnet restore $desktopProject -r $RuntimeIdentifier "-p:ProductEdition=$Edition"
if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
& $dotnet restore $configProject -r $RuntimeIdentifier "-p:ProductEdition=$Edition"
if ($LASTEXITCODE -ne 0) { throw 'Config restore failed.' }
& $dotnet restore $updaterProject -r $RuntimeIdentifier "-p:ProductEdition=$Edition"
if ($LASTEXITCODE -ne 0) { throw 'Updater restore failed.' }

$commonPublishArgs = @(
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '--self-contained', 'true',
    '--no-restore',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:InformationalVersion=$Version",
    "-p:ProductEdition=$Edition"
)

& $dotnet publish $desktopProject @commonPublishArgs -o $desktopPublish
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }

& $dotnet publish $configProject @commonPublishArgs -o $configPublish
if ($LASTEXITCODE -ne 0) { throw 'Config publish failed.' }

& $dotnet publish $updaterProject @commonPublishArgs -o $updaterPublish
if ($LASTEXITCODE -ne 0) { throw 'Updater publish failed.' }

Copy-Item -Path (Join-Path $desktopPublish '*') -Destination $stagingRoot -Recurse -Force
Copy-Item -Path (Join-Path $configPublish '*') -Destination $stagingRoot -Recurse -Force
Copy-Item -Path (Join-Path $updaterPublish '*') -Destination $stagingRoot -Recurse -Force
Get-ChildItem -LiteralPath $stagingRoot -Recurse -File -Filter '*.pdb' | Remove-Item -Force

$appName = if ($isYaroro) { 'Pixel Companion for Yaroro' } else { 'Pixel Companion' }
$appExe = if ($isYaroro) { 'PixelCompanion.Yaroro.exe' } else { 'PixelCompanion.exe' }
$configExe = if ($isYaroro) { 'PixelCompanion.Yaroro.Config.exe' } else { 'PixelCompanion.Config.exe' }
$updaterExe = if ($isYaroro) { 'PixelCompanion.Yaroro.Updater.exe' } else { 'PixelCompanion.Updater.exe' }
$characterFolder = if ($isYaroro) { 'Yaroro' } else { 'DefaultCat' }
$installFolder = if ($isYaroro) { 'PixelCompanion-Yaroro' } else { 'PixelCompanion' }
$appId = if ($isYaroro) { '{{0A9A97F8-2741-4B60-9141-BFE4D18EBA52}' } else { '{{7C0E4C61-4D4A-4E64-A9E4-4CD74A040D92}' }
$outputStem = if ($isYaroro) { "PixelCompanion-Yaroro-$Version-win-x64-Setup" } else { "PixelCompanion-$Version-win-x64-Setup" }
$releaseName = if ($isYaroro) { 'PixelCompanion-Yaroro-Installer.exe' } else { 'PixelCompanion-Installer.exe' }
$autoStartName = if ($isYaroro) { 'PixelCompanionYaroro' } else { 'PixelCompanion' }

$requiredFiles = @(
    (Join-Path $stagingRoot $appExe),
    (Join-Path $stagingRoot $configExe),
    (Join-Path $stagingRoot $updaterExe),
    (Join-Path $stagingRoot 'locales\en.json'),
    (Join-Path $stagingRoot 'locales\ko.json'),
    (Join-Path $stagingRoot "characters\$characterFolder\character.json")
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Published payload is missing: $requiredFile"
    }
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $candidates = @(
        (Join-Path $repoRoot 'artifacts\tools\InnoSetup\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    )
    $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw 'Inno Setup compiler was not found. Pass -InnoCompiler <path-to-ISCC.exe>.'
}

$issPath = Join-Path $repoRoot 'packaging\windows\PixelCompanion.iss'
& $InnoCompiler `
    "/DMyAppVersion=$Version" `
    "/DMyAppName=$appName" `
    "/DMyAppId=$appId" `
    "/DMyAppExeName=$appExe" `
    "/DMyConfigExeName=$configExe" `
    "/DMyInstallFolder=$installFolder" `
    "/DMyOutputStem=$outputStem" `
    "/DMyAutoStartName=$autoStartName" `
    "/DMyStagingRoot=$stagingRoot" `
    "/O$installerRoot" `
    $issPath
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$installer = Join-Path $installerRoot "$outputStem.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Expected installer was not produced: $installer"
}

$releaseInstaller = Join-Path $installerRoot $releaseName
Copy-Item -LiteralPath $installer -Destination $releaseInstaller -Force
$releaseChecksum = $releaseInstaller + '.sha256'
if (Test-Path -LiteralPath $releaseChecksum) {
    Remove-Item -LiteralPath $releaseChecksum -Force
}

$hash = Get-FileHash -LiteralPath $releaseInstaller -Algorithm SHA256
$hashLine = "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($releaseInstaller))"
Set-Content -LiteralPath ($releaseInstaller + '.unsigned.sha256') -Value $hashLine -Encoding Ascii

Write-Output "Unsigned $Edition installer: $releaseInstaller"
Write-Output "Build SHA256:       $($hash.Hash.ToLowerInvariant())"
Write-Output 'Run Finalize-WindowsRelease.ps1 only after the installer has been code-signed.'
