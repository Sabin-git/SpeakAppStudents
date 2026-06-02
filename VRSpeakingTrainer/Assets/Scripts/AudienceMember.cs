using UnityEngine;

/// <summary>
/// Per-avatar component. Attach to each AvatarAnchor GO (set avatarIndex 0–9).
///
/// Holds individual state and cycles through state-variant clips every 8–15
/// seconds for natural per-avatar variation, so avatars don't look frozen on a
/// single clip during long state periods. The cycle timer is randomised per
/// avatar with a 0–8s initial offset so all 10 avatars don't swap on the same
/// frame.
///
/// On global state changes (AudienceController → SetTargetState), 30–70% of
/// avatars switch immediately (randomised per instance); the rest delay over a
/// randomised 0–8s window before switching, producing a natural staggered
/// transition. Individual gaze reactions are fired via SetGazed().
///
/// DistractionReact one-shots have been removed (these were the source of the
/// "fall through chair" bug — standing Mixamo reaction clips played on seated
/// avatars caused the hips to snap up and clip through the chair seat).
/// Restless now just plays its base clip (the same as Neutral, per the
/// existing AnimatorClipWirer behaviour) and cycles variants like every other
/// state. The DistractionReacting state in AudienceAnimator.controller remains
/// as dead code; safe to leave or clean up later via the Animator window.
///
/// Drives an Animator "State" int and "Variant" float. Each state has N clip
/// variants in a Blend Tree; Variant picks one at random whenever the avatar
/// enters a state OR cycles within a state. The blend tree handles the visual
/// crossfade automatically. A "GazeReact" trigger is also fired for per-avatar
/// gaze reactions.
///
/// _variantCounts is populated automatically by AnimatorClipWirer
/// (VR Trainer → Wire Animation Clips).
///
/// ── Gaze override layer (Task 4) ──────────────────────────────────────────
/// Every AudienceMember additionally runs a per-avatar gaze state machine on
/// top of the global state flow. When the user looks at this avatar for
/// _gazeOverrideThreshold seconds (Easy 1.5 / Medium 3 / Hard 5), the avatar's
/// effective state is overridden to Engaged regardless of the global state. The
/// override persists through a short look-away window:
///   - grace      (_gazeBreakGrace,    Easy 0.5 / Medium 1 / Hard 2 s):
///       re-gaze inside this window resumes the accumulator and keeps the
///       override active.
///   - hold       (_gazeHoldDuration,  Easy 8 / Medium 5 / Hard 3 s):
///       override is preserved even though the user has fully looked away.
///   - drift      (_gazeDriftDuration, Easy 15 / Medium 10 / Hard 6 s):
///       override is still set; the existing 0.5s animation crossfade handles
///       the visual blend as the global state takes back over.
///   - after the drift window expires, the override clears and the avatar
///     re-applies its current global state.
///
/// Effective state shown by ApplyState = _gazeOverrideState ?? state. When the
/// override is set, global SetTargetState calls still take their normal stagger
/// path (so the underlying _currentState moves), but the visual reflects the
/// override. When the override clears, ApplyState(_currentState) re-fires so
/// the avatar resumes the latest global state immediately.
///
/// Thresholds are picked once at Awake from the Settings_Responsiveness pref
/// (same source as the global stagger above). Inspector-tweakable for tuning;
/// the per-level values are seeded by the Awake switch and can be overridden
/// per-instance in the Inspector if needed (rare — the prefab values are the
/// source of truth for the study).
///
/// Design rationale and literature citations: see USER_TESTING_PLAN.md Task 4
/// "Design Rationale" and the project root PDF "Literature research background
/// + interactions.pdf". Sustained eye contact maps monotonically to engagement;
/// there is NO upper "uncomfortable stare → Restless" cap.
/// </summary>
public class AudienceMember : MonoBehaviour
{
    [Tooltip("Avatar index 0–9. Set manually in the Inspector.")]
    public int avatarIndex;

    [Tooltip("How many clip variants exist per state (Engaged/Neutral/Distracted/Restless). " +
             "Set automatically by VR Trainer → Wire Animation Clips.")]
    [SerializeField] private int[] _variantCounts = { 1, 1, 1, 1 };

    [Tooltip("How many DistractionReaction clips are available. " +
             "Set automatically by VR Trainer → Wire Animation Clips.")]
    [SerializeField] private int _distractionReactCount = 2;
    // Kept for backward compatibility with AnimatorClipWirer; no longer drives runtime behaviour.

    [Tooltip("Maximum per-avatar delay (seconds) before applying a global state change. " +
             "Defaults to the Medium responsiveness value (8s); overridden in Awake() from " +
             "Settings_Responsiveness PlayerPref (Easy=12s, Medium=8s, Hard=4s).")]
    [SerializeField] private float _maxStateChangeDelay = 8f;

