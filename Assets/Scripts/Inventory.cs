using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public const int MaxSlots = 25; // 5x5

    public delegate void OnInventoryChanged();
    public OnInventoryChanged onInventoryChangedCallback;

    // Fixed 16-slot array — null means empty
    public InventorySlot[] slots = new InventorySlot[MaxSlots];

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
    }


    public bool Add(Item item, int amount)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].item == item)
            { slots[i].amount += amount; onInventoryChangedCallback?.Invoke(); return true; }

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null)
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
}

[System.Serializable]
public class InventorySlot
{
    public Item item;
    public int amount;
    public InventorySlot(Item item, int amount) { this.item = item; this.amount = amount; }
}
