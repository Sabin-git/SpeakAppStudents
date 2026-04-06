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
///   Results_AvgWPM          float
///   Results_FillerCount     int
///   Results_SessionTime     float  (seconds)
///   Results_TimeOnAudience  float  (seconds)
///   Results_TimeOnLectern   float  (seconds)
///   Results_TimeOnOther     float  (seconds)
///
/// Score breakdown (0-100 each):
///   Speech score  — WPM proximity to ideal 110-160 range
///   Filler score  — filler-word rate per minute
///   Gaze score    — fraction of tracked time spent on audience
///   Overall       — weighted average (35% speech, 25% filler, 40% gaze)
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

        float avgWpm, duration, audience, lectern, other;
        int   fillers;

        // Session duration is always the real value regardless of debug mode.
        duration = PlayerPrefs.GetFloat("Results_SessionTime", 0f);

        if (debugMode)
        {
            avgWpm   = DebugWpm     [Random.Range(0, DebugWpm.Length)];
            fillers  = DebugFillers [Random.Range(0, DebugFillers.Length)];
            audience = DebugAudience[Random.Range(0, DebugAudience.Length)];
            lectern  = DebugLectern [Random.Range(0, DebugLectern.Length)];
            other    = DebugOther   [Random.Range(0, DebugOther.Length)];
        }
        else
        {
            avgWpm   = PlayerPrefs.GetFloat("Results_AvgWPM",        0f);
            fillers  = PlayerPrefs.GetInt  ("Results_FillerCount",    0);
            audience = PlayerPrefs.GetFloat("Results_TimeOnAudience", 0f);
            lectern  = PlayerPrefs.GetFloat("Results_TimeOnLectern",  0f);
            other    = PlayerPrefs.GetFloat("Results_TimeOnOther",    0f);
        }

        float speechScore = ComputeWpmScore(avgWpm);
        float fillerScore = ComputeFillerScore(fillers, duration);
        float gazeScore   = ComputeGazeScore(audience, lectern + other);
        int   overall     = Mathf.RoundToInt(speechScore * 0.35f + fillerScore * 0.25f + gazeScore * 0.40f);

        PopulateUI(overall, speechScore, fillerScore, gazeScore, duration);
    }

    // ── Score computations ─────────────────────────────────────────────────────

    private static float ComputeWpmScore(float avgWpm)
    {
        if (avgWpm <= 0f)                     return 0f;
        if (avgWpm >= 110f && avgWpm <= 160f) return 100f;
        if (avgWpm < 110f)                    return Mathf.InverseLerp(60f,  110f, avgWpm) * 100f;
        return                                       Mathf.InverseLerp(220f, 160f, avgWpm) * 100f;
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
        if (score >= 85) return "A";
        if (score >= 70) return "B";
        if (score >= 55) return "C";
        if (score >= 40) return "D";
        return "F";
    }

    private static string ToCaption(int score)
    {
        if (score >= 85) return "Excellent!";
        if (score >= 70) return "Good Job!";
        if (score >= 55) return "Keep Practicing!";
        if (score >= 40) return "Needs Work";
        return "Try Again";
    }

    private static string FormatTime(float seconds)
    {
        int m = (int)seconds / 60;
        int s = (int)seconds % 60;
        return $"Session: {m}:{s:D2}";
    }

    // ── Button callback ────────────────────────────────────────────────────────

    /// <summary>Called by the Back to Menu button onClick.</summary>
    public void OnBackPressed() => SceneManager.LoadScene("MainMenu");
}
