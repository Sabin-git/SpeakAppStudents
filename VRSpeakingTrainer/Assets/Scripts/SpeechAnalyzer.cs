using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Consumes SpeechRecognizer transcription events and computes speech metrics:
/// WPM (rolling 10s window), session-average WPM, filler word count (last 30s),
/// and current pause duration. Emits OnMetricsUpdate every 2 seconds for the
/// live HUD and the audience rule engine.
///
/// At session end, recounts words and fillers from the full concatenated
/// transcript and writes Results_AvgWPM and Results_FillerCount to PlayerPrefs.
/// Recounting from the complete text (instead of summing per-chunk values)
/// captures words and multi-word fillers like "you know" that may have been
/// split across 5-second chunk boundaries by SpeechRecognizer.
/// </summary>
public class SpeechAnalyzer : MonoBehaviour
{
    public static event Action<SpeechMetrics> OnMetricsUpdate;

    [Header("Settings")]
    [SerializeField] private float emitInterval   = 2f;
    [Tooltip("Seconds of recent chunks averaged into the live HUD WPM. Larger = smoother, slower to react. Tuned to 12s to balance noise from 3s chunks.")]
    [SerializeField] private float wpmWindow      = 12f;
    [SerializeField] private float fillerWindow   = 30f;  // seconds for filler count
    [SerializeField] private float pauseThreshold = 1.5f; // seconds before gap is a pause
    [Tooltip("EMA smoothing factor for WPM display (0=no change, 1=instant). 0.4 = moderate damping. Combine with wpmWindow=12 for smooth ~3–4s lag.")]
    [SerializeField] [Range(0f, 1f)] private float wpmSmoothing = 0.4f;

    // English filler set. Listed longer-multi-word entries first so the
    // CountFillers loop matches them before single-token substrings (CountFillers
    // iterates left-to-right, but ordering still helps human readers).
    private static readonly string[] FillerWordsEn =
    {
        "you know", "um", "uh", "like", "basically", "literally", "so", "right"
    };

    // Dutch filler set — locked by the user-testing plan (see Task 1 decisions).
    // Multi-word entries ("zeg maar", "soort van") are matched as substrings
    // by CountFillers with word-boundary checks on each side.
    private static readonly string[] FillerWordsNl =
    {
        "zeg maar", "soort van", "weet je", "eigenlijk",
        "ehm", "uhm", "ofzo", "dus", "nou", "eh"
    };

    // Selected at session start from the Language PlayerPref. Reused for both
    // the live-rolling-window filler count and the end-of-session recount.
    // Never re-read mid-session.
    private string[] _fillerWords = FillerWordsEn;

    // Each entry: (Time.time when transcript arrived, word count in that transcript)
    private readonly List<(float t, int words)> _wordLog    = new();
    // Each entry: (Time.time when transcript arrived, filler count in that transcript)
    private readonly List<(float t, int fillers)> _fillerLog = new();
    // Accumulates every chunk's transcript text so final metrics can be
    // recomputed from the complete session text at OnSessionEnd.
    private readonly StringBuilder _fullTranscript = new();

