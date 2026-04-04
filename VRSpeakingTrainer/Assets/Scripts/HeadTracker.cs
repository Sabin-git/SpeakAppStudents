using UnityEngine;

/// <summary>
/// Reads XR camera orientation every frame, classifies gaze zone, accumulates
/// time per zone, detects per-avatar eye contact. Emits HeadMetrics.
/// Implementation: Stage 6.
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

    // Stage 6 — filled by AudienceController once avatars are spawned
    [HideInInspector] public Transform[] avatarTransforms;

    private HeadMetrics _metrics;

    private void Update()
    {
        // TODO Stage 6: classify zone from xrCamera.forward, accumulate time,
        // detect per-avatar gaze, fire OnHeadMetricsUpdated.
    }
}
