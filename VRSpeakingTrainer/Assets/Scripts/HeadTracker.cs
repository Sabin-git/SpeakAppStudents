using UnityEngine;

/// <summary>
/// Reads XR camera orientation every frame, classifies gaze zone, accumulates
/// time per zone, detects per-avatar eye contact. Emits HeadMetrics every frame.
/// Zone boundaries are Inspector-configurable.
///
/// Per-avatar gaze accumulation (Task 4): each frame the gazed-avatar index is
/// valid (0–9), we add Time.deltaTime into _timeOnAvatar[index]. At session end
/// those 10 floats are persisted to Results_TimeOnAvatar_0..9 PlayerPrefs so the
/// Results scene can render the gaze heat map widget. The per-avatar accumulator
/// is reset at HandleSessionStart so a replayed session doesn't double-count.
/// </summary>
public class HeadTracker : MonoBehaviour
{
    // Task 4 — per-avatar gaze time accumulator. Indexed by avatarIndex (0–9).
    // Read once per frame (Update), written 10 PlayerPrefs at session end.
    // Not [SerializeField] — wiring is purely automatic; exposing it would
    // confuse the Inspector and isn't useful for tuning.
    private readonly float[] _timeOnAvatar = new float[10];

    public static event System.Action<HeadMetrics> OnHeadMetricsUpdated;

    [Header("Zone Boundaries (degrees)")]
    [Tooltip("Horizontal half-angle for Audience zone")]
    [SerializeField] private float audienceHorizontalDeg = 45f;
    [Tooltip("Max degrees above horizontal for Audience zone")]
    [SerializeField] private float audienceVerticalMaxDeg = 20f;
    [Tooltip("Degrees below horizontal for Lectern zone centre")]
    [SerializeField] private float lecternVerticalDeg = 32f;
    [Tooltip("Horizontal half-angle for Lectern zone")]
    [SerializeField] private float lecternHorizontalDeg = 30f;
    [Tooltip("Vertical half-range (degrees) above/below lecternVerticalDeg that still counts as Lectern. " +
             "Increase to make the lectern zone more forgiving of head pitch. Audience zone wins where they overlap.")]
    [SerializeField] private float lecternVerticalRangeDeg = 12f;
    [Tooltip("Dead-zone buffer in degrees between Audience-lower-edge and Lectern-upper-edge. With the wider " +
             "lecternVerticalRangeDeg the two zones usually overlap, in which case Audience priority wins and this is unused.")]
    [SerializeField] private float deadzoneBufDeg = 5f;
    [Tooltip("Cone half-angle for per-avatar gaze detection")]
    [SerializeField] private float avatarGazeDeg = 15f;

    [Header("Scene References")]
    [Tooltip("XR camera (child of XR Rig)")]
    [SerializeField] private Transform xrCamera;
    [Tooltip("AudienceTarget empty GO (centre of avatar rows)")]
    [SerializeField] private Transform audienceTarget;
    [Tooltip("LecternTarget empty GO (centre of lectern surface)")]
    [SerializeField] private Transform lecternTarget;

    [Header("Editor Debug")]
    [Tooltip("Force a specific zone in Editor Play Mode (bypasses camera reading)")]
    [SerializeField] private bool debugOverrideZone;
    [SerializeField] private GazeZone debugZone = GazeZone.Audience;

    // Filled by AudienceController once avatars are located
    [HideInInspector] public Transform[] avatarTransforms;

    // Precomputed vertical boundaries (degrees, negative = below horizontal)
    private float _audienceVertMin;  // lower edge of Audience zone
    private float _lecternVertMax;   // upper edge of Lectern zone
    private float _lecternVertMin;   // lower edge of Lectern zone

