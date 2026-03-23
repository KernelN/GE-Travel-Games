using UnityEngine;
using UnityEngine.SceneManagement;

public class SnakeSessionManager : MonoBehaviour
{
    [SerializeField] TimerManager timerManager;

    void Start()
    {
        if (timerManager != null)
            timerManager.OnTimerEnd += OnSessionEnd;
    }

    void OnDestroy()
    {
        if (timerManager != null)
            timerManager.OnTimerEnd -= OnSessionEnd;
    }

    void OnSessionEnd()
    {
        SceneManager.LoadScene("UserRegister");
    }
}
