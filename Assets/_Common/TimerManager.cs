using System;
using TMPro;
using UnityEngine;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private float startSeconds = 60f;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private string timerFormat = "Time: {0:0}";
    [SerializeField] private GameObject gameOverObject;
    [SerializeField] private bool pauseOnEnd = true;

    public float TimeRemaining { get; private set; }
    public event Action<float> TimeChanged;
    public event Action OnTimerEnd;

    private bool hasEnded;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (gameOverObject != null)
        {
            gameOverObject.SetActive(false);
        }

        ResetTimer();
    }

    private void Update()
    {
        if (hasEnded)
        {
            return;
        }

        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
        Notify();

        if (TimeRemaining <= 0f)
        {
            EndGame();
        }
    }

    public void ResetTimer()
    {
        TimeRemaining = Mathf.Max(0f, startSeconds);
        hasEnded = false;
        Notify();
    }

    private void EndGame()
    {
        hasEnded = true;
        OnTimerEnd?.Invoke();
        if (pauseOnEnd)
        {
            Time.timeScale = 0f;
        }
        if (gameOverObject != null)
        {
            gameOverObject.SetActive(true);
        }
    }

    private void Notify()
    {
        UpdateTimerText();
        TimeChanged?.Invoke(TimeRemaining);
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
        {
            return;
        }

        timerText.text = string.Format(timerFormat, TimeRemaining);
    }
}
