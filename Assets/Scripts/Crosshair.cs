using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a Minecraft-style crosshair at the center of the screen entirely from code.
/// Attach to any GameObject in the scene (e.g., the HotbarManager or a new empty GameObject).
/// </summary>
public class Crosshair : MonoBehaviour
{
    public static Crosshair Instance { get; private set; }

    // Size of each crosshair bar
    private const float BAR_LENGTH    = 20f;
    private const float BAR_THICKNESS = 2f;
    private const float GAP           = 4f; // gap in center

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        BuildCrosshair();
    }

    void BuildCrosshair()
    {
        // Find or create Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject cgo = new GameObject("Canvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.AddComponent<CanvasScaler>();
            cgo.AddComponent<GraphicRaycaster>();
        }

        // Root container — centered on screen
        GameObject root = new GameObject("Crosshair", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin        = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax        = new Vector2(0.5f, 0.5f);
        rootRT.pivot            = new Vector2(0.5f, 0.5f);
        rootRT.anchoredPosition = Vector2.zero;
        rootRT.sizeDelta        = Vector2.zero;

        // Horizontal left bar
        CreateBar(root, "H_Left",
            new Vector2(-(GAP / 2f + BAR_LENGTH / 2f), 0f),
            new Vector2(BAR_LENGTH, BAR_THICKNESS));

        // Horizontal right bar
        CreateBar(root, "H_Right",
            new Vector2(GAP / 2f + BAR_LENGTH / 2f, 0f),
            new Vector2(BAR_LENGTH, BAR_THICKNESS));

        // Vertical top bar
        CreateBar(root, "V_Top",
            new Vector2(0f, GAP / 2f + BAR_LENGTH / 2f),
            new Vector2(BAR_THICKNESS, BAR_LENGTH));

        // Vertical bottom bar
        CreateBar(root, "V_Bottom",
            new Vector2(0f, -(GAP / 2f + BAR_LENGTH / 2f)),
            new Vector2(BAR_THICKNESS, BAR_LENGTH));
    }

    void CreateBar(GameObject parent, string name, Vector2 position, Vector2 size)
    {
        GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent.transform, false);

        RectTransform rt = bar.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta        = size;

        Image img = bar.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.85f); // white, slightly transparent
    }

    /// <summary>Show or hide the crosshair (e.g., hide when inventory is open).</summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
