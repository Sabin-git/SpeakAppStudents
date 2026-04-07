using UnityEngine;

/// <summary>
/// Evaluates speech and head metrics against the rule table and emits
/// OnStateChanged when the audience state changes.
///
/// Rule priority (evaluated on each SpeechMetrics update, every ~2s):
///   1. pause > 5s  OR  fillers >= 4 in 30s                                    → Restless
///   2. pause > 3s  OR  wpm < 80 sustained >= lowWpmSustainSec  OR  wpm > 180 sustained >= 20s  → Distracted
///   3. wpm 110–160 AND fillers < 2 in 30s                                      → Engaged
///   4. default                                                                  → Neutral
///
/// Head tracking modifiers (applied after speech rules, raise severity only):
///   Lectern zone sustained >= 5s  → floor = Distracted
///   Other   zone sustained >= 5s  → floor = Restless
///
/// State holds for a strict minimum of 4 seconds before any transition.
/// </summary>
public class AudienceRuleEngine : MonoBehaviour
{
    public static event System.Action<AudienceState> OnStateChanged;

    [Header("Debug Options")]
    [Tooltip("When enabled, ignores all rules and locks the audience to the state below.")]
    [SerializeField] private bool         debugForceState  = false;
    [SerializeField] private AudienceState debugForcedState = AudienceState.Neutral;

    [Header("Thresholds")]
    [SerializeField] private float minHoldSec          = 4f;
    [SerializeField] private float highWpmSustainSec   = 20f;
    [SerializeField] private float lowWpmSustainSec    = 8f;   // seconds of wpm < 80 before Distracted
    [SerializeField] private float lecternSustainSec   = 5f;
    [SerializeField] private float otherSustainSec     = 5f;

    private AudienceState _currentState = AudienceState.Neutral;
    private float         _holdTimer;           // time since last state change
    private float         _highWpmTimer;        // consecutive seconds with wpm > 180
    private float         _lowWpmTimer;         // consecutive seconds with 0 < wpm < 80
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
        _holdTimer     = minHoldSec; // allow first real transition immediately
        _highWpmTimer  = 0f;
        _lowWpmTimer   = 0f;
        _lecternTimer  = 0f;
        _otherTimer    = 0f;
        _latestSpeech  = default;
        _latestHead    = default;
        _isRunning     = true;
    }

    private void HandleSessionEnd(SpeechMetrics _) => _isRunning = false;

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

        // Sustained WPM timers — increment by emit interval (~2s each call)
        if (s.wpm > 180f)
            _highWpmTimer += 2f;
        else
            _highWpmTimer = 0f;

        if (s.wpm > 0f && s.wpm < 80f)
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

        // Priority 1
        if (pause >= 5f || fillers >= 4)
            return AudienceState.Restless;

        // Priority 2 — low/high WPM require sustained exposure to avoid reacting to inter-phrase gaps
        if (pause >= 3f || _lowWpmTimer >= lowWpmSustainSec || _highWpmTimer >= highWpmSustainSec)
            return AudienceState.Distracted;

        // Priority 3
        if (wpm >= 110f && wpm <= 160f && fillers < 2)
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

        // Strict 4s hold — no bypass for severity. Prevents flickering between states
        // on every emit cycle. The hold resets on every confirmed transition.
        if (_holdTimer < minHoldSec) return;

        _currentState = candidate;
        _holdTimer    = 0f;
        Debug.Log($"[AudienceRuleEngine] State → {_currentState}");
        OnStateChanged?.Invoke(_currentState);
    }

    // ── Debug shortcuts (right-click the component in Inspector) ──────────────

    [ContextMenu("Debug - Force Engaged")]
    private void DebugForceEngaged()    { debugForceState = true; debugForcedState = AudienceState.Engaged;    _holdTimer = minHoldSec; }

    [ContextMenu("Debug - Force Neutral")]
    private void DebugForceNeutral()    { debugForceState = true; debugForcedState = AudienceState.Neutral;    _holdTimer = minHoldSec; }

    [ContextMenu("Debug - Force Distracted")]
    private void DebugForceDistracted() { debugForceState = true; debugForcedState = AudienceState.Distracted; _holdTimer = minHoldSec; }

    [ContextMenu("Debug - Force Restless")]
    private void DebugForceRestless()   { debugForceState = true; debugForcedState = AudienceState.Restless;   _holdTimer = minHoldSec; }

    [ContextMenu("Debug - Release Force")]
    private void DebugReleaseForce()    { debugForceState = false; }
}
