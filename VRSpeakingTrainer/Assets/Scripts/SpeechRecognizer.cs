using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Captures mic audio in chunks and sends each to Google Cloud Speech-to-Text REST API.
/// Fires OnTranscriptionResult(text, isFinal) on the main thread.
/// API key loaded from StreamingAssets/config.json — never hardcoded.
/// </summary>
public class SpeechRecognizer : MonoBehaviour
{
    public static event Action<string, bool> OnTranscriptionResult;

    [Header("Settings")]
    [Tooltip("How many seconds of audio to send per API call")]
    [SerializeField] private int chunkSeconds = 5;
    [SerializeField] private int sampleRate   = 16000;

    private const string ConfigFile  = "config.json";
    private const string ApiEndpoint = "https://speech.googleapis.com/v1/speech:recognize?key=";

    private string    _apiKey;
    private AudioClip _micClip;
    private string    _micDevice;
    private bool      _isCapturing;
    private int       _lastSamplePos;
    private float     _chunkTimer;

    private void Start()
    {
        SessionManager.OnSessionStart += OnSessionStart;
        SessionManager.OnSessionEnd   += OnSessionEnd;
        StartCoroutine(LoadApiKey());
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
        // UnityWebRequest is required for StreamingAssets on Android (files are inside a .jar)
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

        // Session may have already started while the key was loading — start capture now.
        if (SessionManager.Instance != null && SessionManager.Instance.IsRunning)
            OnSessionStart();
    }

    // ── Session lifecycle ─────────────────────────────────────────────────────

    private void OnSessionStart()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("[SpeechRecognizer] Cannot start — no API key.");
            return;
        }

#if UNITY_EDITOR
        _micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
#else
        _micDevice = null; // Android default mic
#endif
        _micClip       = Microphone.Start(_micDevice, true, 10, sampleRate);
        _lastSamplePos = 0;
        _chunkTimer    = 0f;
        _isCapturing   = true;
        Debug.Log("[SpeechRecognizer] Capture started.");
    }

    private void OnSessionEnd(SpeechMetrics _) => StopCapture();

    private void StopCapture()
    {
        if (!_isCapturing) return;
        _isCapturing = false;
        // Flush remaining audio before stopping
        float[] remaining = ExtractSamples();
        if (remaining.Length > 0) StartCoroutine(SendChunk(remaining));
        Microphone.End(_micDevice);
        _micClip = null;
    }

    // ── Per-frame chunking ────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isCapturing) return;
        _chunkTimer += Time.deltaTime;
        if (_chunkTimer >= chunkSeconds)
        {
            _chunkTimer = 0f;
            StartCoroutine(SendChunk(ExtractSamples()));
        }
    }

    private float[] ExtractSamples()
    {
        int currentPos = Microphone.GetPosition(_micDevice);
        int clipLen    = _micClip.samples;
        int count      = currentPos >= _lastSamplePos
            ? currentPos - _lastSamplePos
            : clipLen - _lastSamplePos + currentPos; // ring-buffer wrap

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

        // Convert float samples [-1,1] to LINEAR16 PCM bytes (little-endian)
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
        // Match both "key":"value" and "key": "value" (Google API uses spaces)
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
