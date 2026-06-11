using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public const int MaxSlots = 25; // 5x5

    public delegate void OnInventoryChanged();
    public OnInventoryChanged onInventoryChangedCallback;

    // Fixed 25-slot array — null means empty
    public InventorySlot[] slots = new InventorySlot[MaxSlots];

    // 2x2 crafting grid and result slot
    public InventorySlot[] craftingSlots = new InventorySlot[4];
    public InventorySlot craftingResultSlot = null;

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
    }


    public bool Add(Item item, int amount)
    {
        if (item == null) return false;

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].item != null && slots[i].item.itemName == item.itemName)
            { slots[i].amount += amount; onInventoryChangedCallback?.Invoke(); return true; }

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

    public void UpdateCraftingOutput()
    {
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
            craftingResultSlot = new InventorySlot(CreateItem("Plank", 2), 4);
            return;
        }

        // Recipe 2: 2 Planks (vertically aligned) -> 4 Sticks (blockTypeID = 0)
        bool isStickMatch = false;
        if (i0?.itemName == "Plank" && i2?.itemName == "Plank" && i1 == null && i3 == null) isStickMatch = true;
        else if (i1?.itemName == "Plank" && i3?.itemName == "Plank" && i0 == null && i2 == null) isStickMatch = true;

        if (isStickMatch)
        {
            craftingResultSlot = new InventorySlot(CreateItem("Stick", 0), 4);
            return;
        }

        // Recipe 3: 4 Planks -> 1 Control Block (blockTypeID = 50)
        if (i0?.itemName == "Plank" && i1?.itemName == "Plank" && i2?.itemName == "Plank" && i3?.itemName == "Plank")
        {
            craftingResultSlot = new InventorySlot(CreateItem("Control Block", 50), 1);
            return;
        }

        // Recipe 4: 1 Plank + 1 Stick (vertical: Plank top, Stick bottom) -> 1 Small Wheel (blockTypeID = 20)
        bool isSmallWheelMatch = false;
        if (i0?.itemName == "Plank" && i2?.itemName == "Stick" && i1 == null && i3 == null) isSmallWheelMatch = true;
        else if (i1?.itemName == "Plank" && i3?.itemName == "Stick" && i0 == null && i2 == null) isSmallWheelMatch = true;

        if (isSmallWheelMatch)
        {
            craftingResultSlot = new InventorySlot(CreateItem("Small Wheel", 20), 1);
            return;
        }

        // Recipe 5: 2 Planks (top row) + 2 Sticks (bottom row) -> 1 Large Wheel (blockTypeID = 21)
        if (i0?.itemName == "Plank" && i1?.itemName == "Plank" && i2?.itemName == "Stick" && i3?.itemName == "Stick")
        {
            craftingResultSlot = new InventorySlot(CreateItem("Large Wheel", 21), 1);
            return;
        }

        // Recipe 6: 2 Planks + 2 Sticks (diagonally) -> 1 Propeller (blockTypeID = 22)
        bool isPropellerMatch = false;
        if (i0?.itemName == "Plank" && i3?.itemName == "Plank" && i1?.itemName == "Stick" && i2?.itemName == "Stick") isPropellerMatch = true;
        else if (i1?.itemName == "Plank" && i2?.itemName == "Plank" && i0?.itemName == "Stick" && i3?.itemName == "Stick") isPropellerMatch = true;

        if (isPropellerMatch)
        {
            craftingResultSlot = new InventorySlot(CreateItem("Propeller", 22), 1);
            return;
        }

        // No recipe matches
        craftingResultSlot = null;
    }

    public void ConsumeCraftingInputs()
    {
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            if (craftingSlots[i] != null)
            {
                craftingSlots[i].amount--;
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

    private Item CreateItem(string itemName, int blockTypeID)
    {
        Item item = ScriptableObject.CreateInstance<Item>();
        item.itemName = itemName;
        item.blockTypeID = blockTypeID;
        item.itemID = 0;

        Sprite sprite = null;
        if (itemName.Equals("Grass", System.StringComparison.OrdinalIgnoreCase))
        {
            sprite = StarterItems.MakeGrassBlockIcon();
        }
        else
        {
            sprite = Resources.Load<Sprite>("Sprites/" + itemName.ToLower() + "_block");
            if (sprite == null)
                sprite = Resources.Load<Sprite>("Sprites/" + itemName.ToLower());
        }

        if (sprite == null)
        {
            if (itemName == "Stick")
                sprite = CreateStickIcon();
            else if (itemName == "Control Block")
                sprite = VehicleSpawner.CreateControlBlockIcon();
            else if (itemName == "Small Wheel")
                sprite = VehicleSpawner.CreateWheelIcon(false);
            else if (itemName == "Large Wheel")
                sprite = VehicleSpawner.CreateWheelIcon(true);
            else if (itemName == "Propeller")
                sprite = VehicleSpawner.CreatePropellerIcon();
            else if (itemName == "Plank")
                sprite = StarterItems.MakeBlockIcon(new Color(0.72f, 0.58f, 0.37f));
            else
                sprite = StarterItems.MakeBlockIcon(Color.gray);
        }

        item.icon = sprite;
        return item;
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
}

[System.Serializable]
public class InventorySlot
{
    public Item item;
    public int amount;
    public InventorySlot(Item item, int amount) { this.item = item; this.amount = amount; }
}
