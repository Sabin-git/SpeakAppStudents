using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Captures mic audio in chunks and sends each to Google Cloud Speech-to-Text REST API.
/// Fires OnTranscriptionResult(text, isFinal) on the main thread.
/// API key loaded from StreamingAssets/config.json — never hardcoded.
///
/// Mock mode: when enabled, fires fake transcripts on a timer instead of hitting the API.
/// Use this during development to avoid consuming API quota.
/// </summary>
public class SpeechRecognizer : MonoBehaviour
{
    public static event Action<string, bool> OnTranscriptionResult;

    [Header("Settings")]
    [Tooltip("How many seconds of audio to send per API call")]
    [SerializeField] private int chunkSeconds = 5;
    [SerializeField] private int sampleRate   = 16000;

    [Header("Mock Mode - Only use for debugging to save API calls")]
    [Tooltip("Fire fake transcripts locally — no API calls. Use during development.")]
    [SerializeField] private bool mockMode = false;
    [Tooltip("Seconds between mock transcript events")]
    [SerializeField] private float mockInterval = 4f;
    [Tooltip("Suppress transcript events while mock mode is on. Use when forcing audience state manually via AudienceRuleEngine debug controls.")]
    [SerializeField] private bool mockMuted = false;

    // Mock phrases cycle through in order, covering a range of speech patterns.
    //
    // Word count per phrase is calibrated to the default mockInterval of 4 seconds.
    // Steady-state WPM ≈ (words / 4s) × 60. Target ranges:
    //   Engaged  (110–160 WPM) → 7–11 words per phrase
    //   Distracted high (>180) → 13+ words per phrase
    //   Distracted low  (<80)  → ≤5  words per phrase
    //   Restless (fillers ≥ 4) → any length, 4+ filler words
    private static readonly string[] MockPhrases =
    {
        // ── Engaged zone (7–11 words) ──────────────────────────────────────────
        // ~120–165 WPM → should reach Engaged after a few phrases

        "Good morning, I will present our key findings today.",           // 9 words
        "The study involved two hundred participants from three universities.",  // 9
        "Our objective was to improve virtual learning environments.",     // 8
        "Results show significant improvement in the VR group.",           // 9
        "The methodology used a randomised control trial design.",         // 9
        "Both groups completed pre and post assessments carefully.",       // 9
        "The evidence strongly supports our initial research hypothesis.", // 8
        "These findings have clear implications for professional training.", // 8
        "Let me walk you through each of the key findings.",              // 10
        "The data speaks clearly and these numbers are compelling.",       // 10
        "We conclude that immersive training improves retention significantly.", // 9
        "Thank you for your attention and consideration today.",           // 9

        // ── Fast speech (14–18 words) ──────────────────────────────────────────
        // ~210–270 WPM sustained → triggers Distracted after ~20s of high WPM

        "The second point is very important and we need to cover it quickly before the next slide.", // 17
        "I am trying to fit as much content as possible because there is so much to cover in limited time.", // 19

        // ── Filler-heavy (triggers Restless via fillerCount ≥ 4 in 30s) ────────
        // Rule 1 fires before WPM matters, so word count is less critical here

        "So um basically the uh results were like you know interesting.",  // um, uh, like, you know, basically, so = 6 fillers
        "Um so basically what we found was uh like you know patterns.",    // um, so, basically, uh, like, you know = 6 fillers
        "Right so uh basically um the conclusion is like somewhat unclear.", // right, so, uh, basically, um, like = 6 fillers
        "Um like basically so you know uh right this is complex.",         // um, like, basically, so, you know, uh, right = 7 fillers

        // ── Mixed fillers + content (2–3 fillers → borderline Distracted) ──────

        "The first finding um shows participants performed like better.",   // um, like = 2 fillers, ~9 words
        "So basically this means we need to rethink you know training.",   // so, basically, you know = 3 fillers, ~10 words

        // ── Slow speech (≤5 words) ─────────────────────────────────────────────
        // ~45–75 WPM → triggers Distracted (wpm < 80)

        "The results were interesting.",    // 4 words → ~60 WPM
        "We found learning improved.",      // 4 words → ~60 WPM
        "Data shows clear trends.",         // 4 words → ~60 WPM
        "Conclusions remain preliminary.",  // 3 words → ~45 WPM

        // ── Short phrases (gap between them tests pause detection) ────────────

        "Next point.",   // 2 words — long gap after these triggers lastPauseDuration
        "Moving on.",    // 2 words

        // ── Recovery — back to Engaged after bad stretch ──────────────────────

        "Returning to the main argument, the evidence is clear.",          // 9 words
        "In conclusion this research opens new avenues for investigation.", // 9
        "The practical applications of this work are significant.",        // 8
        "I am happy to answer any questions you may have.",                // 9
    };

    private const string ConfigFile  = "config.json";
    private const string ApiEndpoint = "https://speech.googleapis.com/v1/speech:recognize?key=";

    private string    _apiKey;
    private AudioClip _micClip;
    private string    _micDevice;
    private bool      _isCapturing;
    private int       _lastSamplePos;
    private float     _chunkTimer;

    // Mock state
    private bool  _mockRunning;
    private float _mockTimer;
    private int   _mockPhraseIndex;

    private void Start()
    {
        SessionManager.OnSessionStart += OnSessionStart;
        SessionManager.OnSessionEnd   += OnSessionEnd;

        if (!mockMode)
            StartCoroutine(LoadApiKey());
        else
            Debug.Log("[SpeechRecognizer] Mock mode enabled — API will not be called.");
    }

