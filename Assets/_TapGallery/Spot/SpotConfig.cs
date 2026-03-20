using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum Direction { None = 0, Top = 1, Bottom = 2, Left = 4, Right = 8 }

[Serializable]
public class TappableWeightEntry
{
    public TappableConfig Config;
    public float Weight;
}

[CreateAssetMenu(fileName = "SpotConfig", menuName = "TapGallery/SpotConfig")]
public class SpotConfig : ScriptableObject
{
    [SerializeField] Direction peekDirections;
    [SerializeField] Direction jumpDirections;
    [SerializeField] Direction runDirections;
    [SerializeField] List<TappableWeightEntry> tappableWeights;

    public Direction PeekDirections => peekDirections;
    public Direction JumpDirections => jumpDirections;
    public Direction RunDirections => runDirections;
    public List<TappableWeightEntry> TappableWeights => tappableWeights;
}
