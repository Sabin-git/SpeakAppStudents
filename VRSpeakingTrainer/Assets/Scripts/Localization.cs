using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Lightweight bilingual (en / nl) string lookup, loaded once at app start.
///
/// Reads the Language PlayerPref ("en" or "nl", default "en") and pulls the
/// matching dictionary from StreamingAssets/&lt;lang&gt;.json. UnityWebRequest is
/// required because on Android StreamingAssets is packed inside the .jar —
/// File.ReadAllText fails there. On desktop / Editor the same code path works
/// against the file:// URL produced by Application.streamingAssetsPath.
///
/// Usage:
///   1) Call Localization.Load(this) from MainMenu Awake/Start once per app
///      launch. The coroutine populates the dictionary and logs the key count.
///   2) Anywhere else, call Localization.Get("key_name") — returns the
///      localized string, or the key itself if Load hasn't finished or the
///      key is missing. Falling back to the key (rather than empty string)
///      makes missing keys visible during development.
///
/// Coupled with the rest of the speech pipeline:
///   - SpeechRecognizer reads CurrentLanguage to pick languageCode + mock phrase set
///   - SpeechAnalyzer  picks an English vs Dutch filler array at session start
///   - AudienceRuleEngine picks per-language WPM thresholds at session start
///   - ResultsUI picks per-language WPM ideal band for the WPM score
///
/// The Language PlayerPref is read ONCE at app start (in Load). The same
/// CurrentLanguage value is reused everywhere downstream — do not read the
/// pref per-frame.
/// </summary>
public static class Localization
{
    public const string LangEnglish = "en";
    public const string LangDutch   = "nl";

    /// <summary>The language loaded by the most recent Load() call.
    /// Defaults to LangEnglish until Load has run.</summary>
    public static string CurrentLanguage { get; private set; } = LangEnglish;

    /// <summary>True once a Load() coroutine has completed (success or failure).</summary>
    public static bool   IsLoaded        { get; private set; }

    private static readonly Dictionary<string, string> _strings = new();

    /// <summary>
    /// Kick off async loading of the JSON dictionary for the current Language
    /// PlayerPref. Safe to call multiple times — subsequent calls reload.
    /// Pass any active MonoBehaviour to host the coroutine; MainMenuController
    /// is the canonical caller at app start.
    /// </summary>
    public static void Load(MonoBehaviour host)
    {
        if (host == null) return;
        CurrentLanguage = NormalizeLang(PlayerPrefs.GetString("Language", LangEnglish));
        host.StartCoroutine(LoadCoroutine(CurrentLanguage));
    }

    /// <summary>
    /// Synchronous lookup. Returns the localized string for the key, or the
    /// key itself as a fallback (so missing translations are visible rather
    /// than silently blank).
    /// </summary>
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        return _strings.TryGetValue(key, out string value) ? value : key;
    }

    // ── Coroutine ──────────────────────────────────────────────────────────────

    private static IEnumerator LoadCoroutine(string lang)
    {
        string file = lang + ".json";
        string path = Path.Combine(Application.streamingAssetsPath, file);

        using var req = UnityWebRequest.Get(path);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Localization] Could not load {file}: {req.error}");
            IsLoaded = true;   // mark loaded so Get() doesn't keep waiting; falls back to keys
            yield break;
        }

        _strings.Clear();
        ParseFlatJson(req.downloadHandler.text, _strings);
        IsLoaded = true;

        Debug.Log($"[Localization] Loaded {_strings.Count} keys for language {lang}");
    }

    // ── Flat JSON parser ───────────────────────────────────────────────────────
    // Handles a single-level {"key":"value","key2":"value2"} dictionary with
    // escaped quotes / backslashes inside values. Intentionally not a full
    // JSON parser — keeps zero-allocation strain on Android down. If we ever
    // need nested structure, swap in JsonUtility on a flat wrapper class.

    private static void ParseFlatJson(string json, Dictionary<string, string> into)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        int i = 0;
        int n = json.Length;

        while (i < n)
        {
            // Find the next quoted key
            while (i < n && json[i] != '"') i++;
            if (i >= n) break;
            i++;                             // past opening quote of key
            int keyStart = i;
            while (i < n && json[i] != '"') i++;
            if (i >= n) break;
            string key = json.Substring(keyStart, i - keyStart);
            i++;                             // past closing quote of key

            // Skip whitespace and the colon
            while (i < n && json[i] != ':') i++;
            if (i >= n) break;
            i++;                             // past colon

            // Skip whitespace before the value's opening quote
            while (i < n && json[i] != '"') i++;
            if (i >= n) break;
            i++;                             // past opening quote of value

            // Read the value, respecting backslash escapes
            var sb = new System.Text.StringBuilder();
            while (i < n)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < n)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'r':  sb.Append('\r'); break;
                        default:   sb.Append(next); break;
                    }
                    i += 2;
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
                i++;
            }
            if (i >= n) break;
            i++;                             // past closing quote of value

            into[key] = sb.ToString();
        }
    }

    private static string NormalizeLang(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return LangEnglish;
        return raw == LangDutch ? LangDutch : LangEnglish;
    }
}
