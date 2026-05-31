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
///                     (opens the in-app text sub-panel).
///   3) VR comfort   — brightness slider (0–1) + vignetting toggle.
///
/// The developer panel is unrelated and is NOT touched by this controller.
///
/// PlayerPrefs touched here:
///   Language           — string ("en" | "nl"), default "en" (shared with Localization)
///   Settings_Brightness— float 0..1,            default 1.0
///   Settings_Vignetting— int 0/1,               default 0
///   Consent_Granted    — int 0/1,               default 0 (READ here, WRITTEN by MainMenuController)
///
/// Brightness is applied at runtime through a full-screen black overlay Image
/// whose alpha = (1 - brightness). The overlay GameObject is wired in the
/// Inspector (overlay lives on the MainMenu canvas and persists across the
/// session scene via DontDestroyOnLoad — see WIRING_TASK_2.md).
///
/// Vignetting is applied at runtime through a separate full-screen Image whose
/// alpha is driven by head angular velocity (degrees per second). When the
/// toggle is OFF, the overlay is hidden. When ON, the overlay's alpha fades in
/// above ~60 deg/s of head rotation and fades back out below it.
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

    [Header("Privacy policy sub-panel")]
    [SerializeField] private TextMeshProUGUI privacyPolicyTitleLabel;
    [SerializeField] private TextMeshProUGUI privacyPolicyBodyLabel;
    [SerializeField] private Button          privacyPolicyBackButton;
    [SerializeField] private TextMeshProUGUI privacyPolicyBackLabel;

    [Header("VR comfort section")]
    [SerializeField] private TextMeshProUGUI comfortSectionLabel;
    [SerializeField] private TextMeshProUGUI brightnessLabel;
    [SerializeField] private Slider          brightnessSlider;        // 0..1
    [SerializeField] private TextMeshProUGUI vignettingLabel;
    [SerializeField] private Toggle          vignettingToggle;

    [Header("Runtime overlays (wired once, persist across scenes)")]
    [Tooltip("Full-screen black Image. Its alpha is set to (1 - brightness). Parent canvas should have a very high sortingOrder so it covers everything.")]
    [SerializeField] private Image           brightnessOverlay;
    [Tooltip("Full-screen vignette Image (radial-alpha texture). Its alpha is driven by head angular velocity while the toggle is ON.")]
    [SerializeField] private Image           vignetteOverlay;
    [Tooltip("Camera whose rotation we sample to compute head angular velocity. Leave empty to auto-find Camera.main each scene.")]
    [SerializeField] private Transform       headTransform;

    [Header("Vignetting tuning")]
    [Tooltip("Head angular velocity (deg/s) at which vignette starts fading IN.")]
    [SerializeField] private float vignetteOnThreshold  = 60f;
    [Tooltip("Head angular velocity (deg/s) at which vignette reaches full alpha.")]
    [SerializeField] private float vignetteFullThreshold = 180f;
    [Tooltip("Maximum alpha for the vignette overlay (1 = fully opaque edges).")]
    [SerializeField, Range(0f, 1f)] private float vignetteMaxAlpha = 0.85f;
    [Tooltip("How fast the vignette alpha lerps toward its target (per second).")]
    [SerializeField] private float vignetteFadeSpeed     = 6f;

    // Hook back into MainMenuController for the "Review consent" button so we
    // don't duplicate the consent UI here. Assigned by MainMenuController in
    // its Start() through SetMainMenu().
    private MainMenuController _mainMenu;

    // Runtime state for vignette
    private Quaternion _lastHeadRot;
    private bool       _haveLastHeadRot;
    private float      _vignetteAlpha; // currently displayed alpha

    private const float DefaultBrightness = 1f;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        // The brightness and vignette overlays MUST persist after MainMenu so
        // they continue affecting the Session scene. Their parent canvas
        // should already be set to DontDestroyOnLoad in the scene (see
        // WIRING_TASK_2.md). We don't do that here because it would be a
        // duplicate if the user already set it up.

        ApplyInitialOverlayState();
    }

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

    private void Update()
    {
        UpdateVignetteFromHeadMotion();
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

        if (privacyPolicyBackButton != null)
        {
            privacyPolicyBackButton.onClick.RemoveAllListeners();
            privacyPolicyBackButton.onClick.AddListener(ClosePrivacyPolicy);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.minValue = 0f;
            brightnessSlider.maxValue = 1f;
            brightnessSlider.onValueChanged.RemoveAllListeners();
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }

        if (vignettingToggle != null)
        {
            vignettingToggle.onValueChanged.RemoveAllListeners();
            vignettingToggle.onValueChanged.AddListener(OnVignettingToggled);
        }
    }

    private void RefreshFromPlayerPrefs()
    {
        // Language radio
        string lang = NormalizeLang(PlayerPrefs.GetString("Language", Localization.LangEnglish));
        if (langEnglishToggle != null) langEnglishToggle.SetIsOnWithoutNotify(lang == Localization.LangEnglish);
        if (langDutchToggle   != null) langDutchToggle.SetIsOnWithoutNotify  (lang == Localization.LangDutch);

        // Brightness
        float brightness = PlayerPrefs.GetFloat("Settings_Brightness", DefaultBrightness);
        brightness = Mathf.Clamp01(brightness);
        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(brightness);
        ApplyBrightness(brightness);

        // Vignetting toggle
        bool vignettingOn = PlayerPrefs.GetInt("Settings_Vignetting", 0) == 1;
        if (vignettingToggle != null) vignettingToggle.SetIsOnWithoutNotify(vignettingOn);
        ApplyVignetteEnabled(vignettingOn);
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

        if (privacyPolicyTitleLabel  != null) privacyPolicyTitleLabel.text  = Localization.Get("privacy_policy_title");
        if (privacyPolicyBodyLabel   != null) privacyPolicyBodyLabel.text   = Localization.Get("privacy_policy_body");
        if (privacyPolicyBackLabel   != null) privacyPolicyBackLabel.text   = Localization.Get("privacy_policy_back");

        if (comfortSectionLabel      != null) comfortSectionLabel.text      = Localization.Get("settings_section_comfort");
        if (brightnessLabel          != null) brightnessLabel.text          = Localization.Get("settings_comfort_brightness");
        if (vignettingLabel          != null) vignettingLabel.text          = Localization.Get("settings_comfort_vignetting");
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

    private void OpenPrivacyPolicy()
    {
        if (privacyPolicyPanelRoot != null) privacyPolicyPanelRoot.SetActive(true);
    }

    private void ClosePrivacyPolicy()
    {
        if (privacyPolicyPanelRoot != null) privacyPolicyPanelRoot.SetActive(false);
    }

    // ── Section: VR comfort ───────────────────────────────────────────────────

    public void OnBrightnessChanged(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("Settings_Brightness", value);
        PlayerPrefs.Save();
        ApplyBrightness(value);
    }

    public void OnVignettingToggled(bool isOn)
    {
        PlayerPrefs.SetInt("Settings_Vignetting", isOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVignetteEnabled(isOn);
    }

    // ── Overlay application ───────────────────────────────────────────────────

    private void ApplyInitialOverlayState()
    {
        float brightness = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_Brightness", DefaultBrightness));
        ApplyBrightness(brightness);

        bool vignettingOn = PlayerPrefs.GetInt("Settings_Vignetting", 0) == 1;
        ApplyVignetteEnabled(vignettingOn);
    }

    private void ApplyBrightness(float brightness)
    {
        if (brightnessOverlay == null) return;
        var c = brightnessOverlay.color;
        c.r = 0f; c.g = 0f; c.b = 0f;
        c.a = 1f - Mathf.Clamp01(brightness);
        brightnessOverlay.color = c;
        // Avoid blocking clicks behind the overlay.
        brightnessOverlay.raycastTarget = false;
        // Hide the GameObject entirely when fully transparent to save fillrate.
        if (brightnessOverlay.gameObject.activeSelf != (c.a > 0.001f))
            brightnessOverlay.gameObject.SetActive(c.a > 0.001f);
    }

    private void ApplyVignetteEnabled(bool enabled)
    {
        if (vignetteOverlay == null) return;
        vignetteOverlay.raycastTarget = false;
        if (!enabled)
        {
            _vignetteAlpha = 0f;
            var c = vignetteOverlay.color;
            c.a = 0f;
            vignetteOverlay.color = c;
            vignetteOverlay.gameObject.SetActive(false);
        }
        else
        {
            vignetteOverlay.gameObject.SetActive(true);
        }
    }

    private void UpdateVignetteFromHeadMotion()
    {
        if (vignetteOverlay == null) return;
        if (!vignetteOverlay.gameObject.activeSelf) return;
        bool toggleOn = PlayerPrefs.GetInt("Settings_Vignetting", 0) == 1;
        if (!toggleOn) return;

        Transform head = headTransform != null ? headTransform : (Camera.main != null ? Camera.main.transform : null);
        if (head == null) return;

        Quaternion currentRot = head.rotation;
        float angularVel = 0f;
        if (_haveLastHeadRot && Time.deltaTime > 0f)
        {
            float deltaAngle = Quaternion.Angle(_lastHeadRot, currentRot);
            angularVel = deltaAngle / Time.deltaTime;
        }
        _lastHeadRot     = currentRot;
        _haveLastHeadRot = true;

        // Map angular velocity to a 0..1 strength.
        float t = Mathf.InverseLerp(vignetteOnThreshold, vignetteFullThreshold, angularVel);
        float targetAlpha = Mathf.Clamp01(t) * vignetteMaxAlpha;
        _vignetteAlpha = Mathf.MoveTowards(_vignetteAlpha, targetAlpha, vignetteFadeSpeed * Time.unscaledDeltaTime);

        var c = vignetteOverlay.color;
        c.a = _vignetteAlpha;
        vignetteOverlay.color = c;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeLang(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return Localization.LangEnglish;
        return raw == Localization.LangDutch ? Localization.LangDutch : Localization.LangEnglish;
    }
}
