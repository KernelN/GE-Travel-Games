using UnityEngine;
using UnityEngine.UI;

public class TimerBarEffect : MonoBehaviour
{
    [SerializeField] TimerManager timerManager;
    [SerializeField] Slider timerSlider;
    [SerializeField] float urgencyThreshold = 10f;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color urgentColor = Color.red;

    float totalDuration;
    Image sliderFillImage;

    void Start()
    {
        totalDuration = timerManager.TimeRemaining;
        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = totalDuration;
            timerSlider.value = totalDuration;
            sliderFillImage = timerSlider.fillRect?.GetComponent<Image>();
        }
        UpdateSlider(timerManager.TimeRemaining);
    }

    void OnEnable()  => timerManager.TimeChanged += UpdateSlider;
    void OnDisable() => timerManager.TimeChanged -= UpdateSlider;

    void UpdateSlider(float remaining)
    {
        if (timerSlider == null) return;
        timerSlider.value = remaining;
        if (sliderFillImage != null)
            sliderFillImage.color = remaining <= urgencyThreshold ? urgentColor : normalColor;
    }
}
