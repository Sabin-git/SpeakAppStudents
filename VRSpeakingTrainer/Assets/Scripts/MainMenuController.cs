using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Handles MainMenu UI: duration slider, Start button, placeholder panels (PPTX, Settings),
/// and the Developer overlay panel with all debug/dev controls.
///
/// All dev settings are persisted to PlayerPrefs so Session-scene scripts read them
/// without requiring scene-crossing references.
///
/// PlayerPrefs keys written here:
///   SessionDurationSeconds  — duration in seconds (slider or custom debug value)
///   DebugMode               — 1 if debug session (custom duration + dummy results)
///   Dev_MockMode            — 1 = mock speech, no API calls
///   Dev_MockMuted           — 1 = suppress mock phrase events
///   Dev_MockInterval        — seconds between mock phrases
///   Dev_ForceAudienceState  — -1 = rule engine runs; 0-3 = lock to AudienceState
///   Dev_StartingAudienceState — initial AudienceState at session start (0=Engaged)
///   Dev_ForceGazeZone       — -1 = real tracking; 0=Audience, 1=Lectern, 2=Other
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private Slider          durationSlider;
    [SerializeField] private TextMeshProUGUI durationLabel;

    [Header("Panels")]
    [SerializeField] private GameObject devPanelRoot;
    [SerializeField] private GameObject settingsPanelRoot;
    [SerializeField] private GameObject pptxPanelRoot;

    [Header("Dev Panel — Session")]
    [SerializeField] private Toggle         devDebugToggle;
    [SerializeField] private TMP_InputField devDurationInput;   // numeric, placeholder "10"

    [Header("Dev Panel — Speech")]
    [SerializeField] private Toggle          devMockToggle;
    [SerializeField] private Toggle          devMuteToggle;
    [SerializeField] private Slider          devMockIntervalSlider;
    [SerializeField] private TextMeshProUGUI devMockIntervalLabel;

    [Header("Dev Panel — Audience")]
    [SerializeField] private Toggle  devForceStateToggle;
    [Tooltip("4 buttons in order: Engaged(0), Neutral(1), Distracted(2), Restless(3)")]
    [SerializeField] private Button[] devStateButtons;

    [Header("Dev Panel — Gaze")]
    [SerializeField] private Toggle  devForceGazeToggle;
    [Tooltip("3 buttons in order: Audience(0), Lectern(1), Other(2)")]
    [SerializeField] private Button[] devGazeButtons;

    private const int   DefaultMinutes       = 5;
    private const float DefaultMockInterval  = 4f;

    // Gold and inactive colors for selection button highlighting
    private static readonly Color ColorActive   = new Color(0.788f, 0.659f, 0.298f); // #C9A84C
    private static readonly Color ColorInactive = new Color(0.176f, 0.176f, 0.267f); // #2D2D44

    private int _selectedStartingState = 0;  // default Engaged
    private int _selectedGazeZone      = 0;  // default Audience

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        // Duration slider
        if (durationSlider != null)
        {
            durationSlider.minValue     = 1;
            durationSlider.maxValue     = 15;
            durationSlider.wholeNumbers = true;
            durationSlider.value        = PlayerPrefs.GetFloat("SessionDurationSeconds", DefaultMinutes * 60f) / 60f;
            durationSlider.onValueChanged.AddListener(v => UpdateLabel(v));
            UpdateLabel(durationSlider.value);
        }

        RestoreDevSettings();
    }

    private void UpdateLabel(float minutes)
    {
        if (durationLabel != null)
            durationLabel.text = $"{(int)minutes} min";
    }

    // ── Restore dev panel state from PlayerPrefs ───────────────────────────────

    private void RestoreDevSettings()
    {
        if (devDebugToggle != null && PlayerPrefs.HasKey("DebugMode"))
            devDebugToggle.isOn = PlayerPrefs.GetInt("DebugMode") == 1;

        if (devDurationInput != null)
        {
            int saved = Mathf.RoundToInt(PlayerPrefs.GetFloat("SessionDurationSeconds", 10f));
            devDurationInput.text = saved > 0 && saved < 900 ? saved.ToString() : "10";
        }

        if (devMockToggle != null && PlayerPrefs.HasKey("Dev_MockMode"))
            devMockToggle.isOn = PlayerPrefs.GetInt("Dev_MockMode") == 1;

        if (devMuteToggle != null)
        {
            devMuteToggle.isOn          = PlayerPrefs.GetInt("Dev_MockMuted", 0) == 1;
            devMuteToggle.interactable  = devMockToggle != null && devMockToggle.isOn;
        }

        if (devMockIntervalSlider != null)
        {
            devMockIntervalSlider.minValue = 1f;
            devMockIntervalSlider.maxValue = 10f;
            devMockIntervalSlider.value    = PlayerPrefs.GetFloat("Dev_MockInterval", DefaultMockInterval);
            UpdateMockIntervalLabel(devMockIntervalSlider.value);
        }

        _selectedStartingState = PlayerPrefs.GetInt("Dev_StartingAudienceState", 0);
        HighlightButtons(devStateButtons, _selectedStartingState);

        if (devForceStateToggle != null && PlayerPrefs.HasKey("Dev_ForceAudienceState"))
            devForceStateToggle.isOn = PlayerPrefs.GetInt("Dev_ForceAudienceState") >= 0;

        int savedGaze = PlayerPrefs.GetInt("Dev_ForceGazeZone", -1);
        bool gazeForced = savedGaze >= 0;
        _selectedGazeZone = gazeForced ? savedGaze : 0;

        if (devForceGazeToggle != null && PlayerPrefs.HasKey("Dev_ForceGazeZone"))
            devForceGazeToggle.isOn = gazeForced;

        HighlightButtons(devGazeButtons, _selectedGazeZone);
        SetGazeButtonsInteractable(gazeForced);
    }

    // ── Save dev settings to PlayerPrefs ──────────────────────────────────────

    private void SaveDevSettings()
    {
        bool debugOn = devDebugToggle != null && devDebugToggle.isOn;
        PlayerPrefs.SetInt("DebugMode", debugOn ? 1 : 0);

        PlayerPrefs.SetInt  ("Dev_MockMode",     devMockToggle          != null && devMockToggle.isOn          ? 1 : 0);
        PlayerPrefs.SetInt  ("Dev_MockMuted",    devMuteToggle          != null && devMuteToggle.isOn          ? 1 : 0);
        PlayerPrefs.SetFloat("Dev_MockInterval", devMockIntervalSlider  != null ? devMockIntervalSlider.value   : DefaultMockInterval);

        bool forceState = devForceStateToggle != null && devForceStateToggle.isOn;
        PlayerPrefs.SetInt("Dev_ForceAudienceState",  forceState ? _selectedStartingState : -1);
        PlayerPrefs.SetInt("Dev_StartingAudienceState", _selectedStartingState);

        bool forceGaze = devForceGazeToggle != null && devForceGazeToggle.isOn;
        PlayerPrefs.SetInt("Dev_ForceGazeZone", forceGaze ? _selectedGazeZone : -1);

        PlayerPrefs.Save();
    }

    // ── Main button callbacks ──────────────────────────────────────────────────

    /// <summary>Called by the Start button onClick.</summary>
    public void OnStartPressed()
    {
        SaveDevSettings();

        bool debugOn = devDebugToggle != null && devDebugToggle.isOn;

        float durationSeconds;
        if (debugOn)
        {
            int parsed = int.TryParse(devDurationInput != null ? devDurationInput.text : "10", out int v) ? v : 10;
            durationSeconds = Mathf.Clamp(parsed, 1, 900);
        }
        else
        {
            durationSeconds = (durationSlider != null ? durationSlider.value : DefaultMinutes) * 60f;
        }

        PlayerPrefs.SetFloat("SessionDurationSeconds", durationSeconds);
        PlayerPrefs.Save();

        SessionManager.LoadSession();
    }

    // ── Panel open / close ─────────────────────────────────────────────────────

    public void OnDeveloperPressed()  => SetPanel(devPanelRoot,      true);
    public void OnSettingsPressed()   => SetPanel(settingsPanelRoot, true);
    public void OnPPTXPressed()       => SetPanel(pptxPanelRoot,     true);

    public void OnCloseDevPanel()  { SaveDevSettings(); SetPanel(devPanelRoot,      false); }
    public void OnCloseSettings()  => SetPanel(settingsPanelRoot, false);
    public void OnClosePPTX()      => SetPanel(pptxPanelRoot,     false);

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    // ── Skip to Results ────────────────────────────────────────────────────────

    /// <summary>Called by the Skip to Results button in the Dev panel.</summary>
    public void OnSkipToResults()
    {
        SaveDevSettings();
        PlayerPrefs.SetFloat("Results_SessionTime", 0f);
        PlayerPrefs.Save();
        SceneManager.LoadScene("Results");
    }

    // ── Dev panel — speech callbacks ───────────────────────────────────────────

    /// <summary>Called by Mock Mode toggle onValueChanged.</summary>
    public void OnMockModeChanged(bool isOn)
    {
        if (devMuteToggle != null) devMuteToggle.interactable = isOn;
    }

    /// <summary>Called by Mock Interval slider onValueChanged.</summary>
    public void OnMockIntervalChanged(float value) => UpdateMockIntervalLabel(value);

    private void UpdateMockIntervalLabel(float value)
    {
        if (devMockIntervalLabel != null)
            devMockIntervalLabel.text = $"{value:F1} s";
    }

    // ── Dev panel — audience callbacks ─────────────────────────────────────────

    /// <summary>Called by each of the 4 audience state buttons via onClick (pass int 0–3).</summary>
    public void OnStartingStateSelected(int index)
    {
        _selectedStartingState = Mathf.Clamp(index, 0, 3);
        HighlightButtons(devStateButtons, _selectedStartingState);
    }

    // ── Dev panel — gaze callbacks ─────────────────────────────────────────────

    /// <summary>Called by Force Gaze toggle onValueChanged.</summary>
    public void OnForceGazeChanged(bool isOn) => SetGazeButtonsInteractable(isOn);

    /// <summary>Called by each of the 3 gaze zone buttons via onClick (pass int 0–2).</summary>
    public void OnGazeZoneSelected(int index)
    {
        _selectedGazeZone = Mathf.Clamp(index, 0, 2);
        HighlightButtons(devGazeButtons, _selectedGazeZone);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void HighlightButtons(Button[] buttons, int activeIndex)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            var img = buttons[i].GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = (i == activeIndex) ? ColorActive : ColorInactive;
        }
    }

    private void SetGazeButtonsInteractable(bool interactable)
    {
        if (devGazeButtons == null) return;
        foreach (var btn in devGazeButtons)
            if (btn != null) btn.interactable = interactable;
    }
}
