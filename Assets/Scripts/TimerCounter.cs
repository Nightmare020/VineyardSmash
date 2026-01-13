using UnityEngine;
using TMPro;

public class TimerCounter : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI timerText;

    [Header("Timer Settings")]
    public bool startOnAwake = true;

    // Ignores Time.timeScale when true
    public bool useUnscaledTime = false;
    private bool showHoursIfNeeded = false;

    private float elapsedSeconds;
    private bool isRunning = false;

    private void Awake()
    {
        if (timerText == null)
        {
            Debug.LogError("TimerCounter requires a reference to a TextMeshProUGUI component.");
            enabled = false;
            return;
        }

        elapsedSeconds = 0f;
        isRunning = startOnAwake;
        UpdateDisplay(0f);
    }

    // Update is called once per frame
    void Update()
    {
        if (!isRunning)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsedSeconds += deltaTime;

        UpdateDisplay(elapsedSeconds);
    }

    private void UpdateDisplay(float totalSeconds)
    {
        // Total hundreths (00-99) for the miliseconds part
        int totalHundreths = Mathf.FloorToInt(totalSeconds * 100f);

        int hundreths = totalHundreths % 100;
        int seconds = (totalHundreths / 100) % 60;
        int minutes = (totalHundreths / 100) / 60;

        if (showHoursIfNeeded && minutes >= 60)
        {
            int hours = minutes / 60;
            minutes = minutes % 60;
            
            // Display hours, in a format of decimal and minimum 2 digits width
            timerText.text = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D2}", hours, minutes, seconds, hundreths);
        }
        else
        {
            timerText.text = string.Format("{0:D2}:{1:D2}.{2:D2}", minutes, seconds, hundreths);
        }
    }
}
