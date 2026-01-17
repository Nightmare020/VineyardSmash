using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScorePointsMarker : MonoBehaviour
{
    [Serializable]
    public struct TagPoints
    {
        public string tag;
        public int points;
    }

    [Header("UI")] 
    public TextMeshProUGUI scoreText;
    
    [Header("Points by Tag")]
    public TagPoints[] tagPoints;

    [Header("Special case")] 
    public int cakePoints = 500;
    
    private readonly Dictionary<string, int> scoresMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public int Score { get; private set; }

    private void Awake()
    {
        scoresMap.Clear();
        foreach (var tagPoint in tagPoints)
        {
            if (!string.IsNullOrWhiteSpace(tagPoint.tag))
            {
                scoresMap[tagPoint.tag] = tagPoint.points;
            }
        }
        
        UpdateUI();
    }

    public void ResetScore()
    {
        Score = 0;
        UpdateUI();
    }

    public int GetPointsForTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return 0;
        }

        return scoresMap.TryGetValue(tag, out var p) ? p : 0;
    }

    public void Add(int amount)
    {
        Score += amount;
        UpdateUI();
    }

    public void AddCake()
    {
        Add(cakePoints);
    }

    // Update is called once per frame
    void UpdateUI()
    {
        if (scoreText)
        {
            scoreText.text = Score.ToString("N0");
        }
    }
}
