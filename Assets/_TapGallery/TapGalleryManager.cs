using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public class TapGalleryManager : MonoBehaviour
{
    [Serializable]
    public class SpotWeightEntry
    {
        public Spot Spot;
        public float Weight;
    }

    [Serializable]
    public class TappablePoolEntry
    {
        public TappableConfig Config;
        public Tappable Prefab;
        public int PrewarmCount = 3;
    }

    [SerializeField] float sessionDuration = 60f;
    [SerializeField] float spawnInterval = 1.5f;
    [SerializeField] int maxTappablesOnScreen = 5;
    [SerializeField] List<SpotWeightEntry> spotWeights;
    [SerializeField] List<TappablePoolEntry> tappablePools;
    [SerializeField] TMP_Text scoreLabel;
    [SerializeField] TappableParticleController[] burstEffectPool;
    [SerializeField] TappableTrailController[] trailEffectPool;
    [SerializeField] TimerManager timerManager;
    [SerializeField] SessionEndPanel sessionEndPanel;
    [SerializeField] StageManager stageManager;

    int score;
    int activeTappableCount;
    bool sessionActive;

    Dictionary<TappableConfig, ObjectPool<Tappable>> pools;
    Dictionary<TappableConfig, Sprite[]> tappableSprites;
    readonly Queue<TappableParticleController> availableBurstEffects = new();
    readonly Queue<TappableTrailController> availableTrailEffects = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        pools = new Dictionary<TappableConfig, ObjectPool<Tappable>>();
        tappableSprites = new Dictionary<TappableConfig, Sprite[]>();

        foreach (TappablePoolEntry entry in tappablePools)
        {
            string spritePath = $"TapGallery/Tappables/{entry.Config.name}";
            Sprite[] sprites = Resources.LoadAll<Sprite>(spritePath);
            if (sprites.Length > 0)
                tappableSprites[entry.Config] = sprites;

            TappablePoolEntry captured = entry;
            ObjectPool<Tappable> pool = new ObjectPool<Tappable>(
                createFunc: () => Instantiate(captured.Prefab),
                actionOnGet: t =>
                {
                    t.gameObject.SetActive(true);
                    if (tappableSprites.TryGetValue(captured.Config, out Sprite[] configSprites))
                        t.SetSprite(configSprites[UnityEngine.Random.Range(0, configSprites.Length)]);
                },
                actionOnRelease: t => t.gameObject.SetActive(false),
                actionOnDestroy: t => Destroy(t.gameObject)
            );

            // Pre-warm
            List<Tappable> prewarm = new List<Tappable>(entry.PrewarmCount);
            for (int i = 0; i < entry.PrewarmCount; i++)
                prewarm.Add(pool.Get());
            foreach (Tappable t in prewarm)
                pool.Release(t);

            pools[entry.Config] = pool;
        }

        if (burstEffectPool != null)
            foreach (TappableParticleController effect in burstEffectPool)
                if (effect != null) { effect.Initialize(this); effect.Prepare(); availableBurstEffects.Enqueue(effect); }

        if (trailEffectPool != null)
            foreach (TappableTrailController effect in trailEffectPool)
                if (effect != null) { effect.Initialize(this); effect.Prepare(); availableTrailEffects.Enqueue(effect); }
    }

    void Start()
    {
        score = 0;
        activeTappableCount = 0;
        sessionActive = true;
        timerManager.OnTimerEnd += EndSession;
        UpdateScoreLabel();
        StartCoroutine(SpawnLoop());
    }

    void OnDestroy()
    {
        if (timerManager != null) timerManager.OnTimerEnd -= EndSession;
    }

    // ── Spawn loop ────────────────────────────────────────────────────────────

    IEnumerator SpawnLoop()
    {
        while (sessionActive)
        {
            float interval = stageManager != null
                ? stageManager.GetSpawnInterval(spawnInterval)
                : spawnInterval;
            yield return new WaitForSeconds(interval);
            if (sessionActive)
                TrySpawn();
        }
    }

    void TrySpawn()
    {
        int maxOnScreen = stageManager != null
            ? stageManager.GetMaxTappablesOnScreen(maxTappablesOnScreen)
            : maxTappablesOnScreen;
        if (activeTappableCount >= maxOnScreen) return;

        CheckStageAdvancement();

        List<Spot> activeSpots = stageManager != null ? stageManager.GetActiveSpots() : null;
        List<TappableConfig> allowedTappables = stageManager != null ? stageManager.GetAllowedTappables() : null;

        Spot spot = WeightedRandomSpot(activeSpots);
        if (spot == null) return;

        TappableConfig tappableConfig = WeightedRandomTappable(spot.Config.TappableWeights, allowedTappables);
        if (tappableConfig == null) return;

        Direction validDirections = GetValidDirections(tappableConfig.Behavior, spot.Config);
        bool canUseRunTarget = (tappableConfig.Behavior == TappableBehavior.Run ||
                                tappableConfig.Behavior == TappableBehavior.PeekAndRun) &&
                               spot.RunTargets.Count > 0;
        if (validDirections == Direction.None && !canUseRunTarget)
        {
            Debug.LogWarning($"[TapGallery] No valid directions for {tappableConfig.Behavior} on spot {spot.name}. Skipping.");
            return;
        }

        Direction direction = PickRandomDirection(validDirections); // returns Direction.None if no flags — safe when runTarget is used

        // PeekJumpAndRun only valid for Left/Right
        if (tappableConfig.Behavior == TappableBehavior.PeekJumpAndRun &&
            direction != Direction.Left && direction != Direction.Right)
        {
            Debug.LogWarning($"[TapGallery] PeekJumpAndRun requires Left or Right, got {direction}. Skipping.");
            return;
        }

        Spot runTarget = null;
        if (IsRunBehavior(tappableConfig.Behavior) && spot.RunTargets.Count > 0)
            runTarget = spot.RunTargets[UnityEngine.Random.Range(0, spot.RunTargets.Count)];

        if (!pools.TryGetValue(tappableConfig, out ObjectPool<Tappable> pool))
        {
            Debug.LogWarning($"[TapGallery] No pool found for {tappableConfig.name}. Skipping.");
            return;
        }

        Tappable tappable = pool.Get();
        activeTappableCount++;

        // All run behaviors get a continuous trail; timing is controlled inside Tappable via RunPhase.
        TappableTrailController trail = IsRunBehavior(tappableConfig.Behavior)
            ? GetTrailEffect()
            : null;

        tappable.StartBehavior(direction, spot, runTarget, () =>
        {
            if (tappable.WasTapped)
            {
                int points = tappable.Config.IsPenalty ? -tappable.Config.Score : tappable.Config.Score;
                AddScore(points);
                PlayBurstEffect(tappable.transform.position);
            }

            pool.Release(tappable);
            activeTappableCount--;
        }, trail);
    }

    // ── Score ─────────────────────────────────────────────────────────────────

    void AddScore(int amount)
    {
        score = Mathf.Max(0, score + amount);
        UpdateScoreLabel();
        CheckStageAdvancement();
    }

    void UpdateScoreLabel()
    {
        if (scoreLabel != null)
            scoreLabel.text = score.ToString();
    }

    // ── Session ───────────────────────────────────────────────────────────────

    void CheckStageAdvancement()
    {
        if (stageManager == null) return;
        float elapsed = sessionDuration - timerManager.TimeRemaining;
        stageManager.CheckAdvancement(score, elapsed);
    }

    void EndSession()
    {
        sessionActive = false;
        Debug.Log($"[TapGallery] Session ended. Final score: {score}");
        SceneManager.LoadScene("UserRegister");
    }

    // ── Weighted random helpers ───────────────────────────────────────────────

    Spot WeightedRandomSpot(List<Spot> activeFilter = null)
    {
        float total = 0f;
        foreach (SpotWeightEntry entry in spotWeights)
        {
            if (activeFilter != null && !activeFilter.Contains(entry.Spot)) continue;
            total += entry.Weight;
        }

        if (total <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        Spot lastValid = null;
        foreach (SpotWeightEntry entry in spotWeights)
        {
            if (activeFilter != null && !activeFilter.Contains(entry.Spot)) continue;
            lastValid = entry.Spot;
            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry.Spot;
        }
        return lastValid;
    }

    static TappableConfig WeightedRandomTappable(List<TappableWeightEntry> entries,
        List<TappableConfig> allowedFilter = null)
    {
        if (entries == null || entries.Count == 0) return null;

        float total = 0f;
        foreach (TappableWeightEntry entry in entries)
        {
            if (allowedFilter != null && !allowedFilter.Contains(entry.Config)) continue;
            total += entry.Weight;
        }

        if (total <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        TappableConfig lastValid = null;
        foreach (TappableWeightEntry entry in entries)
        {
            if (allowedFilter != null && !allowedFilter.Contains(entry.Config)) continue;
            lastValid = entry.Config;
            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry.Config;
        }
        return lastValid;
    }

    static Direction GetValidDirections(TappableBehavior behavior, SpotConfig spotConfig)
    {
        return behavior switch
        {
            TappableBehavior.Peek                          => spotConfig.PeekDirections,
            TappableBehavior.PeekAndJump or TappableBehavior.Jump
                                                           => spotConfig.JumpDirections,
            TappableBehavior.PeekJumpAndRun or TappableBehavior.PeekAndRun or TappableBehavior.Run
                                                           => spotConfig.RunDirections,
            _                                              => Direction.None
        };
    }

    static Direction PickRandomDirection(Direction flags)
    {
        List<Direction> valid = new List<Direction>(4);
        if ((flags & Direction.Top)    != 0) valid.Add(Direction.Top);
        if ((flags & Direction.Bottom) != 0) valid.Add(Direction.Bottom);
        if ((flags & Direction.Left)   != 0) valid.Add(Direction.Left);
        if ((flags & Direction.Right)  != 0) valid.Add(Direction.Right);
        return valid.Count == 0 ? Direction.None : valid[UnityEngine.Random.Range(0, valid.Count)];
    }

    static bool IsRunBehavior(TappableBehavior behavior)
    {
        return behavior == TappableBehavior.PeekJumpAndRun ||
               behavior == TappableBehavior.PeekAndRun    ||
               behavior == TappableBehavior.Run;
    }

    // ── VFX helpers ───────────────────────────────────────────────────────────────

    void PlayBurstEffect(Vector2 position)
    {
        if (availableBurstEffects.Count == 0) return;
        availableBurstEffects.Dequeue().PlayAt(position);
    }

    TappableTrailController GetTrailEffect()
    {
        if (availableTrailEffects.Count == 0) return null;
        return availableTrailEffects.Dequeue();
    }

    // Called by TappableParticleController.OnParticleSystemStopped once all burst
    // particles have naturally died.
    internal void ReturnBurstToPool(TappableParticleController effect)
    {
        if (effect == null) return;
        effect.Initialize(this);
        effect.Prepare();
        availableBurstEffects.Enqueue(effect);
    }

    // Called by TappableTrailController.OnParticleSystemStopped once all trail
    // particles have naturally died.
    internal void ReturnTrailToPool(TappableTrailController effect)
    {
        if (effect == null) return;
        effect.Initialize(this);
        effect.Prepare();
        availableTrailEffects.Enqueue(effect);
    }
}