    private void OnDestroy()
    {
        SessionManager.OnSessionStart -= OnSessionStart;
        SessionManager.OnSessionEnd   -= OnSessionEnd;
        StopCapture();
    }

    // ── API key loading ───────────────────────────────────────────────────────

    private IEnumerator LoadApiKey()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, ConfigFile);
        using var req = UnityWebRequest.Get(path);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[SpeechRecognizer] Could not load config.json: {req.error}");
            yield break;
        }

        _apiKey = ParseJsonString(req.downloadHandler.text, "google_api_key");
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("[SpeechRecognizer] google_api_key missing in config.json.");
            yield break;
        }

        Debug.Log("[SpeechRecognizer] API key loaded.");

        if (SessionManager.Instance != null && SessionManager.Instance.IsRunning)
            OnSessionStart();
    }

    // ── Session lifecycle ─────────────────────────────────────────────────────

    private void OnSessionStart()
    {
        if (mockMode)
        {
            _mockPhraseIndex = 0;
            _mockTimer       = 0f;
            _mockRunning     = true;
            Debug.Log("[SpeechRecognizer] Mock capture started.");
            return;
        }

        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("[SpeechRecognizer] Cannot start — no API key.");
            return;
        }

#if UNITY_EDITOR
        _micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
#else
        _micDevice = null;
#endif
        _micClip       = Microphone.Start(_micDevice, true, 10, sampleRate);
        _lastSamplePos = 0;
        _chunkTimer    = 0f;
        _isCapturing   = true;
        Debug.Log("[SpeechRecognizer] Capture started.");
    }

    private void OnSessionEnd(SpeechMetrics _)
    {
        _mockRunning = false;
        StopCapture();
    }

    private void StopCapture()
    {
        if (!_isCapturing) return;
        _isCapturing = false;
        float[] remaining = ExtractSamples();
        if (remaining.Length > 0) StartCoroutine(SendChunk(remaining));
        Microphone.End(_micDevice);
        _micClip = null;
    }

    // ── Per-frame update ──────────────────────────────────────────────────────

    private void Update()
    {
        if (mockMode)
        {
            UpdateMock();
            return;
        }

        if (!_isCapturing) return;
        _chunkTimer += Time.deltaTime;
        if (_chunkTimer >= chunkSeconds)
        {
            _chunkTimer = 0f;
            StartCoroutine(SendChunk(ExtractSamples()));
        }
    }

    // ── Mock transcript emission ──────────────────────────────────────────────

    private void UpdateMock()
    {
        if (!_mockRunning) return;

        _mockTimer += Time.deltaTime;
        if (_mockTimer < mockInterval) return;

        _mockTimer = 0f;
        string phrase = MockPhrases[_mockPhraseIndex % MockPhrases.Length];
        _mockPhraseIndex++;

        if (mockMuted) return;
        Debug.Log($"[SpeechRecognizer] Mock: \"{phrase}\"");
        OnTranscriptionResult?.Invoke(phrase, true);
    }

    // ── Mic sample extraction ─────────────────────────────────────────────────

    private float[] ExtractSamples()
    {
        int currentPos = Microphone.GetPosition(_micDevice);
        int clipLen    = _micClip.samples;
        int count      = currentPos >= _lastSamplePos
            ? currentPos - _lastSamplePos
            : clipLen - _lastSamplePos + currentPos;

        if (count <= 0) return Array.Empty<float>();

        float[] samples = new float[count];
        if (currentPos >= _lastSamplePos)
        {
            _micClip.GetData(samples, _lastSamplePos);
        }
        else
        {
            float[] p1 = new float[clipLen - _lastSamplePos];
            float[] p2 = new float[currentPos];
            _micClip.GetData(p1, _lastSamplePos);
            if (currentPos > 0) _micClip.GetData(p2, 0);
            Array.Copy(p1, 0, samples, 0, p1.Length);
            Array.Copy(p2, 0, samples, p1.Length, p2.Length);
        }

        _lastSamplePos = currentPos;
        return samples;
    }

    // ── API call ──────────────────────────────────────────────────────────────

    private IEnumerator SendChunk(float[] samples)
    {
        if (samples.Length == 0) yield break;

        byte[] pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
            pcm[i * 2]     = (byte)(s & 0xFF);
            pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }

        string body =
            $"{{\"config\":{{\"encoding\":\"LINEAR16\"," +
            $"\"sampleRateHertz\":{sampleRate}," +
            $"\"languageCode\":\"en-US\"," +
            $"\"model\":\"latest_long\"}}," +
            $"\"audio\":{{\"content\":\"{Convert.ToBase64String(pcm)}\"}}}}";

        using var req = new UnityWebRequest(ApiEndpoint + _apiKey, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[SpeechRecognizer] API error: {req.error} | {req.downloadHandler.text}");
            yield break;
        }

        string transcript = ParseJsonString(req.downloadHandler.text, "transcript");
        if (!string.IsNullOrWhiteSpace(transcript))
            OnTranscriptionResult?.Invoke(transcript.Trim(), true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ParseJsonString(string json, string key)
    {
        foreach (var search in new[] { $"\"{key}\":\"", $"\"{key}\": \"" })
        {
            int start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) continue;
            start += search.Length;
            int end = json.IndexOf('"', start);
            if (end >= 0) return json.Substring(start, end - start);
        }
        return string.Empty;
    }
}
