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

    private void Start()
    {
        // Give the player the Control Block (ID 10) so they can actually build it!
        // We delay slightly to ensure Hotbar is initialized.
        Invoke(nameof(GiveControlBlock), 0.5f);
    }

    private void GiveControlBlock()
    {
        if (Hotbar.Instance != null)
        {
            Item controlBlockItem = ScriptableObject.CreateInstance<Item>();
            controlBlockItem.itemName = "Control Block";
            controlBlockItem.itemID = 10;
            controlBlockItem.blockTypeID = 10;
            controlBlockItem.icon = CreateColorIcon(Color.yellow);
            Hotbar.Instance.TryAddItem(controlBlockItem, 64);

            Item smallWheel = ScriptableObject.CreateInstance<Item>();
            smallWheel.itemName = "Small Wheel";
            smallWheel.itemID = 20;
            smallWheel.blockTypeID = 20;
            smallWheel.icon = CreateColorIcon(Color.black);
            Hotbar.Instance.TryAddItem(smallWheel, 64);

            Item largeWheel = ScriptableObject.CreateInstance<Item>();
            largeWheel.itemName = "Large Wheel";
            largeWheel.itemID = 21;
            largeWheel.blockTypeID = 21;
            largeWheel.icon = CreateColorIcon(Color.gray);
            Hotbar.Instance.TryAddItem(largeWheel, 64);

            Debug.Log("[VehicleSpawner] Gave player Control Blocks and Wheels!");
        }
    }

    private Sprite CreateColorIcon(Color c)
    {
        Texture2D tex = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];
        for (int i = 0; i < colors.Length; i++) colors[i] = c;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SpawnVehicle(StructureBlueprint blueprint)
    {
        if (blueprint == null)
        {
            Debug.LogWarning("[VehicleSpawner] SpawnVehicle called with null blueprint.");
            return;
        }

        string vehicleName = $"Vehicle_{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        GameObject vehicleGO = new GameObject(vehicleName);
        try 
        {
            vehicleGO.tag = "Vehicle";
        }
        catch (UnityException)
        {
            Debug.LogWarning("[VehicleSpawner] The 'Vehicle' tag is not defined in your project! Please add it in Edit -> Project Settings -> Tags and Layers so the player can properly ride it.");
        }
        vehicleGO.transform.position = blueprint.worldOrigin;

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
                blockGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blockGO.transform.SetParent(vehicleGO.transform, false);

                var mr = blockGO.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.material.color = GetDebugColor(entry.blockTypeID);
            }

            blockGO.name = $"Block_{entry.blockTypeID}_{entry.localPosition}";
            blockGO.transform.localPosition = entry.localPosition;
            blockGO.transform.localRotation = Quaternion.identity;
            blockGO.transform.localScale    = Vector3.one;

            if (blockGO.GetComponent<Collider>() == null)
            {
                if (entry.blockTypeID == 20 || entry.blockTypeID == 21)
                {
                    SphereCollider sc = blockGO.AddComponent<SphereCollider>();
                    sc.radius = (entry.blockTypeID == 20) ? 0.5f : 1.0f;
                }
                else
                {
                    blockGO.AddComponent<BoxCollider>();
                }
            }

            if (entry.blockTypeID == 10)
            {
                blockGO.AddComponent<ControlBlock>();
            }
            else if (entry.blockTypeID == 20 || entry.blockTypeID == 21)
            {
                WheelBlock wb = blockGO.AddComponent<WheelBlock>();
                wb.wheelSize = (entry.blockTypeID == 20) ? WheelSize.Small : WheelSize.Large;
                wb.forceContribution = (entry.blockTypeID == 20) ? 1f : 2.5f;

                MeshFilter mf = blockGO.GetComponent<MeshFilter>();
                MeshRenderer mr = blockGO.GetComponent<MeshRenderer>();
                if (mf != null && mr != null)
                {
                    GameObject childVisual = new GameObject("WheelVisual");
                    childVisual.transform.SetParent(blockGO.transform, false);
                    childVisual.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    childVisual.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                    
                    Destroy(mf);
                    Destroy(mr);
                    
                    wb.wheelMesh = childVisual.transform;
                }
            }

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
