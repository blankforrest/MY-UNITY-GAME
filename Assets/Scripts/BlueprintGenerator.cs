using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

// ── Data types ────────────────────────────────────────────────────────────────

/// <summary>A single block's position (relative to structure origin) and type.</summary>
[System.Serializable]
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
        { 23, 0.0f },  // large wheel helper
        { 26, 6.0f },  // large propeller
        { 27, 0.0f },  // large propeller helper
        { 50, 1.5f },  // control block
        { 30, 2.2f },  // coal ore
        { 31, 3.5f },  // iron ore
        { 32, 8.0f },  // gold block
        { 33, 5.0f },  // iron block
        { 34, 1.5f },  // sand (34)
        { 35, 0.8f },  // glass
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
        { 23, 0f   },  // large wheel helper
        { 26, 40f },  // large propeller
        { 27, 0f   },  // large propeller helper
        { 50, 15f },  // control block
        { 30, 25f },  // coal ore
        { 31, 45f },  // iron ore
        { 32, 50f },  // gold block
        { 33, 75f },  // iron block
        { 34, 8f   },  // sand (34)
        { 35, 5f   },  // glass
    };

    /// <summary>Fallback mass for block types not in <see cref="BlockMassTable"/>.</summary>
    public const float DefaultMass       = 1f;

    /// <summary>Fallback durability for block types not in <see cref="BlockDurabilityTable"/>.</summary>
    public const float DefaultDurability = 10f;

    // ── Main API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="StructureBlueprint"/> from a list of world grid positions
    /// returned by <see cref="StructureScanner.FloodFillStructure"/>.
    /// </summary>
    public static StructureBlueprint GenerateBlueprint(List<Vector3Int> gridPositions)
    {
        if (gridPositions == null || gridPositions.Count == 0)
        {
            Debug.LogWarning("[BlueprintGenerator] GenerateBlueprint called with empty list.");
            return null;
        }

        int count = gridPositions.Count;

        // Allocate NativeArrays for the Job inputs and outputs using Unity.Mathematics int3
        NativeArray<int3> gridPositionsNative = new NativeArray<int3>(count, Allocator.TempJob);
        NativeArray<int> blockTypeIDsNative = new NativeArray<int>(count, Allocator.TempJob);
        NativeArray<float> massTableNative = new NativeArray<float>(256, Allocator.TempJob);
        NativeArray<float> durabilityTableNative = new NativeArray<float>(256, Allocator.TempJob);

        // Output arrays
        NativeArray<int3> minOut = new NativeArray<int3>(1, Allocator.TempJob);
        NativeArray<int3> maxOut = new NativeArray<int3>(1, Allocator.TempJob);
        NativeArray<float> totalMassOut = new NativeArray<float>(1, Allocator.TempJob);
        NativeArray<float> totalDurabilityOut = new NativeArray<float>(1, Allocator.TempJob);
        NativeArray<BlockEntry> outBlocks = new NativeArray<BlockEntry>(count, Allocator.TempJob);

        // Fetch block type IDs on the main thread
        for (int i = 0; i < count; i++)
        {
            Vector3Int worldPos = gridPositions[i];
            gridPositionsNative[i] = new int3(worldPos.x, worldPos.y, worldPos.z);

            int typeID = 0;
            if (VoxelWorld.Instance != null)
            {
                Vector3 center = new Vector3(worldPos.x + 0.5f, worldPos.y + 0.5f, worldPos.z + 0.5f);
                typeID = VoxelWorld.Instance.GetBlock(center);
            }
            blockTypeIDsNative[i] = typeID;
        }

        // Populate flat arrays for fast Burst-compatible lookup
        for (int i = 0; i < 256; i++)
        {
            massTableNative[i] = BlockMassTable.TryGetValue(i, out float m) ? m : DefaultMass;
            durabilityTableNative[i] = BlockDurabilityTable.TryGetValue(i, out float d) ? d : DefaultDurability;
        }

        // Schedule and execute the job
        GenerateBlueprintJob job = new GenerateBlueprintJob
        {
            GridPositions = gridPositionsNative,
            BlockTypeIDs = blockTypeIDsNative,
            MassTable = massTableNative,
            DurabilityTable = durabilityTableNative,
            DefaultMass = DefaultMass,
            DefaultDurability = DefaultDurability,
            MinOut = minOut,
            MaxOut = maxOut,
            TotalMassOut = totalMassOut,
            TotalDurabilityOut = totalDurabilityOut,
            OutBlocks = outBlocks
        };

        JobHandle handle = job.Schedule();
        handle.Complete();

        // Convert the outputs back to the managed StructureBlueprint class
        StructureBlueprint blueprint = new StructureBlueprint();
        int3 min = minOut[0];
        int3 max = maxOut[0];
        blueprint.worldOrigin = new Vector3(min.x, min.y, min.z);
        blueprint.dimensions  = new Vector3Int(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
        blueprint.totalMass = totalMassOut[0];
        blueprint.totalDurability = totalDurabilityOut[0];

        blueprint.blocks = new List<BlockEntry>(count);
        for (int i = 0; i < count; i++)
        {
            blueprint.blocks.Add(outBlocks[i]);
        }

        // Dispose temporary NativeArrays
        gridPositionsNative.Dispose();
        blockTypeIDsNative.Dispose();
        massTableNative.Dispose();
        durabilityTableNative.Dispose();
        minOut.Dispose();
        maxOut.Dispose();
        totalMassOut.Dispose();
        totalDurabilityOut.Dispose();
        outBlocks.Dispose();

        // Log summary
        Debug.Log($"[BlueprintGenerator] Blueprint: " +
                  $"{blueprint.dimensions.x}x{blueprint.dimensions.y}x{blueprint.dimensions.z}, " +
                  $"Mass: {blueprint.totalMass:F1}, " +
                  $"Durability: {blueprint.totalDurability:F1}, " +
                  $"Blocks: {blueprint.blocks.Count}");

        return blueprint;
    }

    // ── Burst-Compiled Job ───────────────────────────────────────────────────

    [BurstCompile]
    private struct GenerateBlueprintJob : IJob
    {
        [ReadOnly] public NativeArray<int3> GridPositions;
        [ReadOnly] public NativeArray<int> BlockTypeIDs;
        [ReadOnly] public NativeArray<float> MassTable;
        [ReadOnly] public NativeArray<float> DurabilityTable;

        public float DefaultMass;
        public float DefaultDurability;

        public NativeArray<int3> MinOut;
        public NativeArray<int3> MaxOut;
        public NativeArray<float> TotalMassOut;
        public NativeArray<float> TotalDurabilityOut;
        public NativeArray<BlockEntry> OutBlocks;

        public void Execute()
        {
            if (GridPositions.Length == 0) return;

            int3 min = GridPositions[0];
            int3 max = GridPositions[0];

            // 1. Find bounding box min and max using Unity.Mathematics SIMD min/max
            for (int i = 1; i < GridPositions.Length; i++)
            {
                int3 pos = GridPositions[i];
                min = math.min(min, pos);
                max = math.max(max, pos);
            }

            MinOut[0] = min;
            MaxOut[0] = max;

            float totalMass = 0f;
            float totalDurability = 0f;

            // 2. Populate entries and lookup values
            for (int i = 0; i < GridPositions.Length; i++)
            {
                int3 worldPos = GridPositions[i];
                int typeID = BlockTypeIDs[i];

                int3 localPos = worldPos - min;
                Vector3Int localPosV3 = new Vector3Int(localPos.x, localPos.y, localPos.z);
                OutBlocks[i] = new BlockEntry(localPosV3, typeID);

                float m = (typeID >= 0 && typeID < 256) ? MassTable[typeID] : DefaultMass;
                float d = (typeID >= 0 && typeID < 256) ? DurabilityTable[typeID] : DefaultDurability;

                totalMass += m;
                totalDurability += d;
            }

            TotalMassOut[0] = totalMass;
            TotalDurabilityOut[0] = totalDurability;
        }
    }
}
