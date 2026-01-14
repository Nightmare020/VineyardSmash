using UnityEngine;
using TMPro;

public class RepeatableTimerCountdown : MonoBehaviour
{
    [Header("Timer Settings")]
    public float durationInSecondsNewRow = 30f;
    public float durationInSecondsStepRow = 60f;

    // Flag for the timer to restart autmatically when it reaches zero if true
    public bool autoRestart;

    // Flag to use step on fruits timer or normal new row timer
    private bool useStepTimer = false;

    [Header("UI Elements")]
    public TextMeshProUGUI countdownTimerText;

    private float remainingSeconds;

    private void OnValidate()
    {
        if (!useStepTimer)
        {
            // Prevent negative durations
            if (durationInSecondsNewRow < 0f)
            {
                durationInSecondsNewRow = 0f;
            }
        }
        else
        {

            if (durationInSecondsStepRow < 0f)
            {
                durationInSecondsStepRow = 0f;
            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResetTimer();
        UpdateTimerUI();
    }

    // Update is called once per frame
    void Update()
    {
        if (durationInSecondsNewRow <= 0f && durationInSecondsStepRow <= 0f)
        {
            return;
        }

        remainingSeconds -= Time.deltaTime;

        if (remainingSeconds <= 0f)
        {
            remainingSeconds = 0f;
            UpdateTimerUI();

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

    public void SetDurationSeconds(float seconds)
    {
        if (!useStepTimer)
        {
            durationInSecondsNewRow = Mathf.Max(0f,seconds);
        }
        else
        {
            durationInSecondsStepRow = Mathf.Max(0f,seconds);
        }

        ResetTimer();
        UpdateTimerUI();
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
