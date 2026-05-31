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

    private AudienceState _currentState = AudienceState.Neutral;
    private AudienceState _pendingState;
    private bool          _hasPending;
    private float         _switchDelay;
    private float         _switchTimer;
    private bool          _isGazed;

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

        float immediateChance = Random.Range(0.3f, 0.7f);
        if (Random.value < immediateChance)
        {
            ApplyState(state);
        }
        else
        {
            _pendingState = state;
            _switchDelay  = Random.Range(0f, 8f);
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

        _variantCycleTimer += Time.deltaTime;
        if (_variantCycleTimer >= _nextVariantCycleDelay)
        {
            CycleVariant();
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

        Debug.Log($"[AudienceMember {avatarIndex}] → {state}");
    }

    // ── Per-avatar variant cycling ─────────────────────────────────────────────

    private void CycleVariant()
    {
        _variantCycleTimer     = 0f;
        _nextVariantCycleDelay = Random.Range(8f, 15f);

        if (_anim == null) return;

        int stateIdx = (int)_currentState;
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
