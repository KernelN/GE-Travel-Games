using UnityEngine;

// Pooled trailing dust-cloud effect for runners. The particle system simulates in
// world space, so spawned particles float in place and die naturally while the
// "spawner" (this GameObject) follows the runner's position each frame.
//
// Lifecycle:
//   BeginAt()              — activate and play; Tappable calls this at RunPhase start
//   UpdatePositionAndDirection() — called every frame during RunPhase
//   StopEmitting()         — called when the run ends or the runner is tapped;
//                            existing particles continue to live and die naturally
//   OnParticleSystemStopped — Unity fires this once the last particle dies;
//                             the trail self-returns to the pool at that point
public class TappableTrailController : MonoBehaviour
{
    [SerializeField] ParticleSystem trailParticles;

    TapGalleryManager owner;

    public void Initialize(TapGalleryManager manager) => owner = manager;

    // Stop, clear, and deactivate — ready to be re-pooled.
    public void Prepare()
    {
        ParticleSystem ps = Resolve();
        if (ps == null) return;

        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }

    // Position the spawner, orient toward emitDirection, activate, and start emitting.
    // emitDirection should point in the direction particles travel (opposite to runner movement).
    public void BeginAt(Vector2 position, Vector2 emitDirection)
    {
        ParticleSystem ps = Resolve();
        if (ps == null) return;

        transform.position = new Vector3(position.x, position.y, 0f);
        SetEmitDirection(emitDirection);
        gameObject.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play(true);
    }

    // Move the spawner each frame so new particles are emitted from the runner's position.
    // Already-spawned world-space particles stay where they were emitted and die naturally.
    public void UpdatePositionAndDirection(Vector2 position, Vector2 emitDirection)
    {
        transform.position = new Vector3(position.x, position.y, 0f);
        SetEmitDirection(emitDirection);
    }

    // Stop spawning new particles. Existing world-space particles live until their
    // lifetime expires, then OnParticleSystemStopped fires and the trail returns to pool.
    public void StopEmitting()
    {
        Resolve()?.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    // Called by Unity once all particles have died — returns this trail to the pool.
    void OnParticleSystemStopped() => owner?.ReturnTrailToPool(this);

    void SetEmitDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.001f)
            transform.up = dir;
    }

    ParticleSystem Resolve() => trailParticles != null ? trailParticles : GetComponent<ParticleSystem>();
}