    // ── Task 4 — gaze override thresholds (seeded at Awake from Settings_Responsiveness) ──
    [Header("Gaze override (Task 4 — seeded per Settings_Responsiveness in Awake)")]
    [Tooltip("Continuous seconds the user must gaze at this avatar before the " +
             "override flips the avatar to Engaged. Easy=1.5, Medium=3, Hard=5.")]
    [SerializeField] private float _gazeOverrideThreshold = 3f;
    [Tooltip("Seconds the user can look away briefly without the gaze " +
             "accumulator resetting. Re-gazing inside this window resumes it. " +
             "Easy=0.5, Medium=1, Hard=2.")]
    [SerializeField] private float _gazeBreakGrace = 1f;
    [Tooltip("Seconds (after the grace window) the gaze override remains in " +
             "force even though the user has looked away. Easy=8, Medium=5, Hard=3.")]
    [SerializeField] private float _gazeHoldDuration = 5f;
    [Tooltip("Seconds (after hold) over which the gaze override drifts back " +
             "to the global state. The existing 0.5s anim crossfade handles " +
             "the actual visual blend. Easy=15, Medium=10, Hard=6.")]
    [SerializeField] private float _gazeDriftDuration = 10f;

    private AudienceState _currentState = AudienceState.Neutral;
    private AudienceState _pendingState;
    private bool          _hasPending;
    private float         _switchDelay;
    private float         _switchTimer;
    private bool          _isGazed;

    // Task 4 — gaze override runtime state
    private AudienceState? _gazeOverrideState;
    private float          _gazeAccumulator;
    private float          _gazeBreakTimer;

    private float _variantCycleTimer;
    private float _nextVariantCycleDelay;

    private Animator _anim;

    private static readonly int AnimStateId = Animator.StringToHash("State");
    private static readonly int VariantId   = Animator.StringToHash("Variant");
    private static readonly int GazeReactId = Animator.StringToHash("GazeReact");

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();

        // Stagger first variant cycle across avatars so all 10 don't swap on the same frame.
        _variantCycleTimer     = Random.Range(0f, 8f);
        _nextVariantCycleDelay = Random.Range(8f, 15f);

        // Task 2d — responsiveness drives the per-avatar transition stagger.
        // Read the pref once at Awake; never re-read mid-session. Changing the
        // responsiveness mid-session (e.g. via the dev panel "Force Responsiveness"
        // row) only takes effect when the avatars are re-Awoken (next Play).
        string responsiveness = PlayerPrefs.GetString("Settings_Responsiveness", "medium");
        switch (responsiveness.ToLowerInvariant())
        {
            case "easy": _maxStateChangeDelay = 12f; break;
            case "hard": _maxStateChangeDelay =  4f; break;
            default:     _maxStateChangeDelay =  8f; break; // medium (and fallback)
        }

