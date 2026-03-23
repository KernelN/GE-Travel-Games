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
    [SerializeField, Range(0f, 90f)] float peekRotationAngle = 30f;
    [SerializeField] float jumpHeight = 1.5f;
    [SerializeField] AnimationCurve jumpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] float runArrivalDistanceThreshold = 0.15f;
    [SerializeField] float runBumpAmplitude = 0.04f;
    [SerializeField] float runBumpFrequency = 8f;

    public TappableBehavior Behavior => behavior;
    public int Score => score;
    public float MovementSpeed => movementSpeed;
    public float PeekDistance => peekDistance;
    public float PeekDuration => peekDuration;
    public float PeekRotationAngle => peekRotationAngle;
    public float JumpHeight => jumpHeight;
    public AnimationCurve JumpCurve => jumpCurve;
    public float RunArrivalDistanceThreshold => runArrivalDistanceThreshold;
    public float RunBumpAmplitude => runBumpAmplitude;
    public float RunBumpFrequency => runBumpFrequency;
}
