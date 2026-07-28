# Pixel Companion

[한국어](README.md) | [English](README.en.md)

![Pixel Companion hero](docs/media/pixel-companion-hero.png)

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

## v0.4.2 dialogue editor Hot Fix

- Fixes the Yaroro dialogue editor becoming unresponsive as soon as it opens.
- Keeps the selected list item stable while text, probability, affection, and cooldown values are edited.
- The Yaroro installer now automatically uninstalls the Standard application first, while preserving its user data.
- Adds a dedicated front-facing pixel portrait icon to the Yaroro executables, tray, and installer.
- Sets Yaroro's Korean default greeting to `안녕? 야로로대장이야` and safely migrates only the untouched legacy default.

## v0.4.1 window-obstacle Hot Fix

- A foreground window covering part of the supporting title bar now becomes a collision wall.
- The companion stops at the edge, turns around, and continues walking on the unobstructed part of the same window.
- If a foreground window moves into the companion, the companion is moved to the nearest safe side and turns away.
- Windows behind the supporting window, and windows that do not cross its title-bar line, are not treated as obstacles.
- Existing surface recovery is used only when no space remains large enough for the companion.

## What's new in v0.4.0

The Standard and `for Yaroro` editions are now separate products published in the same release. They use different executable names, install directories, startup entries, and user-data directories, so both can be installed side by side.

- Standard: `PixelCompanion-Installer.exe`
- for Yaroro: `PixelCompanion-Yaroro-Installer.exe`
- Right-click the character and choose **Edit dialogues...** to edit dialogue inside the main app.
- Edit Korean and English lines for click, feed, play, and sleep reactions.
- Set output probability, minimum affection, and cooldown per line, then preview it in the speech bubble.
- Saved lines take effect immediately and use atomic saving with backups.
- `{time}` expands to the current localized time; unsupported variables remain visible safely.

## v0.3.3 for Yaroro Hot Fix

- Fixes Yaroro walking while visually facing the opposite direction.
- Keeps the original left-facing artwork while moving left and flips it only while moving right.
- Preserves the existing right-facing convention for user-supplied character images.
- Adds regression coverage for all four movement-direction and source-facing combinations.

## v0.3.2 for Yaroro

`v0.3.2 for Yaroro` is a special build that bundles Yaroro as the default companion.

- Includes front, back, and three walking frames.
- Uses two human mealtime frames with rice and side dishes rather than pet food.
- Includes two calm sleeping frames on a pillow.
- Keeps every frame on the same transparent 418×418 canvas.
- User images selected in Character Settings still take priority over the bundled Yaroro frames.

<p align="center">
  <img src="assets/characters/Yaroro/sprites/yaroro-sprite-sheet.png" alt="Yaroro character set with idle, back, walking, human mealtime, and sleeping actions" width="720">
</p>

The Yaroro character assets are not covered by the repository's MIT software license. Rights to the original character and reference artwork remain with their respective owner(s); do not extract or redistribute the assets without separate permission.

## Default character update in v0.3.0

- Redraws the default cat with a lower body that clearly walks on all four paws.
- Renames the directional walking slots to the neutral Walk 1, Walk 2, and Walk 3.
- Adds a matching back view, two eating frames, and two sleeping frames.
- Plays the new artwork during feeding and sleeping interactions.
- Keeps user characters made with the legacy walking-slot names compatible.

<p align="center">
  <img src="assets/characters/DefaultCat/sprites/default-cat-sprite-sheet.png" alt="The redesigned default cat with idle, back, walking, eating, and sleeping actions" width="720">
</p>

## What's new in v0.2.1

- Aligns the bilingual Choose and Remove controls as equal-width vertical buttons.
- Prevents long Korean and English button labels from overlapping neighboring cards.
- Adds a real character-settings capture, a README hero, and an animated walking preview.
- Revalidates Windows installation, executable versions, and uninstallation for the patch release.

## Custom character images

Drop PNG, JPG, JPEG, or GIF files into nine dedicated slots for the default pose, back view, three walking frames, two eating frames, and two sleeping frames. Missing action images safely fall back to the default pose.

![Pixel Companion v0.2.1 character settings layout](docs/media/character-settings.png)

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
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1 -Version 0.4.2 -Edition Standard
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1 -Version 0.4.2 -Edition Yaroro
```

The installers are written to `artifacts/windows/standard/installer/` and `artifacts/windows/yaroro/installer/`. See the [Windows packaging guide](packaging/windows/README.md) for details.

Public releases are built from version tags and include a final SHA-256 checksum. v0.2.1 is explicitly distributed as an unsigned installer, so the app opens its official GitHub Release page instead of installing it automatically. See the [release process](docs/releasing.md) for the current unsigned flow and the future signed-release path.

## User data

- Windows: `%LOCALAPPDATA%\PixelCompanion`
- Windows, for Yaroro: `%LOCALAPPDATA%\PixelCompanion-Yaroro`
- macOS: `~/Library/Application Support/PixelCompanion`

See [architecture.md](docs/architecture.md) and [roadmap.md](docs/roadmap.md) for implementation boundaries and remaining phases.

## License

Source code is distributed under the [MIT License](LICENSE). Character assets carry their own licenses in each character manifest. The bundled original character is marked CC0-1.0.
