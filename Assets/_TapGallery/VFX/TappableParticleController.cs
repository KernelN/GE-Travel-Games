using UnityEngine;

// Pooled burst particle effect for tappables. Mirrors the FoodParticleController pattern.
// Plays a one-shot burst at a world position, then self-returns to the pool once all
// particles have naturally died (OnParticleSystemStopped callback).
public class TappableParticleController : MonoBehaviour
{
    [SerializeField] ParticleSystem burstParticles;

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

    // Move to world position, activate, and play burst.
    public void PlayAt(Vector2 position)
    {
        ParticleSystem ps = Resolve();
        if (ps == null) return;

        transform.position = new Vector3(position.x, position.y, 0f);
        gameObject.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play(true);
    }

    // Called by Unity once all particles have died — returns this effect to the pool.
    void OnParticleSystemStopped() => owner?.ReturnBurstToPool(this);

    ParticleSystem Resolve() => burstParticles != null ? burstParticles : GetComponent<ParticleSystem>();
}
