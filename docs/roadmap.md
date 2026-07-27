# Delivery roadmap

## Implemented foundation

- Phase 1 runtime skeleton: transparent pixel window, idle/walk motion, drag/fall/land, quick menu, persistence, English/Korean resources.
- Shared core: layered behavior priority, pet values, bounded offline progress, localized dialogues, safe platform interface, character pack schema and validation.
- Separate advanced settings executable with shared storage.
- Version 0.2 foundation: simple image-slot character editing, runtime walking frames, secure GitHub update checks, and a separate Windows updater.

## Current development target: 0.3.0

- Redraw the default cat as a lower four-paw character set with neutral Walk 1/2/3 naming, a back view, and two-frame eating and sleeping actions.
- Treat eligible program-window top edges as read-only walking surfaces. Pixel Companion never moves, resizes, minimizes, or otherwise controls those windows.
- Follow a surface while its window moves or resizes, then recover safely when it is minimized, closed, or no longer eligible.
- Offer desktop-only, program-windows-only, and combined movement modes with a per-process exclusion list.
- Add a translucent multi-region editor with drawing, moving, resizing, naming, monitor presets, deletion, and taskbar-safe working-area presets.
- Recover across monitor hot-plug, resolution, work-area, and DPI changes without changing the chosen character scale.
- Add click-through with tray recovery and the `Ctrl+Alt+P` safety toggle.
- Expand the tray menu with movement mode, regions, click-through, do-not-disturb, auto-start, show/hide, pause, settings, and exit.
- Hide or wait at the edge for full-screen applications, based on user settings, and restore the previous desktop behavior afterward.
- Keep v0.2.1 settings and character images compatible. New fields use safe defaults when an older settings file is loaded.
- Continue distributing unsigned installers until the project has enough public trust signals to revisit code signing.

## Next milestones

1. Finish v0.3.0 Windows validation, installer smoke tests, and non-disruption testing against common desktop apps.
2. Expand the character editor with sprite-sheet slicing, frame ordering/FPS preview, anchors, hit boxes, props, dialogue conditions, import/export, and migration-aware pack validation.
3. Add timer, focus/rest timer, stopwatch, notes, notifications, and richer pet interactions.
4. Implement Windows and macOS adapters for idle time, system load, and notifications.
5. Implement best-effort media and internal-battery adapters, with capability-gated UI and no audio capture.
6. Add macOS packaging, signing guidance, performance budgets, accessibility review, and GitHub release automation.

The priority order is stability and non-disruption first, natural behavior second, then feature breadth.
