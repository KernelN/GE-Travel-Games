using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageConfig
{
    [SerializeField] List<Spot> activeSpots;
    [SerializeField] List<TappableConfig> allowedTappableConfigs;
    [Tooltip("Seconds between spawn-loop ticks for this stage.")]
    [SerializeField] float spawnInterval = 1.5f;
    [Tooltip("Hard cap on simultaneous live tappables for this stage.")]
    [SerializeField] int maxTappablesOnScreen = 5;
    [Tooltip("Minimum live tappables before a spawn is forced immediately. 0 = disabled.")]
    [SerializeField] int minTappablesOnScreen = 0;
    [Tooltip("Score threshold to advance to next stage. 0 = disabled.")]
    [SerializeField] int scoreMilestone;
    [Tooltip("Elapsed session time (seconds) threshold to advance. 0 = disabled.")]
    [SerializeField] float timeMilestone;

    public List<Spot> ActiveSpots => activeSpots;
    public List<TappableConfig> AllowedTappableConfigs => allowedTappableConfigs;
    public float SpawnInterval => spawnInterval;
    public int MaxTappablesOnScreen => maxTappablesOnScreen;
    public int MinTappablesOnScreen => minTappablesOnScreen;
    public int ScoreMilestone => scoreMilestone;
    public float TimeMilestone => timeMilestone;
}
