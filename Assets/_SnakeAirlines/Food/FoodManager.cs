using System.Collections.Generic;
using UnityEngine;

public class FoodManager : MonoBehaviour
{
    [SerializeField] SnakeFood[] foodPrefabs;
    [SerializeField] SnakeBody snakeBody;
    [SerializeField] Transform snakeHead;
    [SerializeField] Vector2 spawnAreaCenter = Vector2.zero;
    [SerializeField] Vector2 spawnAreaSize = new(24f, 16f);
    [SerializeField] float cellSize = 0.5f;
    [SerializeField] FoodParticleController[] foodEatEffectPool;

    SnakeFood activeFood;
    readonly Queue<FoodParticleController> availableEatEffects = new();

    public Vector2 CurrentFoodPosition => activeFood != null ? activeFood.WorldPosition : new Vector2(float.MinValue, float.MinValue);

    private void Awake()
    {
        if (foodEatEffectPool == null)
        {
            return;
        }

        foreach (FoodParticleController effect in foodEatEffectPool)
        {
            if (effect == null)
            {
                continue;
            }

            PrepareEffect(effect);
            availableEatEffects.Enqueue(effect);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(spawnAreaCenter.x, spawnAreaCenter.y, 0f),
                                new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
    }
    
    public void SpawnFood()
    {
        if (foodPrefabs == null || foodPrefabs.Length == 0)
        {
            Debug.LogWarning("Food prefabs are missing on FoodManager.");
            return;
        }

        if (activeFood != null)
        {
            Destroy(activeFood.gameObject);
        }

        Vector2 spawnPosition = FindFreeSpawnPosition();
        SnakeFood selectedPrefab = foodPrefabs[Random.Range(0, foodPrefabs.Length)];
        activeFood = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
    }

    public void PlayFoodEatEffect(Vector2 position)
    {
        if (availableEatEffects.Count == 0)
        {
            return;
        }

        FoodParticleController effect = availableEatEffects.Dequeue();
        effect.PlayAt(position);
    }

    private Vector2 FindFreeSpawnPosition()
    {
        Vector2 areaCenter = spawnAreaCenter;
        Vector2 areaSize = spawnAreaSize;
        Vector2 halfSize = areaSize * 0.5f;
        Vector2 min = areaCenter - halfSize;
        Vector2 max = areaCenter + halfSize;
        int minCellX = Mathf.CeilToInt((min.x - areaCenter.x) / cellSize);
        int maxCellX = Mathf.FloorToInt((max.x - areaCenter.x) / cellSize);
        int minCellY = Mathf.CeilToInt((min.y - areaCenter.y) / cellSize);
        int maxCellY = Mathf.FloorToInt((max.y - areaCenter.y) / cellSize);
        int maxTries = Mathf.Max(1, (maxCellX - minCellX + 1) * (maxCellY - minCellY + 1));
        HashSet<Vector2Int> occupiedCells = BuildOccupiedCells(areaCenter);

        for (int i = 0; i < maxTries; i++)
        {
            Vector2Int cell = new Vector2Int(
                Random.Range(minCellX, maxCellX + 1),
                Random.Range(minCellY, maxCellY + 1));

            if (!occupiedCells.Contains(cell))
            {
                return areaCenter + new Vector2(cell.x * cellSize, cell.y * cellSize);
            }
        }

        return areaCenter;
    }

    private HashSet<Vector2Int> BuildOccupiedCells(Vector2 areaCenter)
    {
        HashSet<Vector2Int> occupied = new();

        if (snakeHead != null)
        {
            occupied.Add(WorldToCell(areaCenter, snakeHead.position));
        }

        if (snakeBody != null)
        {
            foreach (Vector2 segment in snakeBody.BodySegments)
            {
                occupied.Add(WorldToCell(areaCenter, segment));
            }
        }

        if (activeFood != null)
        {
            occupied.Add(WorldToCell(areaCenter, activeFood.WorldPosition));
        }

        return occupied;
    }

    private Vector2Int WorldToCell(Vector2 areaCenter, Vector2 worldPosition)
    {
        float cellX = (worldPosition.x - areaCenter.x) / cellSize;
        float cellY = (worldPosition.y - areaCenter.y) / cellSize;
        return new Vector2Int(Mathf.RoundToInt(cellX), Mathf.RoundToInt(cellY));
    }

    private void PrepareEffect(FoodParticleController effect)
    {
        effect.Initialize(this);
        effect.Prepare();
    }

    internal void ReturnEatEffectToPool(FoodParticleController effect)
    {
        if (effect == null)
        {
            return;
        }

        PrepareEffect(effect);
        availableEatEffects.Enqueue(effect);
    }
}
