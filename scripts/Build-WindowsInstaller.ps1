[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version = '0.1.0',
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

$windowsRoot = Join-Path $repoRoot 'artifacts\windows'
$publishRoot = Join-Path $windowsRoot 'publish'
$desktopPublish = Join-Path $publishRoot 'desktop'
$configPublish = Join-Path $publishRoot 'config'
$stagingRoot = Join-Path $windowsRoot 'staging'
$installerRoot = Join-Path $windowsRoot 'installer'
$desktopProject = Join-Path $repoRoot 'src\PixelCompanion.Desktop\PixelCompanion.Desktop.csproj'
$configProject = Join-Path $repoRoot 'src\PixelCompanion.Config\PixelCompanion.Config.csproj'

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

& $dotnet restore $desktopProject -r $RuntimeIdentifier
if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
& $dotnet restore $configProject -r $RuntimeIdentifier
if ($LASTEXITCODE -ne 0) { throw 'Config restore failed.' }

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
    "-p:InformationalVersion=$Version"
)

& $dotnet publish $desktopProject @commonPublishArgs -o $desktopPublish
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }

& $dotnet publish $configProject @commonPublishArgs -o $configPublish
if ($LASTEXITCODE -ne 0) { throw 'Config publish failed.' }

Copy-Item -Path (Join-Path $desktopPublish '*') -Destination $stagingRoot -Recurse -Force
Copy-Item -Path (Join-Path $configPublish '*') -Destination $stagingRoot -Recurse -Force
Get-ChildItem -LiteralPath $stagingRoot -Recurse -File -Filter '*.pdb' | Remove-Item -Force

$requiredFiles = @(
    (Join-Path $stagingRoot 'PixelCompanion.exe'),
    (Join-Path $stagingRoot 'PixelCompanion.Config.exe'),
    (Join-Path $stagingRoot 'locales\en.json'),
    (Join-Path $stagingRoot 'locales\ko.json'),
    (Join-Path $stagingRoot 'characters\DefaultCat\character.json')
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
& $InnoCompiler "/DMyAppVersion=$Version" "/O$installerRoot" $issPath
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$installer = Join-Path $installerRoot "PixelCompanion-$Version-win-x64-Setup.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Expected installer was not produced: $installer"
}

$hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
$hashLine = "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($installer))"
Set-Content -LiteralPath ($installer + '.sha256') -Value $hashLine -Encoding Ascii

Write-Output "Installer: $installer"
Write-Output "SHA256:   $($hash.Hash.ToLowerInvariant())"
