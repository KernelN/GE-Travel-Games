using UnityEngine;
using UnityEngine.Serialization;

public class TapGallerySFXManager : MonoBehaviour
{
    [SerializeField] MusicManager musicManager;
    [SerializeField] TimerManager timerManager;
    [SerializeField] AudioSource sfxSource;

    [Header("SFX Clips")]
    [SerializeField] AudioClip tapRewardClip;
    [SerializeField] AudioClip tapPenaltyClip;
    [SerializeField] AudioClip sessionStartClip;
    [SerializeField] AudioClip countdownBeepClip;
    [SerializeField] AudioClip timeUpClip;

    [Header("Urgency")]
    [SerializeField, Min(0f)] float urgencyTimeThreshold = 10f;
    [SerializeField, Min(1f)] float urgencyPitch = 1.25f;
    [SerializeField, Min(0f)] float urgentPitchFadeDuration = 2f;
    [SerializeField, Min(0f)] float outPitchFadeDuration = 2f;

    float lastTimeRemaining;
    bool urgencyStarted;

    void Awake()
    {
        if (timerManager != null)
        {
            timerManager.TimeChanged += OnTimeChanged;
            timerManager.OnTimerEnd += OnTimerEnd;
        }
    }

    void Start()
    {
        if(!musicManager) musicManager = MusicManager.inst;
    }

    void OnDestroy()
    {
        if (timerManager != null)
        {
            timerManager.TimeChanged -= OnTimeChanged;
            timerManager.OnTimerEnd -= OnTimerEnd;
        }
    }

    public void PlayTapHit(bool isPenalty)
    {
        AudioClip clip = isPenalty ? tapPenaltyClip : tapRewardClip;
        if (sfxSource && clip)
            sfxSource.PlayOneShot(clip);
    }

    public void PlaySessionStart()
    {
        if (sfxSource && sessionStartClip)
            sfxSource.PlayOneShot(sessionStartClip);

        urgencyStarted = false;
        lastTimeRemaining = timerManager != null ? timerManager.TimeRemaining : float.MaxValue;
    }

    void OnTimeChanged(float timeRemaining)
    {
        if (!urgencyStarted && timeRemaining <= urgencyTimeThreshold)
        {
            urgencyStarted = true;
            musicManager?.SetPitch(urgencyPitch, urgentPitchFadeDuration);
        }

        if (urgencyStarted)
        {
            int prevSecond = Mathf.CeilToInt(lastTimeRemaining);
            int currSecond = Mathf.CeilToInt(timeRemaining);
            if (currSecond < prevSecond && sfxSource && countdownBeepClip)
                sfxSource.PlayOneShot(countdownBeepClip);
        }

        lastTimeRemaining = timeRemaining;
    }

    void OnTimerEnd()
    {
        if (sfxSource && timeUpClip)
            sfxSource.PlayOneShot(timeUpClip);

        musicManager?.SetPitch(1f, outPitchFadeDuration);
    }
}
