using UnityEngine;

/// <summary>
/// Manages current slide index, texture swapping on SlidesPanel,
/// and notes text updates on NotesPanel. Responds to Volume Up/Down keys.
/// Implementation: Stage 8.
/// </summary>
public class SlideController : MonoBehaviour
{
    [Header("Lectern Display")]
    [Tooltip("Renderer on the SlidesPanel quad (left, larger)")]
    [SerializeField] private Renderer slidesPanel;
    [Tooltip("TMPro text on the NotesPanel (right, smaller)")]
    [SerializeField] private TMPro.TextMeshPro notesPanel;

    [Header("Panel Sizes (Inspector-tweakable)")]
    [SerializeField] private Vector2 slidesPanelSize  = new Vector2(0.55f, 0.70f);
    [SerializeField] private Vector2 notesPanelSize   = new Vector2(0.35f, 0.70f);

    public int CurrentSlide { get; private set; }
    public int SlideCount   { get; private set; }

    private Texture2D[] _slideTextures;
    private string[]    _notes;

    // TODO Stage 8: load PNGs + notes.json from Application.persistentDataPath/slides/,
    // handle Volume Up / Down input to advance/retreat slides,
    // suppress default Android volume overlay.
}
