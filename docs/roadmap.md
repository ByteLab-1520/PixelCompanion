# Delivery roadmap

## Implemented foundation

- Phase 1 runtime skeleton: transparent pixel window, idle/walk motion, drag/fall/land, quick menu, persistence, English/Korean resources.
- Shared core: layered behavior priority, pet values, bounded offline progress, localized dialogues, safe platform interface, character pack schema and validation.
- Separate advanced settings executable with shared storage.

## Next milestones

1. Add a visual multi-region overlay editor, monitor hot-plug/DPI recovery, click-through toggle, and tray/menu-bar lifecycle.
2. Expand the character editor with sprite-sheet slicing, frame ordering/FPS preview, anchors, hit boxes, props, dialogue conditions, import/export, and migration-aware pack validation.
3. Add timer, focus/rest timer, stopwatch, notes, notifications, and richer pet interactions.
4. Implement Windows and macOS adapters for idle time, fullscreen, load, auto-start, and notifications.
5. Implement best-effort media and internal-battery adapters, with capability-gated UI and no audio capture.
6. Add Windows/macOS packaging, signing guidance, performance budgets, accessibility review, and GitHub release automation.

The priority order is stability and non-disruption first, natural behavior second, then feature breadth.

