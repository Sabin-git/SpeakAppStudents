using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Ensures the AudienceAnimator Base Layer has no avatar mask so full-body
/// sitting animations drive the whole rig. Also removes the obsolete
/// "Seated Lower Body" layer if present, and disables root motion on every
/// Animator in the scene so sitting clips never move the avatar vertically.
///
/// Run via:  VR Trainer → Setup All
/// </summary>
public static class AvatarLayerSetup
{
    private const string ControllerPath = "Assets/Animations/AudienceAnimator.controller";

    public static void SetupLayers()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AvatarLayerSetup] Controller not found at {ControllerPath}");
            return;
        }

        Undo.RecordObject(controller, "Setup Animation Layers");

        // Remove mask from Base Layer — full-body sitting animations drive the whole rig
        var layers = controller.layers;
        if (layers.Length > 0 && layers[0].avatarMask != null)
        {
            layers[0].avatarMask = null;
            controller.layers    = layers;
            Debug.Log("[AvatarLayerSetup] Removed avatar mask from Base Layer.");
        }

        // Remove obsolete Seated Lower Body layer if it was added by a previous setup run
        RemoveLayerByName(controller, "Seated Lower Body");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        FixSeatedPose();
    }

    public static void FixSeatedPose()
    {
        int count = 0;
        foreach (var anim in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(anim, "Fix Seated Pose");
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(anim);
            count++;
        }
        Debug.Log($"[AvatarLayerSetup] Root motion disabled on {count} Animator(s) in scene.");
    }

    private static void RemoveLayerByName(AnimatorController controller, string layerName)
    {
        var layerList = controller.layers.ToList();
        int idx = layerList.FindIndex(l => l.name == layerName);
        if (idx < 0) return;

        // Clean up sub-asset state machines to avoid orphans in the .controller file
        var sm = layerList[idx].stateMachine;
        if (sm != null && AssetDatabase.Contains(sm))
        {
            foreach (var cs in sm.states)
                if (AssetDatabase.Contains(cs.state))
                    AssetDatabase.RemoveObjectFromAsset(cs.state);
            AssetDatabase.RemoveObjectFromAsset(sm);
        }

        layerList.RemoveAt(idx);
        controller.layers = layerList.ToArray();
        Debug.Log($"[AvatarLayerSetup] Removed '{layerName}' layer from controller.");
    }
}
