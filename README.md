# SnakeAirlines_Codex

Unity 6 project containing one implemented game and two planned subsystems.

## Project Structure
- `Assets/_SnakeAirlinesGame`: current implemented SnakeAirlines prototype.
- `Assets/_CarryOnGame`: reserved for the future second game.
- `Assets/_PrizeManager`: planned shared prize system, UGS integration area, and staff admin app.

Agent-oriented documentation lives in `AGENTS.md` and the guides under `docs/`.

## SnakeAirlines Scripts
Scripts currently live under `Assets/_SnakeAirlinesGame`.

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
4. Set `SnakeHead > Move Action Reference` to `InputSystem_Actions -> Player/Move` from `Assets/_SnakeAirlinesGame/Settings/InputSystem_Actions.inputactions`.
5. Set the starting size on `SnakeBody > Initial Growth`.
6. Create a `Food` prefab with:
   - `SpriteRenderer`
   - `SnakeFood`
7. Assign the `Food` prefab to `FoodManager`.

The playable SnakeAirlines scene currently lives at `Assets/_SnakeAirlinesGame/Scenes/SampleScene.unity`.

## Controls
- Movement comes from the existing `Player/Move` Input Action.
- Default bindings in the provided input asset include WASD and arrow keys.

## Collision Rule
If the head reaches a body segment, the snake trims from that collision point to the tail, and score is reduced by the equivalent removed segment amount instead of ending the game.

## Prize System
The prize subsystem is planned under `Assets/_PrizeManager`. It is intended to host:

- shared prize runtime logic used by kiosk/game clients
- Unity Gaming Services integration
- staff-only admin tooling
- CSV-backed import/export workflows

The architecture and operational expectations for that subsystem are documented in `docs/UGS_PRIZE_SYSTEM.md` and `docs/PRIZE_ADMIN_APP.md`.

## Current Notes
- Some tracked project settings still reference the legacy `Assets/_Game` path.
- New documentation and new code placement should use `Assets/_SnakeAirlinesGame` as the current source of truth.
