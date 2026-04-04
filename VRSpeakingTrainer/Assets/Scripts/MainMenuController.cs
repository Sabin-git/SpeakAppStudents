using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles MainMenu UI: duration slider (1–15 min) and Start button.
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

    private const int DefaultMinutes = 5;

    private void Start()
    {
        if (durationSlider == null) return;

        durationSlider.minValue    = 1;
        durationSlider.maxValue    = 15;
        durationSlider.wholeNumbers = true;
        durationSlider.value       = PlayerPrefs.GetFloat("SessionDurationSeconds", DefaultMinutes * 60f) / 60f;

        durationSlider.onValueChanged.AddListener(OnSliderChanged);
        UpdateLabel(durationSlider.value);
    }

    private void OnSliderChanged(float value) => UpdateLabel(value);

    private void UpdateLabel(float minutes)
    {
        if (durationLabel != null)
            durationLabel.text = $"{(int)minutes} min";
    }

    /// <summary>
    /// Called by the Start button's onClick event.
    /// Saves the selected duration and loads the Session scene.
    /// </summary>
    public void OnStartPressed()
    {
        float minutes = durationSlider != null ? durationSlider.value : DefaultMinutes;
        PlayerPrefs.SetFloat("SessionDurationSeconds", minutes * 60f);
        PlayerPrefs.Save();
        SessionManager.LoadSession();
    }
}
