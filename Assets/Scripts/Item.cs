using UnityEngine;

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
}
