# GE Travel Games

Unity 6 project containing two games and shared database system.

## Project Structure
- `Assets/_SnakeAirlines`: current implemented SnakeAirlines prototype.
- `Assets/_TapGallery`: second game, tap/shooting gallery, implemented.
- `Assets/_PrizeManager`: shared prize-system foundation, internal admin tooling, and future UGS integration area.

Agent-oriented documentation lives in `AGENTS.md` and the guides under `docs/`.

## SnakeAirlines Scripts
Scripts currently live under `Assets/_SnakeAirlines`.

- `Board`: Grid size, world conversion, wrapping, and free-cell lookup.
- `SnakeHead`: Handles movement, collisions, scoring triggers, and Move action subscription.
- `SnakeBody`: Owns body segments, initial growth length, and updates the trail renderer based on head commands.
- `FoodManager`: Spawns and tracks food.
- `ScoreManager`: Adds and subtracts score.
- `SnakeFood`: Holds food grid coordinates.

## SnakeAirlines Scene Setup
1. Create an empty `Game` object and add these components:
   - `Board`
   - `FoodManager`
   - `ScoreManager`
2. Create a `SnakeHead` GameObject with:
   - `SpriteRenderer`
   - `LineRenderer`
   - `SnakeHead`
   - `SnakeBody`
3. In `SnakeHead`, assign references to `Board`, `FoodManager`, `ScoreManager`, and `SnakeBody` from `Game`.
4. Set `SnakeHead > Move Action Reference` to `InputSystem_Actions -> Player/Move` from `Assets/_SnakeAirlines/Settings/InputSystem_Actions.inputactions`.
5. Set the starting size on `SnakeBody > Initial Growth`.
6. Create a `Food` prefab with:
   - `SpriteRenderer`
   - `SnakeFood`
7. Assign the `Food` prefab to `FoodManager`.

The playable SnakeAirlines scene currently lives at `Assets/_SnakeAirlines/Scenes/SampleScene.unity`.

## Controls
- Movement comes from the existing `Player/Move` Input Action.
- Default bindings in the provided input asset include WASD and arrow keys.

## Collision Rule
If the head reaches a body segment, the snake trims from that collision point to the tail, and score is reduced by the equivalent removed segment amount instead of ending the game.

## Prize System
The prize subsystem lives under `Assets/_PrizeManager`. The current slice includes:

- shared prize CSV parsing and validation
- an in-memory admin service layer for initialize/add/settings/export flows
- an internal admin scene at `Assets/_PrizeManager/Scenes/PrizeAdminScene.unity`
- edit-mode tests for the CSV/admin foundation

Later slices are still expected to add:

- live Unity Gaming Services integration
- cloud-authoritative draw and claim flows
- kiosk-facing runtime UX

The architecture, admin workflow, and CSV contracts for that subsystem are documented in:

- `docs/UGS_PRIZE_SYSTEM.md`
- `docs/PRIZE_ADMIN_APP.md`
- `docs/PRIZE_CSV_SPEC.md`
- `docs/TAPGALLERY_GAME.md`

## Current Notes
- `ProjectSettings/EditorBuildSettings.asset` points at the playable SnakeAirlines scene under `Assets/_SnakeAirlines`.
- Older docs may still mention `_Game` or `_SnakeAirlinesGame`; new documentation and code should use the real folder layout.
