# Repository Agent Guide

## Current Repo Truth
- This is a Unity 6 project (`6000.3.5f2`).
- `Assets/_SnakeAirlines` is the only game area with described gameplay implementation today.
- `Assets/_TapGallery` is the second game (tap/shooting gallery). Requirements are fully documented in `docs/TAPGALLERY_GAME.md`. It is specified but not yet implemented.
- `Assets/_PrizeManager` now contains the first Prize Manager slice: CSV parsing and validation, an in-memory admin service layer, edit-mode tests, and an internal admin scene.
- UGS packages for prize work are already present in `Packages/manifest.json`, but live prize backend wiring is still not implemented in this repo.
- Some tracked settings and older docs still reference `Assets/_Game` or `Assets/_SnakeAirlinesGame`; use the real folder layout as the source of truth for new documentation and new code placement.

## Subsystem Boundaries
- Keep SnakeAirlines runtime work inside `Assets/_SnakeAirlines` unless you are doing shared infrastructure that truly belongs elsewhere.
- Keep TapGallery work inside `Assets/_TapGallery`. Use `docs/TAPGALLERY_GAME.md` as the implementation reference.
- Keep prize, kiosk, admin, and UGS-related work inside `Assets/_PrizeManager`.
- Do not mix prize backend logic into SnakeAirlines-specific folders.

## Prize System Expectations
- Treat the prize system as a global service layer that can be called by more than one game.
- The intended v1 backend stack is `Authentication`, `Cloud Code`, `Cloud Save Game Data`, and `Remote Config`.
- `Economy` is out of scope for v1.
- The authoritative draw result, reservation lifecycle, daily cap behavior, and won-prize persistence belong in the cloud, not in kiosk-local game logic.
- CSV import/export is part of the prize/admin workflow, and the concrete CSV contract is documented in `docs/PRIZE_CSV_SPEC.md`.
- CSV parsing is positional rather than header-based so spreadsheet-exported files remain usable across different languages.

## Documentation Rules
- Keep `README.md`, this file, and the docs under `docs/` aligned whenever subsystem names or ownership boundaries change.
- Do not describe `_TapGallery` as implemented unless code actually exists.
- Describe `_PrizeManager` as partially implemented until live cloud-backed prize flows exist.
- When legacy `_Game` or `_SnakeAirlinesGame` references are found in docs, prefer updating the docs to `_SnakeAirlines` unless the file is specifically documenting a known inconsistency.
- If docs conflict with current repo structure, prefer the real folder layout and call out the inconsistency explicitly.

## Recommended Doc Set
- `README.md`: short repo overview and current playable game.
- `docs/PROJECT_GUIDE.md`: subsystem map and ownership boundaries.
- `docs/UGS_PRIZE_SYSTEM.md`: backend architecture and runtime/cloud responsibilities.
- `docs/PRIZE_ADMIN_APP.md`: staff-only admin surface and operational workflow.
- `docs/PRIZE_CSV_SPEC.md`: CSV contract for prize import, settings import, and won-prize export.
- `docs/TAPGALLERY_GAME.md`: full game design, ScriptableObject contracts, MonoBehaviour specs, behavior types, and scene setup for TapGallery.
