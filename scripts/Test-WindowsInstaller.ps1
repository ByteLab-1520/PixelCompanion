[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $InstallerPath,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version = '0.3.2'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$installer = [System.IO.Path]::GetFullPath($InstallerPath)
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "Installer was not found: $installer"
}

$testRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'windows\install-smoke-test'))
if (-not $testRoot.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Smoke-test directory must stay inside artifacts: $testRoot"
}

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

try {
    $install = Start-Process -FilePath $installer -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/TASKS=""',
        "/DIR=`"$testRoot`""
    ) -WindowStyle Hidden -Wait -PassThru
    if ($install.ExitCode -ne 0) {
        throw "Installer smoke test failed with exit code $($install.ExitCode)."
    }

    $executables = @(
        'PixelCompanion.exe',
        'PixelCompanion.Config.exe',
        'PixelCompanion.Updater.exe'
    )
    foreach ($name in $executables) {
        $path = Join-Path $testRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Installed payload is missing: $name"
        }

        $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path).ProductVersion
        if ([string]::IsNullOrWhiteSpace($productVersion) -or
            -not $productVersion.StartsWith($Version, [System.StringComparison]::Ordinal)) {
            throw "Installed file '$name' has product version '$productVersion', expected '$Version'."
        }
    }

    Write-Output "PASS installer smoke test for version $Version"
}
finally {
    $uninstaller = Join-Path $testRoot 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller -PathType Leaf) {
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @(
            '/VERYSILENT',
            '/SUPPRESSMSGBOXES',
            '/NORESTART'
        ) -WindowStyle Hidden -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) {
            Write-Warning "Uninstaller exited with code $($uninstall.ExitCode)."
        }
    }

    if (Test-Path -LiteralPath $testRoot) {
        Start-Sleep -Milliseconds 500
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
