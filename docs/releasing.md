# Release process

Windows releases are created from version tags on `main`. Until a trusted code-signing certificate is available, each GitHub Release clearly identifies the installer as unsigned and includes a SHA-256 checksum plus `UNSIGNED_INSTALLER.txt`.

Each edition selects its own asset from the same GitHub Release. The desktop app offers unattended installation only when the matching installer has an `.authenticode.json` marker. The updater still verifies the downloaded SHA-256 and the installer's trusted Authenticode signature before it can replace an installation. Unsigned releases open the official GitHub Release page for a manual download instead.

## Creating an unsigned release

1. Set the same semantic version in `Directory.Build.props` and the installer.
2. Add bilingual notes at `docs/releases/vMAJOR.MINOR.PATCH.md`.
3. Merge the tested release changes into `main`.
4. Create and push a matching tag such as `v0.2.1`.
5. The `release` workflow builds and smoke-tests the installer, verifies that it is explicitly unsigned, generates its final SHA-256, and creates the GitHub Release.

The workflow stops without publishing if the tag version does not match, the tag is not contained in `main`, the release notes are missing, installation testing fails, or the artifact has an unexpected signature state.

Build and prepare the same files locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1 -Version 0.4.3 -Edition Standard
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1 -Version 0.4.3 -Edition Yaroro
powershell -ExecutionPolicy Bypass -File .\scripts\Test-WindowsInstaller.ps1 -InstallerPath .\artifacts\windows\standard\installer\PixelCompanion-Installer.exe -Version 0.4.3 -Edition Standard
powershell -ExecutionPolicy Bypass -File .\scripts\Test-WindowsInstaller.ps1 -InstallerPath .\artifacts\windows\yaroro\installer\PixelCompanion-Yaroro-Installer.exe -Version 0.4.3 -Edition Yaroro
powershell -ExecutionPolicy Bypass -File .\scripts\Prepare-UnsignedWindowsRelease.ps1 -InstallerPath .\artifacts\windows\standard\installer\PixelCompanion-Installer.exe -Version 0.4.3 -Edition Standard
powershell -ExecutionPolicy Bypass -File .\scripts\Prepare-UnsignedWindowsRelease.ps1 -InstallerPath .\artifacts\windows\yaroro\installer\PixelCompanion-Yaroro-Installer.exe -Version 0.4.3 -Edition Yaroro
```

The publishable files are written to `artifacts/windows/release/`.

## Future signed releases

Keep using `scripts/Finalize-WindowsRelease.ps1` after a trusted Authenticode signing service returns the signed installer. It rejects missing or untrusted signatures and creates a checksum from the final signed bytes.

A signed release must attach all three files:

- `PixelCompanion-Installer.exe`
- `PixelCompanion-Installer.exe.sha256`
- `PixelCompanion-Yaroro-Installer.exe`
- `PixelCompanion-Yaroro-Installer.exe.sha256`
- `PixelCompanion-Installer.exe.authenticode.json`
- `PixelCompanion-Yaroro-Installer.exe.authenticode.json`

The marker file enables the automatic-install button; it does not bypass the updater's own checksum and Authenticode verification.
