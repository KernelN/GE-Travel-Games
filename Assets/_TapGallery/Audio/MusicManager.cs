using System.Collections;
using System.IO;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager inst { get; private set; }

    const string ConfigDataPath = "/Data/TapGalleryConfig.dat";

    [SerializeField, Min(0f)] float fadeDuration = 1f;
    [SerializeField] AudioSource firstSource;
    [SerializeField] AudioSource secondSource;

    [System.Serializable]
    class ConfigData
    {
        public float musicVolume = 1f;
    }

    ConfigData configData;
    Coroutine fadeRoutine;
    Coroutine pitchRoutine;
    AudioSource activeSource;
    AudioSource inactiveSource;
    float currentPitchFadeDuration;

    public float Volume => configData != null ? configData.musicVolume : 1f;

    void Awake()
    {
        if (inst && inst != this)
        {
            Destroy(gameObject);
            return;
        }

        inst = this;
        DontDestroyOnLoad(gameObject);

        LoadConfigData();
        SetupSources();
    }

    void OnDestroy()
    {
        if (inst == this)
        {
            SaveConfigData();
            inst = null;
        }
    }

    public void RequestTrack(AudioClip newClip)
    {
        if (!newClip || !firstSource || !secondSource)
            return;

        if (activeSource != null && activeSource.clip == newClip)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeToTrack(newClip));
    }

    public void SetVolume(float volume)
    {
        if (configData == null)
            configData = new ConfigData();

        float previousVolume = configData.musicVolume;
        float targetVolume = Mathf.Clamp01(volume);

        configData.musicVolume = targetVolume;
        ApplyVolume(previousVolume, targetVolume);
        SaveConfigData();
    }

    public void SetPitch(float targetPitch, float fadeDuration = 0f)
    {
        if (pitchRoutine != null)
            StopCoroutine(pitchRoutine);

        if (fadeDuration <= 0f)
        {
            ApplyPitch(targetPitch);
            return;
        }

        currentPitchFadeDuration = fadeDuration;
        pitchRoutine = StartCoroutine(FadePitch(targetPitch));
    }

    IEnumerator FadePitch(float targetPitch)
    {
        float startPitch = firstSource ? firstSource.pitch : 1f;
        float elapsed = 0f;

        while (elapsed < currentPitchFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / currentPitchFadeDuration);
            ApplyPitch(Mathf.Lerp(startPitch, targetPitch, t));
            yield return null;
        }

        ApplyPitch(targetPitch);
        pitchRoutine = null;
    }

    void ApplyPitch(float pitch)
    {
        if (fadeRoutine == null)
        {
            firstSource.pitch = pitch;
            secondSource.pitch = pitch;
        }
        else if (firstSource) firstSource.pitch = pitch;
        else secondSource.pitch = pitch;
    }

    void SetupSources()
    {
        if (!firstSource || !secondSource)
            return;

        firstSource.loop = true;
        secondSource.loop = true;

        activeSource = firstSource;
        inactiveSource = secondSource;

        firstSource.volume = 0f;
        secondSource.volume = 0f;
    }

    IEnumerator FadeToTrack(AudioClip newClip)
    {
        if (inactiveSource == null || activeSource == null)
            yield break;

        inactiveSource.clip = newClip;
        inactiveSource.volume = 0f;
        inactiveSource.Play();

        if (fadeDuration <= 0f)
        {
            activeSource.Stop();
            activeSource.volume = 0f;
            inactiveSource.volume = Volume;
            SwapSources();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            activeSource.volume = Mathf.Lerp(Volume, 0f, t);
            inactiveSource.volume = Mathf.Lerp(0f, Volume, t);

            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;
        inactiveSource.volume = Volume;

        SwapSources();
        fadeRoutine = null;
    }

    void SwapSources()
    {
        (activeSource, inactiveSource) = (inactiveSource, activeSource);
    }

    void ApplyVolume(float previousVolume, float targetVolume)
    {
        if (activeSource && activeSource.isPlaying)
            activeSource.volume = RemapVolume(activeSource.volume, previousVolume, targetVolume);

        if (inactiveSource && inactiveSource.isPlaying)
            inactiveSource.volume = RemapVolume(inactiveSource.volume, previousVolume, targetVolume);
    }

    float RemapVolume(float currentChannelVolume, float previousVolume, float targetVolume)
    {
        if (Mathf.Approximately(previousVolume, 0f))
            return targetVolume;

        float blendPercent = Mathf.Clamp01(currentChannelVolume / previousVolume);
        return blendPercent * targetVolume;
    }

    void LoadConfigData()
    {
        string path = Application.persistentDataPath + ConfigDataPath;
        try
        {
            if (File.Exists(path))
                configData = JsonUtility.FromJson<ConfigData>(File.ReadAllText(path));
        }
        catch { }

        if (configData == null)
            configData = new ConfigData();

        configData.musicVolume = Mathf.Clamp01(configData.musicVolume);
    }

    void SaveConfigData()
    {
        string path = Application.persistentDataPath + ConfigDataPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(configData));
        }
        catch { }
    }
}
