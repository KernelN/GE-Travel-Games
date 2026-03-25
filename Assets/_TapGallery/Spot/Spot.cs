using System.Collections.Generic;
using UnityEngine;

public class Spot : MonoBehaviour
{
    [SerializeField] SpotConfig config;
    [SerializeField] List<Spot> runTargets;

    SpriteRenderer spriteRenderer;

    Direction occupiedDirections;

    public SpotConfig Config => config;
    public List<Spot> RunTargets => runTargets;
    public Vector2 Center => transform.position;
    public Direction OccupiedDirections => occupiedDirections;

    public void OccupyDirection(Direction dir) => occupiedDirections |= dir;
    public void FreeDirection(Direction dir)   => occupiedDirections &= ~dir;

    public Bounds WorldBounds
    {
        get
        {
            if (spriteRenderer != null)
                return spriteRenderer.bounds;
            return new Bounds(transform.position, Vector3.one);
        }
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
