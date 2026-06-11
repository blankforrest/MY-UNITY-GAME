using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lightweight slot component. Handles only hover highlighting.
/// All drag-drop is handled by DragDropManager.
/// </summary>
public class SlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum Owner { Inventory, Hotbar, CraftingInput, CraftingOutput }

    [HideInInspector] public Owner              owner;
    [HideInInspector] public int                index;
    [HideInInspector] public Image              iconImage;
    [HideInInspector] public TextMeshProUGUI    amountText;
    [HideInInspector] public Image              background;

    // Shared ghost image assigned by InventoryUI or DragDropManager
    public static Image Ghost;

    private static readonly Color NormalBg = new Color(0.35f, 0.35f, 0.35f, 1f);
    private static readonly Color HoverBg  = new Color(0.55f, 0.55f, 0.55f, 1f);

    // ── Public data accessors (used by DragDropManager) ───────────────────────

    public InventorySlot GetItemData()
    {
        if (owner == Owner.Inventory)
        {
            if (Inventory.Instance == null) return null;
            var s = Inventory.Instance.slots;
            return (index >= 0 && index < s.Length) ? s[index] : null; // use runtime length
        }
        else if (owner == Owner.CraftingInput)
        {
            if (Inventory.Instance == null) return null;
            var s = Inventory.Instance.craftingSlots;
            return (index >= 0 && index < s.Length) ? s[index] : null;
        }
        else if (owner == Owner.CraftingOutput)
        {
            if (Inventory.Instance == null) return null;
            return Inventory.Instance.craftingResultSlot;
        }
        return Hotbar.Instance?.GetSlotData(index);
    }

    public void WriteItemData(InventorySlot slot, bool silent = false)
    {
        Item item   = slot?.item;
        int  amount = slot?.amount ?? 0;

        if (owner == Owner.Inventory) Inventory.Instance?.SetSlot(index, item, amount, silent);
        else if (owner == Owner.CraftingInput)
        {
            if (Inventory.Instance != null)
            {
                Inventory.Instance.craftingSlots[index] = (item != null) ? new InventorySlot(item, amount) : null;
                Inventory.Instance.UpdateCraftingOutput();
                if (!silent) Inventory.Instance.onInventoryChangedCallback?.Invoke();
            }
        }
        else if (owner == Owner.CraftingOutput)
        {
            if (Inventory.Instance != null)
            {
                Inventory.Instance.craftingResultSlot = (item != null) ? new InventorySlot(item, amount) : null;
                if (!silent) Inventory.Instance.onInventoryChangedCallback?.Invoke();
            }
        }
        else                          Hotbar.Instance?.SetSlot(index, item, amount);
    }

    public void SetIconVisible(bool visible)
    {
        if (iconImage != null) iconImage.enabled = visible && GetItemData()?.item != null;
    }

    // ── Refresh visual ────────────────────────────────────────────────────────

    public void Refresh()
    {
        InventorySlot data = GetItemData();
        bool hasItem = data != null && data.item != null;

        if (iconImage != null)
        {
            iconImage.sprite  = hasItem ? data.item.icon : null;
            iconImage.enabled = hasItem;
        }
        if (amountText != null)
            amountText.text = (hasItem && data.amount > 1) ? data.amount.ToString() : "";
    }

    // ── Hover highlight ───────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData e)
    {
        if (background) background.color = HoverBg;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (background) background.color = NormalBg;
    }
}
