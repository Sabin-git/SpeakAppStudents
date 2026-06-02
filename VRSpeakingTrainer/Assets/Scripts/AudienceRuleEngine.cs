using UnityEngine;

/// <summary>
/// Evaluates speech and head metrics against the rule table and emits
/// OnStateChanged when the audience state changes.
///
/// Rule priority (evaluated on each SpeechMetrics update, every ~2s):
///   1. pause > pauseRestlessThreshold  OR  fillers >= fillerThreshold in 30s    → Restless
///   2. pause > pauseDistractedThreshold OR wpm < low sustained
///      OR wpm > high sustained >= sustainedWpmDuration                          → Distracted
///   3. wpm in engaged band AND fillers < (fillerThreshold/2) in 30s             → Engaged
///   4. default                                                                  → Neutral
///
/// Head tracking modifiers (applied after speech rules, raise severity only):
///   Lectern zone sustained >= 5s  → floor = Distracted
///   Other   zone sustained >= 5s  → floor = Restless
///
/// State holds for a strict minimum of minStateHoldTime seconds before any
/// transition. This duration is also responsiveness-level dependent.
///
/// All thresholds in the table above are picked at session start from a
/// combination of the Language pref (Task 1) and the Settings_Responsiveness
/// pref (Task 2d). They are never re-read mid-session.
/// </summary>
public class AudienceRuleEngine : MonoBehaviour
{
    public static event System.Action<AudienceState> OnStateChanged;

    /// <summary>
    /// Responsiveness levels for the audience rule engine. Picked at session
    /// start from the Settings_Responsiveness PlayerPref (default = Medium).
    ///
    ///   Easy   — wider WPM band, higher filler tolerance, longer pause /
    ///            sustained windows, slower transitions. Forgiving.
    ///   Medium — baseline (current behaviour pre-Task 2d).
    ///   Hard   — narrower WPM band, lower filler tolerance, shorter pause /
    ///            sustained windows, faster transitions. Demanding.
    /// </summary>
    private enum ResponsivenessLevel { Easy, Medium, Hard }

    [Header("Debug Options")]
    [Tooltip("When enabled, ignores all rules and locks the audience to the state below.")]
    [SerializeField] private bool         debugForceState  = false;
    [SerializeField] private AudienceState debugForcedState = AudienceState.Neutral;

    [Header("Head modifier sustained windows")]
    [SerializeField] private float lecternSustainSec   = 5f;
    [SerializeField] private float otherSustainSec     = 5f;

    // ── Responsiveness-driven thresholds ──────────────────────────────────────
    // Filled by ApplyLevelThresholds() at session start. Sourced from a 2-axis
    // table: (Language, ResponsivenessLevel). See CLAUDE.md → Audience Rule
    // Table → Per-responsiveness-level variations for the full table.

    private ResponsivenessLevel _responsiveness = ResponsivenessLevel.Medium;

    // Pause thresholds (seconds)
    private float _pauseRestlessThreshold   = 5f;  // > this → Restless
    private float _pauseDistractedThreshold = 3f;  // > this → Distracted

    // Filler thresholds (count in 30s window)
    private int   _fillerThreshold          = 4;   // >= this → Restless. Engaged uses fillerThreshold/2.

    // Engaged WPM band (language + level dependent)
    private float _wpmEngagedLow            = 110f;
    private float _wpmEngagedHigh           = 160f;

    // Sustained WPM out-of-band → Distracted (single timer covers both low/high
    // out-of-band durations; the rule fires when the sustained time exceeds
    // _sustainedWpmDuration in either direction).
    private float _sustainedWpmDuration     = 20f;

    // Min hold time before any state transition is allowed
    private float _minStateHoldTime         = 4f;

    private AudienceState _currentState = AudienceState.Neutral;
    private float         _holdTimer;           // time since last state change
    private float         _highWpmTimer;        // consecutive seconds with wpm > high
    private float         _lowWpmTimer;         // consecutive seconds with 0 < wpm < low (of band)
    private float         _lecternTimer;        // consecutive seconds in Lectern zone
    private float         _otherTimer;          // consecutive seconds in Other zone

