using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds and manages the "Convert to Vehicle?" confirmation popup entirely from code.
/// Attach this script to any persistent GameObject (e.g., the Player or an empty Manager).
///
/// HIERARCHY BUILT AT RUNTIME (no manual Canvas setup needed):
///   Canvas  [existing scene canvas]
///   └─ ConfirmationPanel          ← dark overlay, anchored to centre
///      ├─ TitleText               ← "Convert to Vehicle?"
///      ├─ BlocksText              ← "Blocks: 24"
///      ├─ SizeText                ← "Size: 4 x 3 x 2"
///      ├─ WeightText              ← "Weight: 18.4 kg"
///      ├─ DurabilityText          ← "Durability: 320"
///      └─ ButtonRow               ← horizontal layout
///         ├─ ConfirmButton        ← green, "✔ Confirm"
///         └─ CancelButton         ← red,   "✖ Cancel"
/// </summary>
public class ConfirmationWindow : MonoBehaviour
{
    public static ConfirmationWindow Instance { get; private set; }

    /// <summary>True while the confirmation window is visible.</summary>
    public static bool IsOpen { get; private set; } = false;

    // ── UI references (built at runtime) ──────────────────────────────────────
    private GameObject _panel;
    private TextMeshProUGUI _blocksText;
    private TextMeshProUGUI _sizeText;
    private TextMeshProUGUI _weightText;
    private TextMeshProUGUI _durabilityText;

    private StructureBlueprint _storedBlueprint;

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color PanelBg    = new Color(0.08f, 0.08f, 0.10f, 0.94f);
    private static readonly Color ConfirmCol = new Color(0.15f, 0.65f, 0.25f, 1f);
    private static readonly Color CancelCol  = new Color(0.70f, 0.15f, 0.15f, 1f);
    private static readonly Color TextCol    = new Color(0.90f, 0.90f, 0.90f, 1f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    private void Start()
    {
        BuildUI();
        _panel.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the window and populate it with stats from <paramref name="blueprint"/>.
    /// </summary>
    public void ShowWindow(StructureBlueprint blueprint)
    {
        if (blueprint == null) return;

        _storedBlueprint = blueprint;

        _blocksText.text     = $"Blocks: {blueprint.blocks.Count}";
        _sizeText.text       = $"Size: {blueprint.dimensions.x} x {blueprint.dimensions.y} x {blueprint.dimensions.z}";
        _weightText.text     = $"Weight: {blueprint.totalMass:F1} kg";
        _durabilityText.text = $"Durability: {blueprint.totalDurability:F0}";

        _panel.SetActive(true);
        IsOpen = true;

        // Show and unlock cursor so the player can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>Hide the window and restore cursor lock.</summary>
    public void HideWindow()
    {
        _panel.SetActive(false);
        IsOpen = false;

        // Restore locked cursor for first-person gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    private void OnConfirm()
    {
        HideWindow();

        if (VehicleSpawner.Instance != null)
            VehicleSpawner.Instance.SpawnVehicle(_storedBlueprint);
        else
            Debug.LogError("[ConfirmationWindow] VehicleSpawner.Instance is null — " +
                           "add VehicleSpawner component to a GameObject in the scene.");
    }

    private void OnCancel()
    {
        HideWindow();
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ConfirmationWindow] No Canvas found in scene.");
            return;
        }

        // ── Panel ─────────────────────────────────────────────────────────────
        _panel = CreateUIObject("ConfirmationPanel", canvas.transform);
        var panelRT = _panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta        = new Vector2(380f, 340f);
        panelRT.anchoredPosition = Vector2.zero;

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = PanelBg;

        var layout = _panel.AddComponent<VerticalLayoutGroup>();
        layout.padding                = new RectOffset(24, 24, 20, 20);
        layout.spacing                = 10f;
        layout.childAlignment         = TextAnchor.UpperCenter;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;   // Layout controls child heights via LayoutElement
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;  // Don't stretch — use preferredHeight

        // ── Title ─────────────────────────────────────────────────────────────
        var titleGO  = CreateUIObject("TitleText", _panel.transform);
        SetLayoutSize(titleGO, preferredH: 36f);
        var titleTMP = AddTMP(titleGO, "Convert to Vehicle?", 20f, TextAlignmentOptions.Center, FontStyles.Bold);
        titleTMP.color = new Color(1f, 0.85f, 0.2f);

        // ── Divider ───────────────────────────────────────────────────────────
        var divGO  = CreateUIObject("Divider", _panel.transform);
        SetLayoutSize(divGO, preferredH: 2f, minH: 2f);
        divGO.AddComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 1f);

        // ── Stat lines ────────────────────────────────────────────────────────
        _blocksText     = AddStatLine("BlocksText",     "Blocks: —");
        _sizeText       = AddStatLine("SizeText",       "Size: —");
        _weightText     = AddStatLine("WeightText",     "Weight: —");
        _durabilityText = AddStatLine("DurabilityText", "Durability: —");

        // ── Spacer ────────────────────────────────────────────────────────────
        var spacer = CreateUIObject("Spacer", _panel.transform);
        SetLayoutSize(spacer, preferredH: 10f, minH: 4f);

        // ── Button row ────────────────────────────────────────────────────────
        var rowGO = CreateUIObject("ButtonRow", _panel.transform);
        SetLayoutSize(rowGO, preferredH: 52f, minH: 52f);

        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing                = 16f;
        rowLayout.childAlignment         = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth      = true;
        rowLayout.childControlHeight     = true;
        rowLayout.childForceExpandWidth  = true;
        rowLayout.childForceExpandHeight = true;
        rowLayout.padding = new RectOffset(0, 0, 0, 0);

        CreateButton("ConfirmButton", rowGO.transform, "✔  Confirm", ConfirmCol, OnConfirm);
        CreateButton("CancelButton",  rowGO.transform, "✖  Cancel",  CancelCol,  OnCancel);
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private TextMeshProUGUI AddStatLine(string name, string text)
    {
        var go  = CreateUIObject(name, _panel.transform);
        SetLayoutSize(go, preferredH: 26f);
        var tmp = AddTMP(go, text, 15f, TextAlignmentOptions.Left);
        tmp.color = TextCol;
        return tmp;
    }

    private static void SetLayoutSize(GameObject go, float preferredH, float minH = 0f)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredH;
        le.minHeight       = minH > 0f ? minH : preferredH;
    }

    private void CreateButton(string name, Transform parent, string label, Color bg, UnityEngine.Events.UnityAction callback)
    {
        var go  = CreateUIObject(name, parent);
        var img = go.AddComponent<Image>();
        img.color = bg;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.25f);
        colors.pressedColor     = Color.Lerp(bg, Color.black, 0.25f);
        btn.colors = colors;
        btn.onClick.AddListener(callback);

        var textGO  = CreateUIObject("Label", go.transform);
        var textRT  = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.anchoredPosition = Vector2.zero;

        var tmp = AddTMP(textGO, label, 14f, TextAlignmentOptions.Center, FontStyles.Bold);
        tmp.color = Color.white;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI AddTMP(GameObject go, string text, float size,
                                          TextAlignmentOptions align,
                                          FontStyles style = FontStyles.Normal)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.color     = TextCol;
        return tmp;
    }

    private static void SetHeight(GameObject go, float height)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight       = height;
    }
}
