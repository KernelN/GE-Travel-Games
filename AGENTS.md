# Repository Agent Guide

## Current Repo Truth
- This is a Unity 6 project (`6000.3.5f2`).
- `Assets/_SnakeAirlinesGame` is the only game area with described gameplay implementation today.
- `Assets/_CarryOnGame` is reserved for the future second game and should be treated as a placeholder until its requirements exist.
- `Assets/_PrizeManager` is reserved for the prize system, Unity Gaming Services integration, and the staff admin app.
- UGS packages are not currently installed in `Packages/manifest.json`.
- Some tracked settings and older docs still reference `Assets/_Game`; use the current folder layout as the source of truth for new documentation and new code placement.

## Subsystem Boundaries
- Keep SnakeAirlines runtime work inside `Assets/_SnakeAirlinesGame` unless you are doing shared infrastructure that truly belongs elsewhere.
- Keep CarryOn work inside `Assets/_CarryOnGame` and avoid inventing gameplay or data contracts before that game is specified.
- Keep prize, kiosk, admin, and UGS-related work inside `Assets/_PrizeManager`.
- Do not mix prize backend logic into SnakeAirlines-specific folders.

## Prize System Expectations
- Treat the prize system as a global service layer that can be called by more than one game.
- The intended v1 backend stack is `Authentication`, `Cloud Code`, `Cloud Save Game Data`, and `Remote Config`.
- `Economy` is out of scope for v1.
- The authoritative draw result, reservation lifecycle, daily cap behavior, and won-prize persistence belong in the cloud, not in kiosk-local game logic.
- CSV import/export is part of the prize/admin workflow, but the detailed CSV contract is intentionally deferred to a later document.

## Documentation Rules
- Keep `README.md`, this file, and the docs under `docs/` aligned whenever subsystem names or ownership boundaries change.
- Do not describe `_CarryOnGame` or `_PrizeManager` as implemented unless code actually exists.
- When legacy `_Game` references are found in docs, prefer updating the docs to `_SnakeAirlinesGame` unless the file is specifically documenting a known inconsistency.
- If docs conflict with current repo structure, prefer the real folder layout and call out the inconsistency explicitly.

## Recommended Doc Set
- `README.md`: short repo overview and current playable game.
- `docs/PROJECT_GUIDE.md`: subsystem map and ownership boundaries.
- `docs/UGS_PRIZE_SYSTEM.md`: backend architecture and runtime/cloud responsibilities.
- `docs/PRIZE_ADMIN_APP.md`: staff-only admin surface and operational workflow.
