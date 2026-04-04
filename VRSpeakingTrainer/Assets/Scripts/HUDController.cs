using UnityEngine;
using TMPro;

/// <summary>
/// Updates the screen-space HUD: countdown timer and live transcript.
/// Attach to a Screen Space - Overlay Canvas in the Session scene.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("HUD References")]
    [Tooltip("MM:SS countdown label")]
    [SerializeField] private TextMeshProUGUI timerLabel;
    [Tooltip("Transcript label — shows last recognised phrase")]
    [SerializeField] private TextMeshProUGUI transcriptLabel;
    [Tooltip("WPM label — shows rolling words-per-minute")]
    [SerializeField] private TextMeshProUGUI wpmLabel;

    private void OnEnable()
    {
        SessionManager.OnSessionStart          += HandleSessionStart;
        SessionManager.OnSessionEnd            += HandleSessionEnd;
        SpeechRecognizer.OnTranscriptionResult += HandleTranscript;
        SpeechAnalyzer.OnMetricsUpdate         += HandleMetrics;
    }

    private void OnDisable()
    {
        SessionManager.OnSessionStart          -= HandleSessionStart;
        SessionManager.OnSessionEnd            -= HandleSessionEnd;
        SpeechRecognizer.OnTranscriptionResult -= HandleTranscript;
        SpeechAnalyzer.OnMetricsUpdate         -= HandleMetrics;
    }

    private void Update()
    {
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning)
            return;

        float remaining = SessionManager.Instance.RemainingSeconds;
        int   minutes   = Mathf.FloorToInt(remaining / 60f);
        int   seconds   = Mathf.FloorToInt(remaining % 60f);

        if (timerLabel != null)
            timerLabel.text = $"{minutes:D2}:{seconds:D2}";
    }

    private void HandleSessionStart()
    {
        if (timerLabel      != null) timerLabel.gameObject.SetActive(true);
        if (transcriptLabel != null) { transcriptLabel.text = ""; transcriptLabel.gameObject.SetActive(true); }
        if (wpmLabel        != null) { wpmLabel.text = "0 WPM"; wpmLabel.gameObject.SetActive(true); }
    }

    private void HandleSessionEnd(SpeechMetrics _)
    {
        if (timerLabel      != null) timerLabel.gameObject.SetActive(false);
        if (transcriptLabel != null) transcriptLabel.gameObject.SetActive(false);
        if (wpmLabel        != null) wpmLabel.gameObject.SetActive(false);
    }

    private void HandleTranscript(string text, bool isFinal)
    {
        if (transcriptLabel == null) return;
        transcriptLabel.text = isFinal ? text : $"...{text}";
    }

    private void HandleMetrics(SpeechMetrics m)
    {
        if (wpmLabel == null) return;
        wpmLabel.text = $"{Mathf.RoundToInt(m.wpm)} WPM";
    }
}
