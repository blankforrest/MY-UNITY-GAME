using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public const int MaxSlots = 84; // 7x12

    public delegate void OnInventoryChanged();
    public OnInventoryChanged onInventoryChangedCallback;

    // Fixed 35-slot array — null means empty
    public InventorySlot[] slots = new InventorySlot[MaxSlots];

    // 2x2 crafting grid and result slot
    public InventorySlot[] craftingSlots = new InventorySlot[4];
    public InventorySlot craftingResultSlot = null;

    // 3x3 crafting grid and result slot for the placed crafting table
    public InventorySlot[] tableCraftingSlots = new InventorySlot[9];
    public InventorySlot tableCraftingResultSlot = null;
    public bool is3x3Active = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Unity may have serialized a stale 16-element array.
        // Resize to MaxSlots and preserve any existing items.
        if (slots == null || slots.Length != MaxSlots)
        {
            var old = slots;
            slots = new InventorySlot[MaxSlots];
            if (old != null)
                for (int i = 0; i < Mathf.Min(old.Length, MaxSlots); i++)
                    slots[i] = old[i];
        }

        if (craftingSlots == null || craftingSlots.Length != 4)
        {
            craftingSlots = new InventorySlot[4];
        }

        if (tableCraftingSlots == null || tableCraftingSlots.Length != 9)
        {
            tableCraftingSlots = new InventorySlot[9];
        }
    }


    public bool Add(Item item, int amount)
    {
        if (item == null) return false;

        // Tools are not stackable
        bool isTool = item.toolType != ToolType.None;
        if (!isTool)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].item != null && slots[i].item.itemName == item.itemName)
                { slots[i].amount += amount; onInventoryChangedCallback?.Invoke(); return true; }
        }

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null || slots[i].item == null)
            { slots[i] = new InventorySlot(item, amount); onInventoryChangedCallback?.Invoke(); return true; }

        return false;
    }

    public void SetSlot(int index, Item item, int amount, bool silent = false)
    {
        if (index < 0 || index >= slots.Length) return; // use runtime length, not const
        slots[index] = (item != null) ? new InventorySlot(item, amount) : null;
        if (!silent) onInventoryChangedCallback?.Invoke();
    }

    public void ClearSlot(int index, bool silent = false)
    {
        SetSlot(index, null, 0, silent);
    }

    public void SwapSlots(int a, int b)
    {
        if (a < 0 || a >= slots.Length || b < 0 || b >= slots.Length) return;
        (slots[b], slots[a]) = (slots[a], slots[b]);
        onInventoryChangedCallback?.Invoke();
    }

    // ── Crafting System Logic ───────────────────────────────────────────────────

    private int GetCraftingMultiplier()
    {
        int minAmount = int.MaxValue;
        bool hasIngredients = false;
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            if (craftingSlots[i] != null && craftingSlots[i].item != null && craftingSlots[i].amount > 0)
            {
                hasIngredients = true;
                if (craftingSlots[i].amount < minAmount)
                {
                    minAmount = craftingSlots[i].amount;
                }
            }
        }
        return hasIngredients ? minAmount : 1;
    }

    private int GetTableCraftingMultiplier()
    {
        int minAmount = int.MaxValue;
        bool hasIngredients = false;
        for (int i = 0; i < tableCraftingSlots.Length; i++)
        {
            if (tableCraftingSlots[i] != null && tableCraftingSlots[i].item != null && tableCraftingSlots[i].amount > 0)
            {
                hasIngredients = true;
                if (tableCraftingSlots[i].amount < minAmount)
                {
                    minAmount = tableCraftingSlots[i].amount;
                }
            }
        }
        return hasIngredients ? minAmount : 1;
    }

    public void UpdateCraftingOutput()
    {
        craftingResultSlot = null;

        Item i0 = craftingSlots[0]?.item;
        Item i1 = craftingSlots[1]?.item;
        Item i2 = craftingSlots[2]?.item;
        Item i3 = craftingSlots[3]?.item;

        // Recipe 1: 1 Wood -> 4 Planks (blockTypeID = 2)
        bool isWoodMatch = false;
        if (i0?.itemName == "Wood" && i1 == null && i2 == null && i3 == null) isWoodMatch = true;
        else if (i1?.itemName == "Wood" && i0 == null && i2 == null && i3 == null) isWoodMatch = true;
        else if (i2?.itemName == "Wood" && i0 == null && i1 == null && i3 == null) isWoodMatch = true;
        else if (i3?.itemName == "Wood" && i0 == null && i1 == null && i2 == null) isWoodMatch = true;

        if (isWoodMatch)
        {
            int mult = GetCraftingMultiplier();
            craftingResultSlot = new InventorySlot(CreateItem("Plank", 2), 4 * mult);
            return;
        }

        // Recipe 2: 2 Planks (vertically aligned) -> 4 Sticks (blockTypeID = 0)
        bool isStickMatch = false;
        if (i0?.itemName == "Plank" && i2?.itemName == "Plank" && i1 == null && i3 == null) isStickMatch = true;
        else if (i1?.itemName == "Plank" && i3?.itemName == "Plank" && i0 == null && i2 == null) isStickMatch = true;

        if (isStickMatch)
        {
            int mult = GetCraftingMultiplier();
            craftingResultSlot = new InventorySlot(CreateItem("Stick", 0), 4 * mult);
            return;
        }

        // Recipe 3: 4 Planks -> 1 Crafting Table (blockTypeID = 36)
        if (i0?.itemName == "Plank" && i1?.itemName == "Plank" && i2?.itemName == "Plank" && i3?.itemName == "Plank")
        {
            int mult = GetCraftingMultiplier();
            craftingResultSlot = new InventorySlot(CreateItem("Crafting Table", 36), 1 * mult);
            return;
        }

        // Recipe 4: 1 Plank + 1 Stick (vertical: Plank top, Stick bottom) -> 1 Small Wheel (blockTypeID = 20)
        bool isSmallWheelMatch = false;
        if (i0?.itemName == "Plank" && i2?.itemName == "Stick" && i1 == null && i3 == null) isSmallWheelMatch = true;
        else if (i1?.itemName == "Plank" && i3?.itemName == "Stick" && i0 == null && i2 == null) isSmallWheelMatch = true;

        if (isSmallWheelMatch)
        {
            int mult = GetCraftingMultiplier();
            craftingResultSlot = new InventorySlot(CreateItem("Small Wheel", 20), 1 * mult);
            return;
        }

        // Recipe 5: 2 Planks (top row) + 2 Sticks (bottom row) -> 1 Large Wheel (blockTypeID = 21)
        if (i0?.itemName == "Plank" && i1?.itemName == "Plank" && i2?.itemName == "Stick" && i3?.itemName == "Stick")
        {
            int mult = GetCraftingMultiplier();
            craftingResultSlot = new InventorySlot(CreateItem("Large Wheel", 21), 1 * mult);
            return;
        }

        // Recipe 6: 2 Planks + 2 Sticks (diagonally) -> 1 Propeller (blockTypeID = 22)
        bool isPropellerMatch = false;
        if (i0?.itemName == "Plank" && i3?.itemName == "Plank" && i1?.itemName == "Stick" && i2?.itemName == "Stick") isPropellerMatch = true;
        else if (i1?.itemName == "Plank" && i2?.itemName == "Plank" && i0?.itemName == "Stick" && i3?.itemName == "Stick") isPropellerMatch = true;

        if (isPropellerMatch)
        {
            int mult = GetCraftingMultiplier();
            craftingResultSlot = new InventorySlot(CreateItem("Propeller", 22), 1 * mult);
            return;
        }

        // No recipe matches
        craftingResultSlot = null;
    }

    public void ConsumeCraftingInputs()
    {
        int multiplier = GetCraftingMultiplier();
        if (craftingResultSlot != null && craftingResultSlot.item != null && craftingResultSlot.item.toolType != ToolType.None)
        {
            multiplier = 1;
        }

        for (int i = 0; i < craftingSlots.Length; i++)
        {
            if (craftingSlots[i] != null)
            {
                craftingSlots[i].amount -= multiplier;
                if (craftingSlots[i].amount <= 0)
                    craftingSlots[i] = null;
            }
        }
        UpdateCraftingOutput();
    }

    public void ReturnCraftingInputs()
    {
        bool changed = false;
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            if (craftingSlots[i] != null && craftingSlots[i].item != null)
            {
                int amount = craftingSlots[i].amount;
                Item item = craftingSlots[i].item;
                craftingSlots[i] = null;

                bool added = false;
                if (Hotbar.Instance != null)
                    added = Hotbar.Instance.TryAddItem(item, amount);
                if (!added)
                    added = Add(item, amount);

                if (!added)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    Vector3 dropPos = player != null ? player.transform.position + Vector3.up : Vector3.zero;
                    DroppedItem.Spawn(item, amount, dropPos, (byte)item.blockTypeID);
                }
                changed = true;
            }
        }
        if (changed)
        {
            UpdateCraftingOutput();
            onInventoryChangedCallback?.Invoke();
        }
    }

    public void UpdateTableCraftingOutput()
    {
        tableCraftingResultSlot = null;

        // First check tool recipes
        CheckToolRecipes();
        if (tableCraftingResultSlot != null) return;

        // Check 3x3-only recipes first
        // Recipe: Control Block (4 Planks in corners, 1 Iron/Stone in center)
        bool isControlBlock3x3 = true;
        for (int i = 0; i < 9; i++)
        {
            var slot = tableCraftingSlots[i];
            if (i == 0 || i == 2 || i == 6 || i == 8)
            {
                if (slot?.item?.itemName != "Plank") isControlBlock3x3 = false;
            }
            else if (i == 4)
            {
                string n = slot?.item?.itemName;
                if (n != "Iron Block" && n != "Iron Ore" && n != "Stone" && n != "Gravel") isControlBlock3x3 = false;
            }
            else
            {
                if (slot != null && slot.item != null) isControlBlock3x3 = false;
            }
        }
        if (isControlBlock3x3)
        {
            int mult = GetTableCraftingMultiplier();
            tableCraftingResultSlot = new InventorySlot(CreateItem("Control Block", 50), 1 * mult);
            return;
        }

        // Recipe: Furnace (8 Stone in a ring)
        bool isFurnace3x3 = true;
        for (int i = 0; i < 9; i++)
        {
            var slot = tableCraftingSlots[i];
            if (i == 4)
            {
                if (slot != null && slot.item != null) isFurnace3x3 = false;
            }
            else
            {
                if (slot?.item?.itemName != "Stone" && slot?.item?.itemName != "Gravel") isFurnace3x3 = false;
            }
        }
        if (isFurnace3x3)
        {
            int mult = GetTableCraftingMultiplier();
            tableCraftingResultSlot = new InventorySlot(CreateItem("Furnace", 37), 1 * mult);
            return;
        }

        // Recipe: Wooden Stairs (Pattern A & B)
        bool isWoodStairsA = true;
        bool isWoodStairsB = true;
        bool isStoneStairsA = true;
        bool isStoneStairsB = true;

        int[] indicesA = new int[] { 0, 3, 4, 6, 7, 8 };
        int[] emptyA = new int[] { 1, 2, 5 };

        int[] indicesB = new int[] { 2, 4, 5, 6, 7, 8 };
        int[] emptyB = new int[] { 0, 1, 3 };

        foreach (int idx in indicesA)
        {
            if (tableCraftingSlots[idx]?.item?.itemName != "Plank") isWoodStairsA = false;
            if (tableCraftingSlots[idx]?.item?.itemName != "Stone" && tableCraftingSlots[idx]?.item?.itemName != "Gravel") isStoneStairsA = false;
        }
        foreach (int idx in emptyA)
        {
            if (tableCraftingSlots[idx] != null && tableCraftingSlots[idx].item != null)
            {
                isWoodStairsA = false;
                isStoneStairsA = false;
            }
        }

        foreach (int idx in indicesB)
        {
            if (tableCraftingSlots[idx]?.item?.itemName != "Plank") isWoodStairsB = false;
            if (tableCraftingSlots[idx]?.item?.itemName != "Stone" && tableCraftingSlots[idx]?.item?.itemName != "Gravel") isStoneStairsB = false;
        }
        foreach (int idx in emptyB)
        {
            if (tableCraftingSlots[idx] != null && tableCraftingSlots[idx].item != null)
            {
                isWoodStairsB = false;
                isStoneStairsB = false;
            }
        }

        if (isWoodStairsA || isWoodStairsB)
        {
            int mult = GetTableCraftingMultiplier();
            tableCraftingResultSlot = new InventorySlot(CreateItem("Wooden Stairs", 38), 4 * mult);
            return;
        }

        if (isStoneStairsA || isStoneStairsB)
        {
            int mult = GetTableCraftingMultiplier();
            tableCraftingResultSlot = new InventorySlot(CreateItem("Stone Stairs", 39), 4 * mult);
            return;
        }

        // Recipe: Wooden Slab & Stone Slab
        // Can be in Row 0 (0,1,2), Row 1 (3,4,5), or Row 2 (6,7,8)
        bool isWoodSlab = false;
        bool isStoneSlab = false;

        for (int r = 0; r < 3; r++)
        {
            bool rowIsPlank = true;
            bool rowIsStone = true;
            for (int c = 0; c < 3; c++)
            {
                int idx = r * 3 + c;
                if (tableCraftingSlots[idx]?.item?.itemName != "Plank") rowIsPlank = false;
                if (tableCraftingSlots[idx]?.item?.itemName != "Stone" && tableCraftingSlots[idx]?.item?.itemName != "Gravel") rowIsStone = false;
            }

            bool othersEmpty = true;
            for (int or = 0; or < 3; or++)
            {
                if (or == r) continue;
                for (int c = 0; c < 3; c++)
                {
                    int idx = or * 3 + c;
                    if (tableCraftingSlots[idx] != null && tableCraftingSlots[idx].item != null)
                    {
                        othersEmpty = false;
                    }
                }
            }

            if (othersEmpty)
            {
                if (rowIsPlank) { isWoodSlab = true; break; }
                if (rowIsStone) { isStoneSlab = true; break; }
            }
        }

        if (isWoodSlab)
        {
            int mult = GetTableCraftingMultiplier();
            tableCraftingResultSlot = new InventorySlot(CreateItem("Wooden Slab", 46), 6 * mult);
            return;
        }
        if (isStoneSlab)
        {
            int mult = GetTableCraftingMultiplier();
            tableCraftingResultSlot = new InventorySlot(CreateItem("Stone Slab", 47), 6 * mult);
            return;
        }

        // Bounding-box matching for 2x2 and 1x1 recipes
        int minCol = 3, maxCol = -1, minRow = 3, maxRow = -1;
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                int idx = r * 3 + c;
                if (tableCraftingSlots[idx] != null && tableCraftingSlots[idx].item != null)
                {
                    if (c < minCol) minCol = c;
                    if (c > maxCol) maxCol = c;
                    if (r < minRow) minRow = r;
                    if (r > maxRow) maxRow = r;
                }
            }
        }

        if (maxCol < minCol)
        {
            tableCraftingResultSlot = null;
            return;
        }

        int outW = (maxCol - minCol) + 1;
        int outH = (maxRow - minRow) + 1;

        if (outW <= 2 && outH <= 2)
        {
            // Map bounding box to a virtual 2x2 grid
            Item[] boxSlots = new Item[4];
            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    int srcIdx = r * 3 + c;
                    int dstR = r - minRow;
                    int dstC = c - minCol;
                    int dstIdx = dstR * 2 + dstC;
                    boxSlots[dstIdx] = tableCraftingSlots[srcIdx]?.item;
                }
            }

            Item i0 = boxSlots[0];
            Item i1 = boxSlots[1];
            Item i2 = boxSlots[2];
            Item i3 = boxSlots[3];

            // Recipe 1: 1 Wood -> 4 Planks
            bool isWoodMatch = false;
            if (i0?.itemName == "Wood" && i1 == null && i2 == null && i3 == null) isWoodMatch = true;
            else if (i1?.itemName == "Wood" && i0 == null && i2 == null && i3 == null) isWoodMatch = true;
            else if (i2?.itemName == "Wood" && i0 == null && i1 == null && i3 == null) isWoodMatch = true;
            else if (i3?.itemName == "Wood" && i0 == null && i1 == null && i2 == null) isWoodMatch = true;

            if (isWoodMatch)
            {
                int mult = GetTableCraftingMultiplier();
                tableCraftingResultSlot = new InventorySlot(CreateItem("Plank", 2), 4 * mult);
                return;
            }

            // Recipe 2: 2 Planks (vertically aligned) -> 4 Sticks
            bool isStickMatch = false;
            if (i0?.itemName == "Plank" && i2?.itemName == "Plank" && i1 == null && i3 == null) isStickMatch = true;
            else if (i1?.itemName == "Plank" && i3?.itemName == "Plank" && i0 == null && i2 == null) isStickMatch = true;

            if (isStickMatch)
            {
                int mult = GetTableCraftingMultiplier();
                tableCraftingResultSlot = new InventorySlot(CreateItem("Stick", 0), 4 * mult);
                return;
            }

            // Recipe 3: 4 Planks -> 1 Crafting Table (blockTypeID = 36)
            if (i0?.itemName == "Plank" && i1?.itemName == "Plank" && i2?.itemName == "Plank" && i3?.itemName == "Plank")
            {
                int mult = GetTableCraftingMultiplier();
                tableCraftingResultSlot = new InventorySlot(CreateItem("Crafting Table", 36), 1 * mult);
                return;
            }

            // Recipe 4: 1 Plank + 1 Stick (vertical: Plank top, Stick bottom) -> 1 Small Wheel (blockTypeID = 20)
            bool isSmallWheelMatch = false;
            if (i0?.itemName == "Plank" && i2?.itemName == "Stick" && i1 == null && i3 == null) isSmallWheelMatch = true;
            else if (i1?.itemName == "Plank" && i3?.itemName == "Stick" && i0 == null && i2 == null) isSmallWheelMatch = true;

            if (isSmallWheelMatch)
            {
                int mult = GetTableCraftingMultiplier();
                tableCraftingResultSlot = new InventorySlot(CreateItem("Small Wheel", 20), 1 * mult);
                return;
            }

            // Recipe 5: 2 Planks (top row) + 2 Sticks (bottom row) -> 1 Large Wheel (blockTypeID = 21)
            if (i0?.itemName == "Plank" && i1?.itemName == "Plank" && i2?.itemName == "Stick" && i3?.itemName == "Stick")
            {
                int mult = GetTableCraftingMultiplier();
                tableCraftingResultSlot = new InventorySlot(CreateItem("Large Wheel", 21), 1 * mult);
                return;
            }

            // Recipe 6: 2 Planks + 2 Sticks (diagonally) -> 1 Propeller (blockTypeID = 22)
            bool isPropellerMatch = false;
            if (i0?.itemName == "Plank" && i3?.itemName == "Plank" && i1?.itemName == "Stick" && i2?.itemName == "Stick") isPropellerMatch = true;
            else if (i1?.itemName == "Plank" && i2?.itemName == "Plank" && i0?.itemName == "Stick" && i3?.itemName == "Stick") isPropellerMatch = true;

            if (isPropellerMatch)
            {
                int mult = GetTableCraftingMultiplier();
                tableCraftingResultSlot = new InventorySlot(CreateItem("Propeller", 22), 1 * mult);
                return;
            }
        }

        // No recipe matches
        tableCraftingResultSlot = null;
    }

    public void ConsumeTableCraftingInputs()
    {
        int multiplier = GetTableCraftingMultiplier();
        if (tableCraftingResultSlot != null && tableCraftingResultSlot.item != null && tableCraftingResultSlot.item.toolType != ToolType.None)
        {
            multiplier = 1;
        }

        for (int i = 0; i < tableCraftingSlots.Length; i++)
        {
            if (tableCraftingSlots[i] != null)
            {
                tableCraftingSlots[i].amount -= multiplier;
                if (tableCraftingSlots[i].amount <= 0)
                    tableCraftingSlots[i] = null;
            }
        }
        UpdateTableCraftingOutput();
    }

    public void ReturnTableCraftingInputs()
    {
        bool changed = false;
        for (int i = 0; i < tableCraftingSlots.Length; i++)
        {
            if (tableCraftingSlots[i] != null && tableCraftingSlots[i].item != null)
            {
                int amount = tableCraftingSlots[i].amount;
                Item item = tableCraftingSlots[i].item;
                tableCraftingSlots[i] = null;

                bool added = false;
                if (Hotbar.Instance != null)
                    added = Hotbar.Instance.TryAddItem(item, amount);
                if (!added)
                    added = Add(item, amount);

                if (!added)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    Vector3 dropPos = player != null ? player.transform.position + Vector3.up : Vector3.zero;
                    DroppedItem.Spawn(item, amount, dropPos, (byte)item.blockTypeID);
                }
                changed = true;
            }
        }
        if (changed)
        {
            UpdateTableCraftingOutput();
            onInventoryChangedCallback?.Invoke();
        }
    }

    public Item CreateItem(string itemName, int blockTypeID)
    {
        Item item = ScriptableObject.CreateInstance<Item>();
        item.itemName = itemName;
        item.blockTypeID = blockTypeID;
        item.itemID = 0;

        if (itemName.Equals("Wrench", System.StringComparison.OrdinalIgnoreCase))
        {
            item.itemID = 99;
        }
        else if (itemName.Equals("Wolf Spawn Egg", System.StringComparison.OrdinalIgnoreCase))
        {
            item.itemID = 98;
        }
        else if (itemName.Equals("Coal Chunk", System.StringComparison.OrdinalIgnoreCase))
        {
            item.itemID = 97;
        }
        else if (itemName.Equals("Iron Ingot", System.StringComparison.OrdinalIgnoreCase))
        {
            item.itemID = 96;
        }
        else if (itemName.Equals("Sheep Spawn Egg", System.StringComparison.OrdinalIgnoreCase))
        {
            item.itemID = 95;
        }

        // Parse tool characteristics
        ToolType tType;
        ToolTier tTier;
        ParseToolName(itemName, out tType, out tTier);
        if (tType != ToolType.None)
        {
            item.toolType = tType;
            item.toolTier = tTier;
            item.icon = CreateToolIcon(tType, tTier);
            return item;
        }

        Sprite sprite = null;
        if (blockTypeID != 0)
        {
            BlockDefinition def = BlockRegistry.GetDefinition((byte)blockTypeID);
            if (def != null)
            {
                // If they have an inventoryIcon assigned manually in the inspector, prioritize it!
                if (def.inventoryIcon != null)
                {
                    sprite = def.inventoryIcon;
                }
                else
                {
                    bool hasCustomTextures = (def.textureTop != null || def.textureSide != null || def.textureBottom != null);
                    if (hasCustomTextures)
                    {
                        def.inventoryIcon = StarterItems.MakeIsometricBlock(blockTypeID, Color.white);
                        sprite = def.inventoryIcon;
                    }
                }
            }
        }

        if (sprite == null)
        {
            if (itemName.Equals("Grass Block", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = StarterItems.MakeGrassBlockIcon();
            }
            else
            {
                string cleanName = itemName.ToLower().Replace(" ", "_");
                sprite = Resources.Load<Sprite>("Sprites/" + cleanName + "_block");
                if (sprite == null)
                    sprite = Resources.Load<Sprite>("Sprites/" + cleanName);
            }
        }

        if (sprite == null)
        {
            if (itemName == "Stick")
                sprite = CreateStickIcon();
            else if (itemName == "Diamond")
                sprite = CreateDiamondIcon();
            else if (itemName == "Control Block")
                sprite = VehicleSpawner.CreateControlBlockIcon();
            else if (itemName == "Small Wheel")
                sprite = VehicleSpawner.CreateWheelIcon(false);
            else if (itemName == "Large Wheel")
                sprite = VehicleSpawner.CreateWheelIcon(true);
            else if (itemName == "Propeller")
                sprite = VehicleSpawner.CreatePropellerIcon();
            else if (itemName == "Large Propeller")
                sprite = VehicleSpawner.CreateLargePropellerIcon();
            else if (itemName.Equals("Apple", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = VoxelWorld.MakeAppleIcon();
            }
            else if (itemName.Equals("Flower", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = VoxelWorld.MakeFlowerIcon();
            }
            else if (itemName.Equals("Dandelion", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.95f, 0.85f, 0.10f), new Color(0.95f, 0.65f, 0.05f));
            }
            else if (itemName.Equals("Iris", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.55f, 0.18f), new Color(0.40f, 0.20f, 0.90f), new Color(1.00f, 0.80f, 0.10f));
            }
            else if (itemName.Equals("Grass Block", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 4)
            {
                sprite = StarterItems.MakeGrassBlockIcon();
            }
            else if (itemName.Equals("Short Grass", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 13)
            {
                sprite = StarterItems.MakeShortGrassIcon();
            }
            else if (itemName.Equals("Tall Grass", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 14)
            {
                sprite = StarterItems.MakeTallGrassIcon();
            }
            else if (itemName == "Wrench")
            {
                Sprite wrenchLoaded = Resources.Load<Sprite>("WrenchIcon");
                sprite = (wrenchLoaded != null) ? wrenchLoaded : StarterItems.MakeBlockIcon(new Color(0.75f, 0.75f, 0.75f));
            }
            else if (itemName.Equals("Wolf Spawn Egg", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = CreateWolfSpawnEggIcon();
            }
            else if (itemName.Equals("Sheep Spawn Egg", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = CreateSheepSpawnEggIcon();
            }
            else if (itemName.Equals("Coal Chunk", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = CreateCoalChunkIcon();
            }
            else if (itemName.Equals("Iron Ingot", System.StringComparison.OrdinalIgnoreCase))
            {
                sprite = CreateIronIngotIcon();
            }
            else if (itemName == "Iron")
                sprite = StarterItems.MakeBlockIcon(new Color(0.85f, 0.85f, 0.85f));
            else if (blockTypeID != 0)
            {
                Color blockColor = StarterItems.GetBlockColor(blockTypeID);
                sprite = StarterItems.MakeBlockIcon(blockColor, blockTypeID);
            }
            else
            {
                sprite = StarterItems.MakeBlockIcon(Color.gray);
            }
        }

        item.icon = sprite;
        return item;
    }

    private struct CreativeItemData
    {
        public string name;
        public int typeId;
        public int amount;
        public CreativeItemData(string name, int typeId, int amount)
        {
            this.name = name;
            this.typeId = typeId;
            this.amount = amount;
        }
    }

    public void PopulateCreativeCategory(string category)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            ClearSlot(i, silent: true);
        }

        System.Collections.Generic.List<CreativeItemData> items = new System.Collections.Generic.List<CreativeItemData>();

        if (category == "ALL")
        {
            // ── 1. Blocks ──────────────────────────────────────────
            items.Add(new CreativeItemData("Grass Block", 4, 64));
            items.Add(new CreativeItemData("Stone", 3, 64));
            items.Add(new CreativeItemData("Gravel", 56, 64));
            items.Add(new CreativeItemData("Plank", 2, 64));
            items.Add(new CreativeItemData("Wood", 1, 64));
            items.Add(new CreativeItemData("Dirt", 5, 64));
            items.Add(new CreativeItemData("Sand", 34, 64));
            items.Add(new CreativeItemData("Glass", 35, 64));
            items.Add(new CreativeItemData("Gold Block", 32, 64));
            items.Add(new CreativeItemData("Iron Block", 33, 64));
            items.Add(new CreativeItemData("Bedrock", 48, 64));
            items.Add(new CreativeItemData("Cactus", 49, 64));
            items.Add(new CreativeItemData("Birch Log", 51, 64));
            items.Add(new CreativeItemData("Birch Leaves", 52, 64));
            items.Add(new CreativeItemData("Spruce Log", 53, 64));
            items.Add(new CreativeItemData("Spruce Leaves", 54, 64));
            items.Add(new CreativeItemData("Coal Ore", 30, 64));
            items.Add(new CreativeItemData("Iron Ore", 31, 64));
            items.Add(new CreativeItemData("Diamond Ore", 55, 64));
            items.Add(new CreativeItemData("Crafting Table", 36, 64));
            items.Add(new CreativeItemData("Furnace", 37, 64));
            items.Add(new CreativeItemData("Wooden Stairs", 38, 64));
            items.Add(new CreativeItemData("Stone Stairs", 39, 64));
            items.Add(new CreativeItemData("Wooden Slab", 46, 64));
            items.Add(new CreativeItemData("Stone Slab", 47, 64));

            // ── 2. Tools & Materials ────────────────────────────────
            items.Add(new CreativeItemData("Diamond Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Diamond Axe", 0, 1));
            items.Add(new CreativeItemData("Diamond Shovel", 0, 1));
            items.Add(new CreativeItemData("Diamond Sword", 0, 1));
            items.Add(new CreativeItemData("Diamond Rake", 0, 1));
            items.Add(new CreativeItemData("Iron Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Iron Axe", 0, 1));
            items.Add(new CreativeItemData("Iron Shovel", 0, 1));
            items.Add(new CreativeItemData("Iron Sword", 0, 1));
            items.Add(new CreativeItemData("Iron Rake", 0, 1));
            items.Add(new CreativeItemData("Stone Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Stone Axe", 0, 1));
            items.Add(new CreativeItemData("Stone Shovel", 0, 1));
            items.Add(new CreativeItemData("Stone Sword", 0, 1));
            items.Add(new CreativeItemData("Stone Rake", 0, 1));
            items.Add(new CreativeItemData("Wooden Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Wooden Axe", 0, 1));
            items.Add(new CreativeItemData("Wooden Shovel", 0, 1));
            items.Add(new CreativeItemData("Wooden Sword", 0, 1));
            items.Add(new CreativeItemData("Wooden Rake", 0, 1));
            items.Add(new CreativeItemData("Wrench", 0, 1));
            items.Add(new CreativeItemData("Wolf Spawn Egg", 0, 64));
            items.Add(new CreativeItemData("Sheep Spawn Egg", 0, 64));
            items.Add(new CreativeItemData("Coal Chunk", 0, 64));
            items.Add(new CreativeItemData("Iron Ingot", 0, 64));
            items.Add(new CreativeItemData("Iron", 0, 64));
            items.Add(new CreativeItemData("Diamond", 0, 64));
            items.Add(new CreativeItemData("Stick", 0, 64));

            // ── 3. Vehicles ─────────────────────────────────────────
            items.Add(new CreativeItemData("Control Block", 50, 64));
            items.Add(new CreativeItemData("Small Wheel", 20, 64));
            items.Add(new CreativeItemData("Large Wheel", 21, 64));
            items.Add(new CreativeItemData("Propeller", 22, 64));
            items.Add(new CreativeItemData("Large Propeller", 26, 64));

            // ── 4. Foliage ──────────────────────────────────────────
            items.Add(new CreativeItemData("Short Grass", 13, 64));
            items.Add(new CreativeItemData("Tall Grass", 14, 64));
            items.Add(new CreativeItemData("Leaves", 12, 64));
            items.Add(new CreativeItemData("Flower", 9, 64));
            items.Add(new CreativeItemData("Dandelion", 10, 64));
            items.Add(new CreativeItemData("Iris", 11, 64));
            items.Add(new CreativeItemData("Apple", 0, 64));
        }
        else if (category == "BLOCKS")
        {
            items.Add(new CreativeItemData("Grass Block", 4, 64));
            items.Add(new CreativeItemData("Stone", 3, 64));
            items.Add(new CreativeItemData("Gravel", 56, 64));
            items.Add(new CreativeItemData("Plank", 2, 64));
            items.Add(new CreativeItemData("Wood", 1, 64));
            items.Add(new CreativeItemData("Dirt", 5, 64));
            items.Add(new CreativeItemData("Sand", 34, 64));
            items.Add(new CreativeItemData("Glass", 35, 64));
            items.Add(new CreativeItemData("Gold Block", 32, 64));
            items.Add(new CreativeItemData("Iron Block", 33, 64));
            items.Add(new CreativeItemData("Bedrock", 48, 64));
            items.Add(new CreativeItemData("Cactus", 49, 64));
            items.Add(new CreativeItemData("Birch Log", 51, 64));
            items.Add(new CreativeItemData("Birch Leaves", 52, 64));
            items.Add(new CreativeItemData("Spruce Log", 53, 64));
            items.Add(new CreativeItemData("Spruce Leaves", 54, 64));
            items.Add(new CreativeItemData("Coal Ore", 30, 64));
            items.Add(new CreativeItemData("Iron Ore", 31, 64));
            items.Add(new CreativeItemData("Diamond Ore", 55, 64));
            items.Add(new CreativeItemData("Crafting Table", 36, 64));
            items.Add(new CreativeItemData("Furnace", 37, 64));
            items.Add(new CreativeItemData("Wooden Stairs", 38, 64));
            items.Add(new CreativeItemData("Stone Stairs", 39, 64));
            items.Add(new CreativeItemData("Wooden Slab", 46, 64));
            items.Add(new CreativeItemData("Stone Slab", 47, 64));
        }
        else if (category == "TOOLS")
        {
            // Diamond
            items.Add(new CreativeItemData("Diamond Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Diamond Axe", 0, 1));
            items.Add(new CreativeItemData("Diamond Shovel", 0, 1));
            items.Add(new CreativeItemData("Diamond Sword", 0, 1));
            items.Add(new CreativeItemData("Diamond Rake", 0, 1));
            // Iron
            items.Add(new CreativeItemData("Iron Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Iron Axe", 0, 1));
            items.Add(new CreativeItemData("Iron Shovel", 0, 1));
            items.Add(new CreativeItemData("Iron Sword", 0, 1));
            items.Add(new CreativeItemData("Iron Rake", 0, 1));
            // Stone
            items.Add(new CreativeItemData("Stone Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Stone Axe", 0, 1));
            items.Add(new CreativeItemData("Stone Shovel", 0, 1));
            items.Add(new CreativeItemData("Stone Sword", 0, 1));
            items.Add(new CreativeItemData("Stone Rake", 0, 1));
            // Wood
            items.Add(new CreativeItemData("Wooden Pickaxe", 0, 1));
            items.Add(new CreativeItemData("Wooden Axe", 0, 1));
            items.Add(new CreativeItemData("Wooden Shovel", 0, 1));
            items.Add(new CreativeItemData("Wooden Sword", 0, 1));
            items.Add(new CreativeItemData("Wooden Rake", 0, 1));
            // Wrench, materials
            items.Add(new CreativeItemData("Wrench", 0, 1));
            items.Add(new CreativeItemData("Wolf Spawn Egg", 0, 64));
            items.Add(new CreativeItemData("Sheep Spawn Egg", 0, 64));
            items.Add(new CreativeItemData("Coal Chunk", 0, 64));
            items.Add(new CreativeItemData("Iron Ingot", 0, 64));
            items.Add(new CreativeItemData("Iron", 0, 64));
            items.Add(new CreativeItemData("Diamond", 0, 64));
        }
        else if (category == "VEHICLES")
        {
            items.Add(new CreativeItemData("Control Block", 50, 64));
            items.Add(new CreativeItemData("Small Wheel", 20, 64));
            items.Add(new CreativeItemData("Large Wheel", 21, 64));
            items.Add(new CreativeItemData("Propeller", 22, 64));
            items.Add(new CreativeItemData("Large Propeller", 26, 64));
            items.Add(new CreativeItemData("Wrench", 0, 1));
        }
        else if (category == "FOLIAGE")
        {
            items.Add(new CreativeItemData("Short Grass", 13, 64));
            items.Add(new CreativeItemData("Tall Grass", 14, 64));
            items.Add(new CreativeItemData("Leaves", 12, 64));
            items.Add(new CreativeItemData("Flower", 9, 64));
            items.Add(new CreativeItemData("Dandelion", 10, 64));
            items.Add(new CreativeItemData("Iris", 11, 64));
            items.Add(new CreativeItemData("Apple", 0, 64));
        }
        else if (category == "SPAWNERS")
        {
            items.Add(new CreativeItemData("Wolf Spawn Egg", 0, 64));
            items.Add(new CreativeItemData("Sheep Spawn Egg", 0, 64));
        }

        // Dynamically size the slots array to match the populated items list size (indefinite slots)
        slots = new InventorySlot[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var itemData = items[i];
            slots[i] = new InventorySlot(CreateItem(itemData.name, itemData.typeId), itemData.amount);
        }

        onInventoryChangedCallback?.Invoke();
    }

    public void PopulateCreativeInventory()
    {
        if (Hotbar.Instance != null)
        {
            for (int i = 0; i < 8; i++)
            {
                Hotbar.Instance.SetSlot(i, null, 0);
            }
        }

        PopulateCreativeCategory("ALL");
    }

    private static Sprite CreateStickIcon()
    {
        const int SZ = 64;
        Color brown = new Color(0.48f, 0.31f, 0.16f, 1f);
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        for (int i = 10; i < 54; i++)
        {
            px[i * SZ + i] = brown;
            px[i * SZ + i + 1] = brown;
            px[(i + 1) * SZ + i] = brown;
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateDiamondIcon()
    {
        const int SZ = 64;
        Color cyan = new Color(0.20f, 0.85f, 0.88f, 1f);
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        for (int y = 16; y < 48; y++)
        {
            int width = 16 - Mathf.Abs(32 - y);
            for (int x = 32 - width; x <= 32 + width; x++)
            {
                px[y * SZ + x] = cyan;
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCoalChunkIcon()
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color darkGray = new Color(0.15f, 0.15f, 0.15f, 1f);
        Color midGray = new Color(0.25f, 0.25f, 0.25f, 1f);
        Color lightGray = new Color(0.35f, 0.35f, 0.35f, 1f);
        Color outline = new Color(0.05f, 0.05f, 0.05f, 1f);

        for (int y = 16; y <= 48; y++)
        {
            int rx = 16 - Mathf.Abs(32 - y);
            if (y > 32) rx = Mathf.RoundToInt(rx * 0.9f);
            for (int x = 32 - rx; x <= 32 + rx; x++)
            {
                if (x == 32 - rx || x == 32 + rx || y == 16 || y == 48)
                {
                    px[y * SZ + x] = outline;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    px[y * SZ + x] = noise > 0.6f ? lightGray : (noise > 0.3f ? midGray : darkGray);
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateIronIngotIcon()
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color metal = new Color(0.85f, 0.85f, 0.87f, 1f);
        Color shadow = new Color(0.65f, 0.65f, 0.67f, 1f);
        Color highlight = new Color(0.98f, 0.98f, 1.0f, 1f);
        Color outline = new Color(0.35f, 0.35f, 0.37f, 1f);

        for (int y = 20; y <= 44; y++)
        {
            int offset = (y - 20) / 2;
            int startX = 20 - offset;
            int width = 28;
            for (int x = startX; x < startX + width; x++)
            {
                if (x == startX || x == startX + width - 1 || y == 20 || y == 44)
                {
                    px[y * SZ + x] = outline;
                }
                else
                {
                    if (y >= 40 || x >= startX + width - 3)
                        px[y * SZ + x] = shadow;
                    else if (y <= 24 || x <= startX + 2)
                        px[y * SZ + x] = highlight;
                    else
                        px[y * SZ + x] = metal;
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateSheepSpawnEggIcon()
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color eggBase = new Color(0.95f, 0.95f, 0.95f, 1f);
        Color eggSpot = new Color(0.90f, 0.80f, 0.75f, 1f);
        Color eggOutline = new Color(0.12f, 0.12f, 0.14f, 1f);

        for (int y = 14; y <= 50; y++)
        {
            float normY = (y - 14f) / (50f - 14f);
            float widthFactor = Mathf.Sin(normY * Mathf.PI);
            if (normY > 0.4f)
            {
                widthFactor *= (1f - (normY - 0.4f) * 0.4f);
            }
            int rx = Mathf.RoundToInt(15f * widthFactor);

            for (int x = 32 - rx; x <= 32 + rx; x++)
            {
                if (x == 32 - rx || x == 32 + rx || y == 14 || y == 50)
                {
                    px[y * SZ + x] = eggOutline;
                }
                else
                {
                    bool isSpot = false;
                    Vector2[] spots = new Vector2[] {
                        new Vector2(28f, 22f),
                        new Vector2(37f, 26f),
                        new Vector2(30f, 38f),
                        new Vector2(39f, 42f),
                        new Vector2(25f, 30f),
                        new Vector2(34f, 18f)
                    };
                    foreach (var s in spots)
                    {
                        if (Vector2.Distance(new Vector2(x, y), s) < 3.2f)
                        {
                            isSpot = true;
                            break;
                        }
                    }

                    px[y * SZ + x] = isSpot ? eggSpot : eggBase;
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateWolfSpawnEggIcon()
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        // Base color: light gray egg
        Color eggBase = new Color(0.75f, 0.75f, 0.78f, 1f);
        // Spots color: dark charcoal/gray
        Color eggSpot = new Color(0.25f, 0.25f, 0.28f, 1f);
        // Shadow/outline color
        Color eggOutline = new Color(0.12f, 0.12f, 0.14f, 1f);

        for (int y = 14; y <= 50; y++)
        {
            float normY = (y - 14f) / (50f - 14f);
            float widthFactor = Mathf.Sin(normY * Mathf.PI);
            if (normY > 0.4f)
            {
                widthFactor *= (1f - (normY - 0.4f) * 0.4f);
            }
            int rx = Mathf.RoundToInt(15f * widthFactor);

            for (int x = 32 - rx; x <= 32 + rx; x++)
            {
                if (x == 32 - rx || x == 32 + rx || y == 14 || y == 50)
                {
                    px[y * SZ + x] = eggOutline;
                }
                else
                {
                    bool isSpot = false;
                    Vector2[] spots = new Vector2[] {
                        new Vector2(28f, 22f),
                        new Vector2(37f, 26f),
                        new Vector2(30f, 38f),
                        new Vector2(39f, 42f),
                        new Vector2(25f, 30f),
                        new Vector2(34f, 18f)
                    };
                    foreach (var s in spots)
                    {
                        if (Vector2.Distance(new Vector2(x, y), s) < 3.2f)
                        {
                            isSpot = true;
                            break;
                        }
                    }

                    px[y * SZ + x] = isSpot ? eggSpot : eggBase;
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite CreateToolIcon(ToolType type, ToolTier tier)
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color stickColor = new Color(0.48f, 0.31f, 0.16f, 1f);
        Color matColor = Color.white;
        switch (tier)
        {
            case ToolTier.Wood: matColor = new Color(0.65f, 0.50f, 0.30f, 1f); break;
            case ToolTier.Stone: matColor = new Color(0.50f, 0.50f, 0.50f, 1f); break;
            case ToolTier.Iron: matColor = new Color(0.85f, 0.85f, 0.85f, 1f); break;
            case ToolTier.Diamond: matColor = new Color(0.20f, 0.80f, 0.85f, 1f); break;
        }

        int stickLength = (type == ToolType.Pickaxe) ? 48 : 40;
        for (int i = 12; i <= stickLength; i++)
        {
            SetPixelSafe(px, SZ, i, i, stickColor);
            SetPixelSafe(px, SZ, i + 1, i, stickColor);
            SetPixelSafe(px, SZ, i, i + 1, stickColor);
        }

        if (type == ToolType.Sword)
        {
            for (int i = 32; i <= 56; i++)
            {
                SetPixelSafe(px, SZ, i, i, matColor);
                SetPixelSafe(px, SZ, i - 1, i + 1, matColor);
                SetPixelSafe(px, SZ, i + 1, i - 1, matColor);
            }
            for (int offset = -5; offset <= 5; offset++)
            {
                SetPixelSafe(px, SZ, 28 - offset, 28 + offset, stickColor);
                SetPixelSafe(px, SZ, 29 - offset, 29 + offset, stickColor);
            }
        }
        else if (type == ToolType.Pickaxe)
        {
            for (int offset = -14; offset <= 14; offset++)
            {
                int hx = 40 - offset;
                int hy = 40 + offset;
                int curve = (14 * 14 - offset * offset) / 18;
                hx += curve;
                hy += curve;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        SetPixelSafe(px, SZ, hx + dx, hy + dy, matColor);
                    }
                }
            }
        }
        else if (type == ToolType.Axe)
        {
            for (int x = 38; x <= 52; x++)
            {
                for (int y = 38; y <= 52; y++)
                {
                    int dx = x - 40;
                    int dy = y - 40;
                    if (dx + dy >= 4 && dx - dy >= -6 && dy - dx >= -6)
                    {
                        SetPixelSafe(px, SZ, x, y, matColor);
                    }
                }
            }
        }
        else if (type == ToolType.Shovel)
        {
            for (int dx = -6; dx <= 6; dx++)
            {
                for (int dy = -6; dy <= 6; dy++)
                {
                    if (dx * dx + dy * dy <= 36)
                    {
                        SetPixelSafe(px, SZ, 44 + dx, 44 + dy, matColor);
                    }
                }
            }
            SetPixelSafe(px, SZ, 51, 51, matColor);
        }
        else if (type == ToolType.Rake)
        {
            for (int offset = -12; offset <= 12; offset++)
            {
                int hx = 42 - offset;
                int hy = 42 + offset;
                SetPixelSafe(px, SZ, hx, hy, matColor);
                SetPixelSafe(px, SZ, hx + 1, hy, matColor);
                SetPixelSafe(px, SZ, hx, hy + 1, matColor);

                if (offset % 4 == 0)
                {
                    for (int p = 0; p <= 4; p++)
                    {
                        SetPixelSafe(px, SZ, hx + p, hy + p, matColor);
                    }
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void SetPixelSafe(Color[] px, int sz, int x, int y, Color c)
    {
        if (x >= 0 && x < sz && y >= 0 && y < sz)
        {
            px[y * sz + x] = c;
        }
    }

    public static void ParseToolName(string name, out ToolType type, out ToolTier tier)
    {
        type = ToolType.None;
        tier = ToolTier.None;
        string lower = name.ToLower();
        
        if (lower.Contains("wooden")) tier = ToolTier.Wood;
        else if (lower.Contains("stone")) tier = ToolTier.Stone;
        else if (lower.Contains("iron")) tier = ToolTier.Iron;
        else if (lower.Contains("diamond")) tier = ToolTier.Diamond;

        if (lower.Contains("pickaxe")) type = ToolType.Pickaxe;
        else if (lower.Contains("axe")) type = ToolType.Axe;
        else if (lower.Contains("shovel")) type = ToolType.Shovel;
        else if (lower.Contains("rake")) type = ToolType.Rake;
        else if (lower.Contains("sword")) type = ToolType.Sword;
    }

    private void CheckToolRecipes()
    {
        bool stick4 = tableCraftingSlots[4]?.item?.itemName == "Stick";
        bool stick7 = tableCraftingSlots[7]?.item?.itemName == "Stick";
        
        string matName = null;
        ToolTier tier = ToolTier.None;
        
        System.Action<Item> checkMat = (itm) => {
            if (itm == null) return;
            string name = itm.itemName;
            if (name == "Plank") { matName = "Plank"; tier = ToolTier.Wood; }
            else if (name == "Stone" || name == "Gravel") { matName = "Stone"; tier = ToolTier.Stone; }
            else if (name == "Iron") { matName = "Iron"; tier = ToolTier.Iron; }
            else if (name == "Diamond") { matName = "Diamond"; tier = ToolTier.Diamond; }
        };

        Item i0 = tableCraftingSlots[0]?.item;
        Item i1 = tableCraftingSlots[1]?.item;
        Item i2 = tableCraftingSlots[2]?.item;
        Item i3 = tableCraftingSlots[3]?.item;
        Item i4 = tableCraftingSlots[4]?.item;
        Item i5 = tableCraftingSlots[5]?.item;
        Item i6 = tableCraftingSlots[6]?.item;
        Item i7 = tableCraftingSlots[7]?.item;
        Item i8 = tableCraftingSlots[8]?.item;

        if (i7?.itemName == "Stick" && i1 != null && i4 != null && i1.itemName == i4.itemName)
        {
            checkMat(i1);
            if (tier != ToolTier.None && i0 == null && i2 == null && i3 == null && i5 == null && i6 == null && i8 == null)
            {
                string toolName = GetToolNameString(ToolType.Sword, tier);
                tableCraftingResultSlot = new InventorySlot(CreateItem(toolName, 0), 1);
                return;
            }
        }

        if (stick4 && stick7 && i1 != null)
        {
            checkMat(i1);
            if (tier != ToolTier.None && i0 == null && i2 == null && i3 == null && i5 == null && i6 == null && i8 == null)
            {
                string toolName = GetToolNameString(ToolType.Shovel, tier);
                tableCraftingResultSlot = new InventorySlot(CreateItem(toolName, 0), 1);
                return;
            }
        }

        if (stick4 && stick7 && i0 != null && i1 != null && i2 != null && i0.itemName == i1.itemName && i1.itemName == i2.itemName)
        {
            checkMat(i0);
            if (tier != ToolTier.None && i3 == null && i5 == null && i6 == null && i8 == null)
            {
                string toolName = GetToolNameString(ToolType.Pickaxe, tier);
                tableCraftingResultSlot = new InventorySlot(CreateItem(toolName, 0), 1);
                return;
            }
        }

        if (stick4 && stick7)
        {
            if (i0 != null && i1 != null && i3 != null && i0.itemName == i1.itemName && i1.itemName == i3.itemName)
            {
                checkMat(i0);
                if (tier != ToolTier.None && i2 == null && i5 == null && i6 == null && i8 == null)
                {
                    string toolName = GetToolNameString(ToolType.Axe, tier);
                    tableCraftingResultSlot = new InventorySlot(CreateItem(toolName, 0), 1);
                    return;
                }
            }
            if (i1 != null && i2 != null && i5 != null && i1.itemName == i2.itemName && i2.itemName == i5.itemName)
            {
                checkMat(i1);
                if (tier != ToolTier.None && i0 == null && i3 == null && i6 == null && i8 == null)
                {
                    string toolName = GetToolNameString(ToolType.Axe, tier);
                    tableCraftingResultSlot = new InventorySlot(CreateItem(toolName, 0), 1);
                    return;
                }
            }
        }

        if (stick4 && stick7)
        {
            if (i0 != null && i1 != null && i0.itemName == i1.itemName)
            {
                checkMat(i0);
                if (tier != ToolTier.None && i2 == null && i3 == null && i5 == null && i6 == null && i8 == null)
                {
                    string toolName = GetToolNameString(ToolType.Rake, tier);
                    tableCraftingResultSlot = new InventorySlot(CreateItem(toolName, 0), 1);
                    return;
                }
            }
            if (i1 != null && i2 != null && i1.itemName == i2.itemName)
            {
                checkMat(i1);
                if (tier != ToolTier.None && i0 == null && i3 == null && i5 == null && i6 == null && i8 == null)
                {
                    string toolName = GetToolNameString(ToolType.Rake, tier);
                    tableCraftingResultSlot = new InventorySlot(CreateItem(toolName, 0), 1);
                    return;
                }
            }
        }
    }

    private string GetToolNameString(ToolType type, ToolTier tier)
    {
        string tierStr = "";
        switch (tier)
        {
            case ToolTier.Wood: tierStr = "Wooden"; break;
            case ToolTier.Stone: tierStr = "Stone"; break;
            case ToolTier.Iron: tierStr = "Iron"; break;
            case ToolTier.Diamond: tierStr = "Diamond"; break;
        }
        return $"{tierStr} {type.ToString()}";
    }
}

[System.Serializable]
public class InventorySlot
{
    public Item item;
    public int amount;
    public InventorySlot(Item item, int amount) { this.item = item; this.amount = amount; }
}
