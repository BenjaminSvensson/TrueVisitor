using TMPro;
using UnityEngine;

public class SurviveTimerDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private float durationSeconds = 180f;
    [SerializeField] private string prefix = "Survive ";
    [SerializeField] private bool hideUntilStarted = true;

    private float remainingSeconds;
    private bool running;

    public bool IsRunning => running;

    private void Awake()
    {
        ResolveTimerText();
        remainingSeconds = durationSeconds;
        UpdateText();

        if (hideUntilStarted)
        {
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!running)
        {
            return;
        }

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        UpdateText();

        if (remainingSeconds <= 0f)
        {
            running = false;
        }
    }

    public void StartCountdown()
    {
        remainingSeconds = durationSeconds;
        running = true;
        gameObject.SetActive(true);
        UpdateText();
    }

    private void ResolveTimerText()
    {
        if (timerText == null)
        {
            timerText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void UpdateText()
    {
        ResolveTimerText();
        if (timerText == null)
        {
            return;
        }

        int wholeSeconds = Mathf.CeilToInt(remainingSeconds);
        int minutes = wholeSeconds / 60;
        int seconds = wholeSeconds % 60;
        timerText.text = $"{prefix}{minutes:00}:{seconds:00}";
    }

    private void OnValidate()
    {
        durationSeconds = Mathf.Max(0f, durationSeconds);
        if (string.IsNullOrEmpty(prefix))
        {
            prefix = "Survive ";
        }
    }
}
