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

    private const int   COLS       = 5;
    private const int   ROWS       = 5;
    private const float SLOT_SIZE  = 52f;   // matches hotbar slot size
    private const float SLOT_GAP   = 5f;
    private const float PADDING    = 12f;

    private GameObject  panel;
    private SlotUI[]    slotUIs; // initialized in BuildPanel with COLS*ROWS
    private SlotUI[]    craftingInputUIs = new SlotUI[4];
    private SlotUI      craftingOutputUI;


    private bool subscribed = false;

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

        float W = COLS * SLOT_SIZE + (COLS - 1) * SLOT_GAP + PADDING * 2 + 220f; // extra space for 2x2 crafting
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

        // ── Build 2x2 Crafting Panel ──────────────────────────────────────────
        float gridH = ROWS * SLOT_SIZE + (ROWS - 1) * SLOT_GAP;
        float gridTop = -PADDING - 28f;
        float gridCenter = gridTop - gridH * 0.5f;

        float craftingStartX = PADDING + COLS * SLOT_SIZE + (COLS - 1) * SLOT_GAP + 30f;
        float craftH = 2 * SLOT_SIZE + SLOT_GAP;
        float craftTop = gridCenter + craftH * 0.5f;

        // "Crafting" Label
        GameObject craftLabelGO = new GameObject("CraftingLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        craftLabelGO.transform.SetParent(panel.transform, false);
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
            craftingInputUIs[i] = CreateCraftingSlot(panel, i, x, y, isOutput: false);
        }

        // Arrow "→"
        float arrowX = craftingStartX + 2 * SLOT_SIZE + SLOT_GAP + 15f;
        GameObject arrowGO = new GameObject("CraftingArrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        arrowGO.transform.SetParent(panel.transform, false);
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

        // Crafting output slot
        float outputX = arrowX + 15f + SLOT_SIZE * 0.5f;
        craftingOutputUI = CreateCraftingSlot(panel, 0, outputX, gridCenter, isOutput: true);
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

        // Amount text
        GameObject txtGO = new GameObject("Amt", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        RectTransform tRT = txtGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.sizeDelta = new Vector2(-3f, -3f); tRT.anchoredPosition = Vector2.zero;
        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 12; tmp.alignment = TextAlignmentOptions.BottomRight; tmp.color = Color.white;
        tmp.raycastTarget = false; // slot background handles all raycasts

        // SlotUI
        SlotUI slot = go.AddComponent<SlotUI>();
        slot.owner      = SlotUI.Owner.Inventory;
        slot.index      = idx;
        slot.iconImage  = icon;
        slot.amountText = tmp;
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

        // Amount text
        GameObject txtGO = new GameObject("Amt", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        RectTransform tRT = txtGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.sizeDelta = new Vector2(-3f, -3f); tRT.anchoredPosition = Vector2.zero;
        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 12; tmp.alignment = TextAlignmentOptions.BottomRight; tmp.color = Color.white;
        tmp.raycastTarget = false;

        // SlotUI
        SlotUI slot = go.AddComponent<SlotUI>();
        slot.owner      = isOutput ? SlotUI.Owner.CraftingOutput : SlotUI.Owner.CraftingInput;
        slot.index      = idx;
        slot.iconImage  = icon;
        slot.amountText = tmp;
        slot.background = bg;

        return slot;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    void RefreshAll()
    {
        foreach (var s in slotUIs) s?.Refresh();
        foreach (var s in craftingInputUIs) s?.Refresh();
        craftingOutputUI?.Refresh();
    }

    // ── Toggle ────────────────────────────────────────────────────────────────

    public void ToggleInventory()
    {
        IsInventoryOpen = !IsInventoryOpen;
        panel.SetActive(IsInventoryOpen);
        if (IsInventoryOpen)
        {
            RefreshAll();
        }
        else
        {
            if (DragDropManager.Instance != null)
            {
                DragDropManager.Instance.ReturnHeldItem();
                DragDropManager.Instance.ExitSplitMode();
            }
            if (Inventory.Instance != null)
            {
                Inventory.Instance.ReturnCraftingInputs();
            }
        }

        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.SetVisible(!IsInventoryOpen);
        }

        // Keep cursor always visible so UI and drag-drop always work
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void OnInventoryButtonClicked() => ToggleInventory();
}
