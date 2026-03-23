# TapGallery Game

## Overview
TapGallery is the second game in GE-Travel-Games. It is a tap/shooting gallery hybrid: static backdrop images (Spots) are placed around the scene, and tappable targets (Tappables) emerge from them in various movement patterns. The player scores by tapping Tappables before they disappear. Lives under `Assets/_TapGallery`. Has no dependency on `_PrizeManager` in V1; prize integration is a future slice.

## Folder Layout
```
Assets/_TapGallery/
├── Scripts/
├── Scenes/        # TapGalleryScene.unity lives here
├── Prefabs/       # One Spot prefab; one Tappable prefab per Tappable type
├── Art/
└── Configs/       # All ScriptableObject assets (.asset files)
```

---

## ScriptableObjects

### `SpotConfig`
Reusable per-Spot-type config asset. Multiple scene Spot instances can share the same `SpotConfig`.

| Field | Type | Description |
|-------|------|-------------|
| `PeekDirections` | `[Flags] Direction` | Sides valid for **Peek** behavior |
| `JumpDirections` | `[Flags] Direction` | Sides valid for **PeekAndJump** and **Jump** behaviors (Tappable returns to Spot) |
| `RunDirections` | `[Flags] Direction` | Sides valid for **PeekJumpAndRun**, **PeekAndRun**, **Run** behaviors (Tappable exits Spot) |
| `TappableWeights` | `List<TappableWeightEntry>` | Weighted list of `TappableConfig` assets this Spot type can spawn |

`Direction` enum: `[Flags] enum Direction { None = 0, Top = 1, Bottom = 2, Left = 4, Right = 8 }`

`TappableWeightEntry`: `TappableConfig Config`, `float Weight`

### `TappableConfig`
Reusable per-Tappable-type config asset. Each config maps to exactly one prefab variant; the config is baked into the prefab at design time and is not passed at spawn time.

| Field | Type | Description |
|-------|------|-------------|
| Sprite | `Sprite` | Visual for the Tappable |
| `Behavior` | `TappableBehavior` enum | Fixed behavior for this type (see Behavior Types) |
| `Score` | `int` | Flat score awarded (or deducted if `IsPenalty`) on a successful tap |
| `IsPenalty` | `bool` | If true, tapping this Tappable **subtracts** `Score` from the player's total (clamped at 0) |
| `MovementSpeed` | `float` | Units/second for run/approach movement |
| `PeekDuration` | `float` | Seconds the Tappable stays visible during the peek phase |
| `JumpHeight` | `float` | Max displacement units for Jump/PeekAndJump/PeekJumpAndRun |
| `JumpCurve` | `AnimationCurve` | Maps normalized time [0,1] → normalized displacement [0,1]; used by **PeekAndJump** and **Jump** only. Displacement = `JumpCurve.Evaluate(t) × JumpHeight`. Not used by PeekJumpAndRun. |
| `RunArrivalDistanceThreshold` | `float` | Distance (world units) to Spot center that counts as "arrived" at a RunTarget. Keep low — Tappables should hide quickly. |

`TappableBehavior` enum values (in score order): `Peek`, `PeekAndJump`, `Jump`, `PeekJumpAndRun`, `PeekAndRun`, `Run`

### `StageConfig`
`[Serializable]` class (not ScriptableObject) — serialized inline on `StageManager` because `ActiveSpots` holds scene references that ScriptableObjects cannot store. Defines which Spots, Tappable types, and spawn pacing are active during a stage.

| Field | Type | Description |
|-------|------|-------------|
| `ActiveSpots` | `List<Spot>` | Scene MonoBehaviour refs to enabled Spots for this stage |
| `AllowedTappableConfigs` | `List<TappableConfig>` | Only these Tappable types can spawn |
| `SpawnInterval` | `float` | Seconds between spawn-loop ticks for this stage |
| `MaxTappablesOnScreen` | `int` | Hard cap on simultaneous live Tappables for this stage |
| `ScoreMilestone` | `int` | Score threshold to advance to next stage (0 = disabled) |
| `TimeMilestone` | `float` | Elapsed session time (seconds) threshold to advance (0 = disabled) |

---

## Core MonoBehaviours

### `Spot`
Scene instance of a backdrop image. Never moves.

```
[SerializeField] SpotConfig Config
[SerializeField] List<Spot> RunTargets   // scene refs; cannot live in ScriptableObject
```

