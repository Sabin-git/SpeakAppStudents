using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor-only helper for building the classroom environment.
///
/// Menu items (all under "VR Trainer/"):
///   Build All              — room shell + desk grid + avatar placeholders in one shot
///   Build Room Shell       — floor, ceiling, four walls, blackboard, lectern
///   Build Classroom Layout — 2×5 desk grid with AvatarAnchor points
///   Build Avatar Placeholders — capsule+sphere stand-ins at every AvatarAnchor
///   Clear Room / Clear Classroom / Clear Avatars — individual teardown
///
/// Desk layout (top-down, presenter at origin facing +Z):
///
///   [D00][D01][D02][D03][D04]   row 0   z = 2.0
///   [D05][D06][D07][D08][D09]   row 1   z = 4.0
///
/// Lecturer position: (0, 0, 0), facing +Z.
/// Blackboard: back wall (z = RoomBackZ), decorative.
/// Lectern: directly in front of presenter, angled top surface ~32° below horizontal.
/// </summary>
public static class ClassroomBuilder
{
    // ── Desk layout ───────────────────────────────────────────────────────────
    private const int   Rows         = 2;
    private const int   Cols         = 5;
    private const float RowSpacing   = 2.0f;
    private const float ColSpacing   = 1.5f;
    private const float FirstRowZ    = 2.0f;
    private const float DeskTopY     = 0.75f;
    private const float AnchorBehind = 0.35f;

    // ── Room geometry ─────────────────────────────────────────────────────────
    private const float RoomHalfWidth = 4.2f;   // x: -4.2 to +4.2
    private const float RoomFrontZ    = -1.5f;  // front wall (behind presenter)
    private const float RoomBackZ     =  8.0f;  // back wall
    private const float RoomHeight    =  3.0f;
    private const float WallThick     =  0.2f;

    // ── Lectern geometry ──────────────────────────────────────────────────────
    // Base box: centred at (0, 0.55, 0.25)
    private const float LecternBaseY        = 0.55f;
    private const float LecternBaseZ        = 0.25f;
    private const float LecternBaseHeight   = 1.10f;
    // Angled top surface: ~32° below horizontal, sits on top of base
    private const float LecternSurfaceTiltDeg = 32f;

    // ── Menu: Build All ───────────────────────────────────────────────────────

    [MenuItem("VR Trainer/Build All")]
    public static void BuildAll()
    {
        BuildRoom();
        BuildClassroom();
        BuildAvatarPlaceholders();
    }

    [MenuItem("VR Trainer/Clear All")]
    public static void ClearAll()
    {
        ClearAvatarPlaceholders();
        ClearClassroom();
        ClearRoom();
    }

    // ── Menu: Room shell ──────────────────────────────────────────────────────

