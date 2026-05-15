using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour that converts a StructureBlueprint into a physics-enabled vehicle GameObject.
/// Attach to the UIManager (or any persistent scene object).
///
/// INSPECTOR SETUP:
///   Block Prefabs — assign one prefab per blockTypeID (index 0 = unused, 1 = wood, 2 = plank, 5 = iron, etc.)
///   If a prefab slot is left empty or index is out of range, a plain unit cube is used as fallback.
/// </summary>
public class VehicleSpawner : MonoBehaviour
{
    public static VehicleSpawner Instance { get; private set; }

    [Header("Block Prefabs (index = blockTypeID)")]
    [Tooltip("Assign one prefab per block type ID. Index 0 is unused. Leave slots empty to use a default cube.")]
    public GameObject[] blockPrefabs;

    [Header("Vehicle Physics")]
    public float drag        = 1f;
    public float angularDrag = 2f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="blueprint"/> into a vehicle GameObject:
    ///   - Creates a parent GO with a Rigidbody (mass = blueprint.totalMass)
    ///   - Instantiates a block prefab (or fallback cube) for each BlockEntry
    ///   - Removes the original voxels from the world
    /// </summary>
    public void SpawnVehicle(StructureBlueprint blueprint)
    {
        if (blueprint == null)
        {
            Debug.LogWarning("[VehicleSpawner] SpawnVehicle called with null blueprint.");
            return;
        }

        // ── a. Create the vehicle parent ──────────────────────────────────────
        string vehicleName = $"Vehicle_{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        GameObject vehicleGO = new GameObject(vehicleName);
        vehicleGO.transform.position = blueprint.worldOrigin;

        // ── b. Instantiate one block per BlockEntry ───────────────────────────
        foreach (BlockEntry entry in blueprint.blocks)
        {
            GameObject prefab = GetPrefabForType(entry.blockTypeID);

            GameObject blockGO;
            if (prefab != null)
            {
                blockGO = Instantiate(prefab, vehicleGO.transform);
            }
            else
            {
                // Fallback: plain 1×1×1 cube
                blockGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blockGO.transform.SetParent(vehicleGO.transform, false);

                // Tint fallback cube by block type for easy visual debug
                var mr = blockGO.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.material.color = GetDebugColor(entry.blockTypeID);
            }

            blockGO.name = $"Block_{entry.blockTypeID}_{entry.localPosition}";
            blockGO.transform.localPosition = entry.localPosition;
            blockGO.transform.localRotation = Quaternion.identity;
            blockGO.transform.localScale    = Vector3.one;

            // Ensure each block has a collider so the vehicle has collision shape.
            // Parent Rigidbody automatically aggregates all child colliders.
            if (blockGO.GetComponent<Collider>() == null)
                blockGO.AddComponent<BoxCollider>();

            // Child blocks must NOT have their own Rigidbody —
            // remove any that might have come from a prefab.
            Rigidbody childRb = blockGO.GetComponent<Rigidbody>();
            if (childRb != null) Destroy(childRb);
        }

        // ── c. Add Rigidbody to vehicle parent (single physics object) ────────
        Rigidbody rb      = vehicleGO.AddComponent<Rigidbody>();
        rb.mass           = Mathf.Max(0.1f, blueprint.totalMass);
        rb.linearDamping  = drag;
        rb.angularDamping = angularDrag;
        rb.useGravity     = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // ── d. Add VehicleController stub ─────────────────────────────────────
        vehicleGO.AddComponent<VehicleController>();

        // ── e. Remove original voxels from the world ──────────────────────────
        RemoveSourceBlocks(blueprint);

        Debug.Log($"[VehicleSpawner] Vehicle spawned with {blueprint.blocks.Count} blocks " +
                  $"at {blueprint.worldOrigin}.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the prefab assigned for <paramref name="typeID"/>, or null if none is set.
    /// </summary>
    private GameObject GetPrefabForType(int typeID)
    {
        if (blockPrefabs == null || typeID < 0 || typeID >= blockPrefabs.Length)
            return null;
        return blockPrefabs[typeID]; // may still be null if slot left empty
    }

    /// <summary>
    /// Erases the source voxels from VoxelWorld and unregisters them from
    /// PlacedBlockRegistry so the flood-fill system stays consistent.
    /// World position = blueprint.worldOrigin + entry.localPosition.
    /// </summary>
    private void RemoveSourceBlocks(StructureBlueprint blueprint)
    {
        if (VoxelWorld.Instance == null)
        {
            Debug.LogWarning("[VehicleSpawner] VoxelWorld.Instance is null — source blocks not removed.");
            return;
        }

        foreach (BlockEntry entry in blueprint.blocks)
        {
            // Reconstruct the world grid position
            Vector3Int worldGrid = new Vector3Int(
                Mathf.RoundToInt(blueprint.worldOrigin.x) + entry.localPosition.x,
                Mathf.RoundToInt(blueprint.worldOrigin.y) + entry.localPosition.y,
                Mathf.RoundToInt(blueprint.worldOrigin.z) + entry.localPosition.z);

            // Pass voxel centre to ModifyBlock
            Vector3 voxelCentre = new Vector3(
                worldGrid.x + 0.5f,
                worldGrid.y + 0.5f,
                worldGrid.z + 0.5f);

            VoxelWorld.Instance.ModifyBlock(voxelCentre, 0); // 0 = air
            PlacedBlockRegistry.Instance?.Unregister(worldGrid);
        }
    }

    /// <summary>Returns a distinct debug color per block type for fallback cubes.</summary>
    private static Color GetDebugColor(int typeID) => typeID switch
    {
        1 => new Color(0.6f, 0.4f, 0.2f),   // wood — brown
        2 => new Color(0.8f, 0.6f, 0.3f),   // plank — light brown
        5 => new Color(0.6f, 0.6f, 0.7f),   // iron — grey-blue
        _ => new Color(0.7f, 0.7f, 0.7f),   // unknown — grey
    };
}
