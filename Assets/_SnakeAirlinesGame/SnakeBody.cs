using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SnakeBody : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] int initialGrowth = 5;
    [SerializeField] float segmentSpacing = 0.5f; // The logical spacing (Game Units)
    
    [Tooltip("How many visual/collision points to generate per logical segment. Higher = smoother curves.")]
    [SerializeField] int pointsPerSegment = 1; 
    
    [SerializeField] int minTrimIndex = 2; // Minimum logical segments to keep (safe zone)
    [SerializeField] float pathResolution = 0.05f; // Recording frequency

    // "pathHistory" stores the raw path the head has taken
    private readonly List<Vector2> pathHistory = new();
    
    // "bodyPoints" stores the calculated visual/collision points
    private readonly List<Vector2> bodyPoints = new();

    private LineRenderer trailRenderer;
    private int targetLogicalSegments;
    private int targetPointCount;

    // Expose the raw points for SnakeHead to read if needed, though mostly internal now
    public IReadOnlyList<Vector2> BodySegments => bodyPoints;
    public float SegmentSpacing => segmentSpacing;
    public int PointsPerSegment => Mathf.Max(1, pointsPerSegment);

    private void Awake()
    {
        trailRenderer = GetComponent<LineRenderer>();
        if (TryGetComponent(out EdgeCollider2D collider))
        {
            collider.enabled = false;
        }
    }

    public void ResetBody(Vector2 headPosition, Vector2 direction)
    {
        pathHistory.Clear();
        bodyPoints.Clear();
        targetLogicalSegments = initialGrowth;
        targetPointCount = targetLogicalSegments * PointsPerSegment;

        // Initialize path extending backward so snake spawns fully formed
        Vector2 startPos = headPosition;
        Vector2 backDir = -direction.normalized;
        
        // Calculate total length needed based on logical segments
        float requiredLength = (targetLogicalSegments + 1) * segmentSpacing;
        
        pathHistory.Add(startPos);
        pathHistory.Add(startPos + (backDir * requiredLength));

        UpdateBodyPoints(headPosition);
        RefreshVisuals(headPosition);
    }

    public void Advance(Vector2 headPosition, Vector2 direction, bool shouldGrow)
    {
        if (shouldGrow)
        {
            targetLogicalSegments++;
            targetPointCount += PointsPerSegment;
        }

        // 1. Record Path
        if (pathHistory.Count == 0)
        {
            pathHistory.Insert(0, headPosition);
        }
        else
        {
            float dist = Vector2.Distance(pathHistory[0], headPosition);
            if (dist >= pathResolution)
            {
                pathHistory.Insert(0, headPosition);
            }
            else
            {
                pathHistory[0] = headPosition;
            }
        }

        // 2. Reconstruct Body Points
        UpdateBodyPoints(headPosition);

        // 3. Update Visuals
        RefreshVisuals(headPosition);
        
        // 4. Cleanup Memory
        CleanupHistory();
    }

    private void UpdateBodyPoints(Vector2 headPosition)
    {
        bodyPoints.Clear();
        
        int actualPointsPerSeg = PointsPerSegment;
        float subSegmentSpacing = segmentSpacing / actualPointsPerSeg;
        int totalPointsNeeded = targetPointCount;

        // Walk backwards along pathHistory
        float currentDistanceWanted = subSegmentSpacing;
        int pointsFound = 0;
        float distanceTravelled = 0f;

        Vector2 prevPoint = pathHistory[0];

        for (int i = 1; i < pathHistory.Count; i++)
        {
            Vector2 currentPoint = pathHistory[i];
            float distToNext = Vector2.Distance(prevPoint, currentPoint);

            while (distanceTravelled + distToNext >= currentDistanceWanted)
            {
                float remainingDist = currentDistanceWanted - distanceTravelled;
                float t = remainingDist / distToNext;
                Vector2 finalPos = Vector2.Lerp(prevPoint, currentPoint, t);

                bodyPoints.Add(finalPos);
                pointsFound++;

                if (pointsFound >= totalPointsNeeded)
                    return;

                currentDistanceWanted += subSegmentSpacing;
            }

            distanceTravelled += distToNext;
            prevPoint = currentPoint;
        }
    }

    private void CleanupHistory()
    {
        // Keep enough history for the full length + a little buffer
        int logicalSegmentsForLength = Mathf.CeilToInt((float)targetPointCount / PointsPerSegment);
        float maxDist = (logicalSegmentsForLength + 2) * segmentSpacing;
        float currentLen = 0;

        for (int i = 0; i < pathHistory.Count - 1; i++)
        {
            currentLen += Vector2.Distance(pathHistory[i], pathHistory[i+1]);
            if (currentLen > maxDist)
            {
                int removeCount = pathHistory.Count - (i + 2);
                if (removeCount > 0)
                {
                    pathHistory.RemoveRange(i + 2, removeCount);
                }
                return;
            }
        }
    }

    public int TrimFromIndex(int collisionPointIndex, Vector2 headPosition)
    {
        // 1. Convert the collision "Point Index" to a "Logical Segment Index"
        int actualPointsPerSeg = Mathf.Max(1, pointsPerSegment);
        
        // If we hit point 5 and pps is 4: 5/4 = 1. We hit inside segment 1.
        // We trim segment 1 and everything after. So we keep segment 0.
        int collisionSegmentIndex = collisionPointIndex / actualPointsPerSeg;

        // 2. Calculate how many logical segments we are removing
        int logicalSegmentsRemoved = targetLogicalSegments - collisionSegmentIndex;
        
        // 3. Update state
        targetLogicalSegments = collisionSegmentIndex;
        targetPointCount = targetLogicalSegments * actualPointsPerSeg;

        // 4. Force immediate update
        UpdateBodyPoints(headPosition);
        RefreshVisuals(headPosition);
        
        return logicalSegmentsRemoved;
    }

    public int TotalSegments => bodyPoints.Count + 1; // Visual total

    public int RemoveTailSegments(int count, Vector2 headPosition)
    {
        if (count <= 0)
        {
            return 0;
        }

        int newTarget = Mathf.Max(0, targetLogicalSegments - count);
        int removed = targetLogicalSegments - newTarget;
        targetLogicalSegments = newTarget;
        targetPointCount = targetLogicalSegments * PointsPerSegment;

        UpdateBodyPoints(headPosition);
        RefreshVisuals(headPosition);

        return removed;
    }

    public int RemoveTailPoints(int points, Vector2 headPosition, out int removedSegments)
    {
        removedSegments = 0;
        if (points <= 0 || targetPointCount <= 0)
        {
            return 0;
        }

        int pointsPerSeg = PointsPerSegment;
        int previousFullSegments = Mathf.FloorToInt((float)targetPointCount / pointsPerSeg);
        int newTargetPointCount = Mathf.Max(0, targetPointCount - points);
        int newFullSegments = Mathf.FloorToInt((float)newTargetPointCount / pointsPerSeg);
        removedSegments = previousFullSegments - newFullSegments;

        int removedPoints = targetPointCount - newTargetPointCount;
        targetPointCount = newTargetPointCount;
        targetLogicalSegments = Mathf.CeilToInt((float)targetPointCount / pointsPerSeg);

        UpdateBodyPoints(headPosition);
        RefreshVisuals(headPosition);

        return removedPoints;
    }

    public void RefreshVisuals(Vector2 headPosition)
    {
        trailRenderer.positionCount = bodyPoints.Count + 1;
        trailRenderer.SetPosition(0, new Vector3(headPosition.x, headPosition.y, 0f));

        for (int i = 0; i < bodyPoints.Count; i++)
        {
            Vector2 pt = bodyPoints[i];
            trailRenderer.SetPosition(i + 1, new Vector3(pt.x, pt.y, 0f));
        }
    }

    public int FindCollisionIndex(Vector2 headPosition, float collisionRadius)
    {
        if (bodyPoints.Count == 0) return -1;

        int actualPointsPerSeg = Mathf.Max(1, pointsPerSegment);
        // Scale minTrimIndex so the safe zone is physically the same distance
        int startCheckIndex = minTrimIndex * actualPointsPerSeg;

        float radiusSquared = collisionRadius * collisionRadius;

        for (int i = startCheckIndex; i < bodyPoints.Count; i++)
        {
            if ((bodyPoints[i] - headPosition).sqrMagnitude <= radiusSquared)
            {
                return i;
            }
        }
        return -1;
    }
    
    // Helper to find visual segment closest to world point (for debug or other mechanics)
    public int FindClosestSegmentIndex(Vector2 worldPoint)
    {
        if (bodyPoints.Count == 0) return -1;

        int closestIndex = -1;
        float closestDistanceSquared = float.MaxValue;
        int actualPointsPerSeg = Mathf.Max(1, pointsPerSegment);
        int startCheckIndex = minTrimIndex * actualPointsPerSeg;

        for (int i = startCheckIndex; i < bodyPoints.Count; i++)
        {
            float distanceSquared = (bodyPoints[i] - worldPoint).sqrMagnitude;
            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}
