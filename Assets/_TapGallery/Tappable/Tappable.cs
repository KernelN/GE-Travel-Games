using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class Tappable : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TappableConfig config;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] SpriteRenderer bodySpriteRenderer;

    public TappableConfig Config => config;
    public bool WasTapped { get; private set; }

    Action onDone;
    Action onPeekComplete;
    TappableTrailController activeTrail;
    float halfHeight;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        halfHeight = spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.sprite.bounds.extents.y
            : 0f;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetSprite(Sprite sprite)
    {
        spriteRenderer.sprite = sprite;
        halfHeight = sprite != null ? sprite.bounds.extents.y : 0f;
    }

    public void StartBehavior(Direction direction, Spot originSpot, Spot runTarget, Action callback,
        TappableTrailController trail = null, Action peekCallback = null)
    {
        WasTapped = false;
        onDone = callback;
        onPeekComplete = peekCallback;
        activeTrail = trail;
        StopAllCoroutines();
        SetRotationZ(0f); // defensive reset — prevent leftover rotation from pooled instances

        switch (config.Behavior)
        {
            case TappableBehavior.Peek:
                StartCoroutine(PeekRoutine(direction, originSpot));
                break;
            case TappableBehavior.PeekAndJump:
                StartCoroutine(PeekAndJumpRoutine(direction, originSpot));
                break;
            case TappableBehavior.Jump:
                StartCoroutine(JumpRoutine(direction, originSpot));
                break;
            case TappableBehavior.PeekJumpAndRun:
                StartCoroutine(PeekJumpAndRunRoutine(direction, originSpot, runTarget));
                break;
            case TappableBehavior.PeekAndRun:
                StartCoroutine(PeekAndRunRoutine(direction, originSpot, runTarget));
                break;
            case TappableBehavior.Run:
                StartCoroutine(RunRoutine(direction, originSpot, runTarget));
                break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (WasTapped) return;
        WasTapped = true;
        StopAllCoroutines();
        // Stop emitting trail particles; existing world-space particles die naturally.
        activeTrail?.StopEmitting();
        activeTrail = null;
        Complete();
    }

    // ── Behavior coroutines ───────────────────────────────────────────────────

    IEnumerator PeekRoutine(Direction dir, Spot spot)
    {
        Vector2 hiddenPos  = spot.Center;
        Vector2 peekPos    = GetPeekPosition(dir, spot, config.PeekDistance);
        float   peekAngle  = dir == Direction.Bottom ? 180f : (IsHorizontal(dir) ? GetPeekRotationZ(dir) : 0f);
        float   startAngle = IsHorizontal(dir) ? 0f : peekAngle; // horizontal starts upright and leans; vertical starts at final orientation

        transform.position = hiddenPos;
        SetRotationZ(startAngle);

        // Head rotates toward peek direction as it emerges
        yield return IsHorizontal(dir)
            ? MoveTowardsWithRotation(peekPos, 0f, peekAngle)
            : MoveTowards(peekPos);

        yield return new WaitForSeconds(config.PeekDuration);

        // Head rotates back to upright as it retreats
        yield return IsHorizontal(dir)
            ? MoveTowardsWithRotation(hiddenPos, peekAngle, 0f)
            : MoveTowards(hiddenPos);

        SetRotationZ(0f);
        Complete();
    }

    IEnumerator PeekAndJumpRoutine(Direction dir, Spot spot)
    {
        Vector2 hiddenPos  = spot.Center;
        Vector2 peekPos    = GetPeekPosition(dir, spot, config.PeekDistance);
        float   jumpAngle  = GetJumpRotationZ(dir);

        transform.position = hiddenPos;
        SetRotationZ(jumpAngle);            // start already pointing toward jump dir

        yield return MoveTowards(peekPos);  // move out, rotation unchanged

        yield return new WaitForSeconds(config.PeekDuration);

        // Jump phase — rotation held at jumpAngle throughout
        Vector2 outward      = GetOutwardDirection(dir);
        float   jumpDuration = config.JumpHeight / Mathf.Max(config.MovementSpeed, 0.01f);
        float   elapsed      = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t            = Mathf.Clamp01(elapsed / jumpDuration);
            float displacement = config.JumpCurve.Evaluate(t) * config.JumpHeight;
            transform.position = peekPos + outward * displacement;
            yield return null;
        }

        // Return — hold jump orientation throughout; snap upright once hidden
        yield return MoveTowards(hiddenPos);

        SetRotationZ(0f);
        Complete();
    }

    IEnumerator JumpRoutine(Direction dir, Spot spot)
    {
        Vector2 hiddenPos  = spot.Center;
        Vector2 peekPos    = GetPeekPosition(dir, spot, config.PeekDistance);
        float   jumpAngle  = GetJumpRotationZ(dir);

        // No peek — start at edge, already pointing toward jump dir
        transform.position = peekPos;
        SetRotationZ(jumpAngle);

        // Jump phase
        Vector2 outward      = GetOutwardDirection(dir);
        float   jumpDuration = config.JumpHeight / Mathf.Max(config.MovementSpeed, 0.01f);
        float   elapsed      = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t            = Mathf.Clamp01(elapsed / jumpDuration);
            float displacement = config.JumpCurve.Evaluate(t) * config.JumpHeight;
            transform.position = peekPos + outward * displacement;
            yield return null;
        }

        // Hold jump orientation throughout; snap upright once hidden
        yield return MoveTowards(hiddenPos);
        SetRotationZ(0f);
        Complete();
    }

    IEnumerator PeekJumpAndRunRoutine(Direction dir, Spot spot, Spot runTarget)
    {
        Vector2 hiddenPos = spot.Center;
        Vector2 peekPos   = GetPeekPosition(dir, spot, config.PeekDistance);
        float   peekAngle = IsHorizontal(dir) ? GetPeekRotationZ(dir) : 0f;

        transform.position = hiddenPos;
        SetRotationZ(0f);

        // Head points toward jump direction from the start of the peek
        yield return IsHorizontal(dir)
            ? MoveTowardsWithRotation(peekPos, 0f, peekAngle)
            : MoveTowards(peekPos);

        yield return new WaitForSeconds(config.PeekDuration);
        onPeekComplete?.Invoke();
        onPeekComplete = null;

        // Parabolic arc (horizontal movement + fake downward gravity)
        float horizontalSpeed = config.MovementSpeed;
        float gravity         = horizontalSpeed * horizontalSpeed / Mathf.Max(config.JumpHeight * 2f, 0.01f);
        float vy              = Mathf.Sqrt(2f * gravity * config.JumpHeight);
        float vx              = dir == Direction.Left ? -horizontalSpeed : horizontalSpeed;
        // Estimate total arc duration for rotation lerp (exact for symmetric parabola)
        float arcDuration     = 2f * vy / Mathf.Max(gravity, 0.001f);

        Vector2 pos    = peekPos;
        float   startY = peekPos.y;
        float   arcT   = 0f;

        while (pos.y >= startY - 0.01f || vy > 0f)
        {
            pos.x += vx * Time.deltaTime;
            pos.y += vy * Time.deltaTime;
            vy    -= gravity * Time.deltaTime;
            arcT  += Time.deltaTime;

            // Gradually right the rotation: peekAngle → 0° (upright) during the arc
            float rotT = Mathf.Clamp01(arcT / arcDuration);
            SetRotationZ(Mathf.LerpAngle(peekAngle, 0f, rotT));

            transform.position = pos;
            yield return null;

            // Safety: if Tappable exits screen, stop arc
            if (IsOutsideScreen(pos))
                break;
        }

        // Guarantee upright on landing regardless of early screen-exit
        SetRotationZ(0f);

        // Run phase after landing — upright with bumps
        yield return RunPhase(dir, runTarget);
        Complete();
    }

    IEnumerator PeekAndRunRoutine(Direction dir, Spot spot, Spot runTarget)
    {
        Vector2 hiddenPos = spot.Center;
        Vector2 peekPos = GetPeekPosition(dir, spot, config.PeekDistance);

        // Peek phase
        transform.position = hiddenPos;
        yield return MoveTowards(peekPos);
        yield return new WaitForSeconds(config.PeekDuration);
        onPeekComplete?.Invoke();
        onPeekComplete = null;

        // Retract quickly back into spot
        yield return MoveTowards(hiddenPos);

        // Run phase
        yield return RunPhase(dir, runTarget);
        Complete();
    }

    IEnumerator RunRoutine(Direction dir, Spot spot, Spot runTarget)
    {
        // No peek — start at spot edge and immediately run
        transform.position = GetPeekPosition(dir, spot, config.PeekDistance);

        yield return RunPhase(dir, runTarget);
        Complete();
    }

    // ── Shared movement helpers ───────────────────────────────────────────────

    IEnumerator MoveTowards(Vector2 target)
    {
        float speed = config.MovementSpeed;
        while (Vector2.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    IEnumerator MoveTowardsWithRotation(Vector2 target, float fromAngle, float toAngle)
    {
        float totalDist = Vector2.Distance(transform.position, target);
        if (totalDist < 0.001f)
        {
            SetRotationZ(toAngle);
            yield break;
        }

        float speed = config.MovementSpeed;
        while (Vector2.Distance(transform.position, target) > 0.01f)
        {
            float remaining = Vector2.Distance(transform.position, target);
            float t = 1f - Mathf.Clamp01(remaining / totalDist); // 0→1 as we approach target
            SetRotationZ(Mathf.LerpAngle(fromAngle, toAngle, t));
            transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
        SetRotationZ(toAngle);
    }

    IEnumerator RunPhase(Direction dir, Spot runTarget)
    {
        // Track base position separately so the bump offset doesn't corrupt MoveTowards math
        Vector2 basePos  = transform.position;
        float   bumpTime = 0f;

        Vector2 destination = runTarget != null
            ? runTarget.Center
            : GetScreenExitPosition(dir, basePos);

        float arrivalThreshold = runTarget != null
            ? config.RunArrivalDistanceThreshold
            : 0.05f;

        float speed = config.MovementSpeed;

        // Begin trail behind the runner (opposite to initial movement direction).
        if (activeTrail != null)
        {
            Vector2 initialMoveDir = (destination - basePos).normalized;
            activeTrail.BeginAt(GetFootPosition(), -initialMoveDir);
        }

        while (Vector2.Distance(basePos, destination) > arrivalThreshold)
        {
            bumpTime += Time.deltaTime;
            float bumpY = Mathf.Sin(bumpTime * config.RunBumpFrequency * Mathf.PI * 2f)
                          * config.RunBumpAmplitude;

            basePos = Vector2.MoveTowards(basePos, destination, speed * Time.deltaTime);
            transform.position = new Vector2(basePos.x, basePos.y + bumpY);

            // Keep the trail spawner on the runner and pointed backward each frame.
            if (activeTrail != null)
            {
                Vector2 moveDir = (destination - basePos).normalized;
                activeTrail.UpdatePositionAndDirection(GetFootPosition(), -moveDir);
            }

            yield return null;

            // Update destination each frame in case of dynamic targets
            if (runTarget != null)
                destination = runTarget.Center;
        }

        // Snap to destination with no residual bump
        transform.position = destination;

        // Stop spawning new trail particles; existing world-space particles die naturally,
        // then TappableTrailController.OnParticleSystemStopped returns it to the pool.
        activeTrail?.StopEmitting();
        activeTrail = null;
    }

    void Complete()
    {
        Action callback = onDone;
        onDone = null;
        callback?.Invoke();
    }

    Vector2 GetFootPosition()
    {
        float footY = bodySpriteRenderer != null
            ? bodySpriteRenderer.bounds.min.y
            : transform.position.y - halfHeight;
        return new Vector2(transform.position.x, footY);
    }

    // ── Rotation helpers ──────────────────────────────────────────────────────

    void SetRotationZ(float degrees) =>
        transform.rotation = Quaternion.Euler(0f, 0f, degrees);

    // Returns the Z angle (degrees) that tilts the sprite's head toward the given direction.
    // Magnitude is set by config.PeekRotationAngle (0–90). Right tilts clockwise (negative Z).
    // Only used by Peek and PeekJumpAndRun (horizontal-only lean).
    float GetPeekRotationZ(Direction dir) => dir switch
    {
        Direction.Right => -config.PeekRotationAngle,
        Direction.Left  =>  config.PeekRotationAngle,
        _               =>  0f
    };

    // Returns the Z angle that fully points the sprite toward the jump direction.
    // Used by Jump and PeekAndJump to orient the character before they leave the spot.
    // Left/Right use the same lean as peek; Bottom flips the sprite 180° (head-down).
    float GetJumpRotationZ(Direction dir) => dir switch
    {
        Direction.Right  => -config.PeekRotationAngle,
        Direction.Left   =>  config.PeekRotationAngle,
        Direction.Bottom =>  180f,
        _                =>  0f    // Top: upright already points up
    };

    static bool IsHorizontal(Direction dir) =>
        dir == Direction.Left || dir == Direction.Right;

    // ── Position helpers ──────────────────────────────────────────────────────

    static Vector2 GetPeekPosition(Direction dir, Spot spot, float peekDistance)
    {
        Bounds b = spot.WorldBounds;
        Vector2 edge = dir switch
        {
            Direction.Top    => new Vector2(spot.Center.x, b.max.y),
            Direction.Bottom => new Vector2(spot.Center.x, b.min.y),
            Direction.Left   => new Vector2(b.min.x, spot.Center.y),
            Direction.Right  => new Vector2(b.max.x, spot.Center.y),
            _                => spot.Center
        };
        return edge + GetOutwardDirection(dir) * peekDistance;
    }

    static Vector2 GetOutwardDirection(Direction dir)
    {
        return dir switch
        {
            Direction.Top    => Vector2.up,
            Direction.Bottom => Vector2.down,
            Direction.Left   => Vector2.left,
            Direction.Right  => Vector2.right,
            _                => Vector2.up
        };
    }

    static Vector2 GetScreenExitPosition(Direction dir, Vector2 currentPos)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return Vector2.zero;

        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        Vector2 camPos = cam.transform.position;

        return dir switch
        {
            Direction.Top    => new Vector2(currentPos.x, camPos.y + h + 2f),
            Direction.Bottom => new Vector2(currentPos.x, camPos.y - h - 2f),
            Direction.Left   => new Vector2(camPos.x - w - 2f, currentPos.y),
            Direction.Right  => new Vector2(camPos.x + w + 2f, currentPos.y),
            _                => new Vector2(camPos.x + w + 2f, currentPos.y)
        };
    }

    static bool IsOutsideScreen(Vector2 pos)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        float h = cam.orthographicSize + 2f;
        float w = h * cam.aspect;
        Vector2 camPos = cam.transform.position;
        return pos.x < camPos.x - w || pos.x > camPos.x + w ||
               pos.y < camPos.y - h || pos.y > camPos.y + h;
    }
}
