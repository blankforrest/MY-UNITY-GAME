using UnityEngine;

public enum ToolType { None, Pickaxe, Axe, Shovel, Rake, Sword }
public enum ToolTier { None, Wood, Stone, Iron, Diamond }

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    public string itemName;
    public Sprite icon;

    [Tooltip("Unique integer ID for this item. 0 = unset. " +
             "Assign IDs in each Item ScriptableObject asset in the Inspector.")]
    public int itemID;

    [Tooltip("Voxel block type this item places when right-clicked (0 = not a block item, e.g. tools).")]
    public int blockTypeID = 0;

    public ToolType toolType = ToolType.None;
    public ToolTier toolTier = ToolTier.None;
}
