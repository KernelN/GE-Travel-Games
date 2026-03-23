using UnityEngine;

public class Board : MonoBehaviour
{
    [SerializeField] private Vector2 center = Vector2.zero;
    [SerializeField] private Vector2 size = new(24f, 16f);

    public Vector2 Center => center;
    public Vector2 Size => size;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0f));
    }
}
