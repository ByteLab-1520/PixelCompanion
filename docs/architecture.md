# Architecture

## Process boundary

`PixelCompanion.Desktop` owns the lightweight transparent pet window, input, animation scheduling, movement, and quick menu. `PixelCompanion.Config` owns authoring and advanced preferences. Neither process depends on the other being alive.

Both use `PixelCompanion.Core` and the operating-system user-data directory. `AtomicJsonStore` coordinates writers across both processes with a lock file, writes a temporary file, verifies it, backs up the previous value, and atomically replaces the target. Callers continue treating transient IO failures as recoverable.

## Core boundaries

- Models are platform- and UI-independent records.
- `BehaviorEngine` produces layered activity and mood decisions from pet and environment snapshots.
- `PetStateService` uses wall-clock elapsed time and caps offline progression at 12 hours.
- `LocalizationService` resolves selected language → English → key and reports missing keys without throwing.
- `CharacterPackValidator` rejects unsafe paths and missing required animation data while optional animation fallback remains a renderer concern.
- `IPlatformServices` is the capability boundary for media, battery, idle time, fullscreen, load, auto-start, and notifications. Unsupported APIs return unavailable snapshots rather than failing the application.
- `IDesktopIntegration` is the runtime boundary for read-only program-window discovery, full-screen detection, click-through, the click-through recovery hotkey, and login startup. The Windows adapter only reads target-window metadata; it never sends target windows move, resize, minimize, or input commands.
- `MovementGeometry` keeps monitor, custom-region, and window-top placement calculations independent from Avalonia and Win32 so clamping, DPI-aware placement, and recovery rules can be tested without opening a desktop window.

## Runtime timing

Rendering/animation and movement/state calculations use separate timers. Movement is computed in pixels per elapsed second, not pixels per frame. Hidden and paused pets stop movement work. Future system adapters should use low-frequency polling and event-driven APIs where available.

## Privacy and offline behavior

Core behavior has no network dependency, telemetry, model API, keyboard-content capture, screen capture, microphone access, or file-content scanning. Media metadata and environmental state remain local and optional.