    private float _sessionStartTime;
    private float _lastTranscriptTime;
    private bool  _isRunning;
    private float _emitTimer;
    private int   _totalWords;
    private float _smoothedWpm;  // EMA-smoothed WPM for stable display

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        SessionManager.OnSessionStart          += HandleSessionStart;
        SessionManager.OnSessionEnd            += HandleSessionEnd;
        SpeechRecognizer.OnTranscriptionResult += HandleTranscript;
    }

    private void OnDisable()
    {
        SessionManager.OnSessionStart          -= HandleSessionStart;
        SessionManager.OnSessionEnd            -= HandleSessionEnd;
        SpeechRecognizer.OnTranscriptionResult -= HandleTranscript;
    }

    private void HandleSessionStart()
    {
        _wordLog.Clear();
        _fillerLog.Clear();
        _fullTranscript.Clear();
        _totalWords         = 0;
        _sessionStartTime   = Time.time;
        _lastTranscriptTime = Time.time;
        _emitTimer          = 0f;
        _smoothedWpm        = 0f;

        // Language pref read once per session. Picks the filler list used
        // for both the rolling 30s count (live audience rules) and the
        // full-transcript recount at session end.
        string lang = PlayerPrefs.GetString("Language", Localization.LangEnglish);
        _fillerWords = lang == Localization.LangDutch ? FillerWordsNl : FillerWordsEn;

        _isRunning          = true;
    }

    private void HandleSessionEnd(SpeechMetrics m)
    {
        _isRunning = false;
        WriteFinalResultsToPlayerPrefs(m.sessionTime);
    }

    // Recomputes WPM and filler count from the complete accumulated transcript
    // and writes them to PlayerPrefs. Called on OnSessionEnd, before
    // SessionManager flushes PlayerPrefs and loads the Results scene.
    private void WriteFinalResultsToPlayerPrefs(float sessionTime)
    {
        string full = _fullTranscript.ToString();
        int totalWords   = CountWords(full);
        int totalFillers = CountFillers(full);
        float avgWpm     = sessionTime > 0f ? totalWords / sessionTime * 60f : 0f;

        PlayerPrefs.SetFloat("Results_AvgWPM",      avgWpm);
        PlayerPrefs.SetInt  ("Results_FillerCount", totalFillers);
        PlayerPrefs.SetInt  ("Results_TotalWords",  totalWords);

        Debug.Log($"[SpeechAnalyzer] Final results: {totalWords} words, " +
                  $"{totalFillers} fillers, {avgWpm:F1} WPM over {sessionTime:F1}s.");
    }

    // ── Transcript processing ─────────────────────────────────────────────────

    private void HandleTranscript(string text, bool isFinal)
    {
        if (!_isRunning || !isFinal) return;

        float now = Time.time;
        _lastTranscriptTime = now;

        // Append for end-of-session re-count. Space separator preserves word
        // boundaries between chunks; recount handles any double-whitespace.
        if (!string.IsNullOrWhiteSpace(text))
            _fullTranscript.Append(text.Trim()).Append(' ');

        int words = CountWords(text);
        _totalWords += words;
        _wordLog.Add((now, words));

        int fillers = CountFillers(text);
        if (fillers > 0) _fillerLog.Add((now, fillers));
    }

    // ── Per-frame emit ────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isRunning) return;

        _emitTimer += Time.deltaTime;
        if (_emitTimer >= emitInterval)
        {
            _emitTimer = 0f;
            EmitMetrics();
        }
    }

    private void EmitMetrics()
    {
        float now     = Time.time;
        float elapsed = now - _sessionStartTime;

        // Rolling WPM — words in last wpmWindow seconds
        float cutoff = now - wpmWindow;
        PruneLog(_wordLog, cutoff);
        int recentWords = 0;
        foreach (var (_, w) in _wordLog) recentWords += w;
        float rawWpm = wpmWindow > 0f ? recentWords / wpmWindow * 60f : 0f;

        // EMA smoothing — prevents step jumps caused by discrete speech chunks
        // entering/leaving the rolling window. Alpha=wpmSmoothing: 0=frozen, 1=raw.
        _smoothedWpm = _smoothedWpm < 1f
            ? rawWpm                                             // first sample: seed directly
            : Mathf.Lerp(_smoothedWpm, rawWpm, wpmSmoothing);
        float wpm = _smoothedWpm;

        // Session-average WPM
        float avgWpm = elapsed > 0f ? _totalWords / elapsed * 60f : 0f;

        // Filler count in last fillerWindow seconds
        float fillerCutoff = now - fillerWindow;
        PruneLog(_fillerLog, fillerCutoff);
        int fillers = 0;
        foreach (var (_, f) in _fillerLog) fillers += f;

        // Pause duration — how long since last transcript (clamped to 0 if below threshold)
        float gap = now - _lastTranscriptTime;
        float pause = gap >= pauseThreshold ? gap : 0f;

        OnMetricsUpdate?.Invoke(new SpeechMetrics
        {
            wpm               = wpm,
            rollingAvgWpm     = avgWpm,
            fillerCount       = fillers,
            lastPauseDuration = pause,
            sessionTime       = elapsed
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Trim().Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    // Instance method — uses the per-session _fillerWords array picked in
    // HandleSessionStart based on the Language PlayerPref. Word-boundary
    // checks on each side prevent "so" matching inside "also", etc.
    private int CountFillers(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string lower = text.ToLowerInvariant();
        string[] fillers = _fillerWords ?? FillerWordsEn;
        int count = 0;
        foreach (string filler in fillers)
        {
            int idx = 0;
            while ((idx = lower.IndexOf(filler, idx, StringComparison.Ordinal)) >= 0)
            {
                bool startOk = idx == 0 || !char.IsLetter(lower[idx - 1]);
                bool endOk   = idx + filler.Length == lower.Length
                               || !char.IsLetter(lower[idx + filler.Length]);
                if (startOk && endOk) count++;
                idx += filler.Length;
            }
        }
        return count;
    }

    private static void PruneLog<T>(List<(float t, T v)> log, float cutoff)
    {
        int i = 0;
        while (i < log.Count && log[i].t < cutoff) i++;
        if (i > 0) log.RemoveRange(0, i);
    }
}
