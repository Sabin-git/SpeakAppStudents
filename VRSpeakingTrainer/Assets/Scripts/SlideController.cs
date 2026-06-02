using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the lectern display. In the light PPTX feature (Feature C) the source
/// is the in-memory <see cref="NoteDeck"/>: the active note's free-form text is
/// shown on the NotesPanel, and Arrow / PageUp-Down advance/retreat through the
/// notes (read via the new Input System — the project's active input handler).
/// The slide-image panel is hidden in notes mode (the light feature has no slide
/// images), and an optional white background quad can be shown behind the text
/// for readability.
///
/// Navigation input: hardware volume buttons can't be read through the keyboard
/// API. On Android we instead capture
/// them by POLLING the media-volume stream (AudioManager) and inferring the
/// direction from the change — volume-up = next note, volume-down = previous.
/// To keep headroom in both directions the media volume is pinned to a mid level
/// during a notes session (and restored at session end). This needs NO manifest
/// or activity changes. Caveats: the system volume overlay still flashes briefly
/// on each press (the key event isn't consumed), and the in-session media volume
/// is fixed at the reset level. The arrow / PageUp-Down keys (keyboard or a
/// Bluetooth presenter remote) also work everywhere, including the Editor.
///
/// If the deck has no content, the lectern stays blank — no errors, just an
/// empty notes panel — so a session with no notes still runs fine.
///
/// The note text is whatever the user typed (paragraphs, dashes, etc.) and may
/// contain newlines; TextMeshPro renders these natively. The project's
/// "no \n in UI strings" rule targets static localization/HUD labels, not a
/// dynamic notes board, which is the intended exception.
/// </summary>
public class SlideController : MonoBehaviour
{
    [Header("Lectern Display")]
    [Tooltip("Renderer on the SlidesPanel quad (left, larger). Hidden in notes mode — the light PPTX feature has no slide images.")]
    [SerializeField] private Renderer slidesPanel;
    [Tooltip("TMPro text on the NotesPanel. Shows the active note in notes mode. Position/size this in the Editor to fill the lectern surface.")]
    [SerializeField] private TMPro.TextMeshPro notesPanel;
    [Tooltip("Optional white background quad shown behind the notes in notes mode (leave empty to skip). Enable/disable mirrors notes mode.")]
    [SerializeField] private Renderer notesBackground;

    // Keyboard nav uses the new Input System (the project's active input handler):
    // Right Arrow / Page Down = next note, Left Arrow / Page Up = previous. These
    // also cover most Bluetooth presenter remotes. Keys are fixed (not serialized)
    // because the new Input System addresses controls by name, not by KeyCode.

    [Header("Android volume-button navigation")]
    [Tooltip("On device, capture the hardware volume buttons to flip notes (Up = next, Down = previous). Works by polling the media-volume stream — no manifest/activity changes. The system volume overlay still flashes briefly on each press.")]
    [SerializeField] private bool useVolumeButtons = true;
    [Tooltip("Media volume is pinned to this fraction of max while a notes session is active, so there's always headroom to detect up vs down. Also sets the in-session audio level.")]
    [SerializeField, Range(0.1f, 0.9f)] private float volumeResetFraction = 0.5f;
    [Tooltip("How often (seconds) to poll the media volume on device. 0.05 = 20 Hz: responsive and cheap.")]
    [SerializeField] private float volumePollInterval = 0.05f;

    public int CurrentNote => NoteDeck.CurrentIndex;
    public int NoteCount   => NoteDeck.NoteCount;

