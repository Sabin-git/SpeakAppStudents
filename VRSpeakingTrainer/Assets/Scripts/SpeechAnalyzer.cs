using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Consumes SpeechRecognizer transcription events and computes speech metrics:
/// WPM (rolling 10s window), session-average WPM, filler word count (last 30s),
/// and current pause duration. Emits OnMetricsUpdate every 2 seconds.
/// </summary>
public class SpeechAnalyzer : MonoBehaviour
{
    public static event Action<SpeechMetrics> OnMetricsUpdate;

    [Header("Settings")]
    [SerializeField] private float emitInterval   = 2f;
    [SerializeField] private float wpmWindow      = 10f;  // seconds for rolling WPM
    [SerializeField] private float fillerWindow   = 30f;  // seconds for filler count
    [SerializeField] private float pauseThreshold = 1.5f; // seconds before gap is a pause
    [Tooltip("EMA smoothing factor for WPM display (0=no change, 1=instant). 0.4–0.6 recommended.")]
    [SerializeField] [Range(0f, 1f)] private float wpmSmoothing = 0.5f;

    private static readonly string[] FillerWords =
    {
        "you know", "um", "uh", "like", "basically", "literally", "so", "right"
    };

    // Each entry: (Time.time when transcript arrived, word count in that transcript)
    private readonly List<(float t, int words)> _wordLog    = new();
    // Each entry: (Time.time when transcript arrived, filler count in that transcript)
    private readonly List<(float t, int fillers)> _fillerLog = new();

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
        _totalWords         = 0;
        _sessionStartTime   = Time.time;
        _lastTranscriptTime = Time.time;
        _emitTimer          = 0f;
        _smoothedWpm        = 0f;
        _isRunning          = true;
    }

    private void HandleSessionEnd(SpeechMetrics _) => _isRunning = false;

    // ── Transcript processing ─────────────────────────────────────────────────

    private void HandleTranscript(string text, bool isFinal)
    {
        if (!_isRunning || !isFinal) return;

        float now = Time.time;
        _lastTranscriptTime = now;

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

    private static int CountFillers(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string lower = text.ToLowerInvariant();
        int count = 0;
        // Check multi-word fillers first to avoid double-counting substrings
        foreach (string filler in FillerWords)
        {
            int idx = 0;
            while ((idx = lower.IndexOf(filler, idx, StringComparison.Ordinal)) >= 0)
            {
                // Simple word-boundary check: char before and after must not be a letter
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
