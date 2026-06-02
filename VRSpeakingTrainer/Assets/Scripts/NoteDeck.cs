using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-memory deck of free-form note cards for the light PPTX feature (Feature C).
///
/// Instead of uploading slide images, the user types short notes into the PPTX
/// panel on the MainMenu — each note is a single free-form text string in
/// whatever format they like (paragraphs, dashes, bullet glyphs). A per-note
/// character limit (<see cref="MaxNoteLength"/>) keeps each note readable on the
/// lectern. The active note's text renders on the lectern during the session.
///
/// Lifetime: in memory only. The deck PERSISTS across multiple sessions within
/// one app run (static state survives scene loads), and is CLEARED when the app
/// process dies — on device when the app is killed, in the Editor on domain
/// reload (entering/exiting Play Mode). It is NOT written to PlayerPrefs or disk.
/// This matches the Feature C brief: "notes persist… but not once the app is
/// restarted."
///
/// Pure static data class, no MonoBehaviour — like SpeechMetrics and
/// Localization it does not count against the runtime-script cap.
///
/// Consumers:
///   - MainMenuController — the notes editor inside the PPTX panel (create /
///     edit / navigate / delete / reset notes).
///   - SlideController    — renders the active note on the lectern and advances
///     notes on Volume Up/Down.
/// </summary>
public static class NoteDeck
{
    /// <summary>Max characters per note. Enforced by the editor input field and
    /// chosen as a sensible ceiling for lectern readability.</summary>
    public const int MaxNoteLength = 200;

    // Each entry is one note's free-form text (may contain newlines the user typed).
    public static readonly List<string> Notes = new();

    /// <summary>Index of the note currently shown on the lectern / edited in the panel.</summary>
    public static int CurrentIndex;

    public static int NoteCount => Notes.Count;

    // Sample is seeded exactly once per app run. After that an emptied deck stays
    // empty (re-seed explicitly via ResetToSample), so deleting all notes and
    // returning to the menu doesn't silently bring the sample back.
    private static bool _seededOnce;

    /// <summary>True if any note has non-whitespace text — used by SlideController
    /// to decide whether to show notes on the lectern.</summary>
    public static bool HasContent
    {
        get
        {
            foreach (var n in Notes)
                if (!string.IsNullOrWhiteSpace(n)) return true;
            return false;
        }
    }

    /// <summary>Seeds a small sample deck the first time only, so testers see
    /// content on the lectern immediately without having to type anything.</summary>
    public static void LoadSampleIfEmpty()
    {
        if (_seededOnce) return;
        _seededOnce = true;
        if (Notes.Count > 0) return;
        SeedSample();
    }

    /// <summary>Clears the deck and reloads the sample notes. Backs the editor's
    /// "Reset to sample" button.</summary>
    public static void ResetToSample()
    {
        Notes.Clear();
        SeedSample();
    }

    private static void SeedSample()
    {
        Notes.Add("Introduction\n- Greet the audience\n- Outline today's three topics");
        Notes.Add("Main point\n- Key evidence or example\n- Why it matters to listeners");
        Notes.Add("Conclusion\n- Recap the three main points\n- Thank the audience");
        CurrentIndex = 0;
    }

    /// <summary>Appends a new empty note and selects it.</summary>
    public static void AddNote()
    {
        Notes.Add(string.Empty);
        CurrentIndex = Notes.Count - 1;
    }

    /// <summary>Removes the note at index and keeps CurrentIndex in range.</summary>
    public static void RemoveNote(int index)
    {
        if (index < 0 || index >= Notes.Count) return;
        Notes.RemoveAt(index);
        CurrentIndex = Notes.Count == 0 ? 0 : Mathf.Clamp(CurrentIndex, 0, Notes.Count - 1);
    }

    /// <summary>Sets a note's text, clamped to MaxNoteLength as a safety net (the
    /// editor input field also enforces the limit).</summary>
    public static void SetNote(int index, string text)
    {
        if (index < 0 || index >= Notes.Count) return;
        text ??= string.Empty;
        if (text.Length > MaxNoteLength) text = text.Substring(0, MaxNoteLength);
        Notes[index] = text;
    }

    /// <summary>Reads a note's text, or "" if out of range.</summary>
    public static string GetNote(int index)
    {
        if (index < 0 || index >= Notes.Count) return string.Empty;
        return Notes[index];
    }
}