    private bool _notesMode;

#if UNITY_ANDROID && !UNITY_EDITOR
    private const int StreamMusic = 3; // AudioManager.STREAM_MUSIC
    private AndroidJavaObject _audioManager;
    private int   _maxStreamVolume;
    private int   _resetVolume;
    private int   _lastVolume;
    private int   _originalVolume;
    private float _volPollTimer;
#endif

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        SessionManager.OnSessionStart += HandleSessionStart;
        SessionManager.OnSessionEnd   += HandleSessionEnd;
    }

    private void OnDisable()
    {
        SessionManager.OnSessionStart -= HandleSessionStart;
        SessionManager.OnSessionEnd   -= HandleSessionEnd;
        TeardownVolumeCapture();
    }

    private void Start()
    {
        // Apply the initial state immediately so the lectern is correct even if
        // the session is already running (e.g. entering Play directly in Session).
        ApplyNotesMode();
    }

    // Keeps the volume-poll tuning sane and also references these fields on all
    // platforms (they're otherwise only read inside Android-only #if blocks).
    private void OnValidate()
    {
        volumePollInterval  = Mathf.Max(0.02f, volumePollInterval);
        volumeResetFraction = Mathf.Clamp(volumeResetFraction, 0.1f, 0.9f);
    }

    private void HandleSessionStart()
    {
        // Always start a session on the first note.
        NoteDeck.CurrentIndex = 0;
        ApplyNotesMode();

        // Capture the hardware volume buttons for the duration of the session,
        // but only when there are notes to navigate.
        if (_notesMode) InitVolumeCapture();
    }

    private void HandleSessionEnd(SpeechMetrics _)
    {
        // Restore the user's media volume; the deck stays in memory for next time.
        TeardownVolumeCapture();
    }

    // ── Display ────────────────────────────────────────────────────────────────

    private void ApplyNotesMode()
    {
        _notesMode = NoteDeck.HasContent;

        // Hide the slide-image panel in notes mode; show the optional white board.
        if (slidesPanel     != null) slidesPanel.enabled     = !_notesMode;
        if (notesBackground != null) notesBackground.enabled = _notesMode;

        RenderCurrentNote();
    }

    private void RenderCurrentNote()
    {
        if (notesPanel == null) return;

        notesPanel.text = (_notesMode && NoteDeck.NoteCount > 0)
            ? NoteDeck.GetNote(NoteDeck.CurrentIndex)
            : string.Empty;
    }

    // ── Navigation ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_notesMode) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.rightArrowKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame)
            {
                ShowNote(NoteDeck.CurrentIndex + 1);
                return;
            }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame)
            {
                ShowNote(NoteDeck.CurrentIndex - 1);
                return;
            }
        }

        PollVolumeButtons();
    }

    private void ShowNote(int index)
    {
        if (NoteDeck.NoteCount == 0) return;
        NoteDeck.CurrentIndex = Mathf.Clamp(index, 0, NoteDeck.NoteCount - 1);
        RenderCurrentNote();
    }

    // ── Android volume-button capture ──────────────────────────────────────────
    // Pure C# via AndroidJavaObject — no Java plugin, no manifest/activity change.
    // We poll the media-volume index; when it changes we infer the direction and
    // pin it back to a mid value so there's always headroom both ways. All of this
    // compiles out on non-Android platforms (the Editor uses the keyboard keys).

    private void InitVolumeCapture()
    {
        if (!useVolumeButtons) return;   // read on all platforms (no unused-field warning)
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_audioManager != null) return;
        try
        {
            using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            _audioManager = activity.Call<AndroidJavaObject>("getSystemService", "audio");

            _maxStreamVolume = _audioManager.Call<int>("getStreamMaxVolume", StreamMusic);
            _originalVolume  = _audioManager.Call<int>("getStreamVolume",   StreamMusic);
            _resetVolume     = Mathf.Clamp(
                Mathf.RoundToInt(_maxStreamVolume * volumeResetFraction), 1, _maxStreamVolume - 1);

            SetStreamVolume(_resetVolume);
            _lastVolume   = _resetVolume;
            _volPollTimer = 0f;
            Debug.Log($"[SlideController] Volume-button nav active (max {_maxStreamVolume}, pinned {_resetVolume}).");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SlideController] Volume-button capture init failed: {e.Message}");
            _audioManager = null;
        }
#endif
    }

    private void PollVolumeButtons()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_audioManager == null) return;

        _volPollTimer += Time.unscaledDeltaTime;
        if (_volPollTimer < volumePollInterval) return;
        _volPollTimer = 0f;

        int current;
        try { current = _audioManager.Call<int>("getStreamVolume", StreamMusic); }
        catch { return; }

        if (current == _lastVolume) return;

        if (current > _lastVolume) ShowNote(NoteDeck.CurrentIndex + 1);
        else                       ShowNote(NoteDeck.CurrentIndex - 1);

        // Re-centre so the next press in either direction is detectable.
        SetStreamVolume(_resetVolume);
        _lastVolume = _resetVolume;
#endif
    }

    private void SetStreamVolume(int index)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_audioManager == null) return;
        try
        {
            // flags = 0 → our own set is silent (no UI, no beep). The user's
            // hardware press is still handled by the OS, which flashes the overlay.
            _audioManager.Call("setStreamVolume", StreamMusic, index, 0);
        }
        catch (System.Exception e)
        {
            // e.g. SecurityException while Do-Not-Disturb is active — disable so
            // we don't spam, and fall back to the keyboard / remote keys.
            Debug.LogWarning($"[SlideController] setStreamVolume failed, disabling volume nav: {e.Message}");
            _audioManager.Dispose();
            _audioManager = null;
        }
#endif
    }

    private void TeardownVolumeCapture()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_audioManager == null) return;
        try { _audioManager.Call("setStreamVolume", StreamMusic, _originalVolume, 0); }
        catch { /* ignore — restoring volume is best-effort */ }
        _audioManager.Dispose();
        _audioManager = null;
#endif
    }
}