    private HeadMetrics _metrics;
    private bool _isRunning;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Precompute zone vertical boundaries from Inspector values.
        // Lectern band is centred on lecternVerticalDeg and spans
        // ±lecternVerticalRangeDeg around it — e.g. centre 32° + range 12° =
        // lectern from -20° to -44° (24° tall). Audience-zone check has priority
        // (see ClassifyZone), so any overlap between the bands is resolved in
        // favour of Audience.
        _audienceVertMin = -(lecternVerticalDeg - deadzoneBufDeg);   // e.g. -27°
        _lecternVertMax  = -(lecternVerticalDeg - lecternVerticalRangeDeg); // e.g. -20°
        _lecternVertMin  = -(lecternVerticalDeg + lecternVerticalRangeDeg); // e.g. -44°
    }

    private void OnEnable()
    {
        SessionManager.OnSessionStart += HandleSessionStart;
        SessionManager.OnSessionEnd   += HandleSessionEnd;
    }

    private void OnDisable()
    {
        SessionManager.OnSessionStart -= HandleSessionStart;
        SessionManager.OnSessionEnd   -= HandleSessionEnd;
    }

    private void HandleSessionStart()
    {
        _metrics   = default;
        _isRunning = true;

        // Task 4 — zero out the per-avatar accumulator so a replayed session
        // starts clean. Without this, the heat map would show cumulative gaze
        // across every session since app launch.
        for (int i = 0; i < _timeOnAvatar.Length; i++) _timeOnAvatar[i] = 0f;

        // Apply gaze zone override from dev panel.
        int zoneOverride = PlayerPrefs.GetInt("Dev_ForceGazeZone", -1);
        if (zoneOverride >= 0)
        {
            debugOverrideZone = true;
            debugZone         = (GazeZone)Mathf.Clamp(zoneOverride, 0, 2);
        }
        else
        {
            debugOverrideZone = false;
        }
    }

    private void HandleSessionEnd(SpeechMetrics _)
    {
        PlayerPrefs.SetFloat("Results_TimeOnAudience", _metrics.timeOnAudience);
        PlayerPrefs.SetFloat("Results_TimeOnLectern",  _metrics.timeOnLectern);
        PlayerPrefs.SetFloat("Results_TimeOnOther",    _metrics.timeOnOther);

        // Task 4 — write per-avatar gaze totals for the Results heat map.
        // Always writes all 10 keys (zero for avatars that were never gazed
        // at) so the Results scene can read a consistent set every time.
        for (int i = 0; i < _timeOnAvatar.Length; i++)
            PlayerPrefs.SetFloat($"Results_TimeOnAvatar_{i}", _timeOnAvatar[i]);

        string timesCsv = string.Join(", ", System.Array.ConvertAll(_timeOnAvatar, t => t.ToString("F2")));
        int    wiredCount = avatarTransforms != null ? avatarTransforms.Length : 0;
        string camStatus  = xrCamera != null ? "set" : "NULL";
        Debug.Log($"[HeadTracker] End-of-session per-avatar gaze times: [{timesCsv}] " +
                  $"(avatarTransforms wired = {wiredCount}, xrCamera = {camStatus})");

        PlayerPrefs.Save();
        _isRunning = false;
    }

    // ── Per-frame update ───────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isRunning) return;

        GazeZone zone = debugOverrideZone ? debugZone : ClassifyZone();

        // Accumulate time (Deadzone contributes to nothing)
        switch (zone)
        {
            case GazeZone.Audience: _metrics.timeOnAudience += Time.deltaTime; break;
            case GazeZone.Lectern:  _metrics.timeOnLectern  += Time.deltaTime; break;
            case GazeZone.Other:    _metrics.timeOnOther    += Time.deltaTime; break;
        }

        _metrics.currentZone    = zone;
        _metrics.isFacingCrowd  = zone == GazeZone.Audience;
        // Only detect per-avatar gaze while the user is actually in the
        // Audience zone. Otherwise the 15° geometric cone can still catch a
        // back-row avatar when the user pitches into the lectern or deadzone,
        // and the per-avatar timer would tick up beyond the audience-zone
        // total — see bug observed during user testing.
        _metrics.gazedAvatarIndex = zone == GazeZone.Audience ? DetectGazedAvatar() : -1;

        // Task 4 — accumulate per-avatar gaze time. Guard the bounds check
        // explicitly here rather than relying on _timeOnAvatar.Length, so the
        // intent ("10 avatars, indices 0–9") is obvious at the call site.
        int gazed = _metrics.gazedAvatarIndex;
        if (gazed >= 0 && gazed < _timeOnAvatar.Length)
            _timeOnAvatar[gazed] += Time.deltaTime;

        OnHeadMetricsUpdated?.Invoke(_metrics);
    }

    // ── Zone classification ────────────────────────────────────────────────────

    private GazeZone ClassifyZone()
    {
        if (xrCamera == null) return GazeZone.Other;

        Vector3 fwd = xrCamera.forward;

        // Signed horizontal angle around Y axis (0° = straight forward)
        float hAngle = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        // Vertical angle (positive = above horizontal)
        float vAngle = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;

        // Audience: roughly horizontal, within ±audienceHorizontalDeg
        if (Mathf.Abs(hAngle) <= audienceHorizontalDeg
            && vAngle > _audienceVertMin
            && vAngle < audienceVerticalMaxDeg)
            return GazeZone.Audience;

        // Lectern: looking down ~lecternVerticalDeg, within ±lecternHorizontalDeg
        if (Mathf.Abs(hAngle) <= lecternHorizontalDeg
            && vAngle <= _lecternVertMax
            && vAngle >= _lecternVertMin)
            return GazeZone.Lectern;

        // Deadzone: the 5° gap between Audience lower and Lectern upper
        if (vAngle <= _audienceVertMin && vAngle > _lecternVertMax)
            return GazeZone.Deadzone;

        return GazeZone.Other;
    }

    // ── Per-avatar gaze detection ──────────────────────────────────────────────

    private int DetectGazedAvatar()
    {
        if (xrCamera == null || avatarTransforms == null) return -1;

        Vector3 fwd = xrCamera.forward;
        for (int i = 0; i < avatarTransforms.Length; i++)
        {
            if (avatarTransforms[i] == null) continue;
            Vector3 toAvatar = (avatarTransforms[i].position - xrCamera.position).normalized;
            if (Vector3.Angle(fwd, toAvatar) <= avatarGazeDeg)
                return i;
        }
        return -1;
    }
}
