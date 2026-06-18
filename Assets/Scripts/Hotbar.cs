using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Builds and manages a Minecraft-style 8-slot hotbar entirely from code.
/// Attach this script to ANY GameObject in the scene (e.g., the Canvas, or a new empty GameObject).
/// No prefab or manual UI setup is required.
/// </summary>
public class Hotbar : MonoBehaviour
{
    public static Hotbar Instance { get; private set; }

    // ── Tuneable constants ───────────────────────────────────────────────────
    private const int   SLOT_COUNT     = 8;
    private const float SLOT_SIZE      = 52f;   // 90% of original 58
    private const float SLOT_SPACING   = 5f;
    private const float SLOT_PADDING   = 5f;
    private const float BOTTOM_MARGIN  = 5f;    // closer to screen bottom

    // ── Runtime state ────────────────────────────────────────────────────────
    public int SelectedIndex { get; private set; } = 0;

    private List<InventorySlot>      hotbarSlots     = new List<InventorySlot>();
    private List<RectTransform>      slotRects       = new List<RectTransform>();
    private List<Image>              slotIcons       = new List<Image>();
    private List<TextMeshProUGUI>    slotTexts       = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI[]>  slotOutlineTexts = new List<TextMeshProUGUI[]>();
    private List<Image>              slotBackgrounds = new List<Image>();

