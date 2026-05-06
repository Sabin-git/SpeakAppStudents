using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sorts every FBX in Assets/Animations/Clips/ into a subfolder that matches
/// its state prefix (the part before the first underscore).
///
/// Looping states  →  Clips/Engaged/   Clips/Neutral/   Clips/Distracted/   Clips/Restless/
/// One-shot reactions  →  Clips/Reactions/
///
/// Run via:  VR Trainer → Organize Animation Clips
/// Safe to re-run — files already in the right place are skipped.
/// </summary>
public static class ClipOrganizer
{
    private const string ClipsRoot = "Assets/Animations/Clips";

    // Looping state prefixes → their subfolder name
    private static readonly Dictionary<string, string> LoopingPrefixes = new()
    {
        { "Engaged",    "Engaged"    },
        { "Neutral",    "Neutral"    },
        { "Distracted", "Distracted" },
        { "Restless",   "Restless"   },
    };

    // One-shot reaction prefixes → all go into Reactions/
    private static readonly HashSet<string> ReactionPrefixes = new()
    {
        "GazeReact",
        "DistractionReaction",
        "ThinkingReaction",
    };

    public static void OrganizeClips()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ClipsRoot });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[ClipOrganizer] No FBX files found in {ClipsRoot}.");
            return;
        }

        int moved   = 0;
        int skipped = 0;
        int unknown = 0;

        foreach (string guid in guids)
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            string filename = System.IO.Path.GetFileName(path);         // e.g. "Neutral_Idle.fbx"
            string dir      = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');

            // Parse prefix
            string nameNoExt = System.IO.Path.GetFileNameWithoutExtension(path);
            int    sep       = nameNoExt.IndexOf('_');
            if (sep < 1)
            {
                Debug.LogWarning($"[ClipOrganizer] '{filename}' has no underscore — skipped.");
                unknown++;
                continue;
            }
            string prefix = nameNoExt.Substring(0, sep);

            // Determine target subfolder
            string targetFolder;
            if (LoopingPrefixes.TryGetValue(prefix, out string subfolder))
                targetFolder = $"{ClipsRoot}/{subfolder}";
            else if (ReactionPrefixes.Contains(prefix))
                targetFolder = $"{ClipsRoot}/Reactions";
            else
            {
                Debug.LogWarning($"[ClipOrganizer] '{filename}' has unknown prefix '{prefix}' — skipped.");
                unknown++;
                continue;
            }

            // Already in the right place?
            if (dir == targetFolder)
            {
                skipped++;
                continue;
            }

            // Create subfolder if needed
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                string parent = System.IO.Path.GetDirectoryName(targetFolder).Replace('\\', '/');
                string child  = System.IO.Path.GetFileName(targetFolder);
                AssetDatabase.CreateFolder(parent, child);
            }

            // Move the file (and its .meta)
            string dest  = $"{targetFolder}/{filename}";
            string error = AssetDatabase.MoveAsset(path, dest);
            if (string.IsNullOrEmpty(error))
            {
                moved++;
            }
            else
            {
                Debug.LogError($"[ClipOrganizer] Could not move '{filename}': {error}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[ClipOrganizer] Done — moved {moved}, already correct {skipped}, skipped unknown {unknown}.");
    }
}
