# Windows installer

The Windows package is a per-user Inno Setup installer for Windows 10/11 x64. It bundles the .NET runtime, so the target computer does not need .NET installed.

Build it from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1
```

The installer and its build checksum are written to `artifacts/windows/installer/`. The build checksum ends in `.unsigned.sha256`.

Installed components:

- `PixelCompanion.exe` — desktop pet runtime;
- `PixelCompanion.Config.exe` — advanced settings;
- `PixelCompanion.Updater.exe` — signed-release updater;
- English/Korean locale files and the bundled character pack;
- Start menu shortcuts and uninstaller;
- optional desktop shortcut and login auto-start entry.

User data under `%LOCALAPPDATA%\PixelCompanion` is deliberately retained during uninstall.

Current public builds are unsigned, so Windows SmartScreen may warn about an unknown publisher. The release workflow smoke-tests the installer, confirms that it is unsigned, and publishes a final `.sha256` file plus an unsigned-installer notice. See [the release process](../../docs/releasing.md) for details and the future signed-release path.
