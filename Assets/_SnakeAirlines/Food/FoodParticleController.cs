using UnityEngine;

public class FoodParticleController : MonoBehaviour
{
    [SerializeField] new ParticleSystem particleSystem;

    private FoodManager owner;

    public void Initialize(FoodManager manager)
    {
        owner = manager;
    }

    public void Prepare()
    {
        ParticleSystem targetSystem = ResolveParticleSystem();
        if (targetSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule mainModule = targetSystem.main;
        mainModule.stopAction = ParticleSystemStopAction.Callback;
        targetSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }

    public void PlayAt(Vector2 position)
    {
        ParticleSystem targetSystem = ResolveParticleSystem();
        if (targetSystem == null)
        {
            return;
        }

        transform.position = position;
        gameObject.SetActive(true);
        targetSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        targetSystem.Play(true);
    }

    private ParticleSystem ResolveParticleSystem()
    {
        if (particleSystem != null)
        {
            return particleSystem;
        }

        return GetComponent<ParticleSystem>();
    }

    private void OnParticleSystemStopped()
    {
        if (owner == null)
        {
            return;
        }

        owner.ReturnEatEffectToPool(this);
    }
}
