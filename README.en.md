# Pixel Companion

[한국어](README.md) | [English](README.en.md)

![Pixel Companion v0.2.0 hero](docs/media/pixel-companion-hero.png)

Pixel Companion is an offline-first, non-disruptive pixel desktop pet for Windows and macOS. It creates lifelike behavior without generative AI or external AI services by using state machines, conditions, probabilities, cooldowns, and localized dialogue data.

> A Windows 10/11 x64 installer is currently provided first. A distributable macOS package is planned for a later phase.

<p align="center">
  <img src="docs/media/desktop-pet-walk.gif" alt="Pixel Companion walking across the desktop" width="720">
</p>

## Current features

- A transparent, always-on-top Avalonia character window
- Nearest-neighbor rendering that keeps pixel art crisp
- Elapsed-time movement, idle periods, dragging, falling, and landing
- Click dialogue, feeding, and play reactions
- English and Korean resources with English/key fallback
- A separate advanced settings application that shares pet data
- Priority-based behavior decisions, cooldowns, and dialogue repetition prevention
- Tamagotchi-style state values with bounded offline progression
- Character-pack validation and safe fallbacks when platform services are unavailable
- Drag-and-drop PNG, JPEG, and GIF character image slots with walking-frame fallbacks
- Daily GitHub Release checks, with automatic installation restricted to signed releases

## Custom character images

Drop PNG, JPG, JPEG, or GIF files into five dedicated slots for the default pose, back view, and three walking frames. Missing walking images safely fall back to the default pose.

![Pixel Companion character settings with five image slots](docs/media/character-settings.png)

## Install on Windows

Download and run the latest `PixelCompanion-Installer.exe` from [GitHub Releases](https://github.com/ByteLab-1520/PixelCompanion/releases).

Windows may warn that the publisher cannot be verified. The installer is not currently signed with a commercial code-signing certificate, so confirm that it came from this repository and verify it with the SHA-256 file attached to the Release.

## Run from source

Install the .NET 10 SDK, then run:

```powershell
dotnet restore PixelCompanion.slnx
dotnet build PixelCompanion.slnx -c Release
dotnet run --project src/PixelCompanion.Desktop
```

Run the advanced settings application separately:

```powershell
dotnet run --project src/PixelCompanion.Config
```

Run the dependency-free core test harness:

```powershell
dotnet run --project tests/PixelCompanion.Core.Tests
```

## Build the Windows installer

Install Inno Setup 6, then run this command from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1
```

The single installer executable is written to `artifacts/windows/installer/`. See the [Windows packaging guide](packaging/windows/README.md) for details.

Public releases are built from version tags and include a final SHA-256 checksum. v0.2.0 is explicitly distributed as an unsigned installer, so the app opens its official GitHub Release page instead of installing it automatically. See the [release process](docs/releasing.md) for the current unsigned flow and the future signed-release path.

## User data

- Windows: `%LOCALAPPDATA%\PixelCompanion`
- macOS: `~/Library/Application Support/PixelCompanion`

See [architecture.md](docs/architecture.md) and [roadmap.md](docs/roadmap.md) for implementation boundaries and remaining phases.

## License

Source code is distributed under the [MIT License](LICENSE). Character assets carry their own licenses in each character manifest. The bundled original character is marked CC0-1.0.
