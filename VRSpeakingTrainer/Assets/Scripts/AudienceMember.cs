using UnityEngine;

/// <summary>
/// Per-avatar component. Attach to each AvatarAnchor GO (set avatarIndex 0–9).
///
/// When AudienceController calls SetTargetState():
///   30–70% of avatars switch immediately (randomised per instance).
///   The rest delay up to 5 seconds before switching.
///
/// Drives an Animator "State" int parameter if an Animator is present.
/// If no Animator exists (Stage 6 — added in Stage 7), this is a no-op.
/// </summary>
public class AudienceMember : MonoBehaviour
{
    [Tooltip("Avatar index 0–9. Set manually in the Inspector.")]
    public int avatarIndex;

    private AudienceState _currentState = AudienceState.Neutral;
    private AudienceState _pendingState;
    private bool          _hasPending;
    private float         _switchDelay;
    private float         _switchTimer;
    private bool          _isGazed;

    private static readonly int AnimStateId = Animator.StringToHash("State");

    // ── State switching ────────────────────────────────────────────────────────

    public void SetTargetState(AudienceState state)
    {
        if (state == _currentState)
        {
            _hasPending = false;
            return;
        }

        // 30–70% chance of switching immediately (randomised per avatar instance)
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
        if (!_hasPending) return;

        _switchTimer += Time.deltaTime;
        if (_switchTimer >= _switchDelay)
        {
            ApplyState(_pendingState);
            _hasPending = false;
        }
    }

    private void ApplyState(AudienceState state)
    {
        _currentState = state;

        if (TryGetComponent<Animator>(out var anim))
            anim.SetInteger(AnimStateId, (int)state);

        Debug.Log($"[AudienceMember {avatarIndex}] → {state}");
    }

    // ── Gaze reaction ──────────────────────────────────────────────────────────

    public void SetGazed(bool gazed)
    {
        if (gazed == _isGazed) return;
        _isGazed = gazed;

        if (gazed)
            Debug.Log($"[AudienceMember {avatarIndex}] Gazed");
        // Stage 7: play nod/look-back animation here
    }
}
