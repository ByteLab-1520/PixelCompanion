[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $InstallerPath,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version = '0.3.3',
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\windows\release'
}

$source = [System.IO.Path]::GetFullPath($InstallerPath)
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Installer was not found: $source"
}

$signature = Get-AuthenticodeSignature -LiteralPath $source
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
    throw "This release path accepts only an explicitly unsigned installer. Current status: $($signature.Status)"
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($source)
if ([string]::IsNullOrWhiteSpace($versionInfo.ProductVersion) -or
    -not $versionInfo.ProductVersion.StartsWith($Version, [System.StringComparison]::Ordinal)) {
    throw "Installer product version '$($versionInfo.ProductVersion)' does not match release version '$Version'."
}

$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if (-not $output.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output must stay inside the artifacts directory: $output"
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$releaseInstaller = Join-Path $output 'PixelCompanion-Installer.exe'
Copy-Item -LiteralPath $source -Destination $releaseInstaller -Force

$hashValue = (Get-FileHash -LiteralPath $releaseInstaller -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = $releaseInstaller + '.sha256'
Set-Content -LiteralPath $checksumPath -Value "$hashValue  PixelCompanion-Installer.exe" -Encoding Ascii

$noticePath = Join-Path $output 'UNSIGNED_INSTALLER.txt'
$notice = @'
PIXEL COMPANION UNSIGNED INSTALLER

This installer does not have an Authenticode code signature.
Windows SmartScreen may display an unknown publisher warning.

Download Pixel Companion only from:
https://github.com/ByteLab-1520/PixelCompanion/releases

Verify PixelCompanion-Installer.exe against the accompanying
PixelCompanion-Installer.exe.sha256 file before running it.
'@
Set-Content -LiteralPath $noticePath -Value $notice -Encoding Ascii

$verifiedHash = (Get-FileHash -LiteralPath $releaseInstaller -Algorithm SHA256).Hash.ToLowerInvariant()
$writtenHash = ((Get-Content -LiteralPath $checksumPath -Raw) -split '\s+')[0]
if ($verifiedHash -ne $writtenHash) {
    throw 'Release checksum verification failed after writing the output files.'
}

Write-Output "Unsigned release installer: $releaseInstaller"
Write-Output "Release checksum:           $checksumPath"
Write-Output "Unsigned notice:            $noticePath"
Write-Output "SHA256:                     $hashValue"