    [MenuItem("VR Trainer/Build Room Shell %#r")]
    public static void BuildRoom()
    {
        if (GameObject.Find("ClassroomRoom") != null)
        {
            Debug.LogWarning("[ClassroomBuilder] 'ClassroomRoom' already exists. " +
                             "Run 'VR Trainer → Clear Room' first.");
            return;
        }

        EnsureMaterialsFolder();

        var root = new GameObject("ClassroomRoom");
        Undo.RegisterCreatedObjectUndo(root, "Build Room Shell");

        float depth   = RoomBackZ - RoomFrontZ;
        float centreZ = (RoomFrontZ + RoomBackZ) * 0.5f;
        float width   = RoomHalfWidth * 2f;
        float halfH   = RoomHeight * 0.5f;

        // Floor
        CreateBox(root, "Floor",
            new Vector3(0f, -WallThick * 0.5f, centreZ),
            new Vector3(width, WallThick, depth),
            GetOrCreateMat("Floor", new Color(0.68f, 0.62f, 0.48f)));

        // Ceiling
        CreateBox(root, "Ceiling",
            new Vector3(0f, RoomHeight + WallThick * 0.5f, centreZ),
            new Vector3(width, WallThick, depth),
            GetOrCreateMat("Ceiling", new Color(0.94f, 0.94f, 0.90f)));

        // Front wall (behind presenter, -Z)
        CreateBox(root, "WallFront",
            new Vector3(0f, halfH, RoomFrontZ - WallThick * 0.5f),
            new Vector3(width + WallThick * 2f, RoomHeight + WallThick * 2f, WallThick),
            GetOrCreateMat("Wall", new Color(0.86f, 0.82f, 0.74f)));

        // Back wall (+Z)
        CreateBox(root, "WallBack",
            new Vector3(0f, halfH, RoomBackZ + WallThick * 0.5f),
            new Vector3(width + WallThick * 2f, RoomHeight + WallThick * 2f, WallThick),
            GetOrCreateMat("Wall", new Color(0.86f, 0.82f, 0.74f)));

        // Left wall (-X)
        CreateBox(root, "WallLeft",
            new Vector3(-RoomHalfWidth - WallThick * 0.5f, halfH, centreZ),
            new Vector3(WallThick, RoomHeight + WallThick * 2f, depth + WallThick * 2f),
            GetOrCreateMat("Wall", new Color(0.86f, 0.82f, 0.74f)));

        // Right wall (+X)
        CreateBox(root, "WallRight",
            new Vector3(RoomHalfWidth + WallThick * 0.5f, halfH, centreZ),
            new Vector3(WallThick, RoomHeight + WallThick * 2f, depth + WallThick * 2f),
            GetOrCreateMat("Wall", new Color(0.86f, 0.82f, 0.74f)));

        // Blackboard on BACK wall — decorative only
        CreateBox(root, "Blackboard",
            new Vector3(0f, 1.65f, RoomBackZ - 0.02f),
            new Vector3(4.0f, 1.4f, 0.04f),
            GetOrCreateMat("Blackboard", new Color(0.15f, 0.22f, 0.18f)));

        // ── Lectern ───────────────────────────────────────────────────────────
        BuildLectern(root);

        Selection.activeGameObject = root;
        Debug.Log("[ClassroomBuilder] Room shell built.");
    }

