using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Tappable : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TappableConfig config;
    [SerializeField] SpriteRenderer spriteRenderer;

    public TappableConfig Config => config;
    public bool WasTapped { get; private set; }

    Action onDone;
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

    public void StartBehavior(Direction direction, Spot originSpot, Spot runTarget, Action callback,
        TappableTrailController trail = null)
    {
        WasTapped = false;
        onDone = callback;
        activeTrail = trail;
        StopAllCoroutines();

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
        Vector2 hiddenPos = spot.Center;
        Vector2 peekPos = GetPeekPosition(dir, spot, config.PeekDistance);

        transform.position = hiddenPos;
        yield return MoveTowards(peekPos);
        yield return new WaitForSeconds(config.PeekDuration);
        yield return MoveTowards(hiddenPos);
        Complete();
    }

    IEnumerator PeekAndJumpRoutine(Direction dir, Spot spot)
    {
        Vector2 hiddenPos = spot.Center;
        Vector2 peekPos = GetPeekPosition(dir, spot, config.PeekDistance);

        // Peek phase
        transform.position = hiddenPos;
        yield return MoveTowards(peekPos);
        yield return new WaitForSeconds(config.PeekDuration);

        // Jump phase: curve-driven offset from peekPos in outward direction
        Vector2 outward = GetOutwardDirection(dir);
        float jumpDuration = config.JumpHeight / Mathf.Max(config.MovementSpeed, 0.01f);
        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);
            float displacement = config.JumpCurve.Evaluate(t) * config.JumpHeight;
            transform.position = peekPos + outward * displacement;
            yield return null;
        }

        yield return MoveTowards(hiddenPos);
        Complete();
    }

    IEnumerator JumpRoutine(Direction dir, Spot spot)
    {
        Vector2 hiddenPos = spot.Center;
        Vector2 peekPos = GetPeekPosition(dir, spot, config.PeekDistance);

        // No peek — start at edge immediately
        transform.position = peekPos;

        // Jump phase
        Vector2 outward = GetOutwardDirection(dir);
        float jumpDuration = config.JumpHeight / Mathf.Max(config.MovementSpeed, 0.01f);
        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);
            float displacement = config.JumpCurve.Evaluate(t) * config.JumpHeight;
            transform.position = peekPos + outward * displacement;
            yield return null;
        }

        yield return MoveTowards(hiddenPos);
        Complete();
    }

    IEnumerator PeekJumpAndRunRoutine(Direction dir, Spot spot, Spot runTarget)
    {
        Vector2 hiddenPos = spot.Center;
        Vector2 peekPos = GetPeekPosition(dir, spot, config.PeekDistance);

        // Peek phase
        transform.position = hiddenPos;
        yield return MoveTowards(peekPos);
        yield return new WaitForSeconds(config.PeekDuration);

        // Parabolic arc (horizontal movement + fake downward gravity)
        float horizontalSpeed = config.MovementSpeed;
        // Derive gravity so the arc apex equals JumpHeight
        // At apex: vy=0, t_apex = vy0/g, apex_height = vy0^2/(2g) = jumpHeight
        // => vy0 = sqrt(2*g*jumpHeight), pick g so arc looks natural
        float gravity = horizontalSpeed * horizontalSpeed / Mathf.Max(config.JumpHeight * 2f, 0.01f);
        float vy = Mathf.Sqrt(2f * gravity * config.JumpHeight);
        float vx = dir == Direction.Left ? -horizontalSpeed : horizontalSpeed;

        Vector2 pos = peekPos;
        float startY = peekPos.y;

        while (pos.y >= startY - 0.01f || vy > 0f)
        {
            pos.x += vx * Time.deltaTime;
            pos.y += vy * Time.deltaTime;
            vy -= gravity * Time.deltaTime;
            transform.position = pos;
            yield return null;

            // Safety: if Tappable exits screen, stop arc
            if (IsOutsideScreen(pos))
                break;
        }

        // Run phase after landing
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

    IEnumerator RunPhase(Direction dir, Spot runTarget)
    {
        Vector2 startPos = transform.position;

        Vector2 destination = runTarget != null
            ? runTarget.Center
            : GetScreenExitPosition(dir, startPos);

        float arrivalThreshold = runTarget != null
            ? config.RunArrivalDistanceThreshold
            : 0.05f;

        float speed = config.MovementSpeed;

        // Begin trail behind the runner (opposite to initial movement direction).
        if (activeTrail != null)
        {
            Vector2 initialMoveDir = (destination - startPos).normalized;
            activeTrail.BeginAt(GetFootPosition(), -initialMoveDir);
        }

        while (Vector2.Distance(transform.position, destination) > arrivalThreshold)
        {
            // Keep the trail spawner on the runner and pointed backward each frame.
            if (activeTrail != null)
            {
                Vector2 moveDir = ((Vector2)destination - (Vector2)transform.position).normalized;
                activeTrail.UpdatePositionAndDirection(GetFootPosition(), -moveDir);
            }

            transform.position = Vector2.MoveTowards(transform.position, destination, speed * Time.deltaTime);
            yield return null;

            // Update destination each frame in case of dynamic targets
            if (runTarget != null)
                destination = runTarget.Center;
        }

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

    Vector2 GetFootPosition() =>
        new Vector2(transform.position.x, transform.position.y - halfHeight);

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
