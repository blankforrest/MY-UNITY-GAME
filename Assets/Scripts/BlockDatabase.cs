using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BlockDatabase", menuName = "Voxel Game/Block Database")]
public class BlockDatabase : ScriptableObject
{
    [Tooltip("List of all blocks configured in this database. Click the context menu (three dots) on this component and select 'Populate Default Blocks' to auto-fill the list with the 40+ existing base game blocks.")]
    public List<BlockDefinition> blocks = new List<BlockDefinition>();

    private void OnEnable()
    {
        PopulateDefaultBlocks();
    }

    [ContextMenu("Populate Default Blocks")]
    public void PopulateDefaultBlocks()
    {
        List<BlockDefinition> defaults = new List<BlockDefinition>();

        // Populate base game blocks with their standard game properties
        defaults.Add(CreateDef(1, "Wood", 1.5f, ToolType.Axe, true, false));
        defaults.Add(CreateDef(2, "Plank", 1.5f, ToolType.Axe, true, false));
        defaults.Add(CreateDef(3, "Stone", 3.0f, ToolType.Pickaxe, true, false));
        defaults.Add(CreateDef(4, "Grass Block", 0.8f, ToolType.Shovel, true, false));
        defaults.Add(CreateDef(5, "Dirt", 0.8f, ToolType.Shovel, true, false));
        defaults.Add(CreateDef(6, "Grass Slab", 1.5f, ToolType.Axe, true, false));
        defaults.Add(CreateDef(7, "Water", 0.0f, ToolType.None, false, true));
        defaults.Add(CreateDef(8, "Sand", 0.8f, ToolType.Shovel, true, false));
        defaults.Add(CreateDef(9, "Rose", 0.0f, ToolType.None, false, true));
        defaults.Add(CreateDef(10, "Dandelion", 0.0f, ToolType.None, false, true));
        defaults.Add(CreateDef(11, "Iris", 0.0f, ToolType.None, false, true));
        defaults.Add(CreateDef(12, "Leaves", 0.2f, ToolType.Sword, true, true));
        defaults.Add(CreateDef(13, "Short Grass", 0.0f, ToolType.None, false, true));
        defaults.Add(CreateDef(14, "Tall Grass", 0.0f, ToolType.None, false, true));
        
        defaults.Add(CreateDef(20, "Small Wheel", 1.0f, ToolType.Axe, true, false, true, "Wheel"));
        defaults.Add(CreateDef(21, "Large Wheel Anchor", 1.0f, ToolType.Axe, true, false, true, "Wheel"));
        defaults.Add(CreateDef(22, "Propeller", 1.0f, ToolType.Axe, true, false, true, "Propeller"));
        defaults.Add(CreateDef(23, "Large Wheel Helper", 1.0f, ToolType.Axe, true, false, true, "Wheel"));
        defaults.Add(CreateDef(24, "Propeller Casing", 1.0f, ToolType.Axe, true, false, true, "None"));
        defaults.Add(CreateDef(25, "Propeller Blade", 1.0f, ToolType.Axe, true, false, true, "None"));
        defaults.Add(CreateDef(26, "Large Propeller Anchor", 1.0f, ToolType.Axe, true, false, true, "Propeller"));
        defaults.Add(CreateDef(27, "Large Propeller Helper", 1.0f, ToolType.Axe, true, false, true, "Propeller"));

        defaults.Add(CreateDef(30, "Coal Ore", 3.0f, ToolType.Pickaxe, true, false));
        defaults.Add(CreateDef(31, "Iron Ore", 3.0f, ToolType.Pickaxe, true, false));
        defaults.Add(CreateDef(32, "Gold Block", 3.0f, ToolType.Pickaxe, true, false));
        defaults.Add(CreateDef(33, "Iron Block", 3.0f, ToolType.Pickaxe, true, false));
        defaults.Add(CreateDef(34, "Sand (Unused ID)", 0.8f, ToolType.Shovel, true, false));
        defaults.Add(CreateDef(35, "Glass", 0.3f, ToolType.None, true, true));
        defaults.Add(CreateDef(36, "Crafting Table", 1.5f, ToolType.Axe, true, false));
        defaults.Add(CreateDef(37, "Furnace", 3.0f, ToolType.Pickaxe, true, false));

        defaults.Add(CreateDef(38, "Wooden Stairs", 1.5f, ToolType.Axe, true, true));
        defaults.Add(CreateDef(39, "Stone Stairs", 3.0f, ToolType.Pickaxe, true, true));
        defaults.Add(CreateDef(46, "Wooden Slab", 1.5f, ToolType.Axe, true, true));
        defaults.Add(CreateDef(47, "Stone Slab", 3.0f, ToolType.Pickaxe, true, true));

        defaults.Add(CreateDef(48, "Bedrock", 0.0f, ToolType.None, true, false));
        defaults.Add(CreateDef(49, "Cactus", 0.4f, ToolType.None, true, false));
        defaults.Add(CreateDef(50, "Control Block", 1.0f, ToolType.Axe, true, false, true, "ControlBlock"));
        defaults.Add(CreateDef(51, "Birch Log", 1.5f, ToolType.Axe, true, false));
        defaults.Add(CreateDef(52, "Birch Leaves", 0.2f, ToolType.Sword, true, true));
        defaults.Add(CreateDef(53, "Spruce Log", 1.5f, ToolType.Axe, true, false));
        defaults.Add(CreateDef(54, "Spruce Leaves", 0.2f, ToolType.Sword, true, true));
        defaults.Add(CreateDef(55, "Diamond Ore", 3.0f, ToolType.Pickaxe, true, false));
        defaults.Add(CreateDef(56, "Gravel", 1.0f, ToolType.Shovel, true, false));
        defaults.Add(CreateDef(57, "Gold Ore", 3.0f, ToolType.Pickaxe, true, false));

        if (blocks == null)
        {
            blocks = new List<BlockDefinition>();
        }

        // Merge defaults without clearing existing user modifications
        foreach (var def in defaults)
        {
            BlockDefinition existing = blocks.Find(b => b.blockID == def.blockID);
            if (existing != null)
            {
                // Synchronize non-reference settings to ensure gameplay mechanics match defaults
                existing.blockName = def.blockName;
                existing.hardness = def.hardness;
                existing.preferredTool = def.preferredTool;
                existing.isSolid = def.isSolid;
                existing.isTransparent = def.isTransparent;
                existing.emitsLight = def.emitsLight;
                existing.lightLevel = def.lightLevel;
                existing.isVehicleBlock = def.isVehicleBlock;
                existing.vehiclePartType = def.vehiclePartType;
                existing.dropRule = def.dropRule;
                existing.dropAmount = def.dropAmount;

                // Merge references safely: ONLY overwrite if they are null
                if (existing.inventoryIcon == null) existing.inventoryIcon = def.inventoryIcon;
                if (existing.textureTop == null) existing.textureTop = def.textureTop;
                if (existing.textureSide == null) existing.textureSide = def.textureSide;
                if (existing.textureBottom == null) existing.textureBottom = def.textureBottom;
                if (existing.textureFront == null) existing.textureFront = def.textureFront;
                if (existing.textureFrontLit == null) existing.textureFrontLit = def.textureFrontLit;
                if (existing.dropItem == null) existing.dropItem = def.dropItem;
                if (existing.stepSound == null) existing.stepSound = def.stepSound;
                if (existing.placeSound == null) existing.placeSound = def.placeSound;
                if (existing.breakSound == null) existing.breakSound = def.breakSound;
                if (existing.customMesh == null) existing.customMesh = def.customMesh;
            }
            else
            {
                blocks.Add(def);
            }
        }

        // Sort blocks by ID to keep the inspector list clean
        blocks.Sort((a, b) => a.blockID.CompareTo(b.blockID));

        Debug.Log($"[BlockDatabase] Successfully synchronized default block definitions, preserving existing references.");
    }

    private BlockDefinition CreateDef(byte id, string name, float hardness, ToolType tool, bool isSolid, bool isTrans, bool isVehicle = false, string partType = "None", DropRule dropRule = DropRule.DropsSelf, Item dropItem = null)
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
            dropRule = dropRule,
            dropItem = dropItem,
            dropAmount = 1
        };
    }
}
