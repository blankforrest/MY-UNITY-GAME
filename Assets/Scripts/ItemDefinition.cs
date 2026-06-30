using UnityEngine;

[System.Serializable]
public class ItemDefinition
{
    [Header("Identity")]
    public string itemName = "Custom Item";
    [Tooltip("Unique integer ID for this item. Block items typically share their block ID, or non-block items have unique IDs >= 90.")]
    public int itemID = 0;

    [Header("Visuals")]
    [Tooltip("Inventory icon sprite.")]
    public Sprite inventoryIcon;
    
    [Tooltip("Sprite used for rendering the item in the world when dropped. If null, falls back to inventoryIcon.")]
    public Sprite droppedItemSprite;

    [Header("Creative Inventory")]
    [Tooltip("The creative inventory bag/category tab where this item belongs (e.g. BLOCKS, TOOLS, VEHICLES, FOLIAGE, ITEMS, SPAWNERS, ALL).")]
    public string creativeBag = "ITEMS";

    [Header("Properties")]
    [Tooltip("Voxel block type this item places when right-clicked (0 if not a block).")]
    public int blockTypeID = 0;

    public ToolType toolType = ToolType.None;
    public ToolTier toolTier = ToolTier.None;

    [Tooltip("Can this item be stacked in inventory slots?")]
    public bool isStackable = true;

    [Tooltip("Maximum number of items in a single stack.")]
    public int maxStackSize = 64;

    [TextArea(2, 5)]
    public string description = "A useful item.";

    [Header("Behavior")]
    [Tooltip("Value as fuel in a furnace (0 if not burnable).")]
    public float burnDuration = 0f;

    [Tooltip("Amount of health/hunger restored when consumed (0 if not edible).")]
    public int healAmount = 0;
}
