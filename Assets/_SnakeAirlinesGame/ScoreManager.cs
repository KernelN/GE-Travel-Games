using System;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private int pointsPerFood = 10;
    [SerializeField] private int pointsPerRemovedSegment = 10;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private string scoreFormat = "Points: {0}";

    public int Score { get; private set; }
    public int PointsPerRemovedSegment => pointsPerRemovedSegment;
    public event Action<int> ScoreChanged;

    private void Start()
    {
        UpdateScoreText();
    }

    public void AddFoodPoints()
    {
        Score += pointsPerFood;
        Notify();
    }

    public void RemoveBodyPoints(int removedSegments)
    {
        int deducted = removedSegments * pointsPerRemovedSegment;
        RemovePoints(deducted);
    }

    public void RemovePoints(int points)
    {
        if (points <= 0)
        {
            return;
        }

        Score = Mathf.Max(0, Score - points);
        Notify();
    }

    public void ResetScore()
    {
        Score = 0;
        Notify();
    }

    private void Notify()
    {
        UpdateScoreText();
        ScoreChanged?.Invoke(Score);
    }

    private void UpdateScoreText()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = string.Format(scoreFormat, Score);
    }
}
