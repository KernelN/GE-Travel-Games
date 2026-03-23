using UnityEngine;
using UnityEngine.SceneManagement;

public class SnakeSessionManager : MonoBehaviour
{
    [SerializeField] TimerManager timerManager;
    [SerializeField] SnakeHead snakeHead;

    void Start()
    {
        if (timerManager != null)
            timerManager.OnTimerEnd += OnSessionEnd;
        if (snakeHead != null)
            snakeHead.OnSnakeDied += OnSessionEnd;
    }

    void OnDestroy()
    {
        if (timerManager != null)
            timerManager.OnTimerEnd -= OnSessionEnd;
        if (snakeHead != null)
            snakeHead.OnSnakeDied -= OnSessionEnd;
    }

    void OnSessionEnd()
    {
        SceneManager.LoadScene("UserRegister");
    }
}
