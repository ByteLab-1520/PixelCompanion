# Pixel Companion

Pixel Companion is an offline-first, non-disruptive pixel desktop pet for Windows and macOS. It uses deterministic rules, probabilities, cooldowns, and localized dialogue data—never a generative AI service.

The repository currently contains the first runnable foundation:

- a transparent, always-on-top Avalonia pet window with nearest-neighbor rendering;
- elapsed-time movement with idle periods, dragging, falling, landing, dialogue, feeding, and play reactions;
- English and Korean resources with English/key fallback;
- a separate advanced settings application sharing the same atomic JSON data store;
- layered behavior decisions, bounded offline Tamagotchi state progression, dialogue repetition prevention, and character-pack validation;
- safe platform-service fallbacks for unavailable OS capabilities.

## Build and run

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

Build a self-contained Windows 10/11 x64 installer:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Build-WindowsInstaller.ps1
```

The single installer executable is written to `artifacts/windows/installer/`. See [the Windows packaging guide](packaging/windows/README.md) for details.

User data is kept outside the installation directory:

- Windows: `%LOCALAPPDATA%\PixelCompanion`
- macOS: `~/Library/Application Support/PixelCompanion`

See [architecture.md](docs/architecture.md) and [roadmap.md](docs/roadmap.md) for implementation boundaries and remaining phases.

## Licensing

Source code is MIT licensed. Character assets carry their own license in each character manifest. The bundled original character is marked CC0-1.0.
