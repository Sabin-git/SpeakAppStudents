using UnityEngine;
using TMPro;

/// <summary>
/// Updates the screen-space countdown timer HUD.
/// Fixed to screen — does not move with head.
/// Listens to SessionManager.OnSessionStart / OnSessionEnd.
/// Implementation: Stage 3.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("HUD References")]
    [Tooltip("TextMeshProUGUI label showing remaining time MM:SS")]
    [SerializeField] private TextMeshProUGUI timerLabel;

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

    private void Update()
    {
        // TODO Stage 3: read SessionManager.Instance remaining time, format as MM:SS,
        // update timerLabel.text each frame.
    }

    private void HandleSessionStart()  { /* TODO Stage 3 */ }
    private void HandleSessionEnd(SpeechMetrics m) { /* TODO Stage 3 */ }
}
