using UnityEngine;
using TMPro;

/// <summary>
/// Updates the screen-space countdown timer HUD.
/// Attach to a Screen Space - Overlay Canvas in the Session scene.
/// The canvas does not move with the head — it stays fixed to the screen.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("HUD References")]
    [Tooltip("TextMeshProUGUI label that shows remaining time as MM:SS")]
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
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning)
            return;

        float remaining = SessionManager.Instance.RemainingSeconds;
        int   minutes   = Mathf.FloorToInt(remaining / 60f);
        int   seconds   = Mathf.FloorToInt(remaining % 60f);

        if (timerLabel != null)
            timerLabel.text = $"{minutes:D2}:{seconds:D2}";
    }

    private void HandleSessionStart()
    {
        if (timerLabel != null) timerLabel.gameObject.SetActive(true);
    }

    private void HandleSessionEnd(SpeechMetrics _)
    {
        if (timerLabel != null) timerLabel.gameObject.SetActive(false);
    }
}
