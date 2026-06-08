using System.Collections.Generic;
using UnityEngine;

// ── Data types ────────────────────────────────────────────────────────────────

/// <summary>A single block's position (relative to structure origin) and type.</summary>
public struct BlockEntry
{
    /// <summary>Position in local structure space (origin = min corner of bounding box).</summary>
    public Vector3Int localPosition;

    /// <summary>Voxel block type ID (e.g. 1=wood, 2=plank, 5=iron).</summary>
    public int blockTypeID;

    public BlockEntry(Vector3Int localPos, int typeID)
    {
        localPosition = localPos;
        blockTypeID   = typeID;
    }
}

/// <summary>
/// Describes the full shape, mass, and durability of a player-built structure.
/// Produced by <see cref="BlueprintGenerator.GenerateBlueprint"/>.
/// </summary>
public class StructureBlueprint
{
    /// <summary>Every block in the structure with its local position and type.</summary>
    public List<BlockEntry> blocks = new List<BlockEntry>();

    /// <summary>Width (X), Height (Y), Depth (Z) of the axis-aligned bounding box.</summary>
    public Vector3Int dimensions;

    /// <summary>Sum of per-block masses using <see cref="BlueprintGenerator.BlockMassTable"/>.</summary>
    public float totalMass;

    /// <summary>Sum of per-block durability using <see cref="BlueprintGenerator.BlockDurabilityTable"/>.</summary>
    public float totalDurability;

    /// <summary>Minimum world-space corner of the structure (the local origin).</summary>
    public Vector3 worldOrigin;
}

// ── Generator ─────────────────────────────────────────────────────────────────

/// <summary>
/// Static utility that converts a flood-filled set of grid positions into a
/// <see cref="StructureBlueprint"/> with mass, durability, and bounding box data.
/// </summary>
public static class BlueprintGenerator
{
    // ── Property tables — easy to expand ─────────────────────────────────────
    // Key = blockTypeID, Value = mass in kg per block

    /// <summary>
    /// Mass (kg) per block type. Add new entries here as you add block types.
    /// Any block type not listed defaults to <see cref="DefaultMass"/>.
    /// </summary>
    public static readonly Dictionary<int, float> BlockMassTable = new Dictionary<int, float>
    {
        { 1, 1.0f  },  // wood
        { 2, 1.2f  },  // plank
        { 3, 2.5f  },  // stone
        { 5, 5.0f  },  // iron
        { 7, 1.0f  },  // water
        { 8, 1.5f  },  // sand
        { 9, 0.1f  },  // rose flower
        { 10, 0.1f },  // dandelion
        { 11, 0.1f },  // iris
        { 12, 0.3f },  // leaves
        { 20, 2.0f },  // small wheel
        { 21, 4.0f },  // large wheel
        { 22, 2.5f },  // propeller
        { 50, 1.5f },  // control block
    };

    /// <summary>
    /// Durability points per block type. Add new entries here as you add block types.
    /// Any block type not listed defaults to <see cref="DefaultDurability"/>.
    /// </summary>
    public static readonly Dictionary<int, float> BlockDurabilityTable = new Dictionary<int, float>
    {
        { 1, 10f  },  // wood
        { 2, 12f  },  // plank
        { 3, 35f  },  // stone
        { 5, 80f  },  // iron
        { 7, 1f   },  // water
        { 8, 8f   },  // sand
        { 9, 1f   },  // rose flower
        { 10, 1f  },  // dandelion
        { 11, 1f  },  // iris
        { 12, 3f  },  // leaves
        { 20, 20f },  // small wheel
        { 21, 40f },  // large wheel
        { 22, 20f },  // propeller
        { 50, 15f },  // control block
    };

    /// <summary>Fallback mass for block types not in <see cref="BlockMassTable"/>.</summary>
    public const float DefaultMass       = 1f;

    /// <summary>Fallback durability for block types not in <see cref="BlockDurabilityTable"/>.</summary>
    public const float DefaultDurability = 10f;

    // ── Main API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="StructureBlueprint"/> from a list of world grid positions
    /// returned by <see cref="StructureScanner.FloodFillStructure"/>.
    ///
    /// Block type IDs are read live from <see cref="VoxelWorld.GetBlock"/> so no
    /// stale component data is involved.
    /// </summary>
    /// <param name="gridPositions">Connected player-placed voxel positions.</param>
    /// <returns>A fully populated blueprint, or null if the list is empty.</returns>
    public static StructureBlueprint GenerateBlueprint(List<Vector3Int> gridPositions)
    {
        if (gridPositions == null || gridPositions.Count == 0)
        {
            Debug.LogWarning("[BlueprintGenerator] GenerateBlueprint called with empty list.");
            return null;
        }

        // ── Step 1: Find bounding box min corner (world origin) ───────────────
        Vector3Int min = gridPositions[0];
        Vector3Int max = gridPositions[0];

        foreach (Vector3Int pos in gridPositions)
        {
            min = Vector3Int.Min(min, pos);
            max = Vector3Int.Max(max, pos);
        }

        // ── Step 2: Populate blueprint ────────────────────────────────────────
        StructureBlueprint blueprint = new StructureBlueprint();
        blueprint.worldOrigin = new Vector3(min.x, min.y, min.z);
        blueprint.dimensions  = (max - min) + Vector3Int.one; // +1 because max is inclusive

        float totalMass = 0f, totalDurability = 0f;

        foreach (Vector3Int worldPos in gridPositions)
        {
            // Read block type from voxel world (center of voxel for GetBlock)
            int typeID = 0;
            if (VoxelWorld.Instance != null)
            {
                Vector3 center = new Vector3(worldPos.x + 0.5f, worldPos.y + 0.5f, worldPos.z + 0.5f);
                typeID = VoxelWorld.Instance.GetBlock(center);
            }

            Vector3Int localPos = worldPos - min;

            blueprint.blocks.Add(new BlockEntry(localPos, typeID));

            // Accumulate mass
            totalMass += BlockMassTable.TryGetValue(typeID, out float m) ? m : DefaultMass;

            // Accumulate durability
            totalDurability += BlockDurabilityTable.TryGetValue(typeID, out float d) ? d : DefaultDurability;
        }

        blueprint.totalMass       = totalMass;
        blueprint.totalDurability = totalDurability;

        // ── Step 3: Log summary ───────────────────────────────────────────────
        Debug.Log($"[BlueprintGenerator] Blueprint: " +
                  $"{blueprint.dimensions.x}x{blueprint.dimensions.y}x{blueprint.dimensions.z}, " +
                  $"Mass: {blueprint.totalMass:F1}, " +
                  $"Durability: {blueprint.totalDurability:F1}, " +
                  $"Blocks: {blueprint.blocks.Count}");

        return blueprint;
    }
}
