using System.Collections;
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
    [Tooltip("Feature B — shown in place of WPM/transcript when the microphone is " +
             "disabled in Settings. Text comes from hud_mic_off. Optional.")]
    [SerializeField] private TextMeshProUGUI micOffIndicator;

    // Feature B — mirror of Settings_MicEnabled, read at session start.
    private bool _micEnabled = true;

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
        // Feature B — when the mic is disabled, hide the speech HUD (WPM +
        // transcript) and show the "Microphone off" indicator instead. No
        // transcript/metric events fire in silent mode, so those labels would
        // otherwise sit frozen at their placeholder values.
        _micEnabled = PlayerPrefs.GetInt("Settings_MicEnabled", 1) == 1;

        if (timerLabel      != null) timerLabel.gameObject.SetActive(true);
        if (transcriptLabel != null) transcriptLabel.gameObject.SetActive(_micEnabled);
        if (wpmLabel        != null) wpmLabel.gameObject.SetActive(_micEnabled);
        if (micOffIndicator != null) micOffIndicator.gameObject.SetActive(!_micEnabled);

        // Wait for Localization to finish loading before populating initial
        // labels — avoids showing the raw key name (e.g. "hud_transcript_placeholder")
        // when MainMenu was skipped or the JSON load is still in flight.
        // Also kicks off the load defensively if no scene has done so yet
        // (e.g. when entering Play Mode directly in the Session scene).
        StartCoroutine(SetInitialLabelsWhenLoaded());
    }

    private IEnumerator SetInitialLabelsWhenLoaded()
    {
        if (!Localization.IsLoaded)
            Localization.Load(this);

        while (!Localization.IsLoaded) yield return null;

        if (transcriptLabel != null) transcriptLabel.text = Localization.Get("hud_transcript_placeholder");
        if (wpmLabel        != null) wpmLabel.text        = Localization.Get("hud_wpm_zero");
        if (micOffIndicator != null) micOffIndicator.text = Localization.Get("hud_mic_off");
    }

    private void HandleSessionEnd(SpeechMetrics _)
    {
        if (timerLabel      != null) timerLabel.gameObject.SetActive(false);
        if (transcriptLabel != null) transcriptLabel.gameObject.SetActive(false);
        if (wpmLabel        != null) wpmLabel.gameObject.SetActive(false);
        if (micOffIndicator != null) micOffIndicator.gameObject.SetActive(false);
    }

    private void HandleTranscript(string text, bool isFinal)
    {
        if (transcriptLabel == null) return;
        transcriptLabel.text = isFinal ? text : $"...{text}";
    }

    private void HandleMetrics(SpeechMetrics m)
    {
        if (wpmLabel == null) return;

        // If Localization isn't loaded yet, skip formatting — Get() would
        // return the raw key "hud_wpm_format" which has no {0} placeholder,
        // so string.Format leaves it unchanged and the user sees the key.
        if (!Localization.IsLoaded)
        {
            wpmLabel.text = Mathf.RoundToInt(m.wpm) + " WPM";
            return;
        }

        string template = Localization.Get("hud_wpm_format");
        try   { wpmLabel.text = string.Format(template, Mathf.RoundToInt(m.wpm)); }
        catch { wpmLabel.text = Mathf.RoundToInt(m.wpm) + " WPM"; }
    }
}
