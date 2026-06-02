using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the user-facing Settings panel that lives on the MainMenu canvas
/// alongside the existing developer panel. Three sections, no more:
///   1) Language     — English / Dutch radio, writes the Language PlayerPref and
///                     reloads the localization dictionary live.
///   2) Privacy/data — consent-status readout, "Review consent" (reopens the
///                     consent screen via MainMenuController), "Privacy policy"
///                     (opens the in-app text sub-panel), and the "Allow
///                     microphone" toggle (Feature B — writes Settings_MicEnabled;
///                     OFF = silent session, no STT).
///   3) Audience     — responsiveness radio (Easy / Medium / Hard), writes
///                     Settings_Responsiveness. Picked up at session start by
///                     AudienceRuleEngine, AudienceMember, HeadTracker.
///   4) Advanced     — "Show developer button" toggle (Feature A), writes
///                     Settings_ShowDevButton and asks MainMenuController to
///                     re-apply the MainMenu Developer button's visibility.
///
/// The developer panel itself is unrelated and is NOT touched by this controller.
///
/// PlayerPrefs touched here:
///   Language               — string ("en" | "nl"), default "en" (shared with Localization)
///   Settings_Responsiveness— string ("easy"|"medium"|"hard"), default "medium"
///                            Read at session start by AudienceRuleEngine, AudienceMember
///                            (and Task 4 HeadTracker). Constant during a session.
///   Settings_ShowDevButton — int 0/1, default 0 (hidden). Read by MainMenuController
///                            to show/hide the MainMenu Developer button.
///   Settings_MicEnabled    — int 0/1, default 1 (on). Read at session start by
///                            SpeechRecognizer (silent mode), HUDController, ResultsUI.
///   Consent_Granted        — int 0/1,               default 0 (READ here, WRITTEN by MainMenuController)
///
/// All user-facing strings are pulled from Localization.Get() so the panel
/// matches the active Language pref.
/// </summary>
public class SettingsMenuController : MonoBehaviour
{
    // ── Inspector wiring ───────────────────────────────────────────────────────

    [Header("Panel roots")]
    [Tooltip("Root GameObject of the Settings panel — toggled active/inactive by MainMenuController.")]
    [SerializeField] private GameObject settingsPanelRoot;
    [Tooltip("Root of the in-app Privacy Policy sub-panel (opened from the Privacy section).")]
    [SerializeField] private GameObject privacyPolicyPanelRoot;

    [Header("Header / Back")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI backButtonLabel;
    [SerializeField] private Button          backButton;

    [Header("Language section")]
    [SerializeField] private TextMeshProUGUI languageSectionLabel;
    [Tooltip("Two radio-style toggles in order: English(0), Dutch(1). Use the same ToggleGroup so they behave as a radio.")]
    [SerializeField] private Toggle          langEnglishToggle;
    [SerializeField] private Toggle          langDutchToggle;
    [SerializeField] private TextMeshProUGUI langEnglishLabel;
    [SerializeField] private TextMeshProUGUI langDutchLabel;

    [Header("Privacy section")]
    [SerializeField] private TextMeshProUGUI privacySectionLabel;
    [SerializeField] private TextMeshProUGUI privacyStatusLabel;     // "Audio sent to Google Cloud STT"
    [SerializeField] private TextMeshProUGUI privacyStatusValue;     // "YES" / "NO" — driven from Consent_Granted
    [SerializeField] private Button          reviewConsentButton;
    [SerializeField] private TextMeshProUGUI reviewConsentLabel;
    [SerializeField] private Button          privacyPolicyButton;
    [SerializeField] private TextMeshProUGUI privacyPolicyLabel;
    [Tooltip("Feature B — when OFF, the session runs with no microphone/STT (silent " +
             "crowd that drifts to Restless). Default ON. Writes Settings_MicEnabled.")]
    [SerializeField] private Toggle          allowMicToggle;
    [SerializeField] private TextMeshProUGUI allowMicLabel;

    [Header("Privacy policy sub-panel")]
    [SerializeField] private TextMeshProUGUI privacyPolicyTitleLabel;
    [SerializeField] private TextMeshProUGUI privacyPolicyBodyLabel;
    [SerializeField] private Button          privacyPolicyBackButton;
    [SerializeField] private TextMeshProUGUI privacyPolicyBackLabel;

    [Header("Audience section (Task 2d — responsiveness)")]
    [SerializeField] private TextMeshProUGUI audienceSectionLabel;
    [SerializeField] private TextMeshProUGUI responsivenessLabel;
    [Tooltip("Three radio-style toggles in order: Easy(0), Medium(1), Hard(2). " +
             "Place all three in the same ToggleGroup so they behave as a radio.")]
    [SerializeField] private Toggle          easyToggle;
    [SerializeField] private Toggle          mediumToggle;
    [SerializeField] private Toggle          hardToggle;
    [SerializeField] private TextMeshProUGUI easyToggleLabel;
    [SerializeField] private TextMeshProUGUI mediumToggleLabel;
    [SerializeField] private TextMeshProUGUI hardToggleLabel;

    [Header("Advanced section (Feature A — dev button visibility)")]
    [SerializeField] private TextMeshProUGUI advancedSectionLabel;
    [SerializeField] private TextMeshProUGUI showDevButtonLabel;
    [Tooltip("When ON, the MainMenu Developer button is shown. Default OFF (hidden) " +
             "so participants don't see it. Writes Settings_ShowDevButton.")]
    [SerializeField] private Toggle          showDevButtonToggle;

    // Hook back into MainMenuController for the "Review consent" button so we
    // don't duplicate the consent UI here. Assigned by MainMenuController in
    // its Start() through SetMainMenu().
    private MainMenuController _mainMenu;

    private const string DefaultResponsiveness = "medium";
    private const string ResponsivenessEasy    = "easy";
    private const string ResponsivenessMedium  = "medium";
    private const string ResponsivenessHard    = "hard";

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        WireUI();
        RefreshFromPlayerPrefs();
        RefreshLocalizedText();
        RefreshPrivacyStatus();

        // Panels default to hidden — MainMenuController toggles them on demand.
        if (settingsPanelRoot       != null) settingsPanelRoot.SetActive(false);
        if (privacyPolicyPanelRoot  != null) privacyPolicyPanelRoot.SetActive(false);
    }

