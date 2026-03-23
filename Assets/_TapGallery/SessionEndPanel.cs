using TMPro;
using UnityEngine;

public class SessionEndPanel : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
    [SerializeField] TMP_Text finalScoreLabel;
    [SerializeField] string scoreFormat = "Score: {0}";

    void Awake() => panelRoot?.SetActive(false);

    public void Show(int finalScore)
    {
        if (finalScoreLabel != null)
            finalScoreLabel.text = string.Format(scoreFormat, finalScore);
        panelRoot?.SetActive(true);
    }
}
