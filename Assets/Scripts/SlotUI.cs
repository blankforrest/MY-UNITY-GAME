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
    public enum Owner { Inventory, Hotbar, CraftingInput, CraftingOutput, TableCraftingInput, TableCraftingOutput, FurnaceInput, FurnaceFuel, FurnaceOutput }

    [HideInInspector] public Owner              owner;
    [HideInInspector] public int                index;
    [HideInInspector] public Image              iconImage;
    [HideInInspector] public TextMeshProUGUI    amountText;
    [HideInInspector] public TextMeshProUGUI[]  amountOutlineTexts;
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
        else if (owner == Owner.TableCraftingInput)
        {
            if (Inventory.Instance == null) return null;
            var s = Inventory.Instance.tableCraftingSlots;
            return (index >= 0 && index < s.Length) ? s[index] : null;
        }
        else if (owner == Owner.TableCraftingOutput)
        {
            if (Inventory.Instance == null) return null;
            return Inventory.Instance.tableCraftingResultSlot;
        }
        else if (owner == Owner.FurnaceInput)
        {
            return FurnaceManager.ActiveFurnace?.inputSlot;
        }
        else if (owner == Owner.FurnaceFuel)
        {
            return FurnaceManager.ActiveFurnace?.fuelSlot;
        }
        else if (owner == Owner.FurnaceOutput)
        {
            return FurnaceManager.ActiveFurnace?.outputSlot;
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
        else if (owner == Owner.TableCraftingInput)
        {
            if (Inventory.Instance != null)
            {
                Inventory.Instance.tableCraftingSlots[index] = (item != null) ? new InventorySlot(item, amount) : null;
                Inventory.Instance.UpdateTableCraftingOutput();
                if (!silent) Inventory.Instance.onInventoryChangedCallback?.Invoke();
            }
        }
        else if (owner == Owner.TableCraftingOutput)
        {
            if (Inventory.Instance != null)
            {
                Inventory.Instance.tableCraftingResultSlot = (item != null) ? new InventorySlot(item, amount) : null;
                if (!silent) Inventory.Instance.onInventoryChangedCallback?.Invoke();
            }
        }
        else if (owner == Owner.FurnaceInput)
        {
            if (FurnaceManager.ActiveFurnace != null)
            {
                FurnaceManager.ActiveFurnace.inputSlot = (item != null) ? new InventorySlot(item, amount) : null;
                if (!silent) InventoryUI.Instance?.RefreshFurnaceUI();
            }
        }
        else if (owner == Owner.FurnaceFuel)
        {
            if (FurnaceManager.ActiveFurnace != null)
            {
                FurnaceManager.ActiveFurnace.fuelSlot = (item != null) ? new InventorySlot(item, amount) : null;
                if (!silent) InventoryUI.Instance?.RefreshFurnaceUI();
            }
        }
        else if (owner == Owner.FurnaceOutput)
        {
            if (FurnaceManager.ActiveFurnace != null)
            {
                FurnaceManager.ActiveFurnace.outputSlot = (item != null) ? new InventorySlot(item, amount) : null;
                if (!silent) InventoryUI.Instance?.RefreshFurnaceUI();
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
        {
            bool isCreative = false;
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.isCreativeMode) isCreative = true;

            string amtStr = (hasItem && data.amount > 1 && !isCreative) ? data.amount.ToString() : "";
            amountText.text = amtStr;
            if (amountOutlineTexts != null)
            {
                for (int i = 0; i < amountOutlineTexts.Length; i++)
                {
                    if (amountOutlineTexts[i] != null)
                        amountOutlineTexts[i].text = amtStr;
                }
            }
        }
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