    private static void BuildLectern(GameObject roomRoot)
    {
        var lectern = new GameObject("Lectern");
        lectern.transform.SetParent(roomRoot.transform, false);
        lectern.transform.localPosition = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(lectern, "Build Room Shell");

        var baseMat = GetOrCreateMat("WoodDark", new Color(0.36f, 0.24f, 0.14f));

        // Base box
        var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseBox.name = "LecternBase";
        baseBox.transform.SetParent(lectern.transform, false);
        baseBox.transform.localPosition = new Vector3(0f, LecternBaseY, LecternBaseZ);
        baseBox.transform.localScale    = new Vector3(0.60f, LecternBaseHeight, 0.45f);
        baseBox.GetComponent<Renderer>().sharedMaterial = baseMat;
        Undo.RegisterCreatedObjectUndo(baseBox, "Build Room Shell");

        // Angled top surface (~32° tilt, tilted toward presenter)
        // Top of base is at y = LecternBaseHeight = 1.10
        float topY = LecternBaseHeight + 0.05f;
        var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "LecternSurface";
        surface.transform.SetParent(lectern.transform, false);
        surface.transform.localPosition = new Vector3(0f, topY, LecternBaseZ);
        surface.transform.localRotation = Quaternion.Euler(-LecternSurfaceTiltDeg, 0f, 0f);
        surface.transform.localScale    = new Vector3(0.58f, 0.04f, 0.40f);
        surface.GetComponent<Renderer>().sharedMaterial = baseMat;
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(surface, "Build Room Shell");

        // SlidesPanel — left half of surface, larger (slide preview)
        // Positioned slightly above surface so it doesn't z-fight
        var slidesMat = GetOrCreateMat("SlidesPanel", new Color(1f, 1f, 1f));
        var slides = GameObject.CreatePrimitive(PrimitiveType.Quad);
        slides.name = "SlidesPanel";
        slides.transform.SetParent(surface.transform, false);
        slides.transform.localPosition = new Vector3(-0.22f, 0.6f, 0f);
        slides.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        slides.transform.localScale    = new Vector3(0.55f, 0.70f, 1f);
        slides.GetComponent<Renderer>().sharedMaterial = slidesMat;
        Object.DestroyImmediate(slides.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(slides, "Build Room Shell");

        // NotesPanel — right half of surface, smaller (speaker notes)
        var notesMat = GetOrCreateMat("NotesPanel", new Color(0.95f, 0.95f, 0.85f));
        var notes = GameObject.CreatePrimitive(PrimitiveType.Quad);
        notes.name = "NotesPanel";
        notes.transform.SetParent(surface.transform, false);
        notes.transform.localPosition = new Vector3(0.30f, 0.6f, 0f);
        notes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        notes.transform.localScale    = new Vector3(0.35f, 0.70f, 1f);
        notes.GetComponent<Renderer>().sharedMaterial = notesMat;
        Object.DestroyImmediate(notes.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(notes, "Build Room Shell");

        // LecternTarget — empty GO at centre of display surface (used by HeadTracker)
        var lecternTarget = new GameObject("LecternTarget");
        lecternTarget.transform.SetParent(surface.transform, false);
        lecternTarget.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        lecternTarget.transform.localRotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(lecternTarget, "Build Room Shell");

        Debug.Log("[ClassroomBuilder] Lectern built with SlidesPanel, NotesPanel, and LecternTarget.");
    }

    [MenuItem("VR Trainer/Clear Room")]
    public static void ClearRoom()
    {
        var go = GameObject.Find("ClassroomRoom");
        if (go == null) { Debug.LogWarning("[ClassroomBuilder] No 'ClassroomRoom' found."); return; }
        Undo.DestroyObjectImmediate(go);
        Debug.Log("[ClassroomBuilder] Room cleared.");
    }

    // ── Menu: Desk grid ───────────────────────────────────────────────────────

    [MenuItem("VR Trainer/Build Classroom Layout %#b")]
    public static void BuildClassroom()
    {
        if (GameObject.Find("Classroom") != null)
        {
            Debug.LogWarning("[ClassroomBuilder] A 'Classroom' GameObject already exists. " +
                             "Run 'VR Trainer → Clear Classroom' first.");
            return;
        }

        var root = new GameObject("Classroom");
        Undo.RegisterCreatedObjectUndo(root, "Build Classroom");

        for (int row = 0; row < Rows; row++)
        for (int col = 0; col < Cols; col++)
        {
            float x     = (col - (Cols - 1) * 0.5f) * ColSpacing;
            float z     = FirstRowZ + row * RowSpacing;
            int   index = row * Cols + col;
            CreateDesk(root.transform, index, new Vector3(x, 0f, z));
        }

        // AudienceTarget — empty GO at centre of both audience rows (used by HeadTracker)
        float audienceCentreZ = FirstRowZ + (Rows - 1) * RowSpacing * 0.5f;
        var audienceTarget = new GameObject("AudienceTarget");
        audienceTarget.transform.SetParent(root.transform, false);
        audienceTarget.transform.localPosition = new Vector3(0f, 1.2f, audienceCentreZ);
        Undo.RegisterCreatedObjectUndo(audienceTarget, "Build Classroom");

        Selection.activeGameObject = root;
        Debug.Log($"[ClassroomBuilder] Created {Rows * Cols} desks with AvatarAnchor points and AudienceTarget.");
    }

    [MenuItem("VR Trainer/Clear Classroom")]
    public static void ClearClassroom()
    {
        var go = GameObject.Find("Classroom");
        if (go == null) { Debug.LogWarning("[ClassroomBuilder] No 'Classroom' found."); return; }
        Undo.DestroyObjectImmediate(go);
        Debug.Log("[ClassroomBuilder] Classroom cleared.");
    }

    // ── Menu: Avatar placeholders ─────────────────────────────────────────────

    [MenuItem("VR Trainer/Build Avatar Placeholders")]
    public static void BuildAvatarPlaceholders()
    {
        var classroom = GameObject.Find("Classroom");
        if (classroom == null)
        {
            Debug.LogWarning("[ClassroomBuilder] No 'Classroom' found. " +
                             "Run 'VR Trainer → Build Classroom Layout' first.");
            return;
        }

        EnsureMaterialsFolder();
        var bodyMat = GetOrCreateMat("AvatarBody", new Color(0.33f, 0.43f, 0.55f));
        var headMat = GetOrCreateMat("AvatarHead", new Color(0.87f, 0.72f, 0.58f));

        int count = 0;
        foreach (Transform t in classroom.GetComponentsInChildren<Transform>())
        {
            if (t.name != "AvatarAnchor") continue;

            // Seated torso
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "AvatarBody_Placeholder";
            body.transform.SetParent(t, false);
            body.transform.localPosition = new Vector3(0f, 0.50f, 0f);
            body.transform.localScale    = new Vector3(0.35f, 0.38f, 0.30f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(body, "Build Avatar Placeholders");

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "AvatarHead_Placeholder";
            head.transform.SetParent(t, false);
            head.transform.localPosition = new Vector3(0f, 1.10f, 0f);
            head.transform.localScale    = new Vector3(0.22f, 0.22f, 0.22f);
            head.GetComponent<Renderer>().sharedMaterial = headMat;
            Object.DestroyImmediate(head.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(head, "Build Avatar Placeholders");

            count++;
        }

        Debug.Log($"[ClassroomBuilder] Avatar placeholders added at {count} anchor points.");
    }

    [MenuItem("VR Trainer/Clear Avatars")]
    public static void ClearAvatarPlaceholders()
    {
        var classroom = GameObject.Find("Classroom");
        if (classroom == null) { Debug.LogWarning("[ClassroomBuilder] No 'Classroom' found."); return; }

        int count = 0;
        foreach (Transform t in classroom.GetComponentsInChildren<Transform>())
        {
            if (t.name != "AvatarAnchor") continue;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(t.GetChild(i).gameObject);
                count++;
            }
        }

        Debug.Log($"[ClassroomBuilder] Removed {count} placeholder objects.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void CreateDesk(Transform parent, int index, Vector3 origin)
    {
        var group = new GameObject($"Desk_{index:D2}");
        group.transform.SetParent(parent, false);
        group.transform.localPosition = origin;
        Undo.RegisterCreatedObjectUndo(group, "Build Classroom");

        // Desk top
        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.name = "DeskMesh";
        mesh.transform.SetParent(group.transform, false);
        mesh.transform.localPosition = new Vector3(0f, DeskTopY, -0.10f);
        mesh.transform.localScale    = new Vector3(0.85f, 0.05f, 0.55f);
        Undo.RegisterCreatedObjectUndo(mesh, "Build Classroom");

        // Desk leg
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = "DeskLeg";
        leg.transform.SetParent(group.transform, false);
        leg.transform.localPosition = new Vector3(0f, DeskTopY * 0.5f, -0.10f);
        leg.transform.localScale    = new Vector3(0.05f, DeskTopY, 0.05f);
        Undo.RegisterCreatedObjectUndo(leg, "Build Classroom");

        // AvatarAnchor — AudienceController instantiates the avatar here at runtime
        var anchor = new GameObject("AvatarAnchor");
        anchor.transform.SetParent(group.transform, false);
        anchor.transform.localPosition = new Vector3(0f, 0f, AnchorBehind);
        anchor.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        Undo.RegisterCreatedObjectUndo(anchor, "Build Classroom");
    }

    private static void CreateBox(GameObject parent, string name,
                                   Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Only floor needs a collider; everything else is visual-only
        if (name != "Floor")
            Object.DestroyImmediate(go.GetComponent<Collider>());

        Undo.RegisterCreatedObjectUndo(go, "Build Room Shell");
    }

    private static void EnsureMaterialsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
    }

    private static Material GetOrCreateMat(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
