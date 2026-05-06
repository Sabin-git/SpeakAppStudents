using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns the extracted Mixamo diffuse and normal textures to the extracted
/// URP Lit materials, matching by character prefix and UDIM tile order.
///
/// Run via:  VR Trainer → Fix Avatar Materials (URP)
/// Safe to re-run — already-assigned slots are overwritten with the correct value.
/// </summary>
public static class AvatarMaterialFixer
{
    private const string TexturesFolder  = "Assets/Models/Avatars/Textures";
    private const string MaterialsFolder = "Assets/Models/Avatars/Materials";

    public static void FixAvatarMaterials()
    {
        FixNormalMapImportTypes();
        AssignTexturesToMaterials();
    }

    // ── Step 1: mark *_Normal textures as Normal Map type ────────────────────

    private static void FixNormalMapImportTypes()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("Normal")) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) continue;
            importer.textureType = TextureImporterType.NormalMap;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    // ── Step 2: assign diffuse + normal to each material ─────────────────────

    private static void AssignTexturesToMaterials()
    {
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null)
        {
            Debug.LogError("[AvatarMaterialFixer] 'Universal Render Pipeline/Lit' shader not found.");
            return;
        }

        // Build texture lookup:  charId → ( tileNumber → ( "Diffuse"|"Normal" → assetPath ) )
        // e.g. "Ch01" → { 1001 → { "Diffuse" → "…/Ch01_1001_Diffuse.png" } }
        var texMap = new Dictionary<string, SortedDictionary<int, Dictionary<string, string>>>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder }))
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            string file  = System.IO.Path.GetFileNameWithoutExtension(path);
            string[] p   = file.Split('_');
            // Expect:  Ch##  _  ####  _  Type   (e.g. Ch01_1001_Diffuse)
            if (p.Length < 3 || !int.TryParse(p[1], out int tile)) continue;

            string charId  = p[0];           // "Ch01"
            string texType = p[2];           // "Diffuse" | "Normal" | "Specular" | …

            if (!texMap.ContainsKey(charId))
                texMap[charId] = new SortedDictionary<int, Dictionary<string, string>>();
            if (!texMap[charId].ContainsKey(tile))
                texMap[charId][tile] = new Dictionary<string, string>();

            texMap[charId][tile][texType] = path;
        }

        // Build material groups:  charId → sorted list of material asset paths
        var matMap = new Dictionary<string, List<string>>();

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            string name  = System.IO.Path.GetFileNameWithoutExtension(path);
            int    sep   = name.IndexOf('_');
            if (sep < 0) continue;
            string charId = name.Substring(0, sep);   // "Ch01" from "Ch01_body"

            if (!matMap.ContainsKey(charId)) matMap[charId] = new List<string>();
            matMap[charId].Add(path);
        }

        // Assign: material at sorted index i within a character gets UDIM tile at index i
        // (clamped so single-atlas characters work for both body and hair slots)
        int totalAssigned = 0;

        foreach (var kvp in matMap)
        {
            string charId = kvp.Key;
            if (!texMap.TryGetValue(charId, out var tilesDict))
            {
                Debug.LogWarning($"[AvatarMaterialFixer] No textures found for {charId}.");
                continue;
            }

            var tiles    = new List<int>(tilesDict.Keys);   // already sorted ascending
            var matPaths = kvp.Value;
            matPaths.Sort(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < matPaths.Count; i++)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPaths[i]);
                if (mat == null) continue;

                // Ensure URP Lit shader
                if (mat.shader != urpShader) mat.shader = urpShader;
                mat.SetColor("_BaseColor", Color.white);

                // Pick the tile for this material slot (clamp to available tiles)
                var texSet = tilesDict[tiles[Mathf.Min(i, tiles.Count - 1)]];

                if (texSet.TryGetValue("Diffuse", out string diffPath))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
                    if (tex != null)
                    {
                        mat.SetTexture("_BaseMap", tex);
                        mat.SetTexture("_MainTex",  tex);
                    }
                }

                if (texSet.TryGetValue("Normal", out string normPath))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
                    if (tex != null)
                    {
                        mat.SetTexture("_BumpMap", tex);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                }

                EditorUtility.SetDirty(mat);
                totalAssigned++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AvatarMaterialFixer] Assigned textures to {totalAssigned} material(s).");
    }
}
