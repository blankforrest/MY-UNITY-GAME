using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Handles all inventory slot interactions (Minecraft-style clicks/swaps, 
/// Survivalcraft-style split mode via long press, and float cursor rendering).
/// </summary>
public class DragDropManager : MonoBehaviour
{
    public static DragDropManager Instance { get; private set; }

    private const float LongPressDuration = 0.4f;

    // Floating item state
    private InventorySlot heldItem = null;
    private Image ghost;
    private TextMeshProUGUI ghostText;      // stack count (bottom-right of icon)
    private TextMeshProUGUI[] ghostOutlineTexts;
    private TextMeshProUGUI ghostNameLabel; // item name shown below cursor

    // Split mode state
    private SlotUI splitSourceSlot = null;
    private bool isInSplitMode = false;
    private Outline splitOutline = null;

    // Click & Long Press tracking state
    private SlotUI clickedSlotOnPress = null;
    private float pressTime = 0f;
    private bool isPressing = false;
    private bool wasLongPressTriggered = false;
    private int pressButton = 0; // 0 = Left click, 1 = Right click

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        InitializeGhost();
    }

    private void InitializeGhost()
    {
        if (ghost != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Create the cursor-floating ghost GameObject
        GameObject ghostGO = new GameObject("DDGhost", typeof(RectTransform), typeof(Image));
        ghostGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = ghostGO.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(50f, 50f);
        rt.anchorMin  = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        ghost = ghostGO.GetComponent<Image>();
        ghost.raycastTarget = false;
        ghost.color         = new Color(1f, 1f, 1f, 0.85f);
        ghost.enabled       = false;

        SlotUI.Ghost = ghost;

        // Create the 4 stack amount outlines for the ghost
        ghostOutlineTexts = new TextMeshProUGUI[4];
        Vector2[] offsets = new Vector2[] {
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, -1f),
            new Vector2(1f, -1f)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject outlineGO = new GameObject("GhostTextOutline_" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            outlineGO.transform.SetParent(ghostGO.transform, false);
            RectTransform outlineRT = outlineGO.GetComponent<RectTransform>();
            outlineRT.anchorMin = Vector2.zero;
            outlineRT.anchorMax = Vector2.one;
            outlineRT.sizeDelta = new Vector2(-4f, -4f);
            outlineRT.anchoredPosition = offsets[i];

            TextMeshProUGUI oTmp = outlineGO.GetComponent<TextMeshProUGUI>();
            oTmp.fontSize = 14f;
            oTmp.alignment = TextAlignmentOptions.BottomRight;
            oTmp.color = Color.black;
            oTmp.raycastTarget = false;
            oTmp.enabled = false;
            ghostOutlineTexts[i] = oTmp;
        }

        // Create the stack amount text child for the ghost
        GameObject textGO = new GameObject("GhostText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(ghostGO.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = new Vector2(-4f, -4f);
        textRT.anchoredPosition = Vector2.zero;

        ghostText = textGO.GetComponent<TextMeshProUGUI>();
        ghostText.fontSize = 14f;
        ghostText.alignment = TextAlignmentOptions.BottomRight;
        ghostText.color = Color.white;
        ghostText.fontMaterial.EnableKeyword("OUTLINE_ON");
        ghostText.fontMaterial.SetColor("_OutlineColor", Color.black);
        ghostText.fontMaterial.SetFloat("_OutlineWidth", 0.25f);
        ghostText.UpdateMeshPadding();
        ghostText.raycastTarget = false;
        ghostText.enabled = false;

        // Create the item name label (shown below the cursor icon)
        GameObject nameLabelGO = new GameObject("GhostNameLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameLabelGO.transform.SetParent(canvas.transform, false); // sibling of ghost, not child

        RectTransform nameLabelRT = nameLabelGO.GetComponent<RectTransform>();
        nameLabelRT.sizeDelta  = new Vector2(150f, 24f);
        nameLabelRT.anchorMin  = nameLabelRT.anchorMax = nameLabelRT.pivot = new Vector2(0.5f, 1f); // pivot top-centre

        ghostNameLabel = nameLabelGO.GetComponent<TextMeshProUGUI>();
        ghostNameLabel.fontSize = 13f;
        ghostNameLabel.alignment = TextAlignmentOptions.Center;
        ghostNameLabel.color = Color.white;
        ghostNameLabel.fontStyle = FontStyles.Bold;
        ghostNameLabel.fontMaterial.EnableKeyword("OUTLINE_ON");
        ghostNameLabel.fontMaterial.SetColor("_OutlineColor", Color.black);
        ghostNameLabel.fontMaterial.SetFloat("_OutlineWidth", 0.1f); // Thin black text outline
        ghostNameLabel.UpdateMeshPadding();
        ghostNameLabel.raycastTarget = false;

        // Semi-transparent dark background for readability
        GameObject bgGO = new GameObject("GhostNameBG", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(nameLabelGO.transform, false);
        bgGO.transform.SetAsFirstSibling(); // behind text
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = new Vector2(6f, 4f);
        bgRT.anchoredPosition = Vector2.zero;
        
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f); // Original semi-transparent gray background
        bgImg.raycastTarget = false;

        // Start hidden — the whole GO must be inactive so the bg image is also hidden
        nameLabelGO.SetActive(false);
    }

    void Update()
    {
        if (Mouse.current == null) return;

        if (PauseMenu.IsPaused)
        {
            if (ghost != null && ghost.enabled) ghost.enabled = false;
            if (ghostNameLabel != null && ghostNameLabel.gameObject.activeSelf) ghostNameLabel.gameObject.SetActive(false);
            return;
        }

        if (ghost == null)
        {
            InitializeGhost();
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // ── If Inventory is Closed ─────────────────────────────────────────────
        if (!InventoryUI.IsInventoryOpen)
        {
            if (heldItem != null)
            {
                ReturnHeldItem();
            }
            if (isInSplitMode)
            {
                ExitSplitMode();
            }
            if (ghostNameLabel != null)
            {
                ghostNameLabel.gameObject.SetActive(false);
            }
            isPressing = false;
            clickedSlotOnPress = null;
            return;
        }

        // ── Move Ghost to Cursor ─────────────────────────────────────────────────────────────────────────
        if (ghost != null && ghost.enabled)
        {
            ghost.rectTransform.position = mousePos;
        }

        // ── Handle Hover Tooltip or Held Item Label Position ──────────────────
        if (ghostNameLabel != null)
        {
            if (heldItem != null && heldItem.item != null && heldItem.amount > 0)
            {
                // Position held item label below cursor
                ghostNameLabel.rectTransform.position = mousePos + new Vector2(0f, -32f);
            }
            else
            {
                // Tooltip mode: check if hovering a slot containing an item
                SlotUI hoveredSlot = RaycastSlot(mousePos);
                var data = hoveredSlot?.GetItemData();
                if (data != null && data.item != null && data.amount > 0)
                {
                    ghostNameLabel.text = data.item.itemName;
                    ghostNameLabel.rectTransform.position = mousePos + new Vector2(0f, -32f);
                    ghostNameLabel.gameObject.SetActive(true);
                }
                else
                {
                    ghostNameLabel.gameObject.SetActive(false);
                }
            }
        }

        // ── Handle Mouse Press ────────────────────────────────────────────────
        bool leftPressed = Mouse.current.leftButton.wasPressedThisFrame;
        bool rightPressed = Mouse.current.rightButton.wasPressedThisFrame;

        if (leftPressed || rightPressed)
        {
            SlotUI hit = RaycastSlot(mousePos);
            if (hit != null)
            {
                clickedSlotOnPress = hit;
                pressTime = Time.time;
                isPressing = true;
                wasLongPressTriggered = false;
                pressButton = leftPressed ? 0 : 1;
            }
        }

        // ── Handle Long Press Detection ────────────────────────────────────────
        if (isPressing && !wasLongPressTriggered)
        {
            if (Time.time - pressTime >= LongPressDuration)
            {
                wasLongPressTriggered = true;
                HandleLongPress(clickedSlotOnPress);
            }
        }

        // ── Handle Mouse Release ──────────────────────────────────────────────
        bool leftReleased = Mouse.current.leftButton.wasReleasedThisFrame;
        bool rightReleased = Mouse.current.rightButton.wasReleasedThisFrame;

        if (leftReleased || rightReleased)
        {
            if (isPressing)
            {
                isPressing = false;
                if (!wasLongPressTriggered)
                {
                    // Quick click!
                    SlotUI releaseSlot = RaycastSlot(mousePos);
                    if (releaseSlot == clickedSlotOnPress && releaseSlot != null)
                    {
                        bool isShiftHeld = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                        if (isShiftHeld && heldItem == null)
                        {
                            HandleShiftClick(releaseSlot);
                        }
                        else
                        {
                            HandleQuickClick(releaseSlot, pressButton == 0);
                        }
                    }
                }
                clickedSlotOnPress = null;
            }
            else
            {
                // Released when not pressing/dragging (e.g. click release outside slots)
                if (heldItem != null)
                {
                    SlotUI hit = RaycastSlot(mousePos);
                    if (hit == null && !IsPointerOverUI())
                    {
                        DropHeldItemInWorld();
                    }
                }
            }
        }
    }

    // ── Interaction Handlers ──────────────────────────────────────────────────

    private void HandleLongPress(SlotUI slot)
    {
        if (slot == null) return;

        // In Creative Mode: do not split main inventory slots
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null && pc.isCreativeMode && slot.owner == SlotUI.Owner.Inventory)
            return;

        if (isInSplitMode)
        {
            ExitSplitMode();
            return;
        }

        if (heldItem != null)
            return; // Cannot split while holding a floating item

        var data = slot.GetItemData();
        if (data != null && data.item != null && data.amount > 0)
        {
            EnterSplitMode(slot);
        }
    }

    private void HandleQuickClick(SlotUI slot, bool isLeftClick)
    {
        if (slot == null) return;

        // ── CASE 1: SPLIT MODE IS ACTIVE ──────────────────────────────────────
        if (isInSplitMode)
        {
            if (splitSourceSlot == null)
            {
                ExitSplitMode();
                return;
            }

            var srcData = splitSourceSlot.GetItemData();
            if (srcData == null || srcData.item == null || srcData.amount <= 0)
            {
                ExitSplitMode();
                return;
            }

            // Clicked the source slot itself
            if (slot == splitSourceSlot)
            {
                if (isLeftClick)
                {
                    // Exit split mode, pick up whole remaining stack
                    heldItem = new InventorySlot(srcData.item, srcData.amount);
                    slot.WriteItemData(null);
                    slot.Refresh();
                    ExitSplitMode();
                    UpdateGhostVisual();
                }
                else
                {
                    // Right click source: exit split mode (leaves items in slot)
                    ExitSplitMode();
                }
                return;
            }

            // Clicked an empty or same-item slot (split action)
            var clickedData = slot.GetItemData();
            bool isEmpty = clickedData == null || clickedData.item == null;
            bool isSameType = !isEmpty && clickedData.item.itemName == srcData.item.itemName && srcData.item.toolType == ToolType.None;

            if (!isEmpty && !isSameType)
            {
                if (isLeftClick)
                {
                    ExitSplitMode();
                    return;
                }
            }

            if (isEmpty || isSameType)
            {
                if (slot.owner == SlotUI.Owner.CraftingOutput || slot.owner == SlotUI.Owner.TableCraftingOutput || slot.owner == SlotUI.Owner.FurnaceOutput)
                    return; // Cannot place into output slots

                // Keep local reference to splitSourceSlot before we potentially null it out in ExitSplitMode
                var srcSlot = splitSourceSlot;

                // Deduct 1 from source
                int nextSrcAmount = srcData.amount - 1;
                if (nextSrcAmount <= 0)
                {
                    srcSlot.WriteItemData(null);
                    ExitSplitMode();
                }
                else
                {
                    srcSlot.WriteItemData(new InventorySlot(srcData.item, nextSrcAmount));
                }

                // Place/Add 1 in clicked slot
                int newAmount = isEmpty ? 1 : clickedData.amount + 1;
                slot.WriteItemData(new InventorySlot(srcData.item, newAmount));

                srcSlot.Refresh();
                slot.Refresh();
                Inventory.Instance?.onInventoryChangedCallback?.Invoke();
            }
            return;
        }

        // ── CASE 2: NORMAL INTERACTION (NOT IN SPLIT MODE) ─────────────────────
        var slotData = slot.GetItemData();

        if (heldItem == null || heldItem.item == null || heldItem.amount <= 0)
        {
            // ── Floating item is empty -> picking up from slot ──
            if (slotData == null || slotData.item == null || slotData.amount <= 0)
                return;

            if (slot.owner == SlotUI.Owner.CraftingOutput)
            {
                heldItem = new InventorySlot(slotData.item, slotData.amount);
                Inventory.Instance?.ConsumeCraftingInputs();
                Inventory.Instance?.onInventoryChangedCallback?.Invoke();
            }
            else if (slot.owner == SlotUI.Owner.TableCraftingOutput)
            {
                heldItem = new InventorySlot(slotData.item, slotData.amount);
                Inventory.Instance?.ConsumeTableCraftingInputs();
                Inventory.Instance?.onInventoryChangedCallback?.Invoke();
            }
            else if (slot.owner == SlotUI.Owner.FurnaceOutput)
            {
                heldItem = new InventorySlot(slotData.item, slotData.amount);
                slot.WriteItemData(null);
            }
            else
            {
                var pc = FindFirstObjectByType<PlayerController>();
                bool isCreative = pc != null && pc.isCreativeMode;

                if (isCreative && slot.owner == SlotUI.Owner.Inventory)
                {
                    int amountToPick = isLeftClick ? (slotData.item.toolType == ToolType.None ? 64 : 1) : 1;
                    heldItem = new InventorySlot(slotData.item, amountToPick);
                }
                else
                {
                    if (isLeftClick)
                    {
                        heldItem = new InventorySlot(slotData.item, slotData.amount);
                        slot.WriteItemData(null);
                    }
                    else
                    {
                        // Pick up half (rounded up)
                        int amountToPick = (slotData.amount + 1) / 2;
                        int amountLeft = slotData.amount - amountToPick;

                        heldItem = new InventorySlot(slotData.item, amountToPick);
                        if (amountLeft <= 0)
                        {
                            slot.WriteItemData(null);
                        }
                        else
                        {
                            slot.WriteItemData(new InventorySlot(slotData.item, amountLeft));
                        }
                    }
                }
            }
            slot.Refresh();
            UpdateGhostVisual();
        }
        else
        {
            // ── Floating item has items -> placing into slot ──
            var pc = FindFirstObjectByType<PlayerController>();
            bool isCreative = pc != null && pc.isCreativeMode;

            if (isCreative && slot.owner == SlotUI.Owner.Inventory)
            {
                if (slotData != null && slotData.item != null)
                {
                    int amountToGet = isLeftClick ? (slotData.item.toolType == ToolType.None ? 64 : 1) : 1;
                    heldItem = new InventorySlot(slotData.item, amountToGet);
                }
                else
                {
                    heldItem = null;
                }
                slot.Refresh();
                UpdateGhostVisual();
                return;
            }

            if (slot.owner == SlotUI.Owner.CraftingOutput)
            {
                if (slotData != null && slotData.item != null && slotData.item.itemName == heldItem.item.itemName)
                {
                    heldItem.amount += slotData.amount;
                    Inventory.Instance?.ConsumeCraftingInputs();
                    Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                    slot.Refresh();
                    UpdateGhostVisual();
                }
                return;
            }

            if (slot.owner == SlotUI.Owner.TableCraftingOutput)
            {
                if (slotData != null && slotData.item != null && slotData.item.itemName == heldItem.item.itemName)
                {
                    heldItem.amount += slotData.amount;
                    Inventory.Instance?.ConsumeTableCraftingInputs();
                    Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                    slot.Refresh();
                    UpdateGhostVisual();
                }
                return;
            }

            if (slot.owner == SlotUI.Owner.FurnaceOutput)
            {
                if (slotData != null && slotData.item != null && slotData.item.itemName == heldItem.item.itemName)
                {
                    int space = 64 - slotData.amount;
                    int toAdd = Mathf.Min(space, heldItem.amount);
                    if (toAdd > 0)
                    {
                        heldItem.amount -= toAdd;
                        slot.WriteItemData(new InventorySlot(slotData.item, slotData.amount + toAdd));
                        if (heldItem.amount <= 0) heldItem = null;
                        slot.Refresh();
                        UpdateGhostVisual();
                    }
                }
                return;
            }

            // Validation checks before placing/swapping into slots
            if (slot.owner == SlotUI.Owner.FurnaceFuel)
            {
                if (!FurnaceManager.IsFuel(heldItem.item))
                    return; // Fuel slot only accepts fuel (Coal, Wood, Plank, Stick)
            }
            else if (slot.owner == SlotUI.Owner.FurnaceInput)
            {
                if (heldItem.item.itemName != "Sand")
                    return; // Input slot only accepts Sand
            }
            else if (slot.owner == SlotUI.Owner.FurnaceOutput)
            {
                return; // Output slot cannot have items dropped into it directly
            }

            if (slotData == null || slotData.item == null)
            {
                if (isLeftClick)
                {
                    slot.WriteItemData(heldItem);
                    heldItem = null;
                }
                else
                {
                    slot.WriteItemData(new InventorySlot(heldItem.item, 1));
                    heldItem.amount--;
                    if (heldItem.amount <= 0) heldItem = null;
                }
            }
            else if (slotData.item.itemName == heldItem.item.itemName && heldItem.item.toolType == ToolType.None)
            {
                if (isLeftClick)
                {
                    slot.WriteItemData(new InventorySlot(slotData.item, slotData.amount + heldItem.amount));
                    heldItem = null;
                }
                else
                {
                    slot.WriteItemData(new InventorySlot(slotData.item, slotData.amount + 1));
                    heldItem.amount--;
                    if (heldItem.amount <= 0) heldItem = null;
                }
            }
            else
            {
                // Swap items
                var temp = heldItem;
                heldItem = slotData;
                slot.WriteItemData(temp);
            }

            slot.Refresh();
            UpdateGhostVisual();
            Inventory.Instance?.onInventoryChangedCallback?.Invoke();
        }
    }

    private void HandleShiftClick(SlotUI slot)
    {
        if (slot == null) return;
        var slotData = slot.GetItemData();
        if (slotData == null || slotData.item == null || slotData.amount <= 0) return;

        // 1. Shift-Clicking Crafting Output or Table Crafting Output (Craft-All/Bulk)
        if (slot.owner == SlotUI.Owner.CraftingOutput || slot.owner == SlotUI.Owner.TableCraftingOutput)
        {
            int itemsCrafted = 0;
            while (true)
            {
                var currentOutput = slot.GetItemData();
                if (currentOutput == null || currentOutput.item == null || currentOutput.amount <= 0)
                    break;

                int amountToMove = currentOutput.amount;
                Item itemToMove = currentOutput.item;

                bool added = false;
                if (Hotbar.Instance != null)
                {
                    added = Hotbar.Instance.TryAddItem(itemToMove, amountToMove);
                }
                if (!added && Inventory.Instance != null)
                {
                    added = Inventory.Instance.Add(itemToMove, amountToMove);
                }

                if (added)
                {
                    if (slot.owner == SlotUI.Owner.CraftingOutput)
                    {
                        Inventory.Instance?.ConsumeCraftingInputs();
                    }
                    else
                    {
                        Inventory.Instance?.ConsumeTableCraftingInputs();
                    }
                    itemsCrafted++;
                }
                else
                {
                    break; // Inventory and hotbar are full
                }
            }

            if (itemsCrafted > 0)
            {
                Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                slot.Refresh();
            }
            return;
        }

        // 2. Shift-Clicking Hotbar Slot -> Transfer to main inventory (or furnace if furnace open)
        if (slot.owner == SlotUI.Owner.Hotbar)
        {
            if (InventoryUI.Instance != null && InventoryUI.Instance.isFurnaceActive && FurnaceManager.ActiveFurnace != null)
            {
                if (FurnaceManager.IsFuel(slotData.item))
                {
                    var fuel = FurnaceManager.ActiveFurnace.fuelSlot;
                    if (fuel == null || fuel.item == null)
                    {
                        FurnaceManager.ActiveFurnace.fuelSlot = new InventorySlot(slotData.item, slotData.amount);
                        slot.WriteItemData(null);
                        slot.Refresh();
                        InventoryUI.Instance.RefreshFurnaceUI();
                        return;
                    }
                    else if (fuel.item.itemName == slotData.item.itemName)
                    {
                        int space = 64 - fuel.amount;
                        int toAdd = Mathf.Min(space, slotData.amount);
                        if (toAdd > 0)
                        {
                            fuel.amount += toAdd;
                            int left = slotData.amount - toAdd;
                            slot.WriteItemData(left > 0 ? new InventorySlot(slotData.item, left) : null);
                            slot.Refresh();
                            InventoryUI.Instance.RefreshFurnaceUI();
                            return;
                        }
                    }
                }
                
                var input = FurnaceManager.ActiveFurnace.inputSlot;
                if (input == null || input.item == null)
                {
                    FurnaceManager.ActiveFurnace.inputSlot = new InventorySlot(slotData.item, slotData.amount);
                    slot.WriteItemData(null);
                    slot.Refresh();
                    InventoryUI.Instance.RefreshFurnaceUI();
                    return;
                }
                else if (input.item.itemName == slotData.item.itemName)
                {
                    int space = 64 - input.amount;
                    int toAdd = Mathf.Min(space, slotData.amount);
                    if (toAdd > 0)
                    {
                        input.amount += toAdd;
                        int left = slotData.amount - toAdd;
                        slot.WriteItemData(left > 0 ? new InventorySlot(slotData.item, left) : null);
                        slot.Refresh();
                        InventoryUI.Instance.RefreshFurnaceUI();
                        return;
                    }
                }
            }

            if (Inventory.Instance != null)
            {
                bool added = Inventory.Instance.Add(slotData.item, slotData.amount);
                if (added)
                {
                    slot.WriteItemData(null);
                    slot.Refresh();
                    Inventory.Instance.onInventoryChangedCallback?.Invoke();
                }
            }
            return;
        }

        // 3. Shift-Clicking Main Inventory Slot -> Transfer to hotbar (or furnace if furnace open)
        if (slot.owner == SlotUI.Owner.Inventory)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            bool isCreative = pc != null && pc.isCreativeMode;

            if (isCreative)
            {
                if (Hotbar.Instance != null)
                {
                    int amountToMove = slotData.item.toolType == ToolType.None ? 64 : 1;
                    Hotbar.Instance.TryAddItem(slotData.item, amountToMove);
                }
                return;
            }

            if (InventoryUI.Instance != null && InventoryUI.Instance.isFurnaceActive && FurnaceManager.ActiveFurnace != null)
            {
                if (FurnaceManager.IsFuel(slotData.item))
                {
                    var fuel = FurnaceManager.ActiveFurnace.fuelSlot;
                    if (fuel == null || fuel.item == null)
                    {
                        FurnaceManager.ActiveFurnace.fuelSlot = new InventorySlot(slotData.item, slotData.amount);
                        slot.WriteItemData(null);
                        slot.Refresh();
                        InventoryUI.Instance.RefreshFurnaceUI();
                        return;
                    }
                    else if (fuel.item.itemName == slotData.item.itemName)
                    {
                        int space = 64 - fuel.amount;
                        int toAdd = Mathf.Min(space, slotData.amount);
                        if (toAdd > 0)
                        {
                            fuel.amount += toAdd;
                            int left = slotData.amount - toAdd;
                            slot.WriteItemData(left > 0 ? new InventorySlot(slotData.item, left) : null);
                            slot.Refresh();
                            InventoryUI.Instance.RefreshFurnaceUI();
                            return;
                        }
                    }
                }
                
                var input = FurnaceManager.ActiveFurnace.inputSlot;
                if (input == null || input.item == null)
                {
                    FurnaceManager.ActiveFurnace.inputSlot = new InventorySlot(slotData.item, slotData.amount);
                    slot.WriteItemData(null);
                    slot.Refresh();
                    InventoryUI.Instance.RefreshFurnaceUI();
                    return;
                }
                else if (input.item.itemName == slotData.item.itemName)
                {
                    int space = 64 - input.amount;
                    int toAdd = Mathf.Min(space, slotData.amount);
                    if (toAdd > 0)
                    {
                        input.amount += toAdd;
                        int left = slotData.amount - toAdd;
                        slot.WriteItemData(left > 0 ? new InventorySlot(slotData.item, left) : null);
                        slot.Refresh();
                        InventoryUI.Instance.RefreshFurnaceUI();
                        return;
                    }
                }
            }

            if (Hotbar.Instance != null)
            {
                bool added = Hotbar.Instance.TryAddItem(slotData.item, slotData.amount);
                if (added)
                {
                    slot.WriteItemData(null);
                    slot.Refresh();
                    Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                }
            }
            return;
        }

        // 4. Shift-Clicking Crafting Input Slot -> Transfer back to hotbar/inventory
        if (slot.owner == SlotUI.Owner.CraftingInput || slot.owner == SlotUI.Owner.TableCraftingInput ||
            slot.owner == SlotUI.Owner.FurnaceInput || slot.owner == SlotUI.Owner.FurnaceFuel || slot.owner == SlotUI.Owner.FurnaceOutput)
        {
            bool added = false;
            if (Hotbar.Instance != null)
            {
                added = Hotbar.Instance.TryAddItem(slotData.item, slotData.amount);
            }
            if (!added && Inventory.Instance != null)
            {
                added = Inventory.Instance.Add(slotData.item, slotData.amount);
            }

            if (added)
            {
                slot.WriteItemData(null);
                slot.Refresh();
                Inventory.Instance?.onInventoryChangedCallback?.Invoke();
            }
            return;
        }
    }

    // ── Split Mode Helpers ────────────────────────────────────────────────────

    public void EnterSplitMode(SlotUI slot)
    {
        ExitSplitMode();

        splitSourceSlot = slot;
        isInSplitMode = true;

        if (slot != null)
        {
            splitOutline = slot.gameObject.GetComponent<Outline>();
            if (splitOutline == null)
            {
                splitOutline = slot.gameObject.AddComponent<Outline>();
            }
            splitOutline.effectColor = new Color(0.6f, 0.2f, 0.8f, 1f); // Purple
            splitOutline.effectDistance = new Vector2(3f, 3f);
            splitOutline.enabled = true;
        }
    }

    public void ExitSplitMode()
    {
        if (splitOutline != null)
        {
            splitOutline.enabled = false;
            Destroy(splitOutline);
            splitOutline = null;
        }
        splitSourceSlot = null;
        isInSplitMode = false;
    }

    // ── Floating Item Helpers ─────────────────────────────────────────────────

    public bool HasHeldItem()
    {
        return heldItem != null && heldItem.item != null && heldItem.amount > 0;
    }

    public void ReturnHeldItem()
    {
        if (heldItem != null && heldItem.item != null && heldItem.amount > 0)
        {
            bool added = false;
            if (Hotbar.Instance != null)
                added = Hotbar.Instance.TryAddItem(heldItem.item, heldItem.amount);
            if (!added && Inventory.Instance != null)
                added = Inventory.Instance.Add(heldItem.item, heldItem.amount);

            if (!added)
            {
                // Drop in world
                GameObject player = GameObject.FindWithTag("Player");
                Vector3 dropPos = player != null ? player.transform.position + Vector3.up : Vector3.zero;
                DroppedItem dropped = DroppedItem.Spawn(heldItem.item, heldItem.amount, dropPos, (byte)heldItem.item.blockTypeID);
                if (dropped != null && WrenchItem.Instance != null && heldItem.item.itemID == WrenchItem.Instance.wrenchItemID)
                {
                    dropped.overrideMesh = WrenchItem.BuildWrenchMesh();
                }
            }
            heldItem = null;
            UpdateGhostVisual();
        }
    }

    private void DropHeldItemInWorld()
    {
        if (heldItem != null && heldItem.item != null && heldItem.amount > 0)
        {
            GameObject player = GameObject.FindWithTag("Player");
            Vector3 dropPos = player != null ? player.transform.position + Vector3.up : Vector3.zero;
            DroppedItem dropped = DroppedItem.Spawn(heldItem.item, heldItem.amount, dropPos, (byte)heldItem.item.blockTypeID);

            if (dropped != null && WrenchItem.Instance != null && heldItem.item.itemID == WrenchItem.Instance.wrenchItemID)
            {
                dropped.overrideMesh = WrenchItem.BuildWrenchMesh();
            }

            heldItem = null;
            UpdateGhostVisual();
        }
    }

    private void UpdateGhostVisual()
    {
        if (ghost == null) return;

        if (heldItem != null && heldItem.item != null && heldItem.amount > 0)
        {
            ghost.sprite = heldItem.item.icon;
            ghost.enabled = true;
            if (ghostText != null)
            {
                bool isCreative = false;
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null && player.isCreativeMode) isCreative = true;

                string amtStr = (heldItem.amount > 1 && !isCreative) ? heldItem.amount.ToString() : "";
                ghostText.text = amtStr;
                ghostText.enabled = true;
                if (ghostOutlineTexts != null)
                {
                    for (int i = 0; i < ghostOutlineTexts.Length; i++)
                    {
                        if (ghostOutlineTexts[i] != null)
                        {
                            ghostOutlineTexts[i].text = amtStr;
                            ghostOutlineTexts[i].enabled = true;
                        }
                    }
                }
            }
            if (ghostNameLabel != null)
            {
                ghostNameLabel.text = heldItem.item.itemName;
                ghostNameLabel.gameObject.SetActive(true); // show whole GO (includes bg image)
            }
        }
        else
        {
            ghost.sprite = null;
            ghost.enabled = false;
            if (ghostText != null)
            {
                ghostText.text = "";
                ghostText.enabled = false;
            }
            if (ghostOutlineTexts != null)
            {
                for (int i = 0; i < ghostOutlineTexts.Length; i++)
                {
                    if (ghostOutlineTexts[i] != null)
                    {
                        ghostOutlineTexts[i].text = "";
                        ghostOutlineTexts[i].enabled = false;
                    }
                }
            }
            if (ghostNameLabel != null)
            {
                ghostNameLabel.text = "";
                ghostNameLabel.gameObject.SetActive(false); // hide whole GO (includes bg image)
            }
        }
    }

    // ── Raycast Helpers ───────────────────────────────────────────────────────

    private SlotUI RaycastSlot(Vector2 screenPos)
    {
        if (EventSystem.current == null) return null;

        var ped     = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        if (results.Count > 0)
        {
            // If the topmost UI element is not a SlotUI, don't interact with it.
            // This prevents click-through of pause panels to the inventory slots behind.
            SlotUI topSlot = results[0].gameObject.GetComponentInParent<SlotUI>();
            if (topSlot != null)
            {
                return topSlot;
            }
        }
        return null;
    }

    public static bool IsPointerOverUI()
    {
        if (Mouse.current == null || EventSystem.current == null) return false;

        var ped     = new PointerEventData(EventSystem.current)
                      { position = Mouse.current.position.ReadValue() };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        return results.Count > 0;
    }
}