    private static readonly Color NormalSlot   = new Color(0.35f, 0.35f, 0.35f, 1f);
    private static readonly Color SelectedSlot = new Color(0.55f, 0.55f, 0.55f, 1f);


    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        for (int i = 0; i < SLOT_COUNT; i++)
            hotbarSlots.Add(null);
    }

    void Start()
    {
        BuildHotbarUI();
        UpdateHighlight();
    }

    void Update()
    {
        HandleScroll();
        HandleNumberKeys();
    }

    // ── UI Construction (all code, no prefabs) ───────────────────────────────

    void BuildHotbarUI()
    {
        // Find or create a Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        float totalWidth  = SLOT_COUNT * SLOT_SIZE + (SLOT_COUNT - 1) * SLOT_SPACING + SLOT_PADDING * 2;
        float totalHeight = SLOT_SIZE + SLOT_PADDING * 2;

        // ── Hotbar panel ─────────────────────────────────────────────────────
        GameObject panelGO = new GameObject("HotbarPanel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot     = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(totalWidth, totalHeight);
        panelRT.anchoredPosition = new Vector2(0f, BOTTOM_MARGIN);

        Image panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

        // ── 8 Slots ──────────────────────────────────────────────────────────
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            float xPos = SLOT_PADDING + i * (SLOT_SIZE + SLOT_SPACING) + SLOT_SIZE * 0.5f;
            float yPos = SLOT_PADDING + SLOT_SIZE * 0.5f;

            // Slot background
            GameObject slotGO = new GameObject("Slot_" + i, typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(panelGO.transform, false);

            RectTransform slotRT = slotGO.GetComponent<RectTransform>();
            slotRT.anchorMin = new Vector2(0f, 0f);
            slotRT.anchorMax = new Vector2(0f, 0f);
            slotRT.pivot     = new Vector2(0.5f, 0.5f);
            slotRT.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
            slotRT.anchoredPosition = new Vector2(xPos, yPos);
            slotRects.Add(slotRT);

            Image slotImg = slotGO.GetComponent<Image>();
            slotImg.color = NormalSlot;
            slotBackgrounds.Add(slotImg);

            // Item icon (child of slot)
            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(slotGO.transform, false);

            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.1f, 0.1f);
            iconRT.anchorMax = new Vector2(0.9f, 0.9f);
            iconRT.sizeDelta  = Vector2.zero;
            iconRT.anchoredPosition = Vector2.zero;

            Image iconImg = iconGO.GetComponent<Image>();
            iconImg.enabled = false; // hidden until an item is placed
            iconImg.raycastTarget = false; // slot background handles raycasts
            slotIcons.Add(iconImg);

            // Stack outlines (bottom-right of slot)
            TextMeshProUGUI[] outlines = new TextMeshProUGUI[4];
            Vector2[] offsets = new Vector2[] {
                new Vector2(-1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-1f, -1f),
                new Vector2(1f, -1f)
            };

            for (int j = 0; j < 4; j++)
            {
                GameObject outlineGO = new GameObject("AmountTextOutline_" + j, typeof(RectTransform), typeof(TextMeshProUGUI));
                outlineGO.transform.SetParent(slotGO.transform, false);

                RectTransform outlineRT = outlineGO.GetComponent<RectTransform>();
                outlineRT.anchorMin = new Vector2(0f, 0f);
                outlineRT.anchorMax = new Vector2(1f, 1f);
                outlineRT.sizeDelta  = new Vector2(-4f, -4f);
                outlineRT.anchoredPosition = offsets[j];

                TextMeshProUGUI oTmp = outlineGO.GetComponent<TextMeshProUGUI>();
                oTmp.text      = "";
                oTmp.fontSize  = 13f;
                oTmp.alignment = TextAlignmentOptions.BottomRight;
                oTmp.color     = Color.black;
                outlines[j] = oTmp;
            }
            slotOutlineTexts.Add(outlines);

            // Stack count text (bottom-right of slot)
            GameObject textGO = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(slotGO.transform, false);

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0f);
            textRT.anchorMax = new Vector2(1f, 1f);
            textRT.sizeDelta  = new Vector2(-4f, -4f);
            textRT.anchoredPosition = Vector2.zero;

            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text      = "";
            tmp.fontSize  = 13f;
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.color     = Color.white;
            tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
            tmp.fontMaterial.SetColor("_OutlineColor", Color.black);
            tmp.fontMaterial.SetFloat("_OutlineWidth", 0.25f);
            tmp.UpdateMeshPadding();
            slotTexts.Add(tmp);

            // ── SlotUI for drag-drop ──────────────────────────────────────────
            SlotUI slotUI     = slotGO.AddComponent<SlotUI>();
            slotUI.owner      = SlotUI.Owner.Hotbar;
            slotUI.index      = i;
            slotUI.iconImage  = iconImg;
            slotUI.amountText = tmp;
            slotUI.amountOutlineTexts = outlines;
            slotUI.background = slotImg;
            slotUI.background = slotImg;
        }

        // ── No overlay highlight needed — slot background color shows selection ──
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    void HandleScroll()
    {
        if (Mouse.current == null) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0f)  SelectSlot((SelectedIndex - 1 + SLOT_COUNT) % SLOT_COUNT);
        else if (scroll < 0f) SelectSlot((SelectedIndex + 1) % SLOT_COUNT);
    }

    void HandleNumberKeys()
    {
        if (Keyboard.current == null) return;
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
                        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
        for (int i = 0; i < SLOT_COUNT && i < keys.Length; i++)
        {
            if (Keyboard.current[keys[i]].wasPressedThisFrame)
                SelectSlot(i);
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SLOT_COUNT) return;
        SelectedIndex = index;
        UpdateHighlight();
    }

    void UpdateHighlight()
    {
        for (int i = 0; i < slotBackgrounds.Count; i++)
            slotBackgrounds[i].color = (i == SelectedIndex) ? SelectedSlot : NormalSlot;
    }

    // ── Item API ──────────────────────────────────────────────────────────────

    /// <summary>Place an item in a specific slot (0-based).</summary>
    public void SetSlot(int index, Item item, int amount)
    {
        if (index < 0 || index >= SLOT_COUNT) return;
        hotbarSlots[index] = (item != null) ? new InventorySlot(item, amount) : null;
        RefreshSlotVisual(index);
    }

    /// <summary>Try to add an item — stacks if already in bar, else uses first empty slot.</summary>
    public bool TryAddItem(Item item, int amount)
    {
        if (item == null) return false;

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (hotbarSlots[i] != null && hotbarSlots[i].item != null && hotbarSlots[i].item.itemName == item.itemName)
            {
                hotbarSlots[i].amount += amount;
                RefreshSlotVisual(i);
                return true;
            }
        }
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (hotbarSlots[i] == null)
            {
                SetSlot(i, item, amount);
                return true;
            }
        }
        return false;
    }

    /// <summary>Returns the InventorySlot of the currently selected hotbar slot (null if empty).</summary>
    public InventorySlot GetSelectedSlot() => hotbarSlots[SelectedIndex];

    /// <summary>Expose slot data for SlotUI drag-drop.</summary>
    public InventorySlot GetSlotData(int index)
    {
        if (index < 0 || index >= SLOT_COUNT) return null;
        return hotbarSlots[index];
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    void RefreshSlotVisual(int index)
    {
        if (index >= slotIcons.Count) return;

        InventorySlot data = hotbarSlots[index];

        // Icon
        Image icon = slotIcons[index];
        if (data != null && data.item != null && data.item.icon != null)
        {
            icon.sprite  = data.item.icon;
            icon.enabled = true;
        }
        else
        {
            icon.sprite  = null;
            icon.enabled = false;
        }

        // Stack count
        TextMeshProUGUI txt = slotTexts[index];
        string amtStr = (data != null && data.amount > 1) ? data.amount.ToString() : "";
        txt.text = amtStr;
        if (index < slotOutlineTexts.Count && slotOutlineTexts[index] != null)
        {
            var outlines = slotOutlineTexts[index];
            for (int j = 0; j < outlines.Length; j++)
            {
                if (outlines[j] != null)
                    outlines[j].text = amtStr;
            }
        }
    }
}
