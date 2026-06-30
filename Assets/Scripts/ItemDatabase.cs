using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Voxel Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Tooltip("List of all items configured in this database.")]
    public List<ItemDefinition> items = new List<ItemDefinition>();

    private void OnEnable()
    {
        PopulateDefaultItems();
    }

    [ContextMenu("Populate Default Items")]
    public void PopulateDefaultItems()
    {
        List<ItemDefinition> defaults = new List<ItemDefinition>();

        // 1. Specialty / Vehicles
        defaults.Add(CreateDef(99, "Wrench", "VEHICLES", 0, ToolType.None, ToolTier.None, false, 1, "A tool for modifying and rotating vehicles."));
        
        // 2. Spawn Eggs
        defaults.Add(CreateDef(98, "Wolf Spawn Egg", "SPAWNERS", 0, ToolType.None, ToolTier.None, true, 64, "Spawns a friendly wolf."));
        defaults.Add(CreateDef(95, "Sheep Spawn Egg", "SPAWNERS", 0, ToolType.None, ToolTier.None, true, 64, "Spawns a fluffy sheep."));

        // 3. Materials / Items
        defaults.Add(CreateDef(97, "Coal Chunk", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "A chunk of coal. Can be used as fuel.", burn: 8f));
        defaults.Add(CreateDef(96, "Iron Ingot", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "A refined iron ingot."));
        defaults.Add(CreateDef(94, "Gold Ingot", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "A shiny gold ingot."));
        defaults.Add(CreateDef(93, "Diamond", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "A rare and precious diamond."));
        defaults.Add(CreateDef(92, "Stick", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "A simple wooden stick."));
        defaults.Add(CreateDef(91, "Wool", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "Soft wool gathered from sheep."));
        defaults.Add(CreateDef(90, "Leather", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "Tough animal leather."));
        
        // 4. Edibles
        defaults.Add(CreateDef(89, "Mutton", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "Raw mutton meat. Restores health.", healAmount: 4));
        defaults.Add(CreateDef(88, "Apple", "ITEMS", 0, ToolType.None, ToolTier.None, true, 64, "A delicious red apple. Restores health.", healAmount: 2));

        // 5. Tools of various tiers
        // Diamond
        defaults.Add(CreateDef(101, "Diamond Pickaxe", "TOOLS", 0, ToolType.Pickaxe, ToolTier.Diamond, false, 1, "A high-durability diamond pickaxe."));
        defaults.Add(CreateDef(102, "Diamond Axe", "TOOLS", 0, ToolType.Axe, ToolTier.Diamond, false, 1, "A high-durability diamond axe."));
        defaults.Add(CreateDef(103, "Diamond Shovel", "TOOLS", 0, ToolType.Shovel, ToolTier.Diamond, false, 1, "A high-durability diamond shovel."));
        defaults.Add(CreateDef(104, "Diamond Sword", "TOOLS", 0, ToolType.Sword, ToolTier.Diamond, false, 1, "A high-damage diamond sword."));
        defaults.Add(CreateDef(105, "Diamond Rake", "TOOLS", 0, ToolType.Rake, ToolTier.Diamond, false, 1, "A diamond rake."));

        // Iron
        defaults.Add(CreateDef(111, "Iron Pickaxe", "TOOLS", 0, ToolType.Pickaxe, ToolTier.Iron, false, 1, "An iron pickaxe."));
        defaults.Add(CreateDef(112, "Iron Axe", "TOOLS", 0, ToolType.Axe, ToolTier.Iron, false, 1, "An iron axe."));
        defaults.Add(CreateDef(113, "Iron Shovel", "TOOLS", 0, ToolType.Shovel, ToolTier.Iron, false, 1, "An iron shovel."));
        defaults.Add(CreateDef(114, "Iron Sword", "TOOLS", 0, ToolType.Sword, ToolTier.Iron, false, 1, "An iron sword."));
        defaults.Add(CreateDef(115, "Iron Rake", "TOOLS", 0, ToolType.Rake, ToolTier.Iron, false, 1, "An iron rake."));

        // Stone
        defaults.Add(CreateDef(121, "Stone Pickaxe", "TOOLS", 0, ToolType.Pickaxe, ToolTier.Stone, false, 1, "A stone pickaxe."));
        defaults.Add(CreateDef(122, "Stone Axe", "TOOLS", 0, ToolType.Axe, ToolTier.Stone, false, 1, "A stone axe."));
        defaults.Add(CreateDef(123, "Stone Shovel", "TOOLS", 0, ToolType.Shovel, ToolTier.Stone, false, 1, "A stone shovel."));
        defaults.Add(CreateDef(124, "Stone Sword", "TOOLS", 0, ToolType.Sword, ToolTier.Stone, false, 1, "A stone sword."));
        defaults.Add(CreateDef(125, "Stone Rake", "TOOLS", 0, ToolType.Rake, ToolTier.Stone, false, 1, "A stone rake."));

        // Wooden
        defaults.Add(CreateDef(131, "Wooden Pickaxe", "TOOLS", 0, ToolType.Pickaxe, ToolTier.Wood, false, 1, "A fragile wooden pickaxe."));
        defaults.Add(CreateDef(132, "Wooden Axe", "TOOLS", 0, ToolType.Axe, ToolTier.Wood, false, 1, "A fragile wooden axe."));
        defaults.Add(CreateDef(133, "Wooden Shovel", "TOOLS", 0, ToolType.Shovel, ToolTier.Wood, false, 1, "A fragile wooden shovel."));
        defaults.Add(CreateDef(134, "Wooden Sword", "TOOLS", 0, ToolType.Sword, ToolTier.Wood, false, 1, "A fragile wooden sword."));
        defaults.Add(CreateDef(135, "Wooden Rake", "TOOLS", 0, ToolType.Rake, ToolTier.Wood, false, 1, "A fragile wooden rake."));

        if (items == null)
        {
            items = new List<ItemDefinition>();
        }

        // Merge defaults without clearing existing user modifications
        foreach (var def in defaults)
        {
            ItemDefinition existing = items.Find(i => i.itemName == def.itemName || (i.itemID == def.itemID && def.itemID != 0));
            if (existing != null)
            {
                // Synchronize settings
                existing.itemName = def.itemName;
                existing.itemID = def.itemID;
                existing.creativeBag = def.creativeBag;
                existing.blockTypeID = def.blockTypeID;
                existing.toolType = def.toolType;
                existing.toolTier = def.toolTier;
                existing.isStackable = def.isStackable;
                existing.maxStackSize = def.maxStackSize;
                existing.description = def.description;
                existing.burnDuration = def.burnDuration;
                existing.healAmount = def.healAmount;

                // Merge references safely
                if (existing.inventoryIcon == null) existing.inventoryIcon = def.inventoryIcon;
                if (existing.droppedItemSprite == null) existing.droppedItemSprite = def.droppedItemSprite;
            }
            else
            {
                items.Add(def);
            }
        }

        // Sort items by ID
        items.Sort((a, b) => a.itemID.CompareTo(b.itemID));

        Debug.Log($"[ItemDatabase] Successfully synchronized default item definitions.");
    }

    private ItemDefinition CreateDef(int id, string name, string bag, int blockId, ToolType tType, ToolTier tTier, bool stackable, int maxStack, string desc, float burn = 0f, int healAmount = 0)
    {
        return new ItemDefinition
        {
            itemID = id,
            itemName = name,
            creativeBag = bag,
            blockTypeID = blockId,
            toolType = tType,
            toolTier = tTier,
            isStackable = stackable,
            maxStackSize = maxStack,
            description = desc,
            burnDuration = burn,
            healAmount = healAmount
        };
    }
}
