using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] List<StageConfig> stages;

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
    /// Simulates how many stage milestones the given score clears, using score thresholds
    /// only (time milestones are excluded). Used at session end to determine prize tries.
    /// </summary>
    public int ComputeTriesFromScore(int score)
    {
        if (stages == null) return 0;
        int count = 0;
        foreach (var stage in stages)
            if (stage.ScoreMilestone > 0 && score >= stage.ScoreMilestone)
                count++;
        return count;
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
