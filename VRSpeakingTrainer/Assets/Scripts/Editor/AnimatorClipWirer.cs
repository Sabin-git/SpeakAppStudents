using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Scans Assets/Animations/Clips/ for FBX files named  State_Description.fbx
/// and wires them into AudienceAnimator as per-state 1D Blend Trees.
///
/// Valid state prefixes:  Engaged  Neutral  Distracted  Restless  GazeReact
///
/// Run via:  VR Trainer → Wire Animation Clips
///
/// How it works:
///   • Base Layer state machine gets a "Variant" float parameter.
///   • Each state's Motion is replaced with a 1D Blend Tree over Variant.
///   • Clips are placed at thresholds 0, 1, 2, … so setting Variant to an
///     integer plays exactly one clip with no blending.
///   • All AudienceMember components in the open scene have their
///     _variantCounts array updated so they know how many clips each state has.
///   • GazeReact clips are collected and logged but not yet wired (the
///     GazeReact trigger fires a one-shot — handled separately).
/// </summary>
public static class AnimatorClipWirer
{
    private const string ClipsFolder      = "Assets/Animations/Clips";
    private const string ControllerPath   = "Assets/Animations/AudienceAnimator.controller";
    private const string VariantParamName = "Variant";

    // The four looping states that map to AudienceState enum values 0-3
    private static readonly string[] StateNames =
        { "Engaged", "Neutral", "Distracted", "Restless" };

    public static void WireClips()
    {
        // ── 1. Scan clips folder ──────────────────────────────────────────────

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ClipsFolder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[AnimatorClipWirer] No FBX files found in {ClipsFolder}.");
            return;
        }

        // Looping state prefixes that get wired into the blend trees
        var validPrefixes  = new HashSet<string>(StateNames) { "GazeReact", "DistractionReaction" };
        // Reaction prefixes that are one-shots — don't warn if they don't match a looping state
        var reactionPrefixes = new HashSet<string> { "GazeReact", "DistractionReaction", "ThinkingReaction" };
        var clipsByState   = new Dictionary<string, List<AnimationClip>>();
        var badFiles       = new List<string>();

        foreach (string guid in guids)
        {
            string path     = AssetDatabase.GUIDToAssetPath(guid);
            string filename = System.IO.Path.GetFileNameWithoutExtension(path);
            int    sep      = filename.IndexOf('_');

            if (sep < 1)
            {
                badFiles.Add($"  {filename}  (no underscore — can't determine state)");
                continue;
            }

            string prefix = filename.Substring(0, sep);
            if (reactionPrefixes.Contains(prefix) && !validPrefixes.Contains(prefix))
            {
                // Known future reaction type — skip silently, will be wired later
                continue;
            }
            if (!validPrefixes.Contains(prefix))
            {
                badFiles.Add($"  {filename}  (unknown prefix '{prefix}')");
                continue;
            }

            // Extract the AnimationClip from the FBX (skip internal preview clips)
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            if (clip == null)
            {
                Debug.LogWarning($"[AnimatorClipWirer] No AnimationClip found in {filename}.fbx — skipped.");
                continue;
            }

            if (!clipsByState.ContainsKey(prefix)) clipsByState[prefix] = new List<AnimationClip>();
            clipsByState[prefix].Add(clip);
        }

        // Report unrecognised files so the user can fix them
        if (badFiles.Count > 0)
        {
            Debug.LogWarning("[AnimatorClipWirer] These files were SKIPPED — fix their prefix to match a valid state name:\n"
                             + string.Join("\n", badFiles));
        }

        // GazeReact clips are collected but not yet wired into the state machine
        if (clipsByState.TryGetValue("GazeReact", out var gazeClips))
        {
            Debug.Log($"[AnimatorClipWirer] Found {gazeClips.Count} GazeReact clip(s) — " +
                      "these are not auto-wired yet (the GazeReact trigger needs a dedicated Animator layer).");
        }

