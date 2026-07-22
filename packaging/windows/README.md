# Windows installer

The Windows package is a per-user Inno Setup installer for Windows 10/11 x64. It bundles the .NET runtime, so the target computer does not need .NET installed.

Build it from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Build-WindowsInstaller.ps1
```

The resulting installer and checksum are written to `artifacts/windows/installer/`.

Installed components:

- `PixelCompanion.exe` — desktop pet runtime;
- `PixelCompanion.Config.exe` — advanced settings;
- English/Korean locale files and the bundled character pack;
- Start menu shortcuts and uninstaller;
- optional desktop shortcut and login auto-start entry.

User data under `%LOCALAPPDATA%\PixelCompanion` is deliberately retained during uninstall. Current development builds are unsigned, so Windows SmartScreen may warn until release signing is configured.