        // Task 4 — same pref drives the per-avatar gaze override thresholds.
        // Identical Easy/Medium/Hard switch pattern; see USER_TESTING_PLAN.md
        // Task 4 for the table and rationale. Values are seeded here so the
        // Inspector defaults from a fresh Awake match the chosen level — but
        // each field stays [SerializeField] so a researcher can still tweak
        // an individual avatar's thresholds at runtime without recompiling.
        switch (responsiveness.ToLowerInvariant())
        {
            case "easy":
                _gazeOverrideThreshold = 1.5f;
                _gazeBreakGrace        = 0.5f;
                _gazeHoldDuration      = 8f;
                _gazeDriftDuration     = 15f;
                break;
            case "hard":
                _gazeOverrideThreshold = 5f;
                _gazeBreakGrace        = 2f;
                _gazeHoldDuration      = 3f;
                _gazeDriftDuration     = 6f;
                break;
            default: // medium (and fallback)
                _gazeOverrideThreshold = 3f;
                _gazeBreakGrace        = 1f;
                _gazeHoldDuration      = 5f;
                _gazeDriftDuration     = 10f;
                break;
        }
    }

    private void Start()  => ApplyState(_currentState);

    // ── State switching ────────────────────────────────────────────────────────

    public void SetTargetState(AudienceState state)
    {
        if (state == _currentState)
        {
            _hasPending = false;
            return;
        }

        // Every avatar delays 0–_maxStateChangeDelay seconds before switching —
        // no immediate path. This eliminates the synchronised cluster of
        // "immediate switchers" that would otherwise appear at every state
        // change. The max delay is responsiveness-dependent (Easy=12s, Medium=8s,
        // Hard=4s), set once in Awake from Settings_Responsiveness.
        _pendingState = state;
        _switchDelay  = Random.Range(0f, _maxStateChangeDelay);
        _switchTimer  = 0f;
        _hasPending   = true;
    }

    private void Update()
    {
        if (_hasPending)
        {
            _switchTimer += Time.deltaTime;
            if (_switchTimer >= _switchDelay)
            {
                ApplyState(_pendingState);
                _hasPending = false;
            }
        }

        _variantCycleTimer += Time.deltaTime;
        if (_variantCycleTimer >= _nextVariantCycleDelay)
        {
            CycleVariant();
        }

        UpdateGazeOverride();
    }

    // ── Task 4 — gaze override state machine ─────────────────────────────────
    // Runs alongside the existing global state flow. See class docstring for
    // the full design rationale and literature citations. Bypasses the Task 2d
    // 0–_maxStateChangeDelay stagger window — the override takes effect on the
    // exact frame the threshold is crossed, so per-avatar reactivity feels
    // immediate. When the override clears (after grace + hold + drift), we
    // re-apply the most recent _currentState so the avatar resumes whatever
    // the global state has become while it was overridden.
    private void UpdateGazeOverride()
    {
        if (_isGazed)
        {
            _gazeAccumulator += Time.deltaTime;
            _gazeBreakTimer   = 0f;

            if (_gazeAccumulator >= _gazeOverrideThreshold
                && _gazeOverrideState != AudienceState.Engaged)
            {
                _gazeOverrideState = AudienceState.Engaged;
                // Re-apply current state so ApplyState picks up the override
                // immediately and pushes the Animator integer.
                ApplyState(_currentState);
            }
            return;
        }

        // User is not gazing at this avatar. Walk through grace → hold → drift
        // → clear phases. The whole machinery only runs if an override or an
        // in-progress accumulator is actually present — otherwise it's a no-op.
        if (_gazeOverrideState == null && _gazeAccumulator <= 0f) return;

        _gazeBreakTimer += Time.deltaTime;

        float grace     = _gazeBreakGrace;
        float holdEnd   = grace + _gazeHoldDuration;
        float driftEnd  = holdEnd + _gazeDriftDuration;

        if (_gazeBreakTimer < grace)
        {
            // Grace window — accumulator and override preserved, no change.
            return;
        }
        if (_gazeBreakTimer < holdEnd)
        {
            // Hold window — override still set, accumulator preserved. The
            // visible state stays Engaged.
            return;
        }
        if (_gazeBreakTimer < driftEnd)
        {
            // Drift window — override still set; the existing 0.5s Animator
            // crossfade handles the visual transition naturally once the
            // override clears at driftEnd. No per-frame work required here.
            return;
        }

        // Drift expired — clear the override and re-apply current global state.
        _gazeOverrideState = null;
        _gazeAccumulator   = 0f;
        _gazeBreakTimer    = 0f;
        ApplyState(_currentState);
    }

    /// <summary>
    /// Push the avatar's state to the Animator. Honours the Task 4 gaze
    /// override layer: if _gazeOverrideState is set, that value drives the
    /// Animator's State integer instead of the supplied state. The supplied
    /// state is still recorded into _currentState so that when the override
    /// later clears, the avatar resumes the latest global state.
    /// </summary>
    private void ApplyState(AudienceState state)
    {
        _currentState = state;
        if (_anim == null) return;

        AudienceState effective = _gazeOverrideState ?? state;
        int stateIdx = (int)effective;
        int count    = (stateIdx < _variantCounts.Length) ? _variantCounts[stateIdx] : 1;
        _anim.SetFloat(VariantId, Random.Range(0, Mathf.Max(1, count)));
        _anim.SetInteger(AnimStateId, stateIdx);

        Debug.Log($"[AudienceMember {avatarIndex}] → {effective}"
                  + (_gazeOverrideState.HasValue ? "  (gaze override)" : string.Empty));
    }

    // ── Per-avatar variant cycling ─────────────────────────────────────────────

    private void CycleVariant()
    {
        _variantCycleTimer     = 0f;
        _nextVariantCycleDelay = Random.Range(8f, 15f);

        if (_anim == null) return;

        // Use the effective state (override > global) so variant cycling
        // picks from the currently-displayed state's pool, not the global
        // one. Otherwise a gaze-overridden avatar would cycle variants from
        // (e.g.) the Distracted pool while visually playing Engaged.
        AudienceState effective = _gazeOverrideState ?? _currentState;
        int stateIdx = (int)effective;
        int count    = (stateIdx < _variantCounts.Length) ? _variantCounts[stateIdx] : 1;
        _anim.SetFloat(VariantId, Random.Range(0, Mathf.Max(1, count)));
    }

    // ── Gaze reaction ──────────────────────────────────────────────────────────

    public void SetGazed(bool gazed)
    {
        if (gazed == _isGazed) return;
        _isGazed = gazed;

        if (gazed)
        {
            if (_anim != null) _anim.SetTrigger(GazeReactId);
            Debug.Log($"[AudienceMember {avatarIndex}] Gazed");
        }
    }

    // ── Called by AnimatorClipWirer editor tool ────────────────────────────────

#if UNITY_EDITOR
    public void SetVariantCounts(int[] counts) => _variantCounts = (int[])counts.Clone();
    public void SetDistractionReactCount(int count) => _distractionReactCount = count;
#endif
}
