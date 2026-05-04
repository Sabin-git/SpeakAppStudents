using UnityEngine;

/// <summary>
/// Per-avatar component. Attach to each AvatarAnchor GO (set avatarIndex 0–9).
///
/// When AudienceController calls SetTargetState():
///   30–70% of avatars switch immediately (randomised per instance).
///   The rest delay up to 5 seconds before switching.
///
/// Drives an Animator "State" int, "Variant" float, and "GazeReact" trigger.
/// Each state has N clip variants in a Blend Tree; Variant picks one at random
/// whenever the avatar enters that state, so avatars in the same state still
/// look different from each other.
///
/// Restless plays the same base clip as Neutral but randomly fires
/// DistractionReaction one-shots at 2–8 second intervals.
///
/// _variantCounts and _distractionReactCount are populated automatically by
/// AnimatorClipWirer (VR Trainer → Wire Animation Clips).
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

    private AudienceState _currentState = AudienceState.Neutral;
    private AudienceState _pendingState;
    private bool          _hasPending;
    private float         _switchDelay;
    private float         _switchTimer;
    private bool          _isGazed;

    private float _reactTimer;
    private float _nextReactDelay = 99f;

    private Animator _anim;

    private static readonly int AnimStateId          = Animator.StringToHash("State");
    private static readonly int VariantId            = Animator.StringToHash("Variant");
    private static readonly int GazeReactId          = Animator.StringToHash("GazeReact");
    private static readonly int DistractionReactId   = Animator.StringToHash("DistractionReact");
    private static readonly int DistractionVariantId = Animator.StringToHash("DistractionReactVariant");

    private void Awake() => _anim = GetComponentInChildren<Animator>();
    private void Start()  => ApplyState(_currentState);

    // ── State switching ────────────────────────────────────────────────────────

    public void SetTargetState(AudienceState state)
    {
        if (state == _currentState)
        {
            _hasPending = false;
            return;
        }

        float immediateChance = Random.Range(0.3f, 0.7f);
        if (Random.value < immediateChance)
        {
            ApplyState(state);
        }
        else
        {
            _pendingState = state;
            _switchDelay  = Random.Range(0f, 5f);
            _switchTimer  = 0f;
            _hasPending   = true;
        }
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

        if (_currentState == AudienceState.Restless && _distractionReactCount > 0)
        {
            _reactTimer += Time.deltaTime;
            if (_reactTimer >= _nextReactDelay)
            {
                FireDistractionReaction();
                ScheduleNextReaction();
            }
        }
    }

    private void ApplyState(AudienceState state)
    {
        _currentState = state;
        if (_anim == null) return;

        int stateIdx = (int)state;
        int count    = (stateIdx < _variantCounts.Length) ? _variantCounts[stateIdx] : 1;
        _anim.SetFloat(VariantId, Random.Range(0, Mathf.Max(1, count)));
        _anim.SetInteger(AnimStateId, stateIdx);

        if (state == AudienceState.Restless)
            ScheduleNextReaction();

        Debug.Log($"[AudienceMember {avatarIndex}] → {state}");
    }

    // ── Distraction reactions (Restless state) ─────────────────────────────────

    private void ScheduleNextReaction()
    {
        _reactTimer     = 0f;
        _nextReactDelay = Random.Range(2f, 8f);
    }

    private void FireDistractionReaction()
    {
        if (_anim == null) return;
        _anim.SetFloat(DistractionVariantId, Random.Range(0, Mathf.Max(1, _distractionReactCount)));
        _anim.SetTrigger(DistractionReactId);
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
