using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Session lifecycle: countdown timer, pause menu, event dispatch. Singleton.
///
/// Duration is set on MainMenu (PlayerPrefs key "SessionDurationSeconds", default 300).
/// Back button → pause menu with three options:
///   Finish Presentation — saves metrics, loads Results
///   Yes (Exit)          — discards metrics, loads MainMenu
///   No (Resume)         — hides menu, restarts XR, resumes countdown
/// Session auto-ends when RemainingSeconds reaches zero.
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public static event Action OnSessionStart;
    public static event Action<SpeechMetrics> OnSessionEnd;

    private void OnEnable()  => SpeechAnalyzer.OnMetricsUpdate += UpdateMetrics;
    private void OnDisable() => SpeechAnalyzer.OnMetricsUpdate -= UpdateMetrics;

    public bool  IsRunning        { get; private set; }
    public float ElapsedSeconds   { get; private set; }
    public float RemainingSeconds => Mathf.Max(0f, _maxDuration - ElapsedSeconds);

    [Header("Session Settings")]
    [Tooltip("Fallback max duration in seconds if PlayerPrefs has no value")]
    [SerializeField] private float defaultDuration = 300f;

    [Header("UI")]
    [Tooltip("World-space pause menu panel — shown after XR is paused")]
    [FormerlySerializedAs("exitConfirmPanel")]
    [SerializeField] private GameObject _pauseMenuPanel;

    [Header("XR")]
    [SerializeField] private XRLifecycleManager _xrLifecycleManager;

    private float        _maxDuration;
    private SpeechMetrics _finalMetrics;
    private bool          _discardMetrics;  // true when exiting without saving

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _maxDuration = PlayerPrefs.GetFloat("SessionDurationSeconds", defaultDuration);

        if (_xrLifecycleManager == null)
            _xrLifecycleManager = FindFirstObjectByType<XRLifecycleManager>();
    }

    private void Start()
    {
        // Auto-start when the Session scene loads.
        // MainMenu saves the chosen duration to PlayerPrefs before loading this scene.
        StartSession();
    }

    private void Update()
    {
        // ── Volume keys — slide navigation stub (Stage 8) ──────────────────────
        // Keyboard.current vol keys are not available on Android; handled via
        // Android volume key callbacks in SlideController (Stage 8).

        // ── Back / Escape ──────────────────────────────────────────────────────
        bool escapePressed = Keyboard.current != null &&
                             Keyboard.current.escapeKey.wasPressedThisFrame;

        if (escapePressed)
        {
            if (_pauseMenuPanel != null && _pauseMenuPanel.activeSelf)
                ResumeSession();             // second back press = dismiss pause menu
            else if (_pauseMenuPanel != null)
                ShowPauseMenu();
            else
                LoadMainMenu();
            return;
        }

        if (_pauseMenuPanel != null && _pauseMenuPanel.activeSelf)
            return;   // timer frozen while pause menu is open

        if (!IsRunning)
            return;

        ElapsedSeconds += Time.deltaTime;

        if (ElapsedSeconds >= _maxDuration)
            StopSession();
    }

    // ── Pause menu ─────────────────────────────────────────────────────────────

    public void ShowPauseMenu()
    {
        _xrLifecycleManager?.StopXR();
        if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(true);
    }

    /// <summary>Finish Presentation — saves metrics, goes to Results.</summary>
    public void FinishPresentation()
    {
        HidePauseMenu();
        StopSession();           // StopSession fires OnSessionEnd and loads Results
    }

    /// <summary>Yes (Exit) — discards metrics, goes to MainMenu.</summary>
    public void ConfirmExit()
    {
        HidePauseMenu();
        LoadMainMenu();
    }

    /// <summary>No (Resume) — hides pause menu, restarts XR.</summary>
    public void ResumeSession()
    {
        HidePauseMenu();
        _xrLifecycleManager?.StartXR();
    }

    // kept for backwards-compat with any existing scene wiring
    public void CancelExit() => ResumeSession();

    private void HidePauseMenu()
    {
        if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(false);
    }

    // ── Session lifecycle ──────────────────────────────────────────────────────

    public void StartSession()
    {
        if (IsRunning) return;
        ElapsedSeconds = 0f;
        _finalMetrics  = default;
        IsRunning      = true;
        OnSessionStart?.Invoke();
        Debug.Log($"[SessionManager] Session started. Duration: {_maxDuration:F0}s");
    }

    public void StopSession()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _finalMetrics.sessionTime = ElapsedSeconds;
        OnSessionEnd?.Invoke(_finalMetrics);
        Debug.Log($"[SessionManager] Session ended. Elapsed: {ElapsedSeconds:F1}s");
        PlayerPrefs.SetFloat("Results_AvgWPM",      _finalMetrics.rollingAvgWpm);
        PlayerPrefs.SetInt  ("Results_FillerCount", _finalMetrics.fillerCount);
        PlayerPrefs.SetFloat("Results_SessionTime", _finalMetrics.sessionTime);
        PlayerPrefs.Save();
        SceneManager.LoadScene("Results");
    }

    public void UpdateMetrics(SpeechMetrics metrics) => _finalMetrics = metrics;

    public static void LoadSession()   => SceneManager.LoadScene("Session");
    public static void LoadMainMenu()  => SceneManager.LoadScene("MainMenu");

#if UNITY_EDITOR
    [ContextMenu("Debug - Start Session")]
    private void EditorStartSession() => StartSession();

    [ContextMenu("Debug - Stop Session")]
    private void EditorStopSession() => StopSession();
#endif
}
