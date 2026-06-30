using UnityEngine;
using System.Collections.Generic;

public static class ItemRegistry
{
    private static List<ItemDefinition> _registeredItems = new List<ItemDefinition>();
    public static List<ItemDefinition> RegisteredItems
    {
        get
        {
            CheckAndEnsureInitialized();
            return _registeredItems;
        }
    }

    private static Dictionary<int, ItemDefinition> byID = new Dictionary<int, ItemDefinition>();
    private static Dictionary<string, ItemDefinition> byName = new Dictionary<string, ItemDefinition>(System.StringComparer.OrdinalIgnoreCase);

    private static bool _isInitializing = false;

    private static void CheckAndEnsureInitialized()
    {
        if (_isInitializing) return;
        if (_registeredItems.Count == 0 && VoxelWorld.Instance != null && VoxelWorld.Instance.itemDatabase != null)
        {
            _isInitializing = true;
            try
            {
                Debug.Log("[ItemRegistry] Lost static state detected. Re-initializing mappings from itemDatabase...");
                Initialize(VoxelWorld.Instance.itemDatabase.items);
            }
            finally
            {
                _isInitializing = false;
            }
        }
    }

    public static void Initialize(List<ItemDefinition> customDefs)
    {
        _registeredItems.Clear();
        byID.Clear();
        byName.Clear();

        int nextID = 200; // Dynamic IDs start at 200 to avoid collisions with hardcoded/known item IDs

        foreach (var def in customDefs)
        {
            if (def == null) continue;

            if (def.itemID == 0)
            {
                while (byID.ContainsKey(nextID))
                {
                    nextID++;
                }
                def.itemID = nextID;
            }

            if (!byID.ContainsKey(def.itemID))
            {
                byID[def.itemID] = def;
                byName[def.itemName] = def;
                _registeredItems.Add(def);
            }
            else
            {
                Debug.LogWarning($"[ItemRegistry] Duplicate item ID detected: {def.itemID} for '{def.itemName}'");
            }
        }
        
        Debug.Log($"[ItemRegistry] Registered {_registeredItems.Count} items successfully.");
    }

    public static ItemDefinition GetDefinition(int id)
    {
        CheckAndEnsureInitialized();
        if (byID.TryGetValue(id, out var def)) return def;
        return null;
    }

    public static ItemDefinition GetDefinition(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        CheckAndEnsureInitialized();
        if (byName.TryGetValue(name, out var def)) return def;
        return null;
    }
}
