using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Handles MainMenu UI: duration slider, Start button, placeholder panels (PPTX, Settings),
/// and the Developer overlay panel with all debug/dev controls.
///
/// Also owns the first-launch CONSENT flow (Task 2): on Start, if
/// PlayerPrefs Consent_Granted != 1, the main menu interactive UI is hidden
/// behind the consent screen until the user taps "I consent". This avoids
/// adding a separate MonoBehaviour just for two screens of consent UI and
/// keeps us under the project script cap.
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
///   Consent_Granted         — 0/1 first-launch consent; gate for reaching the menu
///   Settings_Responsiveness — "easy"/"medium"/"hard" (Task 2d), mirrored from the
///                              dev panel "Force Responsiveness" row for mid-Play
///                              testing. Authoritative writer is SettingsMenuController.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private Slider          durationSlider;
    [SerializeField] private TextMeshProUGUI durationLabel;

    [Header("Main Menu localized labels (optional)")]
    [Tooltip("Main title TMP — refreshed when language changes from Settings.")]
    [SerializeField] private TextMeshProUGUI menuTitleLabel;
    [SerializeField] private TextMeshProUGUI startButtonLabel;
    [SerializeField] private TextMeshProUGUI settingsButtonLabel;
    [SerializeField] private TextMeshProUGUI developerButtonLabel;
    [SerializeField] private TextMeshProUGUI pptxButtonLabel;
    [SerializeField] private TextMeshProUGUI durationSectionLabel;

    [Header("Main menu interactive roots (hidden when consent is pending)")]
    [Tooltip("Group GameObjects containing the Start/Settings/Developer/PPTX buttons + duration slider + headers. All are SetActive(false) when consent is pending and SetActive(true) once consent is granted. Drag HeaderGroup, StartSessionGroup, OtherButtonsGroup here.")]
    [SerializeField] private GameObject[] mainMenuInteractiveRoots;

    [Header("Panels")]
    [SerializeField] private GameObject devPanelRoot;
    [SerializeField] private GameObject settingsPanelRoot;
    [SerializeField] private GameObject pptxPanelRoot;

    [Header("Settings controller (Task 2)")]
    [SerializeField] private SettingsMenuController settingsMenu;

    [Header("Consent screen (Task 2 — first launch gate)")]
    [SerializeField] private GameObject       consentPanelRoot;
    [SerializeField] private TextMeshProUGUI  consentTitleLabel;
    [SerializeField] private TextMeshProUGUI  consentBodyLabel;
    [SerializeField] private Button           consentAcceptButton;
    [SerializeField] private TextMeshProUGUI  consentAcceptLabel;
    [SerializeField] private Button           consentDeclineButton;
    [SerializeField] private TextMeshProUGUI  consentDeclineLabel;

    [Header("Consent declined sub-screen")]
    [SerializeField] private GameObject       consentBlockedPanelRoot;
    [SerializeField] private TextMeshProUGUI  consentBlockedTitleLabel;
    [SerializeField] private TextMeshProUGUI  consentBlockedBodyLabel;
    [SerializeField] private Button           consentRetryButton;
    [SerializeField] private TextMeshProUGUI  consentRetryLabel;

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

    [Header("Dev Panel — Force Responsiveness (Task 2d)")]
    [Tooltip("Writes Settings_Responsiveness pref. Takes effect on the NEXT session start " +
             "(rule engine + per-avatar stagger window are sampled once at start). " +
             "Use Stop + Play to re-apply on the current session.")]
    [SerializeField] private Button          devForceResponsivenessEasy;
    [SerializeField] private Button          devForceResponsivenessMedium;
    [SerializeField] private Button          devForceResponsivenessHard;
    [SerializeField] private TextMeshProUGUI devForceResponsivenessEasyLabel;
    [SerializeField] private TextMeshProUGUI devForceResponsivenessMediumLabel;
    [SerializeField] private TextMeshProUGUI devForceResponsivenessHardLabel;
    [Tooltip("Optional row-header label rendered in front of the three buttons (e.g. 'Force responsiveness:').")]
    [SerializeField] private TextMeshProUGUI devForceResponsivenessHeaderLabel;

    private const int   DefaultMinutes       = 5;
    private const float DefaultMockInterval  = 4f;

    // Gold and inactive colors for selection button highlighting
    private static readonly Color ColorActive   = new Color(0.788f, 0.659f, 0.298f); // #C9A84C
    private static readonly Color ColorInactive = new Color(0.176f, 0.176f, 0.267f); // #2D2D44

    private int _selectedStartingState = 0;  // default Engaged
    private int _selectedGazeZone      = 0;  // default Audience

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Kick off the localization JSON load once per app launch. Reads the
        // Language PlayerPref (defaults to "en"). Subsequent scenes call
        // Localization.Get() synchronously — they get the key as a fallback
        // until the JSON finishes loading, which is typically within a few
        // frames on Android.
        Localization.Load(this);
    }

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

        // Hand the Settings controller a reference to us so it can call back
        // into the consent flow ("Review consent" button) and ask us to
        // refresh our own labels after a language change.
        if (settingsMenu != null) settingsMenu.SetMainMenu(this);

        WireConsentButtons();
        // Wait for the Localization JSON to finish loading before populating
        // labels — otherwise Get() returns the raw key (e.g. "menu_title") as
        // the documented missing-key fallback, which then shows on screen for
        // the first frame and looks broken to the user.
        StartCoroutine(RefreshLocalizedTextWhenLoaded());

        // First-launch consent gate: block access to the menu UI until we have
        // a valid Consent_Granted == 1 in PlayerPrefs. Pass `firstLaunch=true`
        // so we know not to allow a "back to menu" affordance here.
        if (PlayerPrefs.GetInt("Consent_Granted", 0) == 1)
        {
            HideConsentScreen();
        }
        else
        {
            ShowConsentScreen(reviewingFromSettings: false);
        }
    }

    private void UpdateLabel(float minutes)
    {
        // Main menu stays English — hardcoded " min" suffix.
        if (durationLabel != null)
            durationLabel.text = (int)minutes + " min";
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
            devDurationInput.interactable = devDebugToggle != null && devDebugToggle.isOn;
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

        WireDevForceResponsivenessRow();
        HighlightDevResponsivenessButtons();
    }

    // ── Dev panel — force-responsiveness row (Task 2d) ────────────────────────

    /// <summary>
    /// Hooks the three "Force Responsiveness" dev-panel buttons to write the
    /// Settings_Responsiveness pref. The pref takes effect on the next session
    /// start — researchers should Stop + Play to re-apply on the currently
    /// running scene. We re-highlight the row after every click so the active
    /// level is visually obvious without leaving the dev panel.
    /// </summary>
    private void WireDevForceResponsivenessRow()
    {
        if (devForceResponsivenessEasy != null)
        {
            devForceResponsivenessEasy.onClick.RemoveAllListeners();
            devForceResponsivenessEasy.onClick.AddListener(() => OnDevForceResponsiveness("easy"));
        }
        if (devForceResponsivenessMedium != null)
        {
            devForceResponsivenessMedium.onClick.RemoveAllListeners();
            devForceResponsivenessMedium.onClick.AddListener(() => OnDevForceResponsiveness("medium"));
        }
        if (devForceResponsivenessHard != null)
        {
            devForceResponsivenessHard.onClick.RemoveAllListeners();
            devForceResponsivenessHard.onClick.AddListener(() => OnDevForceResponsiveness("hard"));
        }
    }

    private void OnDevForceResponsiveness(string level)
    {
        PlayerPrefs.SetString("Settings_Responsiveness", level);
        PlayerPrefs.Save();
        HighlightDevResponsivenessButtons();
        // The user-facing Settings panel will re-read Settings_Responsiveness
        // the next time it's opened (via OpenSettingsPanel → RefreshFromPlayerPrefs),
        // so the radio there will visually sync automatically.
        Debug.Log($"[MainMenu] Settings_Responsiveness → {level} (effective on next session start)");
    }

    private void HighlightDevResponsivenessButtons()
    {
        string current = PlayerPrefs.GetString("Settings_Responsiveness", "medium").ToLowerInvariant();
        int active = current == "easy" ? 0 : current == "hard" ? 2 : 1;

        var trio = new Button[] {
            devForceResponsivenessEasy,
            devForceResponsivenessMedium,
            devForceResponsivenessHard
        };
        HighlightButtons(trio, active);
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
    public void OnPPTXPressed()       => SetPanel(pptxPanelRoot,     true);

    /// <summary>
    /// Settings button on MainMenu. Delegates to SettingsMenuController so
    /// the controller can refresh its own state from PlayerPrefs first.
    /// Falls back to a raw panel toggle if the controller isn't wired.
    /// </summary>
    public void OnSettingsPressed()
    {
        if (settingsMenu != null) settingsMenu.OpenSettingsPanel();
        else                      SetPanel(settingsPanelRoot, true);
    }

    public void OnCloseDevPanel()  { SaveDevSettings(); SetPanel(devPanelRoot,      false); }

    public void OnCloseSettings()
    {
        if (settingsMenu != null) settingsMenu.CloseSettingsPanel();
        else                      SetPanel(settingsPanelRoot, false);
    }

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

    /// <summary>Called by Debug Mode toggle onValueChanged.</summary>
    public void OnDebugModeChanged(bool isOn)
    {
        if (devDurationInput != null) devDurationInput.interactable = isOn;
    }

    /// <summary>Called by Mock Mode toggle onValueChanged.</summary>
    public void OnMockModeChanged(bool isOn)
    {
        if (devMuteToggle != null) devMuteToggle.interactable = isOn;
    }

    /// <summary>Called by Mock Interval slider onValueChanged.</summary>
    public void OnMockIntervalChanged(float value) => UpdateMockIntervalLabel(value);

    private void UpdateMockIntervalLabel(float value)
    {
        // Dev panel is researcher-facing, stays English — hardcoded " s" suffix.
        if (devMockIntervalLabel != null)
            devMockIntervalLabel.text = value.ToString("F1") + " s";
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

    // ── Consent screen (Task 2 — first-launch gate) ───────────────────────────
    //
    // The consent flow is intentionally folded into MainMenuController instead
    // of a separate ConsentScreenController to stay under the project's 12+
    // MonoBehaviour cap (we added SettingsMenuController; folding consent here
    // brings the runtime total to 13 instead of 14). Consent is structurally
    // tied to MainMenu visibility, so co-locating it is also clean.

    private void WireConsentButtons()
    {
        if (consentAcceptButton != null)
        {
            consentAcceptButton.onClick.RemoveAllListeners();
            consentAcceptButton.onClick.AddListener(OnConsentAccepted);
        }
        if (consentDeclineButton != null)
        {
            consentDeclineButton.onClick.RemoveAllListeners();
            consentDeclineButton.onClick.AddListener(OnConsentDeclined);
        }
        if (consentRetryButton != null)
        {
            consentRetryButton.onClick.RemoveAllListeners();
            consentRetryButton.onClick.AddListener(OnConsentRetry);
        }
    }

    /// <summary>
    /// Show the consent screen. Hides the main menu interactive root and any
    /// open sub-panels. If reviewingFromSettings is true, the user came from
    /// the Settings "Review consent" button (they already have consent saved);
    /// otherwise this is a first-launch gate.
    /// </summary>
    public void ShowConsentScreen(bool reviewingFromSettings)
    {
        // Hide everything else first.
        SetInteractiveRootsActive(false);
        if (devPanelRoot            != null) devPanelRoot.SetActive(false);
        if (settingsPanelRoot       != null) settingsPanelRoot.SetActive(false);
        if (pptxPanelRoot           != null) pptxPanelRoot.SetActive(false);
        if (consentBlockedPanelRoot != null) consentBlockedPanelRoot.SetActive(false);

        if (consentPanelRoot != null) consentPanelRoot.SetActive(true);
    }

    private void HideConsentScreen()
    {
        if (consentPanelRoot        != null) consentPanelRoot.SetActive(false);
        if (consentBlockedPanelRoot != null) consentBlockedPanelRoot.SetActive(false);
        SetInteractiveRootsActive(true);
    }

    private void SetInteractiveRootsActive(bool active)
    {
        if (mainMenuInteractiveRoots == null) return;
        foreach (var root in mainMenuInteractiveRoots)
            if (root != null) root.SetActive(active);
    }

    private void OnConsentAccepted()
    {
        PlayerPrefs.SetInt("Consent_Granted", 1);
        PlayerPrefs.Save();
        HideConsentScreen();
        if (settingsMenu != null) settingsMenu.RefreshPrivacyStatus();
    }

    private void OnConsentDeclined()
    {
        PlayerPrefs.SetInt("Consent_Granted", 0);
        PlayerPrefs.Save();
        if (consentPanelRoot        != null) consentPanelRoot.SetActive(false);
        if (consentBlockedPanelRoot != null) consentBlockedPanelRoot.SetActive(true);
        if (settingsMenu != null) settingsMenu.RefreshPrivacyStatus();
    }

    private void OnConsentRetry()
    {
        // Bounce the user back to the consent prompt so they can flip their
        // choice. Do NOT quit the app — leaving them stranded in "blocked" is
        // bad UX and the user-testing plan calls out "let them change their
        // mind".
        if (consentBlockedPanelRoot != null) consentBlockedPanelRoot.SetActive(false);
        if (consentPanelRoot        != null) consentPanelRoot.SetActive(true);
    }

    // ── Localized text refresh (called from SettingsMenuController after a
    //    language change so the main menu doesn't look stale). ───────────────

    // Polls Localization.IsLoaded once per frame, then runs RefreshLocalizedText.
    // Used at Start to avoid the empty-dictionary fallback (showing raw keys).
    private IEnumerator RefreshLocalizedTextWhenLoaded()
    {
        while (!Localization.IsLoaded) yield return null;
        RefreshLocalizedText();
    }

    public void RefreshLocalizedText()
    {
        // Main menu labels are intentionally NOT localized — they're the
        // researcher-facing entry point and stay English regardless of the
        // participant's chosen Language. Whatever the TMP text says in the
        // Editor (e.g. "VR Speaking Trainer", "Practice", "Settings", etc.)
        // is what the user sees at runtime.
        // The SerializeField slots below are kept for backward compatibility
        // with existing scene wiring but are no longer driven from Localization.

        // Consent screen text
        if (consentTitleLabel        != null) consentTitleLabel.text        = Localization.Get("consent_title");
        if (consentBodyLabel         != null) consentBodyLabel.text         = Localization.Get("consent_body");
        if (consentAcceptLabel       != null) consentAcceptLabel.text       = Localization.Get("consent_accept");
        if (consentDeclineLabel      != null) consentDeclineLabel.text      = Localization.Get("consent_decline");
        if (consentBlockedTitleLabel != null) consentBlockedTitleLabel.text = Localization.Get("consent_blocked_title");
        if (consentBlockedBodyLabel  != null) consentBlockedBodyLabel.text  = Localization.Get("consent_blocked_body");
        if (consentRetryLabel        != null) consentRetryLabel.text        = Localization.Get("consent_retry");

        // Dev panel — Force Responsiveness row (Task 2d). Localized so the
        // dev row matches whichever language the researcher is testing in.
        if (devForceResponsivenessHeaderLabel != null)
            devForceResponsivenessHeaderLabel.text = Localization.Get("dev_force_responsiveness");
        if (devForceResponsivenessEasyLabel   != null)
            devForceResponsivenessEasyLabel.text   = Localization.Get("settings_responsiveness_easy");
        if (devForceResponsivenessMediumLabel != null)
            devForceResponsivenessMediumLabel.text = Localization.Get("settings_responsiveness_medium");
        if (devForceResponsivenessHardLabel   != null)
            devForceResponsivenessHardLabel.text   = Localization.Get("settings_responsiveness_hard");

        // Update the duration suffix label too.
        if (durationSlider != null) UpdateLabel(durationSlider.value);
    }
}