        // ── 1b. Sitting clips from Assets/Models/Avatars/ override standing clips ─
        // Full-body seated animations replace any standing placeholders for the same state.
        // Each state gets its own canonical seated clip — no shared / forced overrides.
        var sittingMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Neutral-Fidgeting", "Neutral"    },
            { "Distracted",        "Distracted" },
            { "Engaged",           "Engaged"    },
            { "Restless",          "Restless"   },
        };

        string[] avatarGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/Avatars" });
        foreach (string avatarGuid in avatarGuids)
        {
            string avatarPath = AssetDatabase.GUIDToAssetPath(avatarGuid);
            string avatarFile = System.IO.Path.GetFileNameWithoutExtension(avatarPath);

            if (avatarFile.StartsWith("Ch") || avatarFile == "character" ||
                avatarFile.Contains("old") || avatarFile.Contains("buggy")) continue;

            if (!sittingMap.TryGetValue(avatarFile, out string stateName)) continue;

            var clip = AssetDatabase.LoadAllAssetsAtPath(avatarPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            if (clip == null)
            {
                Debug.LogWarning($"[AnimatorClipWirer] No AnimationClip in {avatarFile}.fbx — skipped.");
                continue;
            }

            clipsByState[stateName] = new List<AnimationClip> { clip };
            Debug.Log($"[AnimatorClipWirer] Sitting clip '{clip.name}' assigned to state '{stateName}'.");
        }

        // ── 2. Load the animator controller ──────────────────────────────────

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AnimatorClipWirer] Controller not found at {ControllerPath}");
            return;
        }

        Undo.RecordObject(controller, "Wire Animation Clips");

        // ── 3. Ensure "Variant" float parameter exists ────────────────────────

        bool hasVariant = controller.parameters.Any(p => p.name == VariantParamName);
        if (!hasVariant)
        {
            controller.AddParameter(VariantParamName, AnimatorControllerParameterType.Float);
            Debug.Log("[AnimatorClipWirer] Added 'Variant' float parameter to controller.");
        }

        // ── 4. Wire blend trees into the Base Layer state machine ─────────────

        var sm = controller.layers[0].stateMachine;

        // Track how many variants each state has so AudienceMember can randomise
        var variantCounts = new int[StateNames.Length];

        foreach (var childState in sm.states)
        {
            var   state     = childState.state;
            int   stateIdx  = System.Array.IndexOf(StateNames, state.name);
            if (stateIdx < 0) continue;   // not one of our 4 states

            if (!clipsByState.TryGetValue(state.name, out var clips) || clips.Count == 0)
            {
                Debug.LogWarning($"[AnimatorClipWirer] No clips found for state '{state.name}' — motion unchanged.");
                continue;
            }

            // Remove old blend tree sub-asset to avoid orphans on re-runs
            if (state.motion is BlendTree oldBt)
            {
                AssetDatabase.RemoveObjectFromAsset(oldBt);
                Object.DestroyImmediate(oldBt, true);
            }

            // Create the new 1D blend tree
            var bt = new BlendTree
            {
                name                  = state.name + "_Variants",
                hideFlags             = HideFlags.HideInHierarchy,
                blendType             = BlendTreeType.Simple1D,
                blendParameter        = VariantParamName,
                useAutomaticThresholds = false,
            };

            for (int i = 0; i < clips.Count; i++)
                bt.AddChild(clips[i], i);   // threshold = clip index

            AssetDatabase.AddObjectToAsset(bt, controller);
            Undo.RegisterCreatedObjectUndo(bt, "Wire Animation Clips");

            state.motion          = bt;
            variantCounts[stateIdx] = clips.Count;

            EditorUtility.SetDirty(state);
            Debug.Log($"[AnimatorClipWirer] '{state.name}' wired with {clips.Count} clip(s).");
        }

        // ── 4b. Wire DistractionReaction clips ───────────────────────────────────

        clipsByState.TryGetValue("DistractionReaction", out var reactClips);
        int reactCount = reactClips?.Count ?? 0;

        if (reactCount > 0)
        {
            if (!controller.parameters.Any(p => p.name == "DistractionReact"))
                controller.AddParameter("DistractionReact", AnimatorControllerParameterType.Trigger);
            if (!controller.parameters.Any(p => p.name == "DistractionReactVariant"))
                controller.AddParameter("DistractionReactVariant", AnimatorControllerParameterType.Float);

            AnimatorState restlessState = sm.states
                .Select(cs => cs.state).FirstOrDefault(s => s.name == "Restless");

            AnimatorState reactState = sm.states
                .Select(cs => cs.state).FirstOrDefault(s => s.name == "DistractionReacting");
            if (reactState == null)
            {
                reactState = sm.AddState("DistractionReacting");
                reactState.hideFlags = HideFlags.HideInHierarchy;
                if (!AssetDatabase.Contains(reactState))
                {
                    AssetDatabase.AddObjectToAsset(reactState, controller);
                    Undo.RegisterCreatedObjectUndo(reactState, "Wire Animation Clips");
                }
            }

            if (reactState.motion is BlendTree oldReactBt)
            {
                AssetDatabase.RemoveObjectFromAsset(oldReactBt);
                Object.DestroyImmediate(oldReactBt, true);
            }
            var reactBt = new BlendTree
            {
                name                   = "DistractionReacting_Variants",
                hideFlags              = HideFlags.HideInHierarchy,
                blendType              = BlendTreeType.Simple1D,
                blendParameter         = "DistractionReactVariant",
                useAutomaticThresholds = false,
            };
            for (int i = 0; i < reactClips.Count; i++)
                reactBt.AddChild(reactClips[i], i);
            AssetDatabase.AddObjectToAsset(reactBt, controller);
            Undo.RegisterCreatedObjectUndo(reactBt, "Wire Animation Clips");
            reactState.motion = reactBt;
            EditorUtility.SetDirty(reactState);

            if (restlessState != null)
            {
                foreach (var t in restlessState.transitions
                    .Where(t => t.destinationState?.name == "DistractionReacting").ToArray())
                    restlessState.RemoveTransition(t);
                var toReact = restlessState.AddTransition(reactState);
                toReact.hasExitTime      = false;
                toReact.duration         = 0.15f;
                toReact.hasFixedDuration = true;
                toReact.AddCondition(AnimatorConditionMode.If, 0f, "DistractionReact");
                EditorUtility.SetDirty(restlessState);

                foreach (var t in reactState.transitions
                    .Where(t => t.destinationState?.name == "Restless").ToArray())
                    reactState.RemoveTransition(t);
                var toRestless = reactState.AddTransition(restlessState);
                toRestless.hasExitTime       = true;
                toRestless.exitTime          = 0.85f;
                toRestless.duration          = 0.25f;
                toRestless.hasFixedDuration  = true;
                EditorUtility.SetDirty(reactState);
            }

            Debug.Log($"[AnimatorClipWirer] Wired {reactCount} DistractionReaction clip(s).");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── 5. Push variant counts to every AudienceMember in the open scene ──

        var members = Object.FindObjectsByType<AudienceMember>(FindObjectsSortMode.None);
        foreach (var m in members)
        {
            Undo.RecordObject(m, "Wire Animation Clips");
            m.SetVariantCounts(variantCounts);
            m.SetDistractionReactCount(reactCount);
            EditorUtility.SetDirty(m);
        }

        Debug.Log($"[AnimatorClipWirer] Done. Updated {members.Length} AudienceMember(s) in scene.\n" +
                  $"Variant counts: Engaged={variantCounts[0]}  Neutral={variantCounts[1]}  " +
                  $"Distracted={variantCounts[2]}  Restless={variantCounts[3]}  " +
                  $"DistractionReact={reactCount}");
    }
}