    private SpeechMetrics _latestSpeech;
    private HeadMetrics   _latestHead;
    private bool          _isRunning;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        SessionManager.OnSessionStart       += HandleSessionStart;
        SessionManager.OnSessionEnd         += HandleSessionEnd;
        SpeechAnalyzer.OnMetricsUpdate      += HandleSpeechMetrics;
        HeadTracker.OnHeadMetricsUpdated    += HandleHeadMetrics;
    }

    private void OnDisable()
    {
        SessionManager.OnSessionStart       -= HandleSessionStart;
        SessionManager.OnSessionEnd         -= HandleSessionEnd;
        SpeechAnalyzer.OnMetricsUpdate      -= HandleSpeechMetrics;
        HeadTracker.OnHeadMetricsUpdated    -= HandleHeadMetrics;
    }

    private void HandleSessionStart()
    {
        _currentState  = AudienceState.Engaged;
        _highWpmTimer  = 0f;
        _lowWpmTimer   = 0f;
        _lecternTimer  = 0f;
        _otherTimer    = 0f;
        _latestSpeech  = default;
        _latestHead    = default;
        _isRunning     = true;

        // Language pref read once per session — picks WPM thresholds.
        // Responsiveness pref read once per session — picks every numeric
        // threshold in the rule table. Both pull from PlayerPrefs and are
        // never re-read mid-session.
        _responsiveness = ParseResponsiveness(
            PlayerPrefs.GetString("Settings_Responsiveness", "medium"));

        ApplyLevelThresholds();

        _holdTimer = _minStateHoldTime; // allow first real transition immediately

        // Apply dev panel settings from PlayerPrefs.
        int startingState = PlayerPrefs.GetInt("Dev_StartingAudienceState", 0);
        _currentState = (AudienceState)Mathf.Clamp(startingState, 0, 3);

        int forcedState = PlayerPrefs.GetInt("Dev_ForceAudienceState", -1);
        if (forcedState >= 0)
        {
            debugForceState  = true;
            debugForcedState = (AudienceState)Mathf.Clamp(forcedState, 0, 3);
        }
        else
        {
            debugForceState = false;
        }
    }

    private void HandleSessionEnd(SpeechMetrics _) => _isRunning = false;

    // ── Responsiveness threshold table ────────────────────────────────────────

    /// <summary>
    /// Fill every per-level threshold field based on _responsiveness and the
    /// active Language. Single source of truth — keep in sync with the table
    /// in CLAUDE.md → Audience Rule Table → Per-responsiveness-level variations.
    /// </summary>
    private void ApplyLevelThresholds()
    {
        string lang = PlayerPrefs.GetString("Language", Localization.LangEnglish);
        bool isDutch = (lang == Localization.LangDutch);

        switch (_responsiveness)
        {
            case ResponsivenessLevel.Easy:
                _pauseRestlessThreshold   = 7f;
                _pauseDistractedThreshold = 5f;
                _fillerThreshold          = 6;
                _wpmEngagedLow            = isDutch ?  80f :  90f;
                _wpmEngagedHigh           = isDutch ? 150f : 180f;
                _sustainedWpmDuration     = 30f;
                _minStateHoldTime         = 8f;
                break;

            case ResponsivenessLevel.Hard:
                _pauseRestlessThreshold   = 3f;
                _pauseDistractedThreshold = 2f;
                _fillerThreshold          = 2;
                _wpmEngagedLow            = isDutch ? 100f : 120f;
                _wpmEngagedHigh           = isDutch ? 130f : 150f;
                _sustainedWpmDuration     = 10f;
                _minStateHoldTime         = 2f;
                break;

            case ResponsivenessLevel.Medium:
            default:
                _pauseRestlessThreshold   = 5f;
                _pauseDistractedThreshold = 3f;
                _fillerThreshold          = 4;
                _wpmEngagedLow            = isDutch ?  90f : 110f;
                _wpmEngagedHigh           = isDutch ? 140f : 160f;
                _sustainedWpmDuration     = 20f;
                _minStateHoldTime         = 4f;
                break;
        }
    }

    private static ResponsivenessLevel ParseResponsiveness(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return ResponsivenessLevel.Medium;
        switch (raw.ToLowerInvariant())
        {
            case "easy": return ResponsivenessLevel.Easy;
            case "hard": return ResponsivenessLevel.Hard;
            default:     return ResponsivenessLevel.Medium;
        }
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isRunning) return;
        _holdTimer += Time.deltaTime;

        if (debugForceState)
            TryTransition(debugForcedState);
    }

    // ── Metric handlers ────────────────────────────────────────────────────────

    private void HandleHeadMetrics(HeadMetrics h)
    {
        _latestHead = h;

        // Sustained zone timers — updated every frame via HeadTracker
        if (h.currentZone == GazeZone.Lectern)
        {
            _lecternTimer += Time.deltaTime;
            _otherTimer    = 0f;
        }
        else if (h.currentZone == GazeZone.Other)
        {
            _otherTimer    += Time.deltaTime;
            _lecternTimer   = 0f;
        }
        else
        {
            _lecternTimer = 0f;
            _otherTimer   = 0f;
        }
    }

    private void HandleSpeechMetrics(SpeechMetrics s)
    {
        if (!_isRunning || debugForceState) return;
        _latestSpeech = s;

        // Sustained WPM timers — increment by emit interval (~2s each call).
        // Thresholds are language + responsiveness dependent (set in
        // ApplyLevelThresholds()).
        if (s.wpm > _wpmEngagedHigh)
            _highWpmTimer += 2f;
        else
            _highWpmTimer = 0f;

        if (s.wpm > 0f && s.wpm < _wpmEngagedLow)
            _lowWpmTimer += 2f;
        else
            _lowWpmTimer = 0f;

        EvaluateRules();
    }

    // ── Rule evaluation ────────────────────────────────────────────────────────

    private void EvaluateRules()
    {
        AudienceState candidate = ApplySpeechRules();
        candidate = ApplyHeadModifiers(candidate);
        TryTransition(candidate);
    }

    private AudienceState ApplySpeechRules()
    {
        float pause   = _latestSpeech.lastPauseDuration;
        float wpm     = _latestSpeech.wpm;
        int   fillers = _latestSpeech.fillerCount;

        // Priority 1 — Restless
        if (pause > _pauseRestlessThreshold || fillers >= _fillerThreshold)
            return AudienceState.Restless;

        // Priority 2 — Distracted (low/high WPM require sustained exposure so
        // brief inter-phrase gaps don't trip the rule).
        if (pause > _pauseDistractedThreshold ||
            _lowWpmTimer  >= _sustainedWpmDuration ||
            _highWpmTimer >= _sustainedWpmDuration)
            return AudienceState.Distracted;

        // Priority 3 — Engaged. Engaged uses half the filler threshold as its
        // upper bound so the "clean speech" band is meaningfully tighter than
        // the "demanding" Restless cutoff.
        int engagedFillerCap = Mathf.Max(1, _fillerThreshold / 2);
        if (wpm >= _wpmEngagedLow && wpm <= _wpmEngagedHigh && fillers < engagedFillerCap)
            return AudienceState.Engaged;

        // Priority 4 — default
        return AudienceState.Neutral;
    }

    private AudienceState ApplyHeadModifiers(AudienceState speechState)
    {
        // Head modifiers can only raise severity, never lower it
        AudienceState floor = speechState;

        if (_lecternTimer >= lecternSustainSec && floor < AudienceState.Distracted)
            floor = AudienceState.Distracted;

        if (_otherTimer >= otherSustainSec && floor < AudienceState.Restless)
            floor = AudienceState.Restless;

        return floor;
    }

    private void TryTransition(AudienceState candidate)
    {
        if (candidate == _currentState) return;

        // Strict min-hold — no bypass for severity. Prevents flickering between
        // states on every emit cycle. The hold resets on every confirmed
        // transition. _minStateHoldTime is responsiveness-dependent (8s easy,
        // 4s medium, 2s hard).
        if (_holdTimer < _minStateHoldTime) return;

        _currentState = candidate;
        _holdTimer    = 0f;
        Debug.Log($"[AudienceRuleEngine] State → {_currentState}");
        OnStateChanged?.Invoke(_currentState);
    }

    // ── Debug shortcuts (right-click the component in Inspector) ──────────────

    [ContextMenu("Debug - Force Engaged")]
    private void DebugForceEngaged()    { debugForceState = true; debugForcedState = AudienceState.Engaged;    _holdTimer = _minStateHoldTime; }

    [ContextMenu("Debug - Force Neutral")]
    private void DebugForceNeutral()    { debugForceState = true; debugForcedState = AudienceState.Neutral;    _holdTimer = _minStateHoldTime; }

    [ContextMenu("Debug - Force Distracted")]
    private void DebugForceDistracted() { debugForceState = true; debugForcedState = AudienceState.Distracted; _holdTimer = _minStateHoldTime; }

    [ContextMenu("Debug - Force Restless")]
    private void DebugForceRestless()   { debugForceState = true; debugForcedState = AudienceState.Restless;   _holdTimer = _minStateHoldTime; }

    [ContextMenu("Debug - Release Force")]
    private void DebugReleaseForce()    { debugForceState = false; }

    [ContextMenu("Debug - Print current thresholds")]
    private void DebugPrintThresholds()
    {
        string lang = PlayerPrefs.GetString("Language", Localization.LangEnglish);
        Debug.Log(
            $"[AudienceRuleEngine] Level={_responsiveness} Lang={lang}\n" +
            $"  pauseRestlessThreshold   = {_pauseRestlessThreshold}s\n" +
            $"  pauseDistractedThreshold = {_pauseDistractedThreshold}s\n" +
            $"  fillerThreshold (Restless) = {_fillerThreshold} in 30s\n" +
            $"  Engaged band              = {_wpmEngagedLow}–{_wpmEngagedHigh} WPM\n" +
            $"  sustainedWpmDuration      = {_sustainedWpmDuration}s\n" +
            $"  minStateHoldTime          = {_minStateHoldTime}s");
    }
}