    /// <summary>Called by MainMenuController in its Start() to pass itself in.</summary>
    public void SetMainMenu(MainMenuController menu) => _mainMenu = menu;

    // ── Public entry points (called by MainMenuController) ────────────────────

    /// <summary>Opens the Settings panel. Called by the MainMenu Settings button via MainMenuController.</summary>
    public void OpenSettingsPanel()
    {
        RefreshFromPlayerPrefs();
        RefreshLocalizedText();
        RefreshPrivacyStatus();
        if (settingsPanelRoot      != null) settingsPanelRoot.SetActive(true);
        if (privacyPolicyPanelRoot != null) privacyPolicyPanelRoot.SetActive(false);
    }

    /// <summary>Closes the Settings panel and its policy sub-panel.</summary>
    public void CloseSettingsPanel()
    {
        if (privacyPolicyPanelRoot != null) privacyPolicyPanelRoot.SetActive(false);
        if (settingsPanelRoot      != null) settingsPanelRoot.SetActive(false);
    }

    /// <summary>Recomputes the privacy-status line from the Consent_Granted pref.
    /// Writes the full sentence ("Audio sent to Google Cloud STT: Yes") into
    /// privacyStatusValue so the row needs only one TMP. privacyStatusLabel is
    /// optional — kept in the API for back-compat but no longer needs to be wired.</summary>
    public void RefreshPrivacyStatus()
    {
        if (privacyStatusValue == null) return;
        bool granted = PlayerPrefs.GetInt("Consent_Granted", 0) == 1;
        string yesNo = granted
            ? Localization.Get("settings_privacy_status_yes")
            : Localization.Get("settings_privacy_status_no");
        privacyStatusValue.text = string.Format(
            Localization.Get("settings_privacy_status_format"), yesNo);
    }

    // ── UI wiring ──────────────────────────────────────────────────────────────

