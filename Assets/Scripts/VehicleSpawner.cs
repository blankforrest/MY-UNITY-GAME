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



    private static Sprite _cachedControlBlockIcon;
    private static Sprite _cachedSmallWheelIcon;
    private static Sprite _cachedLargeWheelIcon;
    private static Sprite _cachedPropellerIcon;
    private static Sprite _cachedLargePropellerIcon;

    public static Sprite CreateLargePropellerIcon()
    {
        if (_cachedLargePropellerIcon != null) return _cachedLargePropellerIcon;

        const int SZ = 64;
        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] px = new Color[SZ * SZ];

        Color darkGray   = new Color(0.15f, 0.15f, 0.17f, 1f);
        Color brassColor = new Color(0.82f, 0.58f, 0.16f, 1f);
        Color lightBrass = new Color(0.92f, 0.72f, 0.25f, 1f);
        Color shadowColor= new Color(0.08f, 0.08f, 0.08f, 0.5f);

        float centerX = SZ / 2f;
        float centerY = SZ / 2f;

        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distSq = dx * dx + dy * dy;
                float dist = Mathf.Sqrt(distSq);

                Color c = Color.clear;

                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f; // 0 to 360
                bool inBlade = false;

                for (int i = 0; i < 3; i++)
                {
                    float targetAngle = i * 120f + 30f;
                    float diff = Mathf.DeltaAngle(angle, targetAngle);
                    if (Mathf.Abs(diff) < 18f && dist > 7f && dist < 31f)
                    {
                        inBlade = true;
                        break;
                    }
                }

                if (inBlade)
                {
                    float shade = Mathf.Clamp01(dist / 31f);
                    c = Color.Lerp(lightBrass, brassColor, shade);
                }
                else if (dist <= 8f)
                {
                    c = darkGray;
                }
                else if (dist > 8f && dist <= 11f && dy < -3f)
                {
                    c = shadowColor;
                }

                px[y * SZ + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        _cachedLargePropellerIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedLargePropellerIcon;
    }

    public static Sprite CreatePropellerIcon()
    {
        if (_cachedPropellerIcon != null) return _cachedPropellerIcon;

        const int SZ = 64;
        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] px = new Color[SZ * SZ];

        Color darkGray   = new Color(0.2f, 0.2f, 0.2f, 1f);
        Color brassColor = new Color(0.82f, 0.58f, 0.16f, 1f);
        Color lightBrass = new Color(0.92f, 0.72f, 0.25f, 1f);
        Color shadowColor= new Color(0.08f, 0.08f, 0.08f, 0.5f);

        float centerX = SZ / 2f;
        float centerY = SZ / 2f;

        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distSq = dx * dx + dy * dy;
                float dist = Mathf.Sqrt(distSq);

                Color c = Color.clear;

                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f; // 0 to 360
                bool inBlade = false;

                for (int i = 0; i < 3; i++)
                {
                    float targetAngle = i * 120f + 30f;
                    float diff = Mathf.DeltaAngle(angle, targetAngle);
                    if (Mathf.Abs(diff) < 14f && dist > 5f && dist < 26f)
                    {
                        inBlade = true;
                        break;
                    }
                }

                if (inBlade)
                {
                    float shade = Mathf.Clamp01(dist / 26f);
                    c = Color.Lerp(lightBrass, brassColor, shade);
                }
                else if (dist <= 6f)
                {
                    c = darkGray;
                }
                else if (dist > 6f && dist <= 8f && dy < -2f)
                {
                    c = shadowColor;
                }

                px[y * SZ + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        _cachedPropellerIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedPropellerIcon;
    }

    public static Sprite CreateControlBlockIcon()
    {
        if (_cachedControlBlockIcon != null) return _cachedControlBlockIcon;

        const int SZ = 64;
        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] px = new Color[SZ * SZ];

        Color baseYellow = new Color(0.95f, 0.82f, 0.10f, 1f);
        Color borderGray = new Color(0.35f, 0.35f, 0.35f, 1f);
        Color lightGray  = new Color(0.6f, 0.6f, 0.6f, 1f);
        Color darkGray   = new Color(0.2f, 0.2f, 0.2f, 1f);
        Color screenBlue = new Color(0.1f, 0.6f, 0.95f, 1f);
        Color coreWhite  = new Color(1f, 1f, 1f, 1f);

        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                Color c = Color.clear;
                
                // Border
                if (x < 4 || x >= SZ - 4 || y < 4 || y >= SZ - 4)
                {
                    c = borderGray;
                }
                else if (x < 6 || x >= SZ - 6 || y < 6 || y >= SZ - 6)
                {
                    c = darkGray;
                }
                // Central monitor / control screen
                else if (x >= 18 && x < SZ - 18 && y >= 22 && y < SZ - 22)
                {
                    bool grid = (x == 32 || y == 32);
                    c = grid ? coreWhite : screenBlue;
                }
                // Screen border
                else if (x >= 16 && x < SZ - 16 && y >= 20 && y < SZ - 20)
                {
                    c = lightGray;
                }
                // Warning stripes
                else
                {
                    bool stripe = ((x + y) / 6) % 2 == 0;
                    c = stripe ? baseYellow : new Color(0.15f, 0.15f, 0.15f, 1f);
                }

                px[y * SZ + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        _cachedControlBlockIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedControlBlockIcon;
    }

    public static Sprite CreateWheelIcon(bool isLarge)
    {
        if (isLarge && _cachedLargeWheelIcon != null) return _cachedLargeWheelIcon;
        if (!isLarge && _cachedSmallWheelIcon != null) return _cachedSmallWheelIcon;

        const int SZ = 64;
        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] px = new Color[SZ * SZ];

        Color darkTire   = new Color(0.15f, 0.15f, 0.15f, 1f);
        Color lightTire  = new Color(0.25f, 0.25f, 0.25f, 1f);
        Color metallicRim= new Color(0.7f, 0.7f, 0.72f, 1f);
        Color darkRim    = new Color(0.4f, 0.4f, 0.42f, 1f);
        Color highlight  = new Color(0.9f, 0.9f, 0.95f, 1f);
        Color shadowColor= new Color(0.08f, 0.08f, 0.08f, 0.5f);

        float centerX = SZ / 2f;
        float centerY = SZ / 2f;
        float tireRadius = isLarge ? 28f : 20f;
        float rimRadius  = isLarge ? 14f : 10f;
        float hubRadius  = isLarge ? 5f : 3.5f;

        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distSq = dx * dx + dy * dy;
                float dist = Mathf.Sqrt(distSq);

                Color c = Color.clear;

                if (dist > tireRadius && dist <= tireRadius + 3f && dy < -5f)
                {
                    c = shadowColor;
                }
                else if (dist <= tireRadius)
                {
                    if (dist > rimRadius)
                    {
                        bool isTread = false;
                        if (isLarge)
                        {
                            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                            isTread = Mathf.Abs(angle % 30f) < 4f && dist > tireRadius - 4f;
                        }

                        if (isTread)
                        {
                            c = new Color(0.08f, 0.08f, 0.08f, 1f);
                        }
                        else
                        {
                            float shade = Mathf.Clamp01((dx - dy) / (tireRadius * 1.5f));
                            c = Color.Lerp(lightTire, darkTire, shade);
                        }
                    }
                    else if (dist > hubRadius)
                    {
                        float shade = Mathf.Clamp01((dx - dy) / (rimRadius * 1.5f));
                        c = Color.Lerp(highlight, darkRim, shade);

                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        int spokes = isLarge ? 6 : 4;
                        float angleStep = 360f / spokes;
                        bool onSpoke = Mathf.Abs((angle + 180f) % angleStep) < (isLarge ? 8f : 12f);
                        if (onSpoke && dist > hubRadius + 2f)
                        {
                            // Spoke metal color
                        }
                        else if (dist > hubRadius + 1f)
                        {
                            c = new Color(0.2f, 0.2f, 0.22f, 1f);
                        }
                    }
                    else
                    {
                        c = darkTire;
                    }
                }

                px[y * SZ + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        Sprite result = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        if (isLarge) _cachedLargeWheelIcon = result;
        else _cachedSmallWheelIcon = result;
        return result;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SpawnVehicle(StructureBlueprint blueprint)
    {
        if (blueprint == null)
        {
            Debug.LogWarning("[VehicleSpawner] SpawnVehicle called with null blueprint.");
            return;
        }

        if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
        {
            PlayerController player = VoxelWorld.Instance.playerTransform.GetComponent<PlayerController>();
            player?.SetFrozen(true);
        }

        // 1. Remove original voxels first so the new vehicle colliders do not overlap with them!
        RemoveSourceBlocks(blueprint);

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
        vehicleGO.transform.position = blueprint.worldOrigin + new Vector3(0.5f, 0.5f, 0.5f);

        foreach (BlockEntry entry in blueprint.blocks)
        {
            // Large wheel and Large propeller helper blocks are voxel-world placeholders only — nothing to spawn on the vehicle.
            if (entry.blockTypeID == 23 || entry.blockTypeID == 27) continue;

            bool isWheel = (entry.blockTypeID == 20 || entry.blockTypeID == 21);
            bool isPropeller = (entry.blockTypeID == 22 || entry.blockTypeID == 26);

            GameObject blockGO;
            if (isWheel || isPropeller)
            {
                // Plain empty object for wheels/propellers — no MeshFilter/MeshRenderer to conflict with
                blockGO = new GameObject($"SpecialBlock_{entry.blockTypeID}");
                blockGO.transform.SetParent(vehicleGO.transform, false);
            }
            else
            {
                GameObject prefab = GetPrefabForType(entry.blockTypeID);
                if (prefab != null)
                {
                    blockGO = Instantiate(prefab, vehicleGO.transform);
                }
                else
                {
                    blockGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blockGO.transform.SetParent(vehicleGO.transform, false);
                    var mr = blockGO.GetComponent<MeshRenderer>();
                    if (mr != null) mr.material.color = GetDebugColor(entry.blockTypeID);
                }

                // Apply correct procedural texture and face UVs for blocks (IDs 1–12, 30–37, 50)
                if ((entry.blockTypeID >= 1 && entry.blockTypeID <= 12) || 
                    (entry.blockTypeID >= 30 && entry.blockTypeID <= 37) || 
                    entry.blockTypeID == 50)
                {
                    ApplyVoxelTexture(blockGO, (byte)entry.blockTypeID);
                }
            }

            blockGO.name = $"Block_{entry.blockTypeID}_{entry.localPosition}";
            blockGO.transform.localRotation = Quaternion.identity;
            blockGO.transform.localScale    = Vector3.one;

            // Large wheels are 2x2 voxel blocks — the anchor (ID 21) is at the bottom-left corner.
            // We detect which direction the 2x2 extends (right or forward) by checking adjacent
            // helper blocks (ID 23), then offset the blockGO to the centre of the 2x2 so the
            // sphere collider and suspension raycasts are perfectly centred.
            if (entry.blockTypeID == 21)
            {
                Vector3 sideOffset = Vector3.right; // default: extends along +X
                bool hasSideZ = blueprint.blocks.Exists(b =>
                    b.blockTypeID == 23 &&
                    b.localPosition == entry.localPosition + new Vector3Int(0, 0, 1));
                if (hasSideZ) sideOffset = Vector3.forward;

                // Centre = anchor corner + half a unit up and half a unit to the side
                blockGO.transform.localPosition = (Vector3)entry.localPosition + (Vector3.up + sideOffset) * 0.5f;
            }
            else
            {
                blockGO.transform.localPosition = entry.localPosition;
            }

            // Replace existing collider on wheel blocks
            if (isWheel)
            {
                // Remove any collider the primitive added (BoxCollider from Cube)
                Collider existing = blockGO.GetComponent<Collider>();
                if (existing != null) Destroy(existing);

                SphereCollider sc = blockGO.AddComponent<SphereCollider>();
                sc.radius = (entry.blockTypeID == 20) ? 0.55f : 1.1f;
            }
            else if (isPropeller)
            {
                blockGO.AddComponent<BoxCollider>();
            }
            else if (blockGO.GetComponent<Collider>() == null)
            {
                blockGO.AddComponent<BoxCollider>();
            }

            if (entry.blockTypeID == 50) // Control Block
            {
                blockGO.AddComponent<ControlBlock>();
            }
            else if (isWheel)
            {
                bool isLarge  = entry.blockTypeID == 21;
                float radius  = isLarge ? 1.1f : 0.55f;
                float wWidth  = 1.0f;

                // Build procedural 3D wheel on a child object
                GameObject wheelVisual = new GameObject("WheelVisual");
                wheelVisual.transform.SetParent(blockGO.transform, false);
                WheelMeshBuilder.Apply(wheelVisual, radius, wWidth);

                WheelBlock wb       = blockGO.AddComponent<WheelBlock>();
                wb.wheelSize        = isLarge ? WheelSize.Large : WheelSize.Small;
                wb.forceContribution= isLarge ? 2.5f : 1f;
                wb.wheelMesh        = wheelVisual.transform;
            }
            else if (isPropeller)
            {
                // Build procedural 3D propeller on a child object
                GameObject propVisual = new GameObject("PropellerVisual");
                propVisual.transform.SetParent(blockGO.transform, false);
                propVisual.transform.localRotation = Quaternion.identity; // Align with blockGO
                
                if (entry.blockTypeID == 26)
                {
                    propVisual.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                    PropellerMeshBuilder.Apply(propVisual, 1.5f, 2.0f);
                }
                else
                {
                    propVisual.transform.localPosition = Vector3.zero;
                    PropellerMeshBuilder.Apply(propVisual, 0.6f, 0.6f);
                }

                // Auto-orient blockGO based on neighbors in blueprint
                Quaternion blockRotation = Quaternion.identity;
                
                // Exclude helper blocks (ID 23 and 27) from neighbor checks so we don't orient against them
                bool hasFrontNeighbor = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.forward);
                bool hasBackNeighbor  = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.back);
                bool hasLeftNeighbor  = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.left);
                bool hasRightNeighbor = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.right);
                bool hasBottomNeighbor = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.down);
                bool hasTopNeighbor    = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.up);
                
                if (hasFrontNeighbor && !hasBackNeighbor)
                {
                    blockRotation = Quaternion.Euler(0f, 180f, 0f); // Point backward (stern)
                }
                else if (hasBackNeighbor && !hasFrontNeighbor)
                {
                    blockRotation = Quaternion.identity; // Point forward (bow)
                }
                else if (hasRightNeighbor && !hasLeftNeighbor)
                {
                    blockRotation = Quaternion.Euler(0f, -90f, 0f); // Point left (port)
                }
                else if (hasLeftNeighbor && !hasRightNeighbor)
                {
                    blockRotation = Quaternion.Euler(0f, 90f, 0f); // Point right (starboard)
                }
                else if (hasBottomNeighbor && !hasTopNeighbor)
                {
                    blockRotation = Quaternion.Euler(-90f, 0f, 0f); // Point UP (helicopter main rotor)
                }
                else if (hasTopNeighbor && !hasBottomNeighbor)
                {
                    blockRotation = Quaternion.Euler(90f, 0f, 0f); // Point DOWN
                }
                else
                {
                    if (hasBottomNeighbor)
                        blockRotation = Quaternion.Euler(-90f, 0f, 0f); // Default to UP if sitting on something
                    else
                        blockRotation = Quaternion.Euler(0f, 180f, 0f); // Default to face backward
                }

                blockGO.transform.localRotation = blockRotation;

                PropellerBlock pb = blockGO.AddComponent<PropellerBlock>();
                pb.propellerMesh  = propVisual.transform;

                if (entry.blockTypeID == 26)
                {
                    pb.thrustForce = 22000f;
                    pb.steeringTorque = 15000f;
                    pb.maxRotationSpeed = 600f;
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
        rb.constraints    = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.isKinematic    = true; // Start kinematic to prevent immediate physics explosion while colliders bake

        // ── d. Add VehicleController & HelicopterController if applicable ────
        vehicleGO.AddComponent<VehicleController>();
        bool hasLiftPropeller = false;
        foreach (var entry in blueprint.blocks)
        {
            if (entry.blockTypeID == 22 || entry.blockTypeID == 26)
            {
                // Check if it is a lift propeller based on neighbor connections
                bool hasBottomNeighbor = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.down);
                bool hasTopNeighbor    = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.up);
                bool hasFrontNeighbor  = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.forward);
                bool hasBackNeighbor   = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.back);
                bool hasLeftNeighbor   = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.left);
                bool hasRightNeighbor  = blueprint.blocks.Exists(b => b.blockTypeID != 23 && b.blockTypeID != 27 && b.localPosition == entry.localPosition + Vector3Int.right);

                // Determine if it will be oriented vertically (UP or DOWN)
                bool pointsVertical = false;
                if (hasFrontNeighbor && !hasBackNeighbor) pointsVertical = false;
                else if (hasBackNeighbor && !hasFrontNeighbor) pointsVertical = false;
                else if (hasRightNeighbor && !hasLeftNeighbor) pointsVertical = false;
                else if (hasLeftNeighbor && !hasRightNeighbor) pointsVertical = false;
                else if (hasBottomNeighbor || hasTopNeighbor) pointsVertical = true;

                if (pointsVertical)
                {
                    hasLiftPropeller = true;
                    break;
                }
            }
        }

        if (hasLiftPropeller)
        {
            vehicleGO.AddComponent<HelicopterController>();
        }

        // Warn if no Control Block present (E key won't work without one)
        bool hasControlBlock = blueprint.blocks.Exists(b => b.blockTypeID == 50);
        if (!hasControlBlock)
            Debug.LogWarning("[VehicleSpawner] No Control Block (ID 50) found! " +
                             "Include the yellow Control Block in your structure so you can press E to drive.");

        // Set layer recursively so all colliders are on the Vehicle layer
        int vehicleLayerIndex = LayerMask.NameToLayer("Vehicle");
        if (vehicleLayerIndex != -1)
        {
            SetLayerRecursive(vehicleGO, vehicleLayerIndex);
        }

        Debug.Log($"[VehicleSpawner] Vehicle spawned with {blueprint.blocks.Count} blocks " +
                  $"at {blueprint.worldOrigin}. HasControlBlock={hasControlBlock}");
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }

    private void ApplyVoxelTexture(GameObject go, byte blockTypeID)
    {
        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        MeshRenderer mr = go.GetComponentInChildren<MeshRenderer>();

        if (mf != null && mr != null && VoxelWorld.Instance != null)
        {
            mf.sharedMesh = CreateVoxelMesh(blockTypeID);
            if (blockTypeID == 35)
            {
                mr.sharedMaterial = VoxelWorld.Instance.glassMaterial;
            }
            else if (blockTypeID == 9 || blockTypeID == 10 || blockTypeID == 11 || blockTypeID == 12)
            {
                mr.sharedMaterial = VoxelWorld.Instance.foliageMaterial;
            }
            else
            {
                mr.sharedMaterial = VoxelWorld.Instance.chunkMaterial;
            }
        }
    }

    private static Mesh CreateVoxelMesh(byte blockType)
    {
        Mesh mesh = new Mesh();
        mesh.name = "VoxelCube_" + blockType;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int vertexIndex = 0;

        // 6 faces
        for (int p = 0; p < 6; p++)
        {
            // Vertices for this face
            for (int i = 0; i < 4; i++)
            {
                Vector3 vert = VoxelData.voxelVerts[VoxelData.voxelTris[p, i]];
                vert -= new Vector3(0.5f, 0.5f, 0.5f); // Center pivot
                vertices.Add(vert);
            }

            // UVs for this face based on blockType
            Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, blockType);
            uvs.AddRange(faceUVs);

            // Triangles
            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
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

            VoxelWorld.Instance.ModifyBlock(voxelCentre, 0, suppressDrop: true); // erase silently — no item drops during conversion
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
