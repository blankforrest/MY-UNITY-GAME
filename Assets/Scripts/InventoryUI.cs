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

    private const int   COLS       = 7;
    private const int   ROWS       = 5;
    private const float SLOT_SIZE  = 52f;   // matches hotbar slot size
    private const float SLOT_GAP   = 5f;
    private const float PADDING    = 12f;

    private GameObject  panel;
    private System.Collections.Generic.List<SlotUI> slotUIs = new System.Collections.Generic.List<SlotUI>();
    private SlotUI[]    craftingInputUIs = new SlotUI[4];
    private SlotUI      craftingOutputUI;

    private GameObject  crafting2x2Group;
    private GameObject  crafting3x3Group;
    private SlotUI[]    tableCraftingInputUIs = new SlotUI[9];
    private SlotUI      tableCraftingOutputUI;

    [HideInInspector] public bool isFurnaceActive = false;
    private GameObject  furnaceGroup;
    private SlotUI      furnaceInputUI;
    private SlotUI      furnaceFuelUI;
    private SlotUI      furnaceOutputUI;
    private Image       furnaceFlameImg;
    private Image       furnaceArrowImg;

    private bool subscribed = false;
    private GameObject  tabsContainer;
    private string      activeCategory = "ALL";
    private Image[]     tabImages = new Image[6];
    private string[]    categories = new string[] { "ALL", "BLOCKS", "TOOLS", "VEHICLES", "FOLIAGE", "SPAWNERS" };
    private RectTransform contentRT;

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

        // Press I or Escape to open/close inventory
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame)
            {
                ToggleInventory();
            }
            else if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame && IsInventoryOpen)
            {
                ToggleInventory();
            }
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

        // Initialize slots list
        slotUIs = new System.Collections.Generic.List<SlotUI>();

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

        // ── Create Creative Tabs Container ─────────────────────────────────────
        tabsContainer = new GameObject("CreativeTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabsContainer.transform.SetParent(panel.transform, false);
        RectTransform tabsRT = tabsContainer.GetComponent<RectTransform>();
        float gridWidth = COLS * SLOT_SIZE + (COLS - 1) * SLOT_GAP;
        tabsRT.anchorMin = new Vector2(0f, 1f);
        tabsRT.anchorMax = new Vector2(0f, 1f);
        tabsRT.pivot = new Vector2(0f, 0f);
        tabsRT.anchoredPosition = new Vector2(PADDING, 0f); // sits exactly above the grid slots
        tabsRT.sizeDelta = new Vector2(gridWidth, 36f);

        HorizontalLayoutGroup tabLayout = tabsContainer.GetComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 4f;
        tabLayout.childAlignment = TextAnchor.LowerCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        for (int i = 0; i < categories.Length; i++)
        {
            string cat = categories[i];
            int index = i;

            GameObject tabBtn = new GameObject("Tab_" + cat, typeof(RectTransform), typeof(Image), typeof(Button));
            tabBtn.transform.SetParent(tabsContainer.transform, false);

            tabImages[index] = tabBtn.GetComponent<Image>();
            tabImages[index].color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

            Outline outline = tabBtn.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button btn = tabBtn.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectCategory(cat));

            GameObject txtGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(tabBtn.transform, false);
            RectTransform txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
            tmp.text = cat;
            tmp.fontSize = 11;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }
        tabsContainer.SetActive(false);

        float gridW = COLS * SLOT_SIZE + (COLS - 1) * SLOT_GAP;
        float gridH = ROWS * SLOT_SIZE + (ROWS - 1) * SLOT_GAP;
        float gridTop = -PADDING - 28f;
        float gridCenter = gridTop - gridH * 0.5f;
        float craftingStartX = PADDING + COLS * SLOT_SIZE + (COLS - 1) * SLOT_GAP + 30f;

        // ── Create ScrollView container ─────────────────────────────────────
        GameObject scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollGO.transform.SetParent(panel.transform, false);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 1f);
        scrollRT.anchorMax = new Vector2(0f, 1f);
        scrollRT.pivot = new Vector2(0f, 1f);
        scrollRT.anchoredPosition = new Vector2(PADDING, gridTop);
        scrollRT.sizeDelta = new Vector2(gridW + 8f, gridH);

        ScrollRect scrollRect = scrollGO.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
        scrollRect.scrollSensitivity = 35f;

        // ── Create Viewport ──────────────────────────────────────────
        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        RectTransform viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = Vector2.zero;
        viewportRT.anchoredPosition = Vector2.zero;
        viewportGO.GetComponent<Image>().color = Color.clear;
        scrollRect.viewport = viewportRT;

        // ── Create Content ───────────────────────────────────────────
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(0f, 1f);
        contentRT.pivot = new Vector2(0f, 1f);

        int totalRows = Mathf.CeilToInt((float)Inventory.MaxSlots / COLS);
        float contentHeight = totalRows * SLOT_SIZE + (totalRows - 1) * SLOT_GAP;
        contentRT.sizeDelta = new Vector2(gridW, contentHeight);
        contentRT.anchoredPosition = Vector2.zero;
        scrollRect.content = contentRT;

        // Build initial slots (minimum of 35)
        for (int i = 0; i < 35; i++)
        {
            int col = i % COLS;
            int row = i / COLS;

            float x = col * (SLOT_SIZE + SLOT_GAP) + SLOT_SIZE * 0.5f;
            float y = -row * (SLOT_SIZE + SLOT_GAP) - SLOT_SIZE * 0.5f;

            slotUIs.Add(CreateSlot(contentGO, i, x, y));
        }

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

        // ── Create Furnace Group ───────────────────────────────────────────────
        furnaceGroup = new GameObject("FurnaceGroup", typeof(RectTransform));
        furnaceGroup.transform.SetParent(panel.transform, false);
        RectTransform rtFurnace = furnaceGroup.GetComponent<RectTransform>();
        rtFurnace.anchorMin = rtFurnace.anchorMax = new Vector2(0, 1);
        rtFurnace.pivot = new Vector2(0, 1);
        rtFurnace.anchoredPosition = Vector2.zero;
        rtFurnace.sizeDelta = new Vector2(260f, H);

        // "Furnace" Label
        GameObject furnaceLabelGO = new GameObject("FurnaceLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        furnaceLabelGO.transform.SetParent(furnaceGroup.transform, false);
        RectTransform flRT = furnaceLabelGO.GetComponent<RectTransform>();
        flRT.anchorMin = flRT.anchorMax = new Vector2(0, 1);
        flRT.pivot = new Vector2(0.5f, 0.5f);
        flRT.sizeDelta = new Vector2(100f, 20f);
        flRT.anchoredPosition = new Vector2(craftingStartX + SLOT_SIZE + SLOT_GAP * 0.5f, craftTop + 15f);
        var flText = furnaceLabelGO.GetComponent<TextMeshProUGUI>();
        flText.text = "Furnace";
        flText.fontSize = 12;
        flText.alignment = TextAlignmentOptions.Center;
        flText.color = new Color(0.7f, 0.7f, 0.7f);

        // Slots:
        // Input slot (top)
        furnaceInputUI = CreateFurnaceSlot(furnaceGroup, 0, craftingStartX + SLOT_SIZE * 0.5f, gridCenter + SLOT_SIZE * 0.5f + 15f, SlotUI.Owner.FurnaceInput);

        // Fuel slot (bottom)
        furnaceFuelUI = CreateFurnaceSlot(furnaceGroup, 0, craftingStartX + SLOT_SIZE * 0.5f, gridCenter - SLOT_SIZE * 0.5f - 15f, SlotUI.Owner.FurnaceFuel);

        // Output slot (right)
        float furnaceOutputX = craftingStartX + 2f * SLOT_SIZE + 35f;
        furnaceOutputUI = CreateFurnaceSlot(furnaceGroup, 0, furnaceOutputX, gridCenter, SlotUI.Owner.FurnaceOutput);

        // Flame background (between Input and Fuel)
        GameObject flameBgGO = new GameObject("FlameBG", typeof(RectTransform), typeof(Image));
        flameBgGO.transform.SetParent(furnaceGroup.transform, false);
        RectTransform flameBgRT = flameBgGO.GetComponent<RectTransform>();
        flameBgRT.anchorMin = flameBgRT.anchorMax = new Vector2(0, 1);
        flameBgRT.pivot = new Vector2(0.5f, 0.5f);
        flameBgRT.sizeDelta = new Vector2(24f, 24f);
        flameBgRT.anchoredPosition = new Vector2(craftingStartX + SLOT_SIZE * 0.5f, gridCenter);
        Image flameBgImg = flameBgGO.GetComponent<Image>();
        flameBgImg.sprite = CreateFlameSprite();
        flameBgImg.color = new Color(0.15f, 0.15f, 0.15f, 1f); // dark silhouette

        // Flame foreground (filled)
        GameObject flameGO = new GameObject("FlameFG", typeof(RectTransform), typeof(Image));
        flameGO.transform.SetParent(furnaceGroup.transform, false);
        RectTransform flameRT = flameGO.GetComponent<RectTransform>();
        flameRT.anchorMin = flameRT.anchorMax = new Vector2(0, 1);
        flameRT.pivot = new Vector2(0.5f, 0.5f);
        flameRT.sizeDelta = new Vector2(24f, 24f);
        flameRT.anchoredPosition = new Vector2(craftingStartX + SLOT_SIZE * 0.5f, gridCenter);
        furnaceFlameImg = flameGO.GetComponent<Image>();
        furnaceFlameImg.sprite = flameBgImg.sprite;
        furnaceFlameImg.color = new Color(1.0f, 0.55f, 0.0f, 1f); // bright orange flame
        furnaceFlameImg.type = Image.Type.Filled;
        furnaceFlameImg.fillMethod = Image.FillMethod.Vertical;
        furnaceFlameImg.fillOrigin = (int)Image.OriginVertical.Bottom;
        furnaceFlameImg.fillAmount = 0f;

        // Arrow background (points right)
        GameObject arrowBgGO = new GameObject("ArrowBG", typeof(RectTransform), typeof(Image));
        arrowBgGO.transform.SetParent(furnaceGroup.transform, false);
        RectTransform arrowBgRT = arrowBgGO.GetComponent<RectTransform>();
        arrowBgRT.anchorMin = arrowBgRT.anchorMax = new Vector2(0, 1);
        arrowBgRT.pivot = new Vector2(0.5f, 0.5f);
        arrowBgRT.sizeDelta = new Vector2(28f, 24f);
        arrowBgRT.anchoredPosition = new Vector2(craftingStartX + SLOT_SIZE + 15f, gridCenter);
        Image arrowBgImg = arrowBgGO.GetComponent<Image>();
        arrowBgImg.sprite = CreateArrowSprite();
        arrowBgImg.color = new Color(0.15f, 0.15f, 0.15f, 1f); // dark silhouette

        // Arrow foreground (filled)
        GameObject furnaceArrowGO = new GameObject("ArrowFG", typeof(RectTransform), typeof(Image));
        furnaceArrowGO.transform.SetParent(furnaceGroup.transform, false);
        RectTransform furnaceArrowRT = furnaceArrowGO.GetComponent<RectTransform>();
        furnaceArrowRT.anchorMin = furnaceArrowRT.anchorMax = new Vector2(0, 1);
        furnaceArrowRT.pivot = new Vector2(0.5f, 0.5f);
        furnaceArrowRT.sizeDelta = new Vector2(28f, 24f);
        furnaceArrowRT.anchoredPosition = new Vector2(craftingStartX + SLOT_SIZE + 15f, gridCenter);
        furnaceArrowImg = furnaceArrowGO.GetComponent<Image>();
        furnaceArrowImg.sprite = arrowBgImg.sprite;
        furnaceArrowImg.color = new Color(0.2f, 0.8f, 0.2f, 1f); // green arrow
        furnaceArrowImg.type = Image.Type.Filled;
        furnaceArrowImg.fillMethod = Image.FillMethod.Horizontal;
        furnaceArrowImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        furnaceArrowImg.fillAmount = 0f;

        // Disable Furnace Group by default
        furnaceGroup.SetActive(false);
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
        int neededCount = 35;
        if (Inventory.Instance != null && Inventory.Instance.slots != null)
        {
            neededCount = Inventory.Instance.slots.Length;
        }
        neededCount = Mathf.Max(neededCount, 35);

        UpdateContentHeight(activeCategory, resetScroll: false);

        // Dynamically add SlotUI elements if we don't have enough
        while (slotUIs.Count < neededCount)
        {
            int idx = slotUIs.Count;
            int col = idx % COLS;
            int row = idx / COLS;
            float x = col * (SLOT_SIZE + SLOT_GAP) + SLOT_SIZE * 0.5f;
            float y = -row * (SLOT_SIZE + SLOT_GAP) - SLOT_SIZE * 0.5f;

            slotUIs.Add(CreateSlot(contentRT.gameObject, idx, x, y));
        }

        // Refresh and set active states for indefinite slot support
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] != null)
            {
                slotUIs[i].gameObject.SetActive(i < neededCount);
                if (i < neededCount)
                {
                    slotUIs[i].Refresh();
                }
            }
        }

        foreach (var s in craftingInputUIs) s?.Refresh();
        craftingOutputUI?.Refresh();
        foreach (var s in tableCraftingInputUIs) s?.Refresh();
        tableCraftingOutputUI?.Refresh();
        RefreshFurnaceUI();
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
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null && pc.isCreativeMode)
            {
                SelectCategory("ALL");
            }
            RefreshAll();
        }
        else
        {
            CloseInventoryCleanup();
        }

        UpdateTabsState();

        if (Crosshair.Instance != null)
        {
            Crosshair.Instance.SetVisible(!IsInventoryOpen);
        }

        Cursor.lockState = IsInventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = IsInventoryOpen;
    }

    /// <summary>Open the inventory with the 3x3 Crafting Table grid.</summary>
    public void Open3x3Crafting()
    {
        isFurnaceActive = false;
        FurnaceManager.ActiveFurnace = null;
        if (Inventory.Instance != null) Inventory.Instance.is3x3Active = true;
        if (crafting2x2Group != null) crafting2x2Group.SetActive(false);
        if (crafting3x3Group != null) crafting3x3Group.SetActive(true);
        if (furnaceGroup != null) furnaceGroup.SetActive(false);
        IsInventoryOpen = true;
        panel.SetActive(true);
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null && pc.isCreativeMode)
        {
            SelectCategory("ALL");
        }
        RefreshAll();
        UpdateTabsState();
        if (Crosshair.Instance != null) Crosshair.Instance.SetVisible(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>Switch the crafting panel back to the 2x2 player grid.</summary>
    public void Open2x2Crafting()
    {
        isFurnaceActive = false;
        FurnaceManager.ActiveFurnace = null;
        if (Inventory.Instance != null) Inventory.Instance.is3x3Active = false;
        if (crafting2x2Group != null) crafting2x2Group.SetActive(true);
        if (crafting3x3Group != null) crafting3x3Group.SetActive(false);
        if (furnaceGroup != null) furnaceGroup.SetActive(false);
    }

    public void OpenFurnaceUI(FurnaceState state)
    {
        FurnaceManager.ActiveFurnace = state;
        isFurnaceActive = true;
        if (Inventory.Instance != null) Inventory.Instance.is3x3Active = false;
        if (crafting2x2Group != null) crafting2x2Group.SetActive(false);
        if (crafting3x3Group != null) crafting3x3Group.SetActive(false);
        if (furnaceGroup != null) furnaceGroup.SetActive(true);
        IsInventoryOpen = true;
        panel.SetActive(true);
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null && pc.isCreativeMode)
        {
            SelectCategory("ALL");
        }
        RefreshAll();
        UpdateTabsState();
        if (Crosshair.Instance != null) Crosshair.Instance.SetVisible(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void RefreshFurnaceUI()
    {
        if (isFurnaceActive && furnaceGroup != null && furnaceGroup.activeSelf)
        {
            furnaceInputUI?.Refresh();
            furnaceFuelUI?.Refresh();
            furnaceOutputUI?.Refresh();

            var state = FurnaceManager.ActiveFurnace;
            if (state != null)
            {
                furnaceFlameImg.fillAmount = state.maxFuelBurnTime > 0f ? (state.fuelBurnTimeLeft / state.maxFuelBurnTime) : 0f;
                furnaceArrowImg.fillAmount = state.smeltTimeRequired > 0f ? (state.smeltProgress / state.smeltTimeRequired) : 0f;
            }
            else
            {
                furnaceFlameImg.fillAmount = 0f;
                furnaceArrowImg.fillAmount = 0f;
            }
        }
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
        isFurnaceActive = false;
        FurnaceManager.ActiveFurnace = null;
        UpdateTabsState();
    }

    SlotUI CreateFurnaceSlot(GameObject parent, int idx, float x, float y, SlotUI.Owner owner)
    {
        bool isOutput = owner == SlotUI.Owner.FurnaceOutput;
        GameObject go = new GameObject(owner.ToString(), typeof(RectTransform), typeof(Image));
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
        slot.owner      = owner;
        slot.index      = idx;
        slot.iconImage  = icon;
        slot.amountText = tmp;
        slot.amountOutlineTexts = outlines;
        slot.background = bg;

        return slot;
    }

    Sprite CreateArrowSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool isShaft = (x >= 4 && x <= 16 && y >= 11 && y <= 20);
                bool isHead = (x >= 16 && x <= 28 && Mathf.Abs(y - 15.5f) <= (28 - x) * 0.8f);
                if (isShaft || isHead)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }

    Sprite CreateFlameSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Abs(x - 15.5f);
                float width = (32 - y) * 0.5f;
                float wave = Mathf.Sin(y * 0.4f) * 1.5f;
                if (y >= 4 && y <= 28 && dx <= width + wave)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }

    public void SelectCategory(string category)
    {
        activeCategory = category;
        if (Inventory.Instance != null)
        {
            Inventory.Instance.PopulateCreativeCategory(category);
        }
        UpdateTabVisuals();
        UpdateContentHeight(category, resetScroll: true);
        RefreshAll();
    }

    private void UpdateContentHeight(string category, bool resetScroll = false)
    {
        if (contentRT == null) return;

        int slotCount = 35;
        if (Inventory.Instance != null && Inventory.Instance.slots != null)
        {
            slotCount = Inventory.Instance.slots.Length;
        }
        slotCount = Mathf.Max(slotCount, 35);

        int totalRows = Mathf.CeilToInt((float)slotCount / COLS);
        float contentHeight = totalRows * SLOT_SIZE + (totalRows - 1) * SLOT_GAP;
        contentRT.sizeDelta = new Vector2(0f, contentHeight);

        if (resetScroll)
        {
            contentRT.anchoredPosition = Vector2.zero;
        }
    }

    private int GetCategoryItemCount(string category)
    {
        switch (category)
        {
            case "ALL": return 50; // wood, dirt, etc. + tools
            case "BLOCKS": return 17;
            case "TOOLS": return 24;
            case "VEHICLES": return 5;
            case "FOLIAGE": return 4;
            case "SPAWNERS": return 1;
            default: return 35;
        }
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < categories.Length; i++)
        {
            if (tabImages[i] != null)
            {
                if (categories[i] == activeCategory)
                {
                    tabImages[i].color = new Color(0.2f, 0.45f, 0.85f, 1f);
                }
                else
                {
                    tabImages[i].color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                }
            }
        }
    }

    private void UpdateTabsState()
    {
        if (tabsContainer != null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            bool isCreative = (pc != null && pc.isCreativeMode);
            tabsContainer.SetActive(isCreative && IsInventoryOpen);
            if (isCreative && IsInventoryOpen)
            {
                UpdateTabVisuals();
            }
        }
    }

    public void OnInventoryButtonClicked() => ToggleInventory();
}