- Exposes world bounds so `Tappable` can compute peek edge positions and arrival checks.
- `RunTargets` is a scene-level list of other `Spot` MonoBehaviours a Tappable can run toward. If empty, running Tappables exit to screen edge.

### `Tappable`
The entity the player taps. Managed by a per-config object pool; never calls `Destroy` on itself.

```
[SerializeField] TappableConfig Config   // baked into prefab at design time
```

- Entry point: `StartBehavior(Direction direction, Spot originSpot, Spot runTarget, Action onDone)`
- Resets all state at the start of `StartBehavior` each time it is retrieved from the pool.
- On tap: awards `Config.Score` to manager, invokes `onDone`.
- On behavior complete (unhit): invokes `onDone` without scoring.
- Uses an `Action onDone` callback to notify the manager; no circular reference to manager.
- Requires `Collider2D` sized to sprite for tap detection via `IPointerClickHandler`.

### `TapGalleryManager`
Drives the game loop. One per scene.

```
[SerializeField] float SessionDuration          // total session time in seconds
[SerializeField] float SpawnInterval            // fallback interval used when no StageManager present
[SerializeField] int MaxTappablesOnScreen       // fallback cap used when no StageManager present
[SerializeField] List<SpotWeightEntry> SpotWeights      // Spot + float weight
[SerializeField] List<TappablePoolEntry> TappablePools  // TappableConfig + Tappable prefab + int prewarmCount
[SerializeField] TMP_Text ScoreLabel
[SerializeField] StageManager StageManager      // optional; null = V1 mode
```

`SpotWeightEntry`: `Spot Spot`, `float Weight`
`TappablePoolEntry`: `TappableConfig Config`, `Tappable Prefab`, `int PrewarmCount`

**Pool management:** One `UnityEngine.Pool.ObjectPool<Tappable>` per `TappablePoolEntry`, keyed by `TappableConfig`. Created in `Awake`, pre-warmed to `PrewarmCount`. On get: `gameObject.SetActive(true)`. On release: `gameObject.SetActive(false)`.

**Spawn loop** (ticks every `StageManager.GetSpawnInterval(spawnInterval)` — or `spawnInterval` in V1 mode):
1. If active Tappable count >= `StageManager.GetMaxTappablesOnScreen(maxTappablesOnScreen)`, skip.
2. Weighted-random pick a `Spot` from `SpotWeights` (filter by `StageManager.GetActiveSpots()` if V2).
3. Weighted-random pick a `TappableConfig` from that Spot's `Config.TappableWeights` (filter by `StageManager.GetAllowedTappables()` if V2).
4. Validate that the Tappable's `Behavior` maps to a non-empty direction set on the Spot (see Behavior → Direction mapping below). If invalid, skip and `Debug.LogWarning`.
5. Pick a random valid `Direction` from the appropriate set.
6. For run behaviors: pick a random `Spot` from `Spot.RunTargets` if non-empty, else `null` (target = screen edge).
7. Look up the pool for the selected `TappableConfig`, get a `Tappable`, call `StartBehavior(direction, spot, runTarget, onDone)`, increment active count.

**Tappable completion:** `onDone` fires → manager adds/subtracts score if tapped → releases Tappable to its pool → decrements active count.

**Session:** Countdown from `SessionDuration`. On expiry, stop spawn loop and present end state.

### `StageManager`
Optional component. If not present, `TapGalleryManager` treats all Spots and Tappables as active (V1 mode).

```
[SerializeField] List<StageConfig> Stages   // ordered, index 0 = first stage
int CurrentStageIndex = 0                   // only ever increases
event Action<int> OnStageAdvanced           // fires with new stage index
```

- `CheckAdvancement(int currentScore, float elapsedTime)` is called by `TapGalleryManager` on score changes and spawn-loop ticks. If current stage's `ScoreMilestone` or `TimeMilestone` is exceeded, advance `CurrentStageIndex` by 1. First condition to trigger wins; index never decreases.
- `GetActiveSpots()` → returns `Stages[CurrentStageIndex].ActiveSpots`
- `GetAllowedTappables()` → returns `Stages[CurrentStageIndex].AllowedTappableConfigs`

---

## Behavior Types

Ordered by intended score value, lowest to highest.

