using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BlockDatabase", menuName = "Voxel Game/Block Database")]
public class BlockDatabase : ScriptableObject
{
    [Tooltip("List of all blocks configured in this database. Click the context menu (three dots) on this component and select 'Populate Default Blocks' to auto-fill the list with the 40+ existing base game blocks.")]
    public List<BlockDefinition> blocks = new List<BlockDefinition>();

    [ContextMenu("Populate Default Blocks")]
    public void PopulateDefaultBlocks()
    {
        blocks.Clear();

        // Populate base game blocks with their standard game properties
        blocks.Add(CreateDef(1, "Wood", 1.5f, ToolType.Axe, true, false));
        blocks.Add(CreateDef(2, "Plank", 1.5f, ToolType.Axe, true, false));
        blocks.Add(CreateDef(3, "Stone", 3.0f, ToolType.Pickaxe, true, false));
        blocks.Add(CreateDef(4, "Grass Block", 0.8f, ToolType.Shovel, true, false));
        blocks.Add(CreateDef(5, "Dirt", 0.8f, ToolType.Shovel, true, false));
        blocks.Add(CreateDef(6, "Grass Slab", 1.5f, ToolType.Axe, true, false));
        blocks.Add(CreateDef(7, "Water", 0.0f, ToolType.None, false, true));
        blocks.Add(CreateDef(8, "Sand", 0.8f, ToolType.Shovel, true, false));
        blocks.Add(CreateDef(9, "Rose", 0.0f, ToolType.None, false, true));
        blocks.Add(CreateDef(10, "Dandelion", 0.0f, ToolType.None, false, true));
        blocks.Add(CreateDef(11, "Iris", 0.0f, ToolType.None, false, true));
        blocks.Add(CreateDef(12, "Leaves", 0.2f, ToolType.Sword, true, true));
        blocks.Add(CreateDef(13, "Short Grass", 0.0f, ToolType.None, false, true));
        blocks.Add(CreateDef(14, "Tall Grass", 0.0f, ToolType.None, false, true));
        
        blocks.Add(CreateDef(20, "Small Wheel", 1.0f, ToolType.Axe, true, false, true, "Wheel"));
        blocks.Add(CreateDef(21, "Large Wheel Anchor", 1.0f, ToolType.Axe, true, false, true, "Wheel"));
        blocks.Add(CreateDef(22, "Propeller", 1.0f, ToolType.Axe, true, false, true, "Propeller"));
        blocks.Add(CreateDef(23, "Large Wheel Helper", 1.0f, ToolType.Axe, true, false, true, "Wheel"));
        blocks.Add(CreateDef(24, "Propeller Casing", 1.0f, ToolType.Axe, true, false, true, "None"));
        blocks.Add(CreateDef(25, "Propeller Blade", 1.0f, ToolType.Axe, true, false, true, "None"));
        blocks.Add(CreateDef(26, "Large Propeller Anchor", 1.0f, ToolType.Axe, true, false, true, "Propeller"));
        blocks.Add(CreateDef(27, "Large Propeller Helper", 1.0f, ToolType.Axe, true, false, true, "Propeller"));

        blocks.Add(CreateDef(30, "Coal Ore", 3.0f, ToolType.Pickaxe, true, false));
        blocks.Add(CreateDef(31, "Iron Ore", 3.0f, ToolType.Pickaxe, true, false));
        blocks.Add(CreateDef(32, "Gold Block", 3.0f, ToolType.Pickaxe, true, false));
        blocks.Add(CreateDef(33, "Iron Block", 3.0f, ToolType.Pickaxe, true, false));
        blocks.Add(CreateDef(34, "Sand (Unused ID)", 0.8f, ToolType.Shovel, true, false));
        blocks.Add(CreateDef(35, "Glass", 0.3f, ToolType.None, true, true));
        blocks.Add(CreateDef(36, "Crafting Table", 1.5f, ToolType.Axe, true, false));
        blocks.Add(CreateDef(37, "Furnace", 3.0f, ToolType.Pickaxe, true, false));

        blocks.Add(CreateDef(38, "Wooden Stairs", 1.5f, ToolType.Axe, true, true));
        blocks.Add(CreateDef(39, "Stone Stairs", 3.0f, ToolType.Pickaxe, true, true));
        blocks.Add(CreateDef(46, "Wooden Slab", 1.5f, ToolType.Axe, true, true));
        blocks.Add(CreateDef(47, "Stone Slab", 3.0f, ToolType.Pickaxe, true, true));

        blocks.Add(CreateDef(48, "Bedrock", 0.0f, ToolType.None, true, false));
        blocks.Add(CreateDef(49, "Cactus", 0.4f, ToolType.None, true, false));
        blocks.Add(CreateDef(50, "Control Block", 1.0f, ToolType.Axe, true, false, true, "ControlBlock"));
        blocks.Add(CreateDef(51, "Birch Log", 1.5f, ToolType.Axe, true, false));
        blocks.Add(CreateDef(52, "Birch Leaves", 0.2f, ToolType.Sword, true, true));
        blocks.Add(CreateDef(53, "Spruce Log", 1.5f, ToolType.Axe, true, false));
        blocks.Add(CreateDef(54, "Spruce Leaves", 0.2f, ToolType.Sword, true, true));
        blocks.Add(CreateDef(55, "Diamond Ore", 3.0f, ToolType.Pickaxe, true, false));

        Debug.Log($"[BlockDatabase] Successfully populated default settings for {blocks.Count} base game blocks.");
    }

    private BlockDefinition CreateDef(byte id, string name, float hardness, ToolType tool, bool isSolid, bool isTrans, bool isVehicle = false, string partType = "None")
    {
        return new BlockDefinition
        {
            blockID = id,
            blockName = name,
            hardness = hardness,
            preferredTool = tool,
            isSolid = isSolid,
            isTransparent = isTrans,
            isVehicleBlock = isVehicle,
            vehiclePartType = partType,
            dropAmount = 1
        };
    }
}
