[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SignedInstallerPath,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version = '0.2.0',
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\windows\release'
}

$source = [System.IO.Path]::GetFullPath($SignedInstallerPath)
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Signed installer was not found: $source"
}

$signature = Get-AuthenticodeSignature -LiteralPath $source
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw "Release installer must have a valid trusted Authenticode signature. Current status: $($signature.Status)"
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
if (-not $source.Equals($releaseInstaller, [System.StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $source -Destination $releaseInstaller -Force
}

$hash = Get-FileHash -LiteralPath $releaseInstaller -Algorithm SHA256
$hashValue = $hash.Hash.ToLowerInvariant()
$checksumPath = $releaseInstaller + '.sha256'
Set-Content -LiteralPath $checksumPath -Value "$hashValue  PixelCompanion-Installer.exe" -Encoding Ascii

$verifiedHash = (Get-FileHash -LiteralPath $releaseInstaller -Algorithm SHA256).Hash.ToLowerInvariant()
$writtenHash = ((Get-Content -LiteralPath $checksumPath -Raw) -split '\s+')[0]
if ($verifiedHash -ne $writtenHash) {
    throw 'Release checksum verification failed after writing the output files.'
}

Write-Output "Release installer: $releaseInstaller"
Write-Output "Release checksum:  $checksumPath"
Write-Output "Signer:            $($signature.SignerCertificate.Subject)"
Write-Output "SHA256:            $hashValue"
