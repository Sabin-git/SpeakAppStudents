using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates audience behaviour: finds all AudienceMember components at
/// session start, wires HeadTracker's avatar transform array, propagates
/// AudienceState changes to each member, and handles per-avatar gaze events.
///
/// Audio: one AudioSource on this GameObject plays ambient crowd audio.
/// Assign clips per state in the Inspector (all optional — no audio if null).
/// </summary>
public class AudienceController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private HeadTracker headTracker;

    [Header("Audience Size")]
    [Tooltip("How many audience members are active this session (1–10). The rest are hidden.")]
    [SerializeField] [Range(1, 10)] private int activeAudienceCount = 10;

    [Header("Ambient Audio (optional — leave null to skip)")]
    [SerializeField] private AudioClip clipEngaged;
    [SerializeField] private AudioClip clipNeutral;
    [SerializeField] private AudioClip clipDistracted;
    [SerializeField] private AudioClip clipRestless;
    [SerializeField] private float audioFadeDuration = 1f;

    private AudienceMember[] _members;
    private AudioSource      _audioSource;
    private int              _lastGazedIndex = -1;
    private bool             _isRunning;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        _audioSource = gameObject.GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.loop        = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume      = 0f;
    }

    private void OnEnable()
    {
        SessionManager.OnSessionStart        += HandleSessionStart;
        SessionManager.OnSessionEnd          += HandleSessionEnd;
        AudienceRuleEngine.OnStateChanged    += HandleStateChanged;
        HeadTracker.OnHeadMetricsUpdated     += HandleHeadMetrics;
    }

    private void OnDisable()
    {
        SessionManager.OnSessionStart        -= HandleSessionStart;
        SessionManager.OnSessionEnd          -= HandleSessionEnd;
        AudienceRuleEngine.OnStateChanged    -= HandleStateChanged;
        HeadTracker.OnHeadMetricsUpdated     -= HandleHeadMetrics;
    }

    private void HandleSessionStart()
    {
        // Gather all AudienceMembers and sort by index
        var all = FindObjectsByType<AudienceMember>(FindObjectsSortMode.None);
        System.Array.Sort(all, (a, b) => a.avatarIndex.CompareTo(b.avatarIndex));

        // Enable only the first activeAudienceCount members; hide the rest
        int count = Mathf.Clamp(activeAudienceCount, 1, all.Length);
        _members = new AudienceMember[count];
        for (int i = 0; i < all.Length; i++)
        {
            bool active = i < count;
            all[i].gameObject.SetActive(active);
            if (active) _members[i] = all[i];
        }

        // Wire avatar transforms into HeadTracker for per-avatar gaze detection
        if (headTracker != null)
        {
            headTracker.avatarTransforms = new Transform[_members.Length];
            for (int i = 0; i < _members.Length; i++)
                headTracker.avatarTransforms[i] = _members[i].transform;
        }

        _lastGazedIndex = -1;
        _isRunning = true;

        // Start in Engaged
        foreach (var m in _members)
            m.SetTargetState(AudienceState.Engaged);
    }

    private void HandleSessionEnd(SpeechMetrics _)
    {
        _isRunning = false;
        StopAllCoroutines();
        if (_audioSource != null) _audioSource.Stop();

        // Re-enable all members so the scene is clean if the session restarts
        var all = FindObjectsByType<AudienceMember>(FindObjectsSortMode.None);
        foreach (var m in all)
            m.gameObject.SetActive(true);
    }

    // ── State propagation ──────────────────────────────────────────────────────

    private void HandleStateChanged(AudienceState state)
    {
        if (!_isRunning || _members == null) return;

        foreach (var m in _members)
            m.SetTargetState(state);

        CrossfadeAudio(ClipForState(state));
    }

    // ── Per-avatar gaze ────────────────────────────────────────────────────────

    private void HandleHeadMetrics(HeadMetrics h)
    {
        if (!_isRunning || _members == null) return;

        int newIndex = h.gazedAvatarIndex;
        if (newIndex == _lastGazedIndex) return;

        // Clear previous
        if (_lastGazedIndex >= 0 && _lastGazedIndex < _members.Length)
            _members[_lastGazedIndex].SetGazed(false);

        // Set new
        if (newIndex >= 0 && newIndex < _members.Length)
            _members[newIndex].SetGazed(true);

        _lastGazedIndex = newIndex;
    }

    // ── Audio ──────────────────────────────────────────────────────────────────

    private AudioClip ClipForState(AudienceState state) => state switch
    {
        AudienceState.Engaged    => clipEngaged,
        AudienceState.Neutral    => clipNeutral,
        AudienceState.Distracted => clipDistracted,
        AudienceState.Restless   => clipRestless,
        _                        => null
    };

    private void CrossfadeAudio(AudioClip clip)
    {
        if (_audioSource == null) return;
        StopAllCoroutines();
        StartCoroutine(DoFade(clip));
    }

    private IEnumerator DoFade(AudioClip clip)
    {
        // Fade out current
        float startVol = _audioSource.volume;
        for (float t = 0; t < audioFadeDuration; t += Time.deltaTime)
        {
            _audioSource.volume = Mathf.Lerp(startVol, 0f, t / audioFadeDuration);
            yield return null;
        }
        _audioSource.volume = 0f;
        _audioSource.Stop();

        if (clip == null) yield break;

        // Fade in new clip
        _audioSource.clip = clip;
        _audioSource.Play();
        for (float t = 0; t < audioFadeDuration; t += Time.deltaTime)
        {
            _audioSource.volume = Mathf.Lerp(0f, 1f, t / audioFadeDuration);
            yield return null;
        }
        _audioSource.volume = 1f;
    }
}
