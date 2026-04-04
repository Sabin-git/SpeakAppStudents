using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Session lifecycle: start, stop, timer, event dispatch. Singleton.
/// Input: New Input System only (Keyboard.current for Escape / Android back).
/// Exit confirmation pauses Cardboard XR first, then shows a flat phone UI overlay.
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public static event Action OnSessionStart;
    public static event Action<SpeechMetrics> OnSessionEnd;

    public bool IsRunning { get; private set; }
    public float ElapsedSeconds { get; private set; }

    [Header("Session Settings")]
    [Tooltip("Maximum session length in seconds (0 = unlimited)")]
    [SerializeField] private float maxDuration = 300f;

    [Header("UI")]
    [Tooltip("Flat phone-screen panel shown after Cardboard XR is paused")]
    [FormerlySerializedAs("exitConfirmPanel")]
    [SerializeField] private GameObject _exitConfirmPanel;

    [Header("XR")]
    [Tooltip("Optional explicit reference to the Cardboard XR lifecycle helper")]
    [FormerlySerializedAs("xrLifecycleManager")]
    [SerializeField] private XRLifecycleManager _xrLifecycleManager;

    private SpeechMetrics finalMetrics;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_xrLifecycleManager == null)
            _xrLifecycleManager = FindFirstObjectByType<XRLifecycleManager>();
    }

    private void Update()
    {
        bool escapePressed = Keyboard.current != null &&
                             Keyboard.current.escapeKey.wasPressedThisFrame;

        if (escapePressed)
        {
            if (_exitConfirmPanel != null && _exitConfirmPanel.activeSelf)
                CancelExit();
            else if (_exitConfirmPanel != null)
                ShowExitConfirmation();
            else
                LoadMainMenu();

            return;
        }

        if (_exitConfirmPanel != null && _exitConfirmPanel.activeSelf)
            return;

        if (!IsRunning)
            return;

        ElapsedSeconds += Time.deltaTime;

        if (maxDuration > 0f && ElapsedSeconds >= maxDuration)
            StopSession();
    }

    public void ShowExitConfirmation()
    {
        _xrLifecycleManager?.StopXR();

        if (_exitConfirmPanel != null)
            _exitConfirmPanel.SetActive(true);
    }

    public void ConfirmExit()
    {
        HideExitConfirmation();
        LoadMainMenu();
    }

    public void CancelExit()
    {
        HideExitConfirmation();
        _xrLifecycleManager?.StartXR();
    }

    public void StartSession()
    {
        if (IsRunning)
            return;

        ElapsedSeconds = 0f;
        finalMetrics = default;
        IsRunning = true;
        OnSessionStart?.Invoke();
        Debug.Log("[SessionManager] Session started.");
    }

    public void StopSession()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        finalMetrics.sessionTime = ElapsedSeconds;
        OnSessionEnd?.Invoke(finalMetrics);
        Debug.Log($"[SessionManager] Session ended. Duration: {ElapsedSeconds:F1}s");
        SceneManager.LoadScene("Results");
    }

    public void UpdateMetrics(SpeechMetrics metrics)
    {
        finalMetrics = metrics;
    }

    public static void LoadSession() => SceneManager.LoadScene("Session");

    public static void LoadMainMenu() => SceneManager.LoadScene("MainMenu");

    private void HideExitConfirmation()
    {
        if (_exitConfirmPanel != null)
            _exitConfirmPanel.SetActive(false);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug - Start Session")]
    private void EditorStartSession() => StartSession();

    [ContextMenu("Debug - Stop Session")]
    private void EditorStopSession() => StopSession();
#endif
}
