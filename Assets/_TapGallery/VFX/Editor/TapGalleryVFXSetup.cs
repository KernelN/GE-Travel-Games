#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Run once via Tools > TapGallery > Create VFX Prefabs to generate the two particle
// prefabs used by the TapGallery VFX system. After creation, drag the prefab instances
// into the TapGalleryManager inspector arrays (burstEffectPool / trailEffectPool).
public static class TapGalleryVFXSetup
{
    const string VFXFolder       = "Assets/_TapGallery/VFX";
    const string BurstPrefabPath = VFXFolder + "/Tappable Burst Particles.prefab";
    const string TrailPrefabPath = VFXFolder + "/Tappable Trail Particles.prefab";

    [MenuItem("Tools/TapGallery/Create VFX Prefabs")]
    public static void CreateVFXPrefabs()
    {
        if (!Directory.Exists(VFXFolder))
            Directory.CreateDirectory(VFXFolder);

        CreateBurstPrefab();
        CreateTrailPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TapGallery] VFX prefabs created in " + VFXFolder);
    }

    // ── Burst effect ─────────────────────────────────────────────────────────────
    // One-shot burst of ~15 coloured particles that explode outward from the
    // tappable's last position. World-space so particles continue outward even
    // after the tappable is deactivated. Returned to pool via OnParticleSystemStopped.

    static void CreateBurstPrefab()
    {
        var go  = new GameObject("Tappable Burst Particles");
        var ps  = go.AddComponent<ParticleSystem>();
        go.AddComponent<TappableParticleController>();

        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.stopAction      = ParticleSystemStopAction.Callback;
        main.duration        = 0.8f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.2f, 1f),
            new Color(1f, 0.4f,  0.1f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.3f);

        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)15) });

        var shape = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Sphere;
        shape.radius     = 0.1f;

        // Particles shrink as they age
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = DefaultParticleMaterial();

        PrefabUtility.SaveAsPrefabAsset(go, BurstPrefabPath);
        Object.DestroyImmediate(go);
    }

    // ── Runner trail ─────────────────────────────────────────────────────────────
    // Continuous world-space dust cloud emitted behind the runner. The spawner GO
    // follows the runner each frame via TappableTrailController.UpdatePositionAndDirection();
    // already-spawned particles float in place and die naturally.
    // The cone emitter is oriented along local +Y; Tappable.cs rotates the spawner's
    // transform.up to point opposite to the runner's movement direction each frame.
    // Returned to pool via OnParticleSystemStopped once the last particle dies.

    static void CreateTrailPrefab()
    {
        var go  = new GameObject("Tappable Trail Particles");
        var ps  = go.AddComponent<ParticleSystem>();
        go.AddComponent<TappableTrailController>();

        var main = ps.main;
        main.loop            = true;
        main.playOnAwake     = false;
        main.stopAction      = ParticleSystemStopAction.Callback;
        main.duration        = 2f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.65f, 0.35f, 0.9f),
            new Color(0.6f,  0.4f,  0.2f,  0.4f));
        // World space: particles stay where emitted while the spawner follows the runner
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(25f);

        // Narrow cone along local +Y; TappableTrailController rotates transform.up
        // to the backward direction so dust puffs trail behind the runner.
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 20f;
        shape.radius    = 0.05f;

        // Particles shrink as they fade
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = DefaultParticleMaterial();

        PrefabUtility.SaveAsPrefabAsset(go, TrailPrefabPath);
        Object.DestroyImmediate(go);
    }

    static Material DefaultParticleMaterial() =>
        AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
}
#endif
