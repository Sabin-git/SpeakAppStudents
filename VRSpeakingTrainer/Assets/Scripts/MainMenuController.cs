using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles MainMenu UI: duration slider (1–15 min), Start button, and Debug Mode toggle.
///
/// Debug Mode (PlayerPrefs key "DebugMode", 1 = on, 0 = off):
///   ON  — session duration is forced to 10 seconds; Results screen uses random dummy metrics.
///   OFF — slider value is used; Results screen uses real session metrics.
///
/// Saves chosen duration to PlayerPrefs before loading the Session scene.
/// Attach to the MainMenu Canvas (or any persistent GO in the MainMenu scene).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Slider — min 1, max 15, whole numbers only")]
    [SerializeField] private Slider durationSlider;
    [Tooltip("Label that shows the current slider value, e.g. '5 min'")]
    [SerializeField] private TextMeshProUGUI durationLabel;
    [Tooltip("Toggle for enabling debug mode (forces 10s session + dummy results)")]
    [SerializeField] private Toggle debugToggle;

    private const int DefaultMinutes = 5;

    private void Start()
    {
        if (durationSlider != null)
        {
            durationSlider.minValue     = 1;
            durationSlider.maxValue     = 15;
            durationSlider.wholeNumbers = true;
            durationSlider.value        = PlayerPrefs.GetFloat("SessionDurationSeconds", DefaultMinutes * 60f) / 60f;
            durationSlider.onValueChanged.AddListener(OnSliderChanged);
            UpdateLabel(durationSlider.value);
        }

        if (debugToggle != null)
        {
            // Only restore saved state if it was previously written.
            // On the very first run there is no saved value, so keep the
            // toggle's scene default instead of forcing it to off.
            if (PlayerPrefs.HasKey("DebugMode"))
                debugToggle.isOn = PlayerPrefs.GetInt("DebugMode") == 1;

            debugToggle.onValueChanged.AddListener(OnDebugToggleChanged);
        }
    }

    private void OnSliderChanged(float value) => UpdateLabel(value);

    private void OnDebugToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt("DebugMode", isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateLabel(float minutes)
    {
        if (durationLabel == null) return;
        durationLabel.text = $"{(int)minutes} min";
    }

    /// <summary>
    /// Called by the Start button's onClick event.
    /// Saves the selected duration (or 10s if debug mode is on) and loads the Session scene.
    /// </summary>
    public void OnStartPressed()
    {
        bool debugOn = debugToggle != null && debugToggle.isOn;

        float durationSeconds = debugOn
            ? 10f
            : (durationSlider != null ? durationSlider.value : DefaultMinutes) * 60f;

        PlayerPrefs.SetFloat("SessionDurationSeconds", durationSeconds);
        PlayerPrefs.SetInt  ("DebugMode", debugOn ? 1 : 0);
        PlayerPrefs.Save();

        SessionManager.LoadSession();
    }
}
