using UnityEditor;
using UnityEngine;

/// <summary>
/// Enables Loop Time on every FBX animation clip inside Assets/Animations/Clips/.
/// Mixamo exports have loop time off by default — this fixes all of them in one go.
///
/// Run via:  VR Trainer → Fix Animation Loop Time
/// Safe to re-run — files already set to loop are skipped.
/// </summary>
public static class AnimationImportFixer
{
    private const string ClipsFolder = "Assets/Animations/Clips";

    public static void FixLoopTime()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ClipsFolder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[AnimationImportFixer] No FBX files found in {ClipsFolder}.");
            return;
        }

        int totalFixed  = 0;
        int skipped     = 0;

        foreach (string guid in guids)
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            var    importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            // Mixamo FBX may have no explicitly-defined clips — fall back to defaults
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            // ModelImporterClipAnimation is a struct — must modify by index, not foreach
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime) continue;
                clips[i].loopTime = true;
                changed = true;
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                totalFixed++;
            }
            else
            {
                skipped++;
            }
        }

        Debug.Log($"[AnimationImportFixer] Done — {totalFixed} clip(s) set to loop, {skipped} already looping.");
    }
}
