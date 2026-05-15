using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utility for detecting connected structures made of player-placed voxels.
/// Works directly with the VoxelWorld grid and PlacedBlockRegistry.
/// Does NOT touch terrain generation code.
/// </summary>
public static class StructureScanner
{
    // Six face-adjacent directions on the voxel grid (no diagonals).
    private static readonly Vector3Int[] Neighbours = new Vector3Int[]
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down,
        new Vector3Int(0, 0,  1),
        new Vector3Int(0, 0, -1),
    };

    /// <summary>
    /// BFS flood fill starting from <paramref name="startGridPos"/>.
    /// Only expands into positions that:
    ///   (a) contain a non-air voxel in VoxelWorld, AND
    ///   (b) are registered as player-placed in PlacedBlockRegistry.
    ///
    /// Stops early if the bounding box exceeds <paramref name="maxRange"/> on any axis.
    /// </summary>
    /// <param name="startGridPos">Integer voxel grid coordinate to start from.</param>
    /// <param name="maxRange">Max bounding-box size on any single axis (default 32).</param>
    /// <returns>All connected player-placed grid positions, including the start.</returns>
    public static List<Vector3Int> FloodFillStructure(Vector3Int startGridPos, float maxRange = 32f)
    {
        List<Vector3Int> result = new List<Vector3Int>();

        if (VoxelWorld.Instance == null)
        {
            Debug.LogError("[StructureScanner] VoxelWorld.Instance is null.");
            return result;
        }

        if (PlacedBlockRegistry.Instance == null)
        {
            Debug.LogError("[StructureScanner] PlacedBlockRegistry.Instance is null. " +
                           "Add PlacedBlockRegistry to a GameObject in the scene.");
            return result;
        }

        // Start position must itself be player-placed.
        if (!PlacedBlockRegistry.Instance.IsPlayerPlaced(startGridPos))
        {
            Debug.Log("[StructureScanner] Start position is not a player-placed block.");
            return result;
        }

        // --- BFS ---
        HashSet<Vector3Int> visited  = new HashSet<Vector3Int>();
        Queue<Vector3Int>   queue    = new Queue<Vector3Int>();

        Vector3Int minBound = startGridPos;
        Vector3Int maxBound = startGridPos;

        visited.Add(startGridPos);
        queue.Enqueue(startGridPos);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            result.Add(current);

            // Expand bounding box
            minBound = Vector3Int.Min(minBound, current);
            maxBound = Vector3Int.Max(maxBound, current);

            // Check bounding-box limit
            Vector3Int size = maxBound - minBound;
            if (size.x >= maxRange || size.y >= maxRange || size.z >= maxRange)
            {
                Debug.LogWarning(
                    $"[StructureScanner] Bounding box exceeded {maxRange} units on at least one axis. " +
                    $"Stopping early. Blocks found so far: {result.Count}");
                break;
            }

            // Visit face neighbours
            foreach (Vector3Int dir in Neighbours)
            {
                Vector3Int neighbour = current + dir;

                if (visited.Contains(neighbour))
                    continue;

                visited.Add(neighbour);

                // Only continue through player-placed, non-air voxels
                if (!PlacedBlockRegistry.Instance.IsPlayerPlaced(neighbour))
                    continue;

                // Verify the voxel is actually solid in the world (handles stale registry entries)
                Vector3 worldCenter = new Vector3(neighbour.x + 0.5f, neighbour.y + 0.5f, neighbour.z + 0.5f);
                byte voxelID = VoxelWorld.Instance.GetBlock(worldCenter);
                if (voxelID == 0)
                    continue;

                queue.Enqueue(neighbour);
            }
        }

        Debug.Log($"[StructureScanner] Flood fill complete. Connected player-placed blocks found: {result.Count}");
        return result;
    }
}
