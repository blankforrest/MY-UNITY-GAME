using UnityEngine;
using System.Collections.Generic;

public static class BlockRegistry
{
    public static List<BlockDefinition> RegisteredBlocks { get; private set; } = new List<BlockDefinition>();
    private static Dictionary<byte, BlockDefinition> byID = new Dictionary<byte, BlockDefinition>();
    private static Dictionary<string, BlockDefinition> byName = new Dictionary<string, BlockDefinition>();

    // Mapping of custom block ID to the tile indices in the atlas:
    // Key: Block ID. Value: Face indices (0=back, 1=front, 2=top, 3=bottom, 4=left, 5=right)
    private static Dictionary<byte, int[]> faceTiles = new Dictionary<byte, int[]>();

    // Tracking active point lights for light-emitting blocks
    private static Dictionary<Vector3Int, GameObject> activeLights = new Dictionary<Vector3Int, GameObject>();

    public static void Initialize(List<BlockDefinition> customDefs)
    {
        RegisteredBlocks.Clear();
        byID.Clear();
        byName.Clear();
        faceTiles.Clear();
        ClearAllLights();

        byte nextID = 60; // Dynamic IDs start at 60 to avoid collisions with hardcoded IDs

        foreach (var def in customDefs)
        {
            if (def == null) continue;

            // Automatically assign dynamic block ID if not set
            if (def.blockID == 0)
            {
                while (IsReservedID(nextID) || byID.ContainsKey(nextID))
                {
                    nextID++;
                }
                def.blockID = nextID;
            }

            if (!byID.ContainsKey(def.blockID))
            {
                byID[def.blockID] = def;
                byName[def.blockName] = def;
                RegisteredBlocks.Add(def);

                // Auto-configure the associated Item SO if present
                if (def.dropItem != null)
                {
                    def.dropItem.blockTypeID = def.blockID;
                    if (def.inventoryIcon != null)
                    {
                        def.dropItem.icon = def.inventoryIcon;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[BlockRegistry] Duplicate block ID detected: {def.blockID} for '{def.blockName}'");
            }
        }
    }

    private static bool IsReservedID(byte id)
    {
        // Reserve IDs below 60 for hardcoded base game blocks
        return id < 60;
    }

    public static BlockDefinition GetDefinition(byte id)
    {
        if (byID.TryGetValue(id, out var def)) return def;
        return null;
    }

    public static BlockDefinition GetDefinition(string name)
    {
        if (byName.TryGetValue(name, out var def)) return def;
        return null;
    }

    public static void RegisterFaceTiles(byte blockID, int topTile, int sideTile, int bottomTile)
    {
        faceTiles[blockID] = new int[6] {
            sideTile,   // back
            sideTile,   // front
            topTile,    // top
            bottomTile, // bottom
            sideTile,   // left
            sideTile    // right
        };
    }

    public static int GetTileIndex(byte blockID, int face)
    {
        if (faceTiles.TryGetValue(blockID, out var tiles))
        {
            if (face >= 0 && face < 6) return tiles[face];
        }
        return -1;
    }

    // ── Point Lights Management ────────────────────────────────────────────────

    public static void AddLight(Vector3 worldPos, int lightLevel)
    {
        Vector3Int gridPos = new Vector3Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y),
            Mathf.FloorToInt(worldPos.z)
        );

        if (activeLights.ContainsKey(gridPos)) return;

        GameObject lightGO = new GameObject($"BlockLight_{gridPos}");
        lightGO.transform.position = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, gridPos.z + 0.5f);

        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        
        // Cozy warm tone light
        light.color = new Color(1.0f, 0.92f, 0.75f);
        
        // Scale range and intensity based on configured lightLevel
        float levelFactor = Mathf.Clamp01((float)lightLevel / 15f);
        light.range = Mathf.Lerp(4f, 15f, levelFactor);
        light.intensity = Mathf.Lerp(0.5f, 2.5f, levelFactor);

        if (VoxelWorld.Instance != null)
        {
            lightGO.transform.SetParent(VoxelWorld.Instance.transform, true);
        }

        activeLights[gridPos] = lightGO;
    }

    public static void RemoveLight(Vector3 worldPos)
    {
        Vector3Int gridPos = new Vector3Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y),
            Mathf.FloorToInt(worldPos.z)
        );

        if (activeLights.TryGetValue(gridPos, out GameObject lightGO))
        {
            if (lightGO != null)
            {
                Object.Destroy(lightGO);
            }
            activeLights.Remove(gridPos);
        }
    }

    public static void ClearAllLights()
    {
        foreach (var lightGO in activeLights.Values)
        {
            if (lightGO != null) Object.Destroy(lightGO);
        }
        activeLights.Clear();
    }
}