| # | Name | Description | Valid Direction Set |
|---|------|-------------|---------------------|
| 1 | **Peek** | Appears at Spot edge, stays `PeekDuration` seconds, retracts. No net displacement. | `PeekDirections` |
| 2 | **PeekAndJump** | Peeks, then jumps out and returns to the Spot. Jump position driven by `JumpCurve.Evaluate(t) × JumpHeight`. Not physics-simulated. | `JumpDirections` |
| 3 | **Jump** | No peek; immediately jumps and returns. Same curve-driven motion as PeekAndJump. Harder to react to. | `JumpDirections` |
| 4 | **PeekJumpAndRun** | Peeks, then launches a true parabolic arc in the chosen direction (fake gravity always points **down**; Left/Right only), lands, then runs to screen edge or RunTarget. | `RunDirections` (**Left/Right only**) |
| 5 | **PeekAndRun** | Peeks in direction, quickly retracts into Spot, then runs out in same direction to screen edge or RunTarget. | `RunDirections` |
| 6 | **Run** | No peek; exits immediately in a straight line to screen edge or RunTarget. | `RunDirections` |

**Direction constraints:**
- Behavior → direction set mapping: `Peek→PeekDirections`, `PeekAndJump/Jump→JumpDirections`, `PeekJumpAndRun/PeekAndRun/Run→RunDirections`.
- `PeekJumpAndRun` is only valid for `Left` or `Right`. If the picked direction is `Top` or `Bottom`, skip with `Debug.LogWarning`.

**RunTarget arrival:** Compare Tappable world position to RunTarget `Spot` center each frame. When distance < `TappableConfig.RunArrivalDistanceThreshold`, invoke `onDone` without scoring (Tappable "hid" in the Spot).

---

## Score Values

Score on tap = `TappableConfig.Score` (flat int). If `IsPenalty` is true, the score is subtracted instead of added (player total clamped at 0). No runtime multiplier. Behavior difficulty and point value are determined when configuring Tappable prefabs. Design guideline (not code-enforced):

`Score(Peek) < Score(PeekAndJump) < Score(Jump) < Score(PeekJumpAndRun) < Score(PeekAndRun) < Score(Run)`

---

## UI / Score Display

One UI Canvas inside `TapGalleryScene`. `TapGalleryManager` holds a `TMP_Text ScoreLabel` reference and updates it immediately on every score change. No shared HUD from the project.

---

## Input

Uses the Input System package already present in `Packages/manifest.json`. Tap detection: each `Tappable` implements `IPointerClickHandler` on a `Collider2D` sized to its sprite. No extra dependencies or input action assets required.

---

## Scene Setup (V1)

1. Create `Assets/_TapGallery/Scenes/TapGalleryScene.unity`.
2. Create `SpotConfig` assets in `Assets/_TapGallery/Configs/`; fill `PeekDirections`, `JumpDirections`, `RunDirections`, and `TappableWeights`.
3. Create `TappableConfig` assets for each Tappable type; set `Behavior`, `Score`, movement fields, and `JumpCurve` where applicable.
4. Create a `TapGalleryConfig` asset; set `SessionDuration`, `MaxTappablesOnScreen`, `SpawnInterval`.
5. Create a `Spot` prefab: `SpriteRenderer` + `Spot` MonoBehaviour.
6. Place Spot instances in the scene; assign each instance's `Config` (SpotConfig) and `RunTargets` (list of other scene Spots).
7. Create one `Tappable` prefab per Tappable type: `SpriteRenderer` + `Collider2D` + `Tappable` MonoBehaviour; assign the matching `TappableConfig` to each prefab.
8. Create an empty `GameManager` object; attach `TapGalleryManager`; assign `TapGalleryConfig`, `ScoreLabel`; fill `SpotWeights` (scene Spot refs + weights); fill `TappablePools` (one entry per Tappable type: config + prefab + prewarm count).
9. Add a UI Canvas with a `TMP_Text` score label; assign it to `TapGalleryManager.ScoreLabel`.
10. Register scene in `ProjectSettings/EditorBuildSettings.asset`.

---

## Boundary Rules

- All TapGallery gameplay scripts live in `Assets/_TapGallery`.
- No `_PrizeManager` calls from TapGallery scripts in V1.
- No shared infrastructure inside `Assets/_TapGallery`; shared utilities belong elsewhere.
- `StageManager` is a V2 addition. `TapGalleryManager` must not hard-depend on it; check for presence at runtime (null-safe optional reference).
- `RunTargets` are scene-level `Spot` MonoBehaviour references on the `Spot` component. They are never stored inside `SpotConfig` ScriptableObject assets (ScriptableObjects cannot hold scene references).
