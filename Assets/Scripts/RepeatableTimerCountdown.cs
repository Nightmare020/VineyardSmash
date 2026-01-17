using System;
using UnityEngine;
using TMPro;

public class RepeatableTimerCountdown : MonoBehaviour
{
    [Header("Timer Settings")]
    public float durationInSecondsNewRow = 60f;
    public float durationInSecondsStepRow = 30f;

    // Flag for the timer to restart autmatically when it reaches zero if true
    public bool autoRestart;

    [Header("UI Elements")]
    public TextMeshProUGUI countdownTimerText;
    
    // Flag to use step on fruits timer or normal new row timer
    private bool useStepTimer = false;

    private float remainingSeconds;

    public event Action TimerExpired;

    public float RemainingSeconds => remainingSeconds;
    public bool UsingStepTimer => useStepTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResetTimer();
        UpdateTimerUI();
    }

    // Update is called once per frame
    void Update()
    {
        float duration = useStepTimer ? durationInSecondsStepRow : durationInSecondsNewRow;
        
        if (duration <= 0f)
        {
            return;
        }

        remainingSeconds -= Time.deltaTime;

        if (remainingSeconds <= 0f)
        {
            remainingSeconds = 0f;
            UpdateTimerUI();
            
            TimerExpired?.Invoke();

            if (autoRestart)
            {
                ResetTimer();
            }
            else
            {
                // Stop updating if not auto-restarting
                enabled = false;
            }
        }
        else
        {
            UpdateTimerUI();
        }
    }

    public void UseStepTimerMode(bool stepMode, bool restartNow = true)
    {
        useStepTimer = stepMode;
        enabled = true;

        if (restartNow)
        {
            ResetTimer();
            UpdateTimerUI();
        }
    }

    public void ResetTimer()
    {
        remainingSeconds = useStepTimer ? durationInSecondsStepRow : durationInSecondsNewRow;
    }

    private void UpdateTimerUI()
    {
        if (countdownTimerText == null)
        {
            return;
        }

        // Use ceil so timer stays visible until next full second thick
        int totalSeconds = Mathf.CeilToInt(remainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        countdownTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
