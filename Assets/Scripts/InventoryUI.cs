using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds a code-driven 4x4 inventory panel. Toggle with OnInventoryButtonClicked().
/// Attach to any GameObject in the scene.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static bool IsInventoryOpen = false;
    public static InventoryUI Instance { get; private set; }

    private const int   COLS       = 5;
    private const int   ROWS       = 5;
    private const float SLOT_SIZE  = 52f;   // matches hotbar slot size
    private const float SLOT_GAP   = 5f;
    private const float PADDING    = 12f;

    private GameObject  panel;
    private SlotUI[]    slotUIs; // initialized in BuildPanel with COLS*ROWS
    private SlotUI[]    craftingInputUIs = new SlotUI[4];
    private SlotUI      craftingOutputUI;

    private GameObject  crafting2x2Group;
    private GameObject  crafting3x3Group;
    private SlotUI[]    tableCraftingInputUIs = new SlotUI[9];
    private SlotUI      tableCraftingOutputUI;

    private bool subscribed = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private TextMeshProUGUI[] CreateTextWithOutline(GameObject parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI[] outlines = new TextMeshProUGUI[4];
        Vector2[] offsets = new Vector2[] {
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, -1f),
            new Vector2(1f, -1f)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject outlineGO = new GameObject(name + "_Outline_" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            outlineGO.transform.SetParent(parent.transform, false);
            RectTransform oRT = outlineGO.GetComponent<RectTransform>();
            oRT.anchorMin = Vector2.zero; oRT.anchorMax = Vector2.one;
            oRT.sizeDelta = new Vector2(-3f, -3f); oRT.anchoredPosition = offsets[i];
            
            TextMeshProUGUI oTmp = outlineGO.GetComponent<TextMeshProUGUI>();
            oTmp.fontSize = fontSize; oTmp.alignment = alignment; oTmp.color = Color.black;
            oTmp.raycastTarget = false;
            outlines[i] = oTmp;
        }

        return outlines;
    }

    void Start()
    {
        BuildPanel();
        panel.SetActive(false);
    }

    void Update()
    {
        // Keep trying to subscribe until Inventory.Instance is available
        if (!subscribed && Inventory.Instance != null)
        {
            Inventory.Instance.onInventoryChangedCallback += OnInventoryChanged;
            subscribed = true;
        }

        // Press I to open/close inventory
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }
    }


    void OnInventoryChanged()
    {
        // Only refresh visuals if the panel is open — saves performance
        if (IsInventoryOpen) RefreshAll();
    }

    // ── Panel construction ────────────────────────────────────────────────────

    void BuildPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        float W = COLS * SLOT_SIZE + (COLS - 1) * SLOT_GAP + PADDING * 2 + 260f; // extra space for 3x3 crafting
        float H = ROWS * SLOT_SIZE + (ROWS - 1) * SLOT_GAP + PADDING * 2 + 32f;

        // Initialize with LOCAL constants — avoids stale Inventory.MaxSlots cache
        slotUIs = new SlotUI[COLS * ROWS];

        // Background panel
        panel = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform rt = panel.GetComponent<RectTransform>();
        // Anchor at screen center — stays centered at any resolution (windowed or fullscreen)
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(W, H);
        rt.anchoredPosition = new Vector2(0f, 30f);  // slightly above center

        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(panel.transform, false);
        RectTransform tRT = titleGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 1); tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.anchoredPosition = new Vector2(0, 0);
        tRT.sizeDelta = new Vector2(0, 28f);
        var title = titleGO.GetComponent<TextMeshProUGUI>();
        title.text = "Inventory & Crafting"; title.fontSize = 15; title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.8f, 0.8f, 0.8f);

        // Build 5x5 grid
        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            int col = i % COLS;
            int row = i / COLS;

            float x = PADDING + col * (SLOT_SIZE + SLOT_GAP) + SLOT_SIZE * 0.5f;
            float y = -PADDING - 28f - row * (SLOT_SIZE + SLOT_GAP) - SLOT_SIZE * 0.5f;

            slotUIs[i] = CreateSlot(panel, i, x, y);
        }

        float gridH = ROWS * SLOT_SIZE + (ROWS - 1) * SLOT_GAP;
        float gridTop = -PADDING - 28f;
        float gridCenter = gridTop - gridH * 0.5f;
        float craftingStartX = PADDING + COLS * SLOT_SIZE + (COLS - 1) * SLOT_GAP + 30f;

        // ── Create 2x2 Crafting Group ──────────────────────────────────────────
        crafting2x2Group = new GameObject("Crafting2x2Group", typeof(RectTransform));
        crafting2x2Group.transform.SetParent(panel.transform, false);
        RectTransform rt2x2 = crafting2x2Group.GetComponent<RectTransform>();
        rt2x2.anchorMin = rt2x2.anchorMax = new Vector2(0, 1);
        rt2x2.pivot = new Vector2(0, 1);
        rt2x2.anchoredPosition = Vector2.zero;
        rt2x2.sizeDelta = new Vector2(260f, H);

        float craftH = 2 * SLOT_SIZE + SLOT_GAP;
        float craftTop = gridCenter + craftH * 0.5f;

        // "Crafting" Label (2x2)
        GameObject craftLabelGO = new GameObject("CraftingLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        craftLabelGO.transform.SetParent(crafting2x2Group.transform, false);
        RectTransform clRT = craftLabelGO.GetComponent<RectTransform>();
        clRT.anchorMin = clRT.anchorMax = new Vector2(0, 1);
        clRT.pivot = new Vector2(0.5f, 0.5f);
        clRT.sizeDelta = new Vector2(100f, 20f);
        clRT.anchoredPosition = new Vector2(craftingStartX + SLOT_SIZE + SLOT_GAP * 0.5f, craftTop + 15f);
        var clText = craftLabelGO.GetComponent<TextMeshProUGUI>();
        clText.text = "Crafting";
        clText.fontSize = 12;
        clText.alignment = TextAlignmentOptions.Center;
        clText.color = new Color(0.7f, 0.7f, 0.7f);

        // 2x2 Crafting input slots
        for (int i = 0; i < 4; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = craftingStartX + col * (SLOT_SIZE + SLOT_GAP) + SLOT_SIZE * 0.5f;
            float y = craftTop - row * (SLOT_SIZE + SLOT_GAP) - SLOT_SIZE * 0.5f;
            craftingInputUIs[i] = CreateCraftingSlot(crafting2x2Group, i, x, y, isOutput: false);
        }

        // Arrow "→" (2x2)
        float arrowX = craftingStartX + 2 * SLOT_SIZE + SLOT_GAP + 15f;
        GameObject arrowGO = new GameObject("CraftingArrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowGO.transform.SetParent(crafting2x2Group.transform, false);
        RectTransform arrowRT = arrowGO.GetComponent<RectTransform>();
        arrowRT.anchorMin = arrowRT.anchorMax = new Vector2(0, 1);
        arrowRT.pivot = new Vector2(0.5f, 0.5f);
        arrowRT.sizeDelta = new Vector2(30f, 30f);
        arrowRT.anchoredPosition = new Vector2(arrowX, gridCenter);
        var arrowText = arrowGO.GetComponent<TextMeshProUGUI>();
        arrowText.text = "→";
        arrowText.fontSize = 24;
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.color = new Color(0.8f, 0.8f, 0.8f);

        // Crafting output slot (2x2)
        float outputX = arrowX + 15f + SLOT_SIZE * 0.5f;
        craftingOutputUI = CreateCraftingSlot(crafting2x2Group, 0, outputX, gridCenter, isOutput: true);

        // ── Create 3x3 Crafting Group ──────────────────────────────────────────
        crafting3x3Group = new GameObject("Crafting3x3Group", typeof(RectTransform));
        crafting3x3Group.transform.SetParent(panel.transform, false);
        RectTransform rt3x3 = crafting3x3Group.GetComponent<RectTransform>();
        rt3x3.anchorMin = rt3x3.anchorMax = new Vector2(0, 1);
        rt3x3.pivot = new Vector2(0, 1);
        rt3x3.anchoredPosition = Vector2.zero;
        rt3x3.sizeDelta = new Vector2(260f, H);

        float tableCraftH = 3 * SLOT_SIZE + 2 * SLOT_GAP;
        float tableCraftTop = gridCenter + tableCraftH * 0.5f;

        // "Crafting Table" Label (3x3)
        GameObject tableLabelGO = new GameObject("TableLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        tableLabelGO.transform.SetParent(crafting3x3Group.transform, false);
        RectTransform tlRT = tableLabelGO.GetComponent<RectTransform>();
        tlRT.anchorMin = tlRT.anchorMax = new Vector2(0, 1);
        tlRT.pivot = new Vector2(0.5f, 0.5f);
        tlRT.sizeDelta = new Vector2(120f, 20f);
        tlRT.anchoredPosition = new Vector2(craftingStartX + 1.5f * SLOT_SIZE + SLOT_GAP, tableCraftTop + 15f);
        var tlText = tableLabelGO.GetComponent<TextMeshProUGUI>();
        tlText.text = "Crafting Table";
        tlText.fontSize = 12;
        tlText.alignment = TextAlignmentOptions.Center;
        tlText.color = new Color(0.7f, 0.7f, 0.7f);

        // 3x3 Crafting input slots
        for (int i = 0; i < 9; i++)
        {
            int col = i % 3;
            int row = i / 3;
            float x = craftingStartX + col * (SLOT_SIZE + SLOT_GAP) + SLOT_SIZE * 0.5f;
            float y = tableCraftTop - row * (SLOT_SIZE + SLOT_GAP) - SLOT_SIZE * 0.5f;
            tableCraftingInputUIs[i] = CreateTableCraftingSlot(crafting3x3Group, i, x, y, isOutput: false);
        }

        // Arrow "→" (3x3)
        float tableArrowX = craftingStartX + 3 * SLOT_SIZE + 2 * SLOT_GAP + 20f;
        GameObject tableArrowGO = new GameObject("TableArrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        tableArrowGO.transform.SetParent(crafting3x3Group.transform, false);
        RectTransform tableArrowRT = tableArrowGO.GetComponent<RectTransform>();
        tableArrowRT.anchorMin = tableArrowRT.anchorMax = new Vector2(0, 1);
        tableArrowRT.pivot = new Vector2(0.5f, 0.5f);
        tableArrowRT.sizeDelta = new Vector2(30f, 30f);
        tableArrowRT.anchoredPosition = new Vector2(tableArrowX, gridCenter);
        var tableArrowText = tableArrowGO.GetComponent<TextMeshProUGUI>();
        tableArrowText.text = "→";
        tableArrowText.fontSize = 24;
        tableArrowText.alignment = TextAlignmentOptions.Center;
        tableArrowText.color = new Color(0.8f, 0.8f, 0.8f);

        // Crafting output slot (3x3)
        float tableOutputX = tableArrowX + 20f + SLOT_SIZE * 0.5f;
        tableCraftingOutputUI = CreateTableCraftingSlot(crafting3x3Group, 0, tableOutputX, gridCenter, isOutput: true);

        // Disable 3x3 group by default
        crafting3x3Group.SetActive(false);
    }

    SlotUI CreateSlot(GameObject parent, int idx, float x, float y)
    {
        // Background
        GameObject go = new GameObject("InvSlot_" + idx, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
        rt.anchoredPosition = new Vector2(x, y);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.35f, 0.35f, 0.35f, 1f);

        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iRT = iconGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.1f, 0.1f); iRT.anchorMax = new Vector2(0.9f, 0.9f);
        iRT.sizeDelta = Vector2.zero; iRT.anchoredPosition = Vector2.zero;
        Image icon = iconGO.GetComponent<Image>();
        icon.enabled = false;
        icon.raycastTarget = false; // Slot background handles all raycasts

        // Amount text outlines
        TextMeshProUGUI[] outlines = CreateTextWithOutline(go, "Amt", 12, TextAlignmentOptions.BottomRight);

        // Amount text
        GameObject txtGO = new GameObject("Amt", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        RectTransform tRT = txtGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.sizeDelta = new Vector2(-3f, -3f); tRT.anchoredPosition = Vector2.zero;
        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 12; tmp.alignment = TextAlignmentOptions.BottomRight; tmp.color = Color.white;
        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
        tmp.fontMaterial.SetColor("_OutlineColor", Color.black);
        tmp.fontMaterial.SetFloat("_OutlineWidth", 0.25f);
        tmp.UpdateMeshPadding();
        tmp.raycastTarget = false; // slot background handles all raycasts

        // SlotUI
        SlotUI slot = go.AddComponent<SlotUI>();
        slot.owner      = SlotUI.Owner.Inventory;
        slot.index      = idx;
        slot.iconImage  = icon;
        slot.amountText = tmp;
        slot.amountOutlineTexts = outlines;
        slot.background = bg;

        return slot;
    }

    SlotUI CreateCraftingSlot(GameObject parent, int idx, float x, float y, bool isOutput)
    {
        // Background
        GameObject go = new GameObject(isOutput ? "CraftOutputSlot" : "CraftInputSlot_" + idx, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
        rt.anchoredPosition = new Vector2(x, y);
        Image bg = go.GetComponent<Image>();
        // Output slot is dark green, input slots are standard dark gray
        bg.color = isOutput ? new Color(0.20f, 0.32f, 0.20f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);

        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iRT = iconGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.1f, 0.1f); iRT.anchorMax = new Vector2(0.9f, 0.9f);
        iRT.sizeDelta = Vector2.zero; iRT.anchoredPosition = Vector2.zero;
        Image icon = iconGO.GetComponent<Image>();
        icon.enabled = false;
        icon.raycastTarget = false;

        // Amount text outlines
        TextMeshProUGUI[] outlines = CreateTextWithOutline(go, "Amt", 12, TextAlignmentOptions.BottomRight);

        // Amount text
        GameObject txtGO = new GameObject("Amt", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        RectTransform tRT = txtGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.sizeDelta = new Vector2(-3f, -3f); tRT.anchoredPosition = Vector2.zero;
        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 12; tmp.alignment = TextAlignmentOptions.BottomRight; tmp.color = Color.white;
        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
        tmp.fontMaterial.SetColor("_OutlineColor", Color.black);
        tmp.fontMaterial.SetFloat("_OutlineWidth", 0.25f);
        tmp.UpdateMeshPadding();
        tmp.raycastTarget = false;

        // SlotUI
        SlotUI slot = go.AddComponent<SlotUI>();
        slot.owner      = isOutput ? SlotUI.Owner.CraftingOutput : SlotUI.Owner.CraftingInput;
        slot.index      = idx;
        slot.iconImage  = icon;
        slot.amountText = tmp;
        slot.amountOutlineTexts = outlines;
        slot.background = bg;

        return slot;
    }

    SlotUI CreateTableCraftingSlot(GameObject parent, int idx, float x, float y, bool isOutput)
    {
        GameObject go = new GameObject(isOutput ? "TableCraftOutputSlot" : "TableCraftInputSlot_" + idx, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
        rt.anchoredPosition = new Vector2(x, y);
        Image bg = go.GetComponent<Image>();
        bg.color = isOutput ? new Color(0.20f, 0.32f, 0.20f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iRT = iconGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.1f, 0.1f); iRT.anchorMax = new Vector2(0.9f, 0.9f);
        iRT.sizeDelta = Vector2.zero; iRT.anchoredPosition = Vector2.zero;
        Image icon = iconGO.GetComponent<Image>();
        icon.enabled = false;
        icon.raycastTarget = false;

        TextMeshProUGUI[] outlines = CreateTextWithOutline(go, "Amt", 12, TextAlignmentOptions.BottomRight);

        GameObject txtGO = new GameObject("Amt", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        RectTransform tRT = txtGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.sizeDelta = new Vector2(-3f, -3f); tRT.anchoredPosition = Vector2.zero;
        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 12; tmp.alignment = TextAlignmentOptions.BottomRight; tmp.color = Color.white;
        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
        tmp.fontMaterial.SetColor("_OutlineColor", Color.black);
        tmp.fontMaterial.SetFloat("_OutlineWidth", 0.25f);
        tmp.UpdateMeshPadding();
        tmp.raycastTarget = false;

        SlotUI slot = go.AddComponent<SlotUI>();
        slot.owner      = isOutput ? SlotUI.Owner.TableCraftingOutput : SlotUI.Owner.TableCraftingInput;
        slot.index      = idx;
        slot.iconImage  = icon;
        slot.amountText = tmp;
        slot.amountOutlineTexts = outlines;
        slot.background = bg;

        return slot;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    void RefreshAll()
    {
        foreach (var s in slotUIs) s?.Refresh();
        foreach (var s in craftingInputUIs) s?.Refresh();
        craftingOutputUI?.Refresh();
        foreach (var s in tableCraftingInputUIs) s?.Refresh();
        tableCraftingOutputUI?.Refresh();
    }

    // ── Toggle ────────────────────────────────────────────────────────────────

    public void ToggleInventory()
    {
        IsInventoryOpen = !IsInventoryOpen;
        panel.SetActive(IsInventoryOpen);
        if (IsInventoryOpen)
        {
            // Opening via 'I' key always shows the 2x2 personal crafting grid
            Open2x2Crafting();
            RefreshAll();
        }
        else
        {
            CloseInventoryCleanup();
        }

        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.SetVisible(!IsInventoryOpen);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>Open the inventory with the 3x3 Crafting Table grid.</summary>
    public void Open3x3Crafting()
    {
        if (Inventory.Instance != null) Inventory.Instance.is3x3Active = true;
        if (crafting2x2Group != null) crafting2x2Group.SetActive(false);
        if (crafting3x3Group != null) crafting3x3Group.SetActive(true);
        IsInventoryOpen = true;
        panel.SetActive(true);
        RefreshAll();
        if (Crosshair.Instance != null) Crosshair.Instance.SetVisible(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>Switch the crafting panel back to the 2x2 player grid.</summary>
    public void Open2x2Crafting()
    {
        if (Inventory.Instance != null) Inventory.Instance.is3x3Active = false;
        if (crafting2x2Group != null) crafting2x2Group.SetActive(true);
        if (crafting3x3Group != null) crafting3x3Group.SetActive(false);
    }

    private void CloseInventoryCleanup()
    {
        if (DragDropManager.Instance != null)
        {
            DragDropManager.Instance.ReturnHeldItem();
            DragDropManager.Instance.ExitSplitMode();
        }
        if (Inventory.Instance != null)
        {
            Inventory.Instance.ReturnCraftingInputs();
            Inventory.Instance.ReturnTableCraftingInputs();
            Inventory.Instance.is3x3Active = false;
        }
    }

    public void OnInventoryButtonClicked() => ToggleInventory();
}
