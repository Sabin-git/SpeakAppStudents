using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reads end-of-session metrics from PlayerPrefs, computes scored breakdown,
/// and populates the Results screen UI.
///
/// In Debug Mode (PlayerPrefs "DebugMode" == 1), all metric values are randomly
/// chosen from hardcoded pools so the UI can be tested without a real session.
///
/// Keys read (written by SessionManager + HeadTracker before scene transition):
///   Results_AvgWPM             float
///   Results_FillerCount        int
///   Results_SessionTime        float  (seconds)
///   Results_TimeOnAudience     float  (seconds)
///   Results_TimeOnLectern      float  (seconds)
///   Results_TimeOnOther        float  (seconds)
///   Results_TimeOnAvatar_0..9  float  (seconds — Task 4 heat map)
///
/// Score breakdown (0-100 each):
///   Speech score  — WPM proximity to ideal 110-160 range
///   Filler score  — filler-word rate per minute
///   Gaze score    — fraction of tracked time spent on audience
///   Overall       — weighted average (35% speech, 25% filler, 40% gaze)
///
/// Task 4 — gaze heat map widget:
///   A 2×5 grid of "seat" Images, one per avatar, coloured by Results_TimeOnAvatar_*.
///   Below the grid: a single TMP line with the top-3 most-looked-at avatars.
///   Wiring is optional; if gazeHeatMapRoot or gazeHeatSeats[0] is null, the
///   widget is skipped silently (with one Debug.Log for dev visibility).
/// </summary>
public class ResultsUI : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private TextMeshProUGUI overallScoreText;
    [SerializeField] private TextMeshProUGUI gradeText;
    [SerializeField] private TextMeshProUGUI captionText;
    [SerializeField] private TextMeshProUGUI sessionTimeText;

    [Header("Speech Row")]
    [SerializeField] private Image           speechBar;
    [SerializeField] private TextMeshProUGUI speechPct;

    [Header("Gaze Row")]
    [SerializeField] private Image           gazeBar;
    [SerializeField] private TextMeshProUGUI gazePct;

    [Header("Pacing Row")]
    [SerializeField] private Image           pacingBar;
    [SerializeField] private TextMeshProUGUI pacingPct;

    [Header("Raw Metrics (optional — mirrors SpeechAnalyzer's end-of-session log)")]
    [SerializeField] private TextMeshProUGUI wordsText;
    [SerializeField] private TextMeshProUGUI fillersText;
    [SerializeField] private TextMeshProUGUI wpmText;

    [Header("Gaze Heat Map (Task 4 — opens as a modal overlay from the Results screen)")]
    [Tooltip("Button on the main Results screen that opens the heat map overlay. " +
             "Wired in Start() to call OpenHeatMapOverlay. Optional — if null, the heat " +
             "map is unreachable from the UI but the data is still computed.")]
    [SerializeField] private Button gazeHeatOpenButton;
    [Tooltip("Label TMP on the open button — localized to 'Where you looked' / " +
             "'Waar je keek'. Set from results_heatmap_label.")]
    [SerializeField] private TextMeshProUGUI gazeHeatOpenButtonLabel;
    [Tooltip("Container GameObject for the heat map modal overlay (Backdrop + Card). " +
             "Toggled off at Start(); opens via gazeHeatOpenButton, closes via " +
             "gazeHeatCloseButton. Heat map cells are populated at Start() so data " +
             "is ready by the time the user opens the overlay.")]
    [SerializeField] private GameObject gazeHeatMapRoot;
    [Tooltip("Close button inside the heat map overlay's Card. Hides the overlay.")]
    [SerializeField] private Button gazeHeatCloseButton;
    [Tooltip("Label TMP on the close button. Localized from settings_back.")]
    [SerializeField] private TextMeshProUGUI gazeHeatCloseButtonLabel;
    [Tooltip("10 seat Images, indexed by avatarIndex. Seat[i] must correspond to " +
             "Desk_NN's avatarIndex == i so the 2×5 grid matches the audience layout.")]
    [SerializeField] private Image[]  gazeHeatSeats = new Image[10];
    [Tooltip("Top-3 most-looked-at avatars line. Format string lives in Localization key " +
             "results_heatmap_top3_format (e.g. 'Most-looked-at avatars: {0}').")]
    [SerializeField] private TextMeshProUGUI gazeHeatTop3;
    [Tooltip("Heat-map modal — time spent looking at the audience. Format: " +
             "results_heatmap_audience_format (e.g. 'Looking at audience: {0}'). Optional.")]
    [SerializeField] private TextMeshProUGUI gazeHeatAudienceTime;
    [Tooltip("Heat-map modal — time spent looking at the lectern. Format: " +
             "results_heatmap_lectern_format. Optional.")]
    [SerializeField] private TextMeshProUGUI gazeHeatLecternTime;
    [Tooltip("Heat-map modal — time spent looking elsewhere. Format: " +
             "results_heatmap_other_format. Optional.")]
    [SerializeField] private TextMeshProUGUI gazeHeatOtherTime;

    // ── Debug value pools ──────────────────────────────────────────────────────
    // Each pool covers a range of realistic values — good, average, and poor —
    // so the UI bars visibly move around on repeated runs in debug mode.

    private static readonly float[] DebugWpm = {
        55f, 75f, 88f, 95f, 105f, 120f, 130f, 145f, 155f, 170f, 185f, 200f, 215f, 100f, 140f
    };
    private static readonly int[] DebugFillers = {
        0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 2, 4, 6
    };
    private static readonly float[] DebugAudience = {
        5f, 10f, 15f, 20f, 25f, 35f, 45f, 55f, 65f, 75f, 80f, 30f, 40f, 60f, 70f
    };
    private static readonly float[] DebugLectern = {
        3f, 5f, 8f, 10f, 12f, 15f, 18f, 20f, 25f, 7f, 14f, 22f, 6f, 16f, 9f
    };
    private static readonly float[] DebugOther = {
        1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 12f, 15f, 3f, 6f, 4f
    };

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        bool debugMode = PlayerPrefs.GetInt("DebugMode", 0) == 1;
        // Read language once at Start so the WPM band and all UI strings stay
        // consistent for the duration of the Results screen, even if the user
        // somehow changes the Language pref while looking at it.
        bool dutch     = PlayerPrefs.GetString("Language", Localization.LangEnglish) == Localization.LangDutch;

        float avgWpm, duration, audience, lectern, other;
        int   fillers, words;

        // Session duration is always the real value regardless of debug mode.
        duration = PlayerPrefs.GetFloat("Results_SessionTime", 0f);

        if (debugMode)
        {
            avgWpm   = DebugWpm     [Random.Range(0, DebugWpm.Length)];
            fillers  = DebugFillers [Random.Range(0, DebugFillers.Length)];
            audience = DebugAudience[Random.Range(0, DebugAudience.Length)];
            lectern  = DebugLectern [Random.Range(0, DebugLectern.Length)];
            other    = DebugOther   [Random.Range(0, DebugOther.Length)];
            // No words pool — derive from the random WPM so the three raw
            // values stay internally consistent (words ≈ wpm × minutes).
            words    = Mathf.RoundToInt(avgWpm * duration / 60f);
        }
        else
        {
            avgWpm   = PlayerPrefs.GetFloat("Results_AvgWPM",        0f);
            fillers  = PlayerPrefs.GetInt  ("Results_FillerCount",    0);
            words    = PlayerPrefs.GetInt  ("Results_TotalWords",     0);
            audience = PlayerPrefs.GetFloat("Results_TimeOnAudience", 0f);
            lectern  = PlayerPrefs.GetFloat("Results_TimeOnLectern",  0f);
            other    = PlayerPrefs.GetFloat("Results_TimeOnOther",    0f);
        }

        float speechScore = ComputeWpmScore(avgWpm, dutch);
        float fillerScore = ComputeFillerScore(fillers, duration);
        float gazeScore   = ComputeGazeScore(audience, lectern + other);
        int   overall     = Mathf.RoundToInt(speechScore * 0.35f + fillerScore * 0.25f + gazeScore * 0.40f);

        PopulateUI(overall, speechScore, fillerScore, gazeScore, duration);

        // Feature B — in silent mode (mic disabled) the speech metrics are all
        // zero, so hide the raw Words/Fillers/WPM labels rather than showing a
        // misleading "0 / 0 / 0". Debug mode still shows its random pool values
        // (debug intent wins). The speech/pacing score bars stay as computed so
        // the overall score honestly reflects the silent run.
        bool micEnabled = PlayerPrefs.GetInt("Settings_MicEnabled", 1) == 1;
        if (!micEnabled && !debugMode)
            HideRawMetrics();
        else
            PopulateRawMetrics(words, fillers, avgWpm);

        PopulateGazeHeatMap(audience, lectern, other);
    }

    private void HideRawMetrics()
    {
        if (wordsText   != null) wordsText.gameObject.SetActive(false);
        if (fillersText != null) fillersText.gameObject.SetActive(false);
        if (wpmText     != null) wpmText.gameObject.SetActive(false);
    }

    private void PopulateRawMetrics(int words, int fillers, float avgWpm)
    {
        if (wordsText   != null) wordsText.text   = Format(Localization.Get("results_words_format"),   words);
        if (fillersText != null) fillersText.text = Format(Localization.Get("results_fillers_format"), fillers);
        if (wpmText     != null) wpmText.text     = Format(Localization.Get("results_wpm_format"),     Mathf.RoundToInt(avgWpm));
    }

    // Defensive wrapper around string.Format — falls back to a "label: value"
    // composition if the localized template is missing or malformed. Keeps
    // the UI usable even when a translation key has been forgotten.
    private static string Format(string template, object arg)
    {
        if (string.IsNullOrEmpty(template)) return arg != null ? arg.ToString() : string.Empty;
        try   { return string.Format(template, arg); }
        catch { return $"{template} {arg}"; }
    }

    // ── Task 4 — gaze heat map ────────────────────────────────────────────────
    // Reads the 10 Results_TimeOnAvatar_* prefs once and paints the 10 seat
    // Images plus the top-3 text line. Skipped silently (with one log) when
    // wiring is missing, so a half-wired Editor scene doesn't NRE.

    private static readonly Color HeatColorZero    = Hex("#3A3A4E");
    private static readonly Color HeatColorNeutral = Hex("#A8A8B8");
    private static readonly Color HeatColorWarm    = Hex("#D89870");
    private static readonly Color HeatColorHot     = Hex("#C9504C");

    private void PopulateGazeHeatMap(float audienceSeconds, float lecternSeconds, float otherSeconds)
    {
        // Wire open / close buttons. The heat map is now a modal overlay
        // (Backdrop + Card pattern), opened from a button on the main Results
        // screen rather than rendered inline. Data is populated below so the
        // grid is ready by the time the user opens the overlay.
        if (gazeHeatOpenButton != null)
        {
            gazeHeatOpenButton.onClick.RemoveAllListeners();
            gazeHeatOpenButton.onClick.AddListener(OpenHeatMapOverlay);
        }
        if (gazeHeatCloseButton != null)
        {
            gazeHeatCloseButton.onClick.RemoveAllListeners();
            gazeHeatCloseButton.onClick.AddListener(CloseHeatMapOverlay);
        }

        // Localized labels for the open + close buttons.
        if (gazeHeatOpenButtonLabel != null)
            gazeHeatOpenButtonLabel.text = Localization.Get("results_heatmap_label");
        if (gazeHeatCloseButtonLabel != null)
            gazeHeatCloseButtonLabel.text = Localization.Get("settings_back");

        // Overlay starts hidden; user reveals it via the open button.
        if (gazeHeatMapRoot != null) gazeHeatMapRoot.SetActive(false);

        if (gazeHeatSeats == null || gazeHeatSeats.Length == 0 || gazeHeatSeats[0] == null)
        {
            // Wiring not present — keep the rest of the Results screen
            // working and just log once so devs notice during play-testing.
            Debug.Log("[ResultsUI] Gaze heat map seats not wired — skipping. " +
                      "Wire gazeHeatMapRoot (overlay) + gazeHeatSeats[10] + gazeHeatTop3 + " +
                      "gazeHeatOpenButton + gazeHeatCloseButton in the Inspector.");
            // Hide the open button if its target overlay isn't wired so users
            // don't click a button that opens an empty overlay.
            if (gazeHeatOpenButton != null) gazeHeatOpenButton.gameObject.SetActive(false);
            return;
        }

        // Read the 10 per-avatar prefs once and compute the average over
        // non-zero values. If every avatar is zero (no gaze data this session)
        // every seat goes to the cool-gray colour and the top-3 line shows
        // nothing — but the widget is still visible so the user knows it exists.
        const int N = 10;
        float[] times = new float[N];
        float   sum   = 0f;
        int     nonZeroCount = 0;
        for (int i = 0; i < N; i++)
        {
            times[i] = PlayerPrefs.GetFloat($"Results_TimeOnAvatar_{i}", 0f);
            if (times[i] > 0f) { sum += times[i]; nonZeroCount++; }
        }
        float avg = nonZeroCount > 0 ? sum / nonZeroCount : 0f;

        // Top-3 indices by gaze time (descending). Simple O(N²) — N is 10.
        // Used both to flag the top-1/top-2 seats for the "hot" red colour
        // and to render the text line below the grid.
        int[] topIndices = new int[3] { -1, -1, -1 };
        bool[] taken = new bool[N];
        for (int slot = 0; slot < 3; slot++)
        {
            int bestIdx   = -1;
            float bestVal = 0f;
            for (int i = 0; i < N; i++)
            {
                if (taken[i] || times[i] <= 0f) continue;
                if (times[i] > bestVal) { bestVal = times[i]; bestIdx = i; }
            }
            if (bestIdx < 0) break;
            topIndices[slot] = bestIdx;
            taken[bestIdx] = true;
        }

        // Colour each seat. The bounds check on gazeHeatSeats stops the
        // loop early if the user wired fewer than 10 seats (e.g. mistake).
        int seatCount = Mathf.Min(N, gazeHeatSeats.Length);
        for (int i = 0; i < seatCount; i++)
        {
            if (gazeHeatSeats[i] == null) continue;
            gazeHeatSeats[i].color = ColorForTime(times[i], avg, IsTop2(i, topIndices));
        }

        // Render the top-3 line. Skip silently if no non-zero values exist
        // (e.g. the heatmap label still shows but the top-3 line stays blank).
        if (gazeHeatTop3 != null)
        {
            string body = BuildTop3Body(topIndices, times);
            if (string.IsNullOrEmpty(body))
            {
                gazeHeatTop3.text = string.Empty;
            }
            else
            {
                string template = Localization.Get("results_heatmap_top3_format");
                try   { gazeHeatTop3.text = string.Format(template, body); }
                catch { gazeHeatTop3.text = $"{template} {body}"; }
            }
        }

        // Per-zone gaze totals (audience / lectern / other). Same M:SS time
        // formatting as the session-time line so all durations on the Results
        // screen read identically. Each label is independently optional —
        // wire only the ones you want shown.
        if (gazeHeatAudienceTime != null)
            gazeHeatAudienceTime.text = Format(Localization.Get("results_heatmap_audience_format"), FormatTime(audienceSeconds));
        if (gazeHeatLecternTime != null)
            gazeHeatLecternTime.text  = Format(Localization.Get("results_heatmap_lectern_format"),  FormatTime(lecternSeconds));
        if (gazeHeatOtherTime != null)
            gazeHeatOtherTime.text    = Format(Localization.Get("results_heatmap_other_format"),    FormatTime(otherSeconds));
    }

    // ── Heat map overlay open/close ────────────────────────────────────────────

    /// <summary>Called by the gazeHeatOpenButton onClick. Shows the heat map modal.</summary>
    public void OpenHeatMapOverlay()
    {
        if (gazeHeatMapRoot != null) gazeHeatMapRoot.SetActive(true);
    }

    /// <summary>Called by the gazeHeatCloseButton onClick. Hides the heat map modal.</summary>
    public void CloseHeatMapOverlay()
    {
        if (gazeHeatMapRoot != null) gazeHeatMapRoot.SetActive(false);
    }

    private static bool IsTop2(int avatarIndex, int[] topIndices)
        => avatarIndex == topIndices[0] || avatarIndex == topIndices[1];

    private static Color ColorForTime(float t, float avg, bool isTop2)
    {
        // 0s → cool gray
        if (t <= 0f) return HeatColorZero;

        // Top-1/top-2 above 1.5×avg → deep red
        if (isTop2 && t >= avg * 1.5f) return HeatColorHot;

        // 0 < t < avg → blue-gray to neutral
        if (t < avg)
        {
            float k = avg > 0f ? Mathf.InverseLerp(0f, avg, t) : 0f;
            return Color.Lerp(HeatColorZero, HeatColorNeutral, k);
        }

        // avg < t < 1.5×avg → neutral to warm orange
        if (t < avg * 1.5f)
        {
            float k = Mathf.InverseLerp(avg, avg * 1.5f, t);
            return Color.Lerp(HeatColorNeutral, HeatColorWarm, k);
        }

        // t >= 1.5×avg but not in top-2 → stay warm orange (not promoted to red)
        return HeatColorWarm;
    }

    private static string BuildTop3Body(int[] topIndices, float[] times)
    {
        // Compose e.g. "Avatar #4: 12s · Avatar #7: 9s · Avatar #1: 4s".
        // Avatar labels are 1-indexed for human readability (matches the
        // hierarchy's "Desk_00" → "Avatar #1" convention researchers expect).
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < topIndices.Length; i++)
        {
            int idx = topIndices[i];
            if (idx < 0) break;
            if (sb.Length > 0) sb.Append(" · "); // middot
            sb.Append($"Avatar #{idx + 1}: {Mathf.RoundToInt(times[idx])}s");
        }
        return sb.ToString();
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }

    // ── Score computations ─────────────────────────────────────────────────────

    // WPM score curve scales with language. The Dutch band is shifted down
    // to match conversational pacing in Dutch (90–140 vs English 110–160);
    // the lower/upper falloff endpoints shift by the same amount so the
    // shape of the curve is preserved.
    private static float ComputeWpmScore(float avgWpm, bool dutch)
    {
        float lowIdeal  = dutch ?  90f : 110f;
        float highIdeal = dutch ? 140f : 160f;
        float lowFloor  = dutch ?  40f :  60f;   // score 0 below this
        float highFloor = dutch ? 200f : 220f;   // score 0 above this

        if (avgWpm <= 0f)                                return 0f;
        if (avgWpm >= lowIdeal && avgWpm <= highIdeal)   return 100f;
        if (avgWpm <  lowIdeal)                          return Mathf.InverseLerp(lowFloor,  lowIdeal,  avgWpm) * 100f;
        return                                                  Mathf.InverseLerp(highFloor, highIdeal, avgWpm) * 100f;
    }

    private static float ComputeFillerScore(int fillers, float durationSeconds)
    {
        if (durationSeconds <= 0f) return 100f;
        float perMinute = fillers / (durationSeconds / 60f);
        return Mathf.Clamp01(1f - perMinute / 4f) * 100f;
    }

    private static float ComputeGazeScore(float audienceSeconds, float otherSeconds)
    {
        float total = audienceSeconds + otherSeconds;
        if (total <= 0f) return 0f;
        float fraction = audienceSeconds / total;
        return Mathf.Clamp01(fraction / 0.7f) * 100f;
    }

    // ── UI population ──────────────────────────────────────────────────────────

    private void PopulateUI(int overall, float speech, float filler, float gaze, float durationSeconds)
    {
        if (overallScoreText != null) overallScoreText.text = overall.ToString();
        if (gradeText        != null) gradeText.text        = ToGrade(overall);
        if (captionText      != null) captionText.text      = ToCaption(overall);
        if (sessionTimeText  != null) sessionTimeText.text  = FormatTime(durationSeconds);

        SetBar(speechBar, speechPct, speech);
        SetBar(gazeBar,   gazePct,   gaze);
        SetBar(pacingBar, pacingPct, filler);
    }

    private static void SetBar(Image bar, TextMeshProUGUI label, float score)
    {
        if (bar   != null) bar.fillAmount = Mathf.Clamp01(score / 100f);
        if (label != null) label.text     = $"{Mathf.RoundToInt(score)}%";
    }

    private static string ToGrade(int score)
    {
        if (score >= 85) return Localization.Get("results_grade_a");
        if (score >= 70) return Localization.Get("results_grade_b");
        if (score >= 55) return Localization.Get("results_grade_c");
        if (score >= 40) return Localization.Get("results_grade_d");
        return                  Localization.Get("results_grade_f");
    }

    private static string ToCaption(int score)
    {
        if (score >= 85) return Localization.Get("results_caption_excellent");
        if (score >= 70) return Localization.Get("results_caption_good");
        if (score >= 55) return Localization.Get("results_caption_keep");
        if (score >= 40) return Localization.Get("results_caption_needs");
        return                  Localization.Get("results_caption_try");
    }

    private static string FormatTime(float seconds)
    {
        int m = (int)seconds / 60;
        int s = (int)seconds % 60;
        string template = Localization.Get("results_session_format");
        try   { return string.Format(template, m, s.ToString("D2")); }
        catch { return $"{m}:{s:D2}"; }
    }

    // ── Button callback ────────────────────────────────────────────────────────

    /// <summary>Called by the Back to Menu button onClick.</summary>
    public void OnBackPressed() => SceneManager.LoadScene("MainMenu");
}
