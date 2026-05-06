using UnityEditor;
using UnityEngine;

/// <summary>
/// Single entry-point that runs every setup step in the correct order:
///   1. Fix avatar materials (URP textures)
///   2. Organise animation clips into subfolders
///   3. Enable loop time on all FBX clips
///   4. Remove obsolete masked layers, disable root motion
///   5. Wire sitting clips from Assets/Models/Avatars/ into blend trees
///   6. Randomise avatar models across all seats
///   7. Disable root motion on newly placed avatars
///
/// Run via:  VR Trainer → Setup All
/// </summary>
public static class VRTrainerSetup
{
    [MenuItem("VR Trainer/Setup All")]
    public static void SetupAll()
    {
        Debug.Log("[VRTrainerSetup] === Starting full setup ===");

        AvatarMaterialFixer.FixAvatarMaterials();
        ClipOrganizer.OrganizeClips();
        AnimationImportFixer.FixLoopTime();
        AvatarLayerSetup.SetupLayers();          // removes mask/lower-body layer, disables root motion
        AnimatorClipWirer.WireClips();           // wires sitting clips into blend trees
        ClassroomBuilder.RandomizeAvatarModels();
        AvatarLayerSetup.FixSeatedPose();        // disables root motion on newly placed avatars

        Debug.Log("[VRTrainerSetup] === Setup complete ===");
    }
}
