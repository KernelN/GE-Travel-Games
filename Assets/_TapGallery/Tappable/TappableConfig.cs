using UnityEngine;

public enum TappableBehavior { Peek, PeekAndJump, Jump, PeekJumpAndRun, PeekAndRun, Run }

[CreateAssetMenu(fileName = "TappableConfig", menuName = "TapGallery/TappableConfig")]
public class TappableConfig : ScriptableObject
{
    [SerializeField] TappableBehavior behavior;
    [SerializeField] int score;
    [SerializeField] float movementSpeed = 3f;
    [SerializeField] float peekDistance = 0f;
    [SerializeField] float peekDuration = 1f;
    [SerializeField] float jumpHeight = 1.5f;
    [SerializeField] AnimationCurve jumpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] float runArrivalDistanceThreshold = 0.15f;

    public TappableBehavior Behavior => behavior;
    public int Score => score;
    public float MovementSpeed => movementSpeed;
    public float PeekDistance => peekDistance;
    public float PeekDuration => peekDuration;
    public float JumpHeight => jumpHeight;
    public AnimationCurve JumpCurve => jumpCurve;
    public float RunArrivalDistanceThreshold => runArrivalDistanceThreshold;
}
