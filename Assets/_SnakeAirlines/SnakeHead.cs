using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
public class SnakeHead : MonoBehaviour
{
    [SerializeField] FoodManager foodManager;
    [SerializeField] ScoreManager scoreManager;
    [SerializeField] SnakeBody snakeBody;
    [SerializeField] Board board;
    [SerializeField] InputActionReference dragPositionActionReference;
    InputAction dragPositionAction;
    [SerializeField] InputActionReference dragPressActionReference;
    InputAction dragPressAction;
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float rotationSpeed = 180;
    [SerializeField] float foodCollisionRadius = 0.4f;
    [SerializeField] float selfCollisionRadius = 0.3f;

    Vector2 headPosition;
    Vector2 currentDirection = Vector2.right;
    Vector2 lastDragDirection = Vector2.right;
    bool isDragging;
    int pendingGrowth;
    Camera mainCamera;
    float stuckDistanceAccumulator;
    int stuckPointsRemoved;

    private void Awake()
    {
        dragPositionAction = dragPositionActionReference ? dragPositionActionReference.action : null;
        dragPressAction = dragPressActionReference ? dragPressActionReference.action : null;
        mainCamera = Camera.main;
        if (TryGetComponent(out Collider2D collider))
        {
            collider.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (dragPressAction != null)
        {
            dragPressAction.started += OnDragStarted;
            dragPressAction.canceled += OnDragCanceled;
            dragPressAction.Enable();
        }

        if (dragPositionAction != null)
        {
            dragPositionAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (dragPressAction != null)
        {
            dragPressAction.started -= OnDragStarted;
            dragPressAction.canceled -= OnDragCanceled;
            dragPressAction.Disable();
        }

        if (dragPositionAction != null)
        {
            dragPositionAction.Disable();
        }
    }

    private void Start()
    {
        ResetSnake();
    }

    private void Update()
    {
        UpdateDirectionFromInput();
        StepSnake();
    }
    
    private void OnDragStarted(InputAction.CallbackContext context)
    {
        isDragging = true;
    }

    private void OnDragCanceled(InputAction.CallbackContext context)
    {
        isDragging = false;
    }

    private void UpdateDirectionFromInput()
    {
        if (isDragging && dragPositionAction != null && mainCamera)
        {
            Vector2 screenPosition = dragPositionAction.ReadValue<Vector2>();
            Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                -mainCamera.transform.position.z));
            Vector2 direction = (Vector2)(worldPoint - transform.position);
            UpdateDirectionFromVector(direction);
        }
    }

    private void UpdateDirectionFromVector(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.01f)
            return;

        currentDirection = input.normalized;
    }

    private void ResetSnake()
    {
        if (!foodManager || !scoreManager || !snakeBody)
        {
            Debug.LogError("SnakeHead requires FoodManager, ScoreManager, and SnakeBody references.");
            enabled = false;
            return;
        }

        if (!board)
        {
            board = FindAnyObjectByType<Board>();
        }

        headPosition = new Vector2(transform.position.x, transform.position.y);

        currentDirection = Vector2.right;
        lastDragDirection = currentDirection;
        snakeBody.ResetBody(headPosition, currentDirection);
        pendingGrowth = snakeBody.BodySegments.Count;
        stuckDistanceAccumulator = 0f;
        stuckPointsRemoved = 0;

        scoreManager.ResetScore();
        foodManager.SpawnFood();
        snakeBody.RefreshVisuals(headPosition);
        UpdateHeadRotation(currentDirection);
    }

    private void StepSnake()
    {
        UpdateHeadRotation(currentDirection);
        float distance = moveSpeed * Time.deltaTime;
        Vector2 dir = transform.up;
        Vector2 desiredPosition = headPosition + dir * distance;
        bool isStuck = false;

        if (board != null)
        {
            Vector2 halfSize = board.Size * 0.5f;
            Vector2 min = board.Center - halfSize;
            Vector2 max = board.Center + halfSize;
            if (desiredPosition.x < min.x || desiredPosition.x > max.x
                || desiredPosition.y < min.y || desiredPosition.y > max.y)
            {
                isStuck = true;
            }
        }

        if (!isStuck)
        {
            headPosition = desiredPosition;
            stuckDistanceAccumulator = 0f;
            stuckPointsRemoved = 0;
        }
        else
        {
            stuckDistanceAccumulator += distance;
        }

        bool shouldGrow = pendingGrowth > 0 && !isStuck;
        if (shouldGrow)
        {
            pendingGrowth--;
        }

        transform.position = new Vector2(headPosition.x, headPosition.y);
        snakeBody.Advance(headPosition, dir, shouldGrow);

        if (isStuck && snakeBody != null && snakeBody.SegmentSpacing > 0f)
        {
            float progressInSegments = stuckDistanceAccumulator / snakeBody.SegmentSpacing;
            int totalPointsToRemove = Mathf.FloorToInt(progressInSegments * snakeBody.PointsPerSegment);
            int pointsToRemove = totalPointsToRemove - stuckPointsRemoved;
            if (pointsToRemove > 0)
            {
                int removedSegments;
                int removedPoints = snakeBody.RemoveTailPoints(pointsToRemove, headPosition, out removedSegments);
                if (removedPoints > 0)
                {
                    stuckPointsRemoved += removedPoints;
                }

                if (removedSegments > 0)
                {
                    scoreManager.RemoveBodyPoints(removedSegments);
                }
            }

            int segmentsToRemove = Mathf.FloorToInt(stuckDistanceAccumulator / snakeBody.SegmentSpacing);
            if (segmentsToRemove > 0)
            {
                stuckDistanceAccumulator -= segmentsToRemove * snakeBody.SegmentSpacing;
                stuckPointsRemoved = Mathf.Max(0, stuckPointsRemoved - segmentsToRemove * snakeBody.PointsPerSegment);
            }
        }

        CheckFoodCollision();
        CheckSelfCollision();
    }

    private void UpdateHeadRotation(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
        if (rotationSpeed <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        float maxStep = rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxStep);
    }

    private void CheckFoodCollision()
    {
        Vector2 foodPosition = foodManager.CurrentFoodPosition;
        if (foodPosition.x == float.MinValue)
        {
            return;
        }

        float radiusSquared = foodCollisionRadius * foodCollisionRadius;
        if ((foodPosition - headPosition).sqrMagnitude <= radiusSquared)
        {
            pendingGrowth++;
            scoreManager.AddFoodPoints();
            foodManager.PlayFoodEatEffect(foodPosition);
            foodManager.SpawnFood();
        }
    }

    private void CheckSelfCollision()
    {
        int hitIndex = snakeBody.FindCollisionIndex(headPosition, selfCollisionRadius);
        if (hitIndex < 0)
        {
            return;
        }

        int removed = snakeBody.TrimFromIndex(hitIndex, headPosition);
        scoreManager.RemoveBodyPoints(removed);
    }
}
