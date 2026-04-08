using UnityEngine;

/// <summary>
/// Reads XR camera orientation every frame, classifies gaze zone, accumulates
/// time per zone, detects per-avatar eye contact. Emits HeadMetrics every frame.
/// Zone boundaries are Inspector-configurable.
/// </summary>
public class HeadTracker : MonoBehaviour
{
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
    [Tooltip("Dead-zone buffer in degrees between zone boundaries")]
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
        // Precompute zone vertical boundaries from Inspector values
        _audienceVertMin = -(lecternVerticalDeg - deadzoneBufDeg);  // e.g. -27°
        _lecternVertMax  = _audienceVertMin - deadzoneBufDeg;        // e.g. -32°
        _lecternVertMin  = -(lecternVerticalDeg + deadzoneBufDeg);   // e.g. -37°
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
        _metrics.gazedAvatarIndex = DetectGazedAvatar();

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