    private void WireUI()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseSettingsPanel);
        }

        if (langEnglishToggle != null)
        {
            langEnglishToggle.onValueChanged.RemoveAllListeners();
            langEnglishToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) OnLanguageSelected(Localization.LangEnglish);
            });
        }

        if (langDutchToggle != null)
        {
            langDutchToggle.onValueChanged.RemoveAllListeners();
            langDutchToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) OnLanguageSelected(Localization.LangDutch);
            });
        }

        if (reviewConsentButton != null)
        {
            reviewConsentButton.onClick.RemoveAllListeners();
            reviewConsentButton.onClick.AddListener(OnReviewConsentPressed);
        }

        if (privacyPolicyButton != null)
        {
            privacyPolicyButton.onClick.RemoveAllListeners();
            privacyPolicyButton.onClick.AddListener(OpenPrivacyPolicy);
        }

        // Allow-microphone toggle (Feature B).
        if (allowMicToggle != null)
        {
            allowMicToggle.onValueChanged.RemoveAllListeners();
            allowMicToggle.onValueChanged.AddListener(OnAllowMicToggled);
        }

        if (privacyPolicyBackButton != null)
        {
            privacyPolicyBackButton.onClick.RemoveAllListeners();
            privacyPolicyBackButton.onClick.AddListener(ClosePrivacyPolicy);
        }

        // Advanced — "Show developer button" (Feature A).
        if (showDevButtonToggle != null)
        {
            showDevButtonToggle.onValueChanged.RemoveAllListeners();
            showDevButtonToggle.onValueChanged.AddListener(OnShowDevButtonToggled);
        }

        // Audience responsiveness radio (Task 2d).
        // Each toggle writes the matching string to Settings_Responsiveness on
        // turn-on. The ToggleGroup component on the parent Row enforces mutual
        // exclusion in Unity Editor wiring — see WIRING_TASK_2D.md.
        if (easyToggle != null)
        {
            easyToggle.onValueChanged.RemoveAllListeners();
            easyToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) OnResponsivenessSelected(ResponsivenessEasy);
            });
        }
        if (mediumToggle != null)
        {
            mediumToggle.onValueChanged.RemoveAllListeners();
            mediumToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) OnResponsivenessSelected(ResponsivenessMedium);
            });
        }
        if (hardToggle != null)
        {
            hardToggle.onValueChanged.RemoveAllListeners();
            hardToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) OnResponsivenessSelected(ResponsivenessHard);
            });
        }
    }

    private void RefreshFromPlayerPrefs()
    {
        // Language radio
        string lang = NormalizeLang(PlayerPrefs.GetString("Language", Localization.LangEnglish));
        if (langEnglishToggle != null) langEnglishToggle.SetIsOnWithoutNotify(lang == Localization.LangEnglish);
        if (langDutchToggle   != null) langDutchToggle.SetIsOnWithoutNotify  (lang == Localization.LangDutch);

        // Audience responsiveness radio — restore the active toggle from the
        // pref (default = medium). SetIsOnWithoutNotify so the listener doesn't
        // fire a redundant write back to PlayerPrefs.
        string responsiveness = NormalizeResponsiveness(
            PlayerPrefs.GetString("Settings_Responsiveness", DefaultResponsiveness));
        if (easyToggle   != null) easyToggle.SetIsOnWithoutNotify  (responsiveness == ResponsivenessEasy);
        if (mediumToggle != null) mediumToggle.SetIsOnWithoutNotify(responsiveness == ResponsivenessMedium);
        if (hardToggle   != null) hardToggle.SetIsOnWithoutNotify  (responsiveness == ResponsivenessHard);

        // Advanced — show-developer-button toggle (Feature A). Default OFF.
        bool showDev = PlayerPrefs.GetInt("Settings_ShowDevButton", 0) == 1;
        if (showDevButtonToggle != null) showDevButtonToggle.SetIsOnWithoutNotify(showDev);

        // Privacy — allow-microphone toggle (Feature B). Default ON.
        bool micOn = PlayerPrefs.GetInt("Settings_MicEnabled", 1) == 1;
        if (allowMicToggle != null) allowMicToggle.SetIsOnWithoutNotify(micOn);
    }

    private void RefreshLocalizedText()
    {
        if (titleLabel               != null) titleLabel.text               = Localization.Get("settings_title");
        if (backButtonLabel          != null) backButtonLabel.text          = Localization.Get("settings_back");

        if (languageSectionLabel     != null) languageSectionLabel.text     = Localization.Get("settings_section_language");
        if (langEnglishLabel         != null) langEnglishLabel.text         = Localization.Get("settings_lang_english");
        if (langDutchLabel           != null) langDutchLabel.text           = Localization.Get("settings_lang_dutch");

        if (privacySectionLabel      != null) privacySectionLabel.text      = Localization.Get("settings_section_privacy");
        if (privacyStatusLabel       != null) privacyStatusLabel.text       = Localization.Get("settings_privacy_status_label");
        if (reviewConsentLabel       != null) reviewConsentLabel.text       = Localization.Get("settings_privacy_review_consent");
        if (privacyPolicyLabel       != null) privacyPolicyLabel.text       = Localization.Get("settings_privacy_policy_button");
        if (allowMicLabel            != null) allowMicLabel.text            = Localization.Get("settings_privacy_allow_mic");

        if (privacyPolicyTitleLabel  != null) privacyPolicyTitleLabel.text  = Localization.Get("privacy_policy_title");
        if (privacyPolicyBodyLabel   != null) privacyPolicyBodyLabel.text   = Localization.Get("privacy_policy_body");
        if (privacyPolicyBackLabel   != null) privacyPolicyBackLabel.text   = Localization.Get("privacy_policy_back");

        // Audience section (Task 2d)
        if (audienceSectionLabel     != null) audienceSectionLabel.text     = Localization.Get("settings_section_audience");
        if (responsivenessLabel      != null) responsivenessLabel.text      = Localization.Get("settings_responsiveness_label");
        if (easyToggleLabel          != null) easyToggleLabel.text          = Localization.Get("settings_responsiveness_easy");
        if (mediumToggleLabel        != null) mediumToggleLabel.text        = Localization.Get("settings_responsiveness_medium");
        if (hardToggleLabel          != null) hardToggleLabel.text          = Localization.Get("settings_responsiveness_hard");

        // Advanced section (Feature A)
        if (advancedSectionLabel     != null) advancedSectionLabel.text     = Localization.Get("settings_section_advanced");
        if (showDevButtonLabel       != null) showDevButtonLabel.text       = Localization.Get("settings_advanced_show_dev");
    }

    // ── Section: Language ─────────────────────────────────────────────────────

    private void OnLanguageSelected(string lang)
    {
        string current = PlayerPrefs.GetString("Language", Localization.LangEnglish);
        if (current == lang) return; // nothing to do

        PlayerPrefs.SetString("Language", lang);
        PlayerPrefs.Save();

        // Reload the dictionary against the new language. Localization.Load
        // reads the Language pref afresh and re-runs the JSON coroutine; we
        // then refresh our own labels once it finishes.
        Localization.Load(this);
        StartCoroutine(RefreshTextAfterLoad());
    }

    private IEnumerator RefreshTextAfterLoad()
    {
        // Localization.IsLoaded is set true at the end of the coroutine; we
        // bounce that flag false here so we can wait for the new load. Since
        // we can't access the private setter, just yield a few frames and
        // re-read; in practice the coroutine finishes within 1-2 frames on
        // desktop and a handful of frames on Android.
        float timeout = 2f;
        while (timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
            if (Localization.IsLoaded) break;
        }
        // Re-read labels regardless — by now the dictionary is either updated
        // or we've timed out and will fall back to keys.
        RefreshLocalizedText();
        RefreshPrivacyStatus();
        // Ask the main menu to refresh its own visible labels too.
        if (_mainMenu != null) _mainMenu.RefreshLocalizedText();
    }

    // ── Section: Privacy / data ───────────────────────────────────────────────

    private void OnReviewConsentPressed()
    {
        // Hand off to MainMenuController — it owns the consent screen.
        if (_mainMenu != null)
        {
            CloseSettingsPanel();
            _mainMenu.ShowConsentScreen(reviewingFromSettings: true);
        }
    }

    /// <summary>
    /// Persists the microphone master switch (Feature B). OFF means the next
    /// session runs silent — SpeechRecognizer starts no mic and makes no API
    /// calls, the HUD hides WPM/transcript and shows "Microphone off", and the
    /// audience drifts to Restless from the unbroken pause. Takes effect on the
    /// next session start (read once in SpeechRecognizer/HUD/Results), not mid-run.
    /// </summary>
    private void OnAllowMicToggled(bool isOn)
    {
        PlayerPrefs.SetInt("Settings_MicEnabled", isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OpenPrivacyPolicy()
    {
        if (privacyPolicyPanelRoot != null) privacyPolicyPanelRoot.SetActive(true);
    }

    private void ClosePrivacyPolicy()
    {
        if (privacyPolicyPanelRoot != null) privacyPolicyPanelRoot.SetActive(false);
    }

    // ── Section: Audience responsiveness (Task 2d) ────────────────────────────

    /// <summary>
    /// Persists the chosen responsiveness level. Picked up by AudienceRuleEngine,
    /// AudienceMember (and, when Task 4 lands, HeadTracker) at the next session
    /// start. The change does NOT apply to an already-running session — that's
    /// intentional, since the rule thresholds and per-avatar stagger windows
    /// are sampled once in HandleSessionStart / Awake respectively.
    /// </summary>
    private void OnResponsivenessSelected(string level)
    {
        string current = PlayerPrefs.GetString("Settings_Responsiveness", DefaultResponsiveness);
        if (current == level) return;
        PlayerPrefs.SetString("Settings_Responsiveness", level);
        PlayerPrefs.Save();
    }

    // ── Section: Advanced (Feature A — dev button visibility) ─────────────────

    /// <summary>
    /// Persists the "show developer button" preference and asks MainMenuController
    /// to re-apply the button's visibility immediately, so the change is visible
    /// the moment the user closes Settings (no app restart needed).
    /// </summary>
    private void OnShowDevButtonToggled(bool isOn)
    {
        PlayerPrefs.SetInt("Settings_ShowDevButton", isOn ? 1 : 0);
        PlayerPrefs.Save();
        if (_mainMenu != null) _mainMenu.RefreshDevButtonVisibility();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeLang(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return Localization.LangEnglish;
        return raw == Localization.LangDutch ? Localization.LangDutch : Localization.LangEnglish;
    }

    private static string NormalizeResponsiveness(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return DefaultResponsiveness;
        switch (raw.ToLowerInvariant())
        {
            case ResponsivenessEasy:   return ResponsivenessEasy;
            case ResponsivenessHard:   return ResponsivenessHard;
            case ResponsivenessMedium: return ResponsivenessMedium;
            default:                   return DefaultResponsiveness;
        }
    }
}
