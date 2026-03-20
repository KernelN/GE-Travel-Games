using UnityEngine;

[CreateAssetMenu(fileName = "TapGalleryConfig", menuName = "TapGallery/TapGalleryConfig")]
public class TapGalleryConfig : ScriptableObject
{
    [SerializeField] float sessionDuration = 60f;
    [SerializeField] int maxTappablesOnScreen = 5;
    [SerializeField] float spawnInterval = 2f;

    public float SessionDuration => sessionDuration;
    public int MaxTappablesOnScreen => maxTappablesOnScreen;
    public float SpawnInterval => spawnInterval;
}
