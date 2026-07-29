# Delivery roadmap

## Implemented foundation

- Phase 1 runtime skeleton: transparent pixel window, idle/walk motion, drag/fall/land, quick menu, persistence, English/Korean resources.
- Shared core: layered behavior priority, pet values, bounded offline progress, localized dialogues, safe platform interface, character pack schema and validation.
- Separate advanced settings executable with shared storage.
- Version 0.2 foundation: simple image-slot character editing, runtime walking frames, secure GitHub update checks, and a separate Windows updater.

## Released in 0.3.0

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

## 0.3.2 Yaroro edition

- Bundle Yaroro as the default companion for this special Windows release.
- Provide consistent front, back, Walk 1/2/3, human mealtime, and sleeping frames.
- Keep custom character images and v0.3.0 settings fully compatible.
- Keep the Yaroro asset notice separate from the MIT software license.

## 0.3.3 Yaroro Hot Fix

- Correct Yaroro's bundled left-facing frames so the character faces the actual movement direction.
- Preserve the existing right-facing convention for user-supplied character images.
- Add regression coverage for both source-facing directions and both movement directions.

## 0.4.0 separate editions and in-app dialogue editing

- Publish the Standard cat edition and `for Yaroro` edition as separate Windows installers in the same release.
- Give each edition its own executable names, install directory, startup entry, user-data directory, bundled character, and update asset.
- Preserve existing Yaroro data by copying the legacy data directory once when the separated edition first starts.
- Add **Edit dialogues...** to the character right-click menu instead of creating another standalone program.
- Edit Korean and English click, feed, play, and sleep dialogue with probability, affection, cooldown, preview, safe saving, and backups.
- Apply saved dialogue immediately and expand the localized `{time}` variable.
- Build and smoke-test both editions in GitHub Actions.

## 0.4.1 window-obstacle Hot Fix

- Preserve native window Z order while discovering candidate walking surfaces.
- Treat only the foreground windows that cover the supporting title-bar line as horizontal collision walls.
- Keep the companion inside the current unobstructed range, turn it around at a wall, and push it to the nearest safe side when a window moves into it.
- Ignore windows behind the supporting window and windows that do not cross its top edge.
- Fall back to normal surface recovery only when no unobstructed range can fit the companion.

## 0.4.2 dialogue editor Hot Fix

- Remove the selection-event feedback loop that could freeze the in-app dialogue editor.
- Keep editing responsive across line selection, text input, numeric options, and language changes.
- Make the Yaroro installer automatically uninstall the Standard application while preserving Standard user data.

## 0.4.3 UI & Drag Hot Fix

- Keep the dialogue editor usable in a small window and at high Windows display scaling by scrolling the field column without moving the footer.
- Stop attached-window synchronization for the duration of a pointer drag.
- Refresh surfaces when the pointer is released, then land on the nearest valid window or desktop surface without requiring the supporting window to close.
- Cover the drag synchronization rule with a regression test.

## 0.4.4 Falling Grab Hot Fix

- Cancel an in-progress fall and landing when the character is grabbed again.
- Prevent outdated landing operations from moving or reattaching a newly dragged character.
- Keep Yaroro's default Korean greeting to one captain-introduction line while preserving user-authored dialogue.
- Keep the unsaved-dialogue confirmation above the modal editor and focus its safe cancel action.
- Preview every bundled character image in Advanced Settings when no custom image is selected.
- Remove stray right-edge fragments from Yaroro's second eating and first sleeping frames.
- Refresh the Korean README around the Yaroro edition and current application captures.

## 0.5.2 Yaroro Life Hotfix

- Preview and lock the intended window or desktop landing surface while dragging.
- Detach from the previous title bar after a downward drag and restore the original placement with `Esc`.
- Add Yaroro tail-propeller, fast-fall, and playful stunned-landing frames.
- Add condition display, petting, cleaning, persistent sleeping and waking, idle-time sleep, and return greetings.
- Keep hunger, cleanliness, happiness, fatigue, and affection bounded with a 12-hour offline cap.
- Add general, focus, rest, and custom timers with persistent remaining time.
- Add hunger, fatigue, happiness, and local-time conditions to the in-app dialogue editor.
- Add two clear Yaroro motion sets for propeller dragging and fast falling with a stunned landing.
- Make quiet bottom-right corner sitting the default while a foreground application is full screen, then restore the previous surface.
- Keep Standard and Yaroro data, custom images, dialogues, and installers compatible.

## 0.5.3 UI and Landing Hotfix

- Wrap bilingual labels in Advanced Settings without allowing text to overlap input controls.
- Treat falling and stunned landing as one exclusive animation operation.
- Prevent idle, walking, sleeping, and profile refresh animation ticks from replacing the active fall frame.
- Keep the stunned landing visible for 2.5 seconds and preserve mid-air grab cancellation.
- Migrate the legacy saved full-screen Hide default to quiet edge waiting.

## Next milestones

1. Continue Windows non-disruption testing against common desktop apps and refine window-surface behavior.
2. Add stopwatch, notes, scheduled reminders, and native system notifications.
3. Expand the character editor with sprite-sheet slicing, frame ordering/FPS preview, anchors, hit boxes, props, dialogue conditions, import/export, and migration-aware pack validation.
4. Implement Windows and macOS adapters for idle time, system load, and notifications.
5. Implement best-effort media and internal-battery adapters, with capability-gated UI and no audio capture.
6. Add macOS packaging, signing guidance, performance budgets, accessibility review, and GitHub release automation.

The priority order is stability and non-disruption first, natural behavior second, then feature breadth.
