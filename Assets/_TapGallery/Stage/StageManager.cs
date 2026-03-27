using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] List<StageConfig> stages;
    [Tooltip("Score thresholds that each earn the player one extra prize try at session end. " +
             "Independent of stage-advancement milestones. Leave empty to use stage score milestones instead.")]
    [SerializeField] List<int> rerollScoreMilestones;

    int currentStageIndex;

    public int CurrentStageIndex => currentStageIndex;
    public event Action<int> OnStageAdvanced;

    public List<Spot> GetActiveSpots()
    {
        if (stages == null || stages.Count == 0) return null;
        return stages[Mathf.Min(currentStageIndex, stages.Count - 1)].ActiveSpots;
    }

    public List<TappableConfig> GetAllowedTappables()
    {
        if (stages == null || stages.Count == 0) return null;
        return stages[Mathf.Min(currentStageIndex, stages.Count - 1)].AllowedTappableConfigs;
    }

    public float GetSpawnInterval(float fallback) =>
        (stages != null && stages.Count > 0)
            ? stages[Mathf.Min(currentStageIndex, stages.Count - 1)].SpawnInterval
            : fallback;

    public int GetMaxTappablesOnScreen(int fallback) =>
        (stages != null && stages.Count > 0)
            ? stages[Mathf.Min(currentStageIndex, stages.Count - 1)].MaxTappablesOnScreen
            : fallback;

    public int GetMinTappablesOnScreen(int fallback) =>
        (stages != null && stages.Count > 0)
            ? stages[Mathf.Min(currentStageIndex, stages.Count - 1)].MinTappablesOnScreen
            : fallback;

    /// <summary>
    /// Returns how many prize tries the player earned for the given score.
    /// Uses <see cref="rerollScoreMilestones"/> when configured; otherwise falls back
    /// to counting cleared stage score milestones (legacy behaviour).
    /// </summary>
    public int ComputeTriesFromScore(int score)
    {
        if (rerollScoreMilestones != null && rerollScoreMilestones.Count > 0)
        {
            int count = 0;
            foreach (var threshold in rerollScoreMilestones)
                if (threshold > 0 && score >= threshold) count++;
            return count;
        }

        // Legacy fallback: count cleared stage score milestones.
        if (stages == null) return 0;
        int legacy = 0;
        foreach (var stage in stages)
            if (stage.ScoreMilestone > 0 && score >= stage.ScoreMilestone) legacy++;
        return legacy;
    }

    public void CheckAdvancement(int currentScore, float elapsedTime)
    {
        if (stages == null || currentStageIndex >= stages.Count - 1) return;

        StageConfig current = stages[currentStageIndex];
        bool scoreTriggered = current.ScoreMilestone > 0 && currentScore >= current.ScoreMilestone;
        bool timeTriggered = current.TimeMilestone > 0f && elapsedTime >= current.TimeMilestone;

        if (scoreTriggered || timeTriggered)
        {
            currentStageIndex++;
            OnStageAdvanced?.Invoke(currentStageIndex);
        }
    }
}
