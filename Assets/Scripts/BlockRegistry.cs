using UnityEngine;
using System.Collections.Generic;

public static class BlockRegistry
{
    private static List<BlockDefinition> _registeredBlocks = new List<BlockDefinition>();
    public static List<BlockDefinition> RegisteredBlocks
    {
        get
        {
            CheckAndEnsureInitialized();
            return _registeredBlocks;
        }
    }
    private static Dictionary<byte, BlockDefinition> byID = new Dictionary<byte, BlockDefinition>();
    private static Dictionary<string, BlockDefinition> byName = new Dictionary<string, BlockDefinition>();

    public static int TotalTilesCount { get; set; } = 41;

    // Mapping of custom block ID to the tile indices in the atlas:
    // Key: Block ID. Value: Face indices (0=back, 1=front, 2=top, 3=bottom, 4=left, 5=right)
    private static Dictionary<byte, int[]> faceTiles = new Dictionary<byte, int[]>();

    // Tracking active point lights for light-emitting blocks
    private static Dictionary<Vector3Int, GameObject> activeLights = new Dictionary<Vector3Int, GameObject>();

    // Native/Blittable representations for Jobs
    private static Unity.Collections.NativeArray<BlittableBlockDefinition> _nativeDefinitions;
    private static Unity.Collections.NativeArray<Vector3> _customMeshVertices;
    private static Unity.Collections.NativeArray<int> _customMeshIndices;
    private static Unity.Collections.NativeArray<Vector2> _customMeshUVs;
    private static Unity.Collections.NativeArray<Vector3> _voxelVerts;
    private static Unity.Collections.NativeArray<int> _voxelTris;
    private static Unity.Collections.NativeArray<Vector3> _faceChecks;

    public static Unity.Collections.NativeArray<BlittableBlockDefinition> NativeDefinitions => _nativeDefinitions;
    public static Unity.Collections.NativeArray<Vector3> CustomMeshVertices => _customMeshVertices;
    public static Unity.Collections.NativeArray<int> CustomMeshIndices => _customMeshIndices;
    public static Unity.Collections.NativeArray<Vector2> CustomMeshUVs => _customMeshUVs;
    public static Unity.Collections.NativeArray<Vector3> VoxelVerts => _voxelVerts;
    public static Unity.Collections.NativeArray<int> VoxelTris => _voxelTris;
    public static Unity.Collections.NativeArray<Vector3> FaceChecks => _faceChecks;

    public static int GetTileIndexNative(byte blockID, int face, bool isLit = false, int facing = -1)
    {
        if (_nativeDefinitions.IsCreated && blockID < _nativeDefinitions.Length)
        {
            var def = _nativeDefinitions[blockID];
            if (face == 2) return def.tileTop;
            if (face == 3) return def.tileBottom;

            bool isFront = (facing != -1) ? (face == facing) : (blockID == 37 ? face == 0 : face == 1);
            if (isFront)
            {
                return isLit ? def.tileFrontLit : def.tileFront;
            }
            return def.tileLeft;
        }
        return GetDefaultTileIndex(blockID, face, isLit, facing);
    }

    private static bool _isInitializing = false;
    private static void CheckAndEnsureInitialized()
    {
        if (_isInitializing) return;
        if (_registeredBlocks.Count == 0 && VoxelWorld.Instance != null && VoxelWorld.Instance.blockDatabase != null)
        {
            _isInitializing = true;
            try
            {
                Debug.Log("[BlockRegistry] Lost static state detected. Re-initializing mappings from blockDatabase...");
                Initialize(VoxelWorld.Instance.blockDatabase.blocks);
                RebuildRegistryMappings();
            }
            finally
            {
                _isInitializing = false;
            }
        }
    }

    public static void RebuildRegistryMappings()
    {
        if (VoxelWorld.Instance == null || VoxelWorld.Instance.blockDatabase == null) return;

        int baseTilesCount = 42; // GrassTextureGenerator.TILE_COUNT
        BlockRegistry.TotalTilesCount = baseTilesCount;

        List<Texture2D> texturesToAppend = new List<Texture2D>();
        Dictionary<Texture2D, int> textureTileIndices = new Dictionary<Texture2D, int>();

        int customCount = _registeredBlocks.Count;
        for (int i = 0; i < customCount; i++)
        {
            BlockDefinition def = _registeredBlocks[i];
            if (def == null) continue;

            if (def.textureTop != null && !textureTileIndices.ContainsKey(def.textureTop))
            {
                textureTileIndices[def.textureTop] = -1;
                texturesToAppend.Add(def.textureTop);
            }
            if (def.textureSide != null && !textureTileIndices.ContainsKey(def.textureSide))
            {
                textureTileIndices[def.textureSide] = -1;
                texturesToAppend.Add(def.textureSide);
            }
            if (def.textureBottom != null && !textureTileIndices.ContainsKey(def.textureBottom))
            {
                textureTileIndices[def.textureBottom] = -1;
                texturesToAppend.Add(def.textureBottom);
            }
            if (def.textureFront != null && !textureTileIndices.ContainsKey(def.textureFront))
            {
                textureTileIndices[def.textureFront] = -1;
                texturesToAppend.Add(def.textureFront);
            }
            if (def.textureFrontLit != null && !textureTileIndices.ContainsKey(def.textureFrontLit))
            {
                textureTileIndices[def.textureFrontLit] = -1;
                texturesToAppend.Add(def.textureFrontLit);
            }
        }

        // Assign tile indices
        for (int i = 0; i < texturesToAppend.Count; i++)
        {
            textureTileIndices[texturesToAppend[i]] = baseTilesCount + i;
        }

        BlockRegistry.TotalTilesCount = baseTilesCount + texturesToAppend.Count;

        // Register face tiles
        for (int i = 0; i < customCount; i++)
        {
            BlockDefinition def = _registeredBlocks[i];
            if (def == null) continue;

            def.resolvedTopTile = def.textureTop != null ? textureTileIndices[def.textureTop] : -1;
            def.resolvedSideTile = def.textureSide != null ? textureTileIndices[def.textureSide] : -1;
            def.resolvedBottomTile = def.textureBottom != null ? textureTileIndices[def.textureBottom] : -1;
            def.resolvedFrontTile = def.textureFront != null ? textureTileIndices[def.textureFront] : def.resolvedSideTile;
            def.resolvedFrontLitTile = def.textureFrontLit != null ? textureTileIndices[def.textureFrontLit] : def.resolvedFrontTile;

            bool hasCustom = (def.textureTop != null || def.textureSide != null || def.textureBottom != null || def.textureFront != null || def.textureFrontLit != null);
            if (hasCustom || def.blockID >= 60)
            {
                int topTile = def.resolvedTopTile != -1 ? def.resolvedTopTile : GetDefaultTileIndex(def.blockID, 2);
                int sideTile = def.resolvedSideTile != -1 ? def.resolvedSideTile : GetDefaultTileIndex(def.blockID, 1);
                int bottomTile = def.resolvedBottomTile != -1 ? def.resolvedBottomTile : GetDefaultTileIndex(def.blockID, 3);
                int frontTile = def.resolvedFrontTile != -1 ? def.resolvedFrontTile : sideTile;

                RegisterFaceTiles(def.blockID, topTile, sideTile, bottomTile);
            }
        }
        
        Debug.Log($"[BlockRegistry] Rebuilt face tile mappings. TotalTilesCount: {BlockRegistry.TotalTilesCount}");
        RebuildBlittableDefinitions();
    }

    public static void Initialize(List<BlockDefinition> customDefs)
    {
        _registeredBlocks.Clear();
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
                _registeredBlocks.Add(def);

                // Cache custom mesh arrays on the main thread
                if (def.customMesh != null)
                {
                    try
                    {
                        Debug.Log($"[BlockRegistry] Caching custom mesh for block {def.blockName} (ID: {def.blockID}) - vertices: {def.customMesh.vertexCount}");
                        Vector3[] sourceVerts = def.customMesh.vertices;
                        Vector3[] offsetVerts = new Vector3[sourceVerts.Length];
                        if (sourceVerts.Length > 0)
                        {
                            Vector3 min = sourceVerts[0];
                            Vector3 max = sourceVerts[0];
                            for (int v = 1; v < sourceVerts.Length; v++)
                            {
                                min = Vector3.Min(min, sourceVerts[v]);
                                max = Vector3.Max(max, sourceVerts[v]);
                            }

                            float scale = 1f;
                            if (max.x - min.x > 2f || max.z - min.z > 2f)
                            {
                                scale = 0.0625f;
                            }

                            float width = (max.x - min.x) * scale;
                            float length = (max.z - min.z) * scale;
                            float offsetX = (1f - width) / 2f;
                            float offsetZ = (1f - length) / 2f;

                            for (int v = 0; v < sourceVerts.Length; v++)
                            {
                                float px = (sourceVerts[v].x - min.x) * scale + offsetX;
                                float py = (sourceVerts[v].y - min.y) * scale;
                                float pz = (sourceVerts[v].z - min.z) * scale + offsetZ;
                                offsetVerts[v] = new Vector3(px, py, pz);
                            }
                        }
                        def.cachedMeshVertices = offsetVerts;
                        def.cachedMeshTriangles = def.customMesh.triangles;
                        def.cachedMeshUVs = def.customMesh.uv;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[BlockRegistry] Failed to cache custom mesh for block '{def.blockName}' (ID: {def.blockID}): {ex.Message}. Please make sure Read/Write is enabled on the model's import settings.");
                        def.cachedMeshVertices = null;
                        def.cachedMeshTriangles = null;
                        def.cachedMeshUVs = null;
                    }
                }
                else
                {
                    if (def.blockID == 38 || def.blockID == 46)
                    {
                        Debug.LogWarning($"[BlockRegistry] Block {def.blockName} (ID: {def.blockID}) has NULL customMesh!");
                    }
                }

                // Auto-configure the associated Item SO if present (DropsCustomItem) or generate one dynamically (DropsSelf)
                if (def.dropRule == DropRule.DropsSelf)
                {
                    Item selfItem = StarterItems.CreateItemInstance(def.blockName, def.blockID, Color.white);
                    def.dropItem = selfItem;
                }
                else if (def.dropRule == DropRule.DropsCustomItem && def.dropItem != null)
                {
                    def.dropItem.blockTypeID = def.blockID;
                    if (def.inventoryIcon == null && (def.textureTop != null || def.textureSide != null || def.textureBottom != null))
                    {
                        def.inventoryIcon = StarterItems.MakeIsometricBlock(def.blockID, Color.white);
                    }
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

        RebuildBlittableDefinitions();
    }

    private static bool IsReservedID(byte id)
    {
        // Reserve IDs below 60 for hardcoded base game blocks
        return id < 60;
    }

    public static BlockDefinition GetDefinition(byte id)
    {
        CheckAndEnsureInitialized();
        if (byID.TryGetValue(id, out var def)) return def;
        return null;
    }

    public static BlockDefinition GetDefinition(string name)
    {
        CheckAndEnsureInitialized();
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

    public static int GetTileIndex(byte blockID, int face, bool isLit = false, int facing = -1)
    {
        CheckAndEnsureInitialized();
        BlockDefinition def = GetDefinition(blockID);
        if (def != null)
        {
            if (face == 2)
            {
                return def.resolvedTopTile != -1 ? def.resolvedTopTile : GetDefaultTileIndex(blockID, face, isLit, facing);
            }
            if (face == 3)
            {
                return def.resolvedBottomTile != -1 ? def.resolvedBottomTile : GetDefaultTileIndex(blockID, face, isLit, facing);
            }

            // Determine if this face is the front face of the block
            bool isFront = (facing != -1) ? (face == facing) : (blockID == 37 ? face == 0 : face == 1);
            if (isFront)
            {
                if (isLit)
                    return def.resolvedFrontLitTile != -1 ? def.resolvedFrontLitTile : GetDefaultTileIndex(blockID, face, isLit, facing);
                return def.resolvedFrontTile != -1 ? def.resolvedFrontTile : GetDefaultTileIndex(blockID, face, isLit, facing);
            }

            return def.resolvedSideTile != -1 ? def.resolvedSideTile : GetDefaultTileIndex(blockID, face, isLit, facing);
        }
        return GetDefaultTileIndex(blockID, face, isLit, facing);
    }

    public static int GetDefaultTileIndex(byte blockID, int face, bool isLit = false, int facing = -1)
    {
        // Maps block face to default hardcoded atlas tile index
        if (blockID == 1)      // Wood
            return (face == 2 || face == 3) ? 4 : 5;
        if (blockID == 2)      // Plank
            return 6;
        if (blockID == 3)      // Stone
            return 3;
        if (blockID == 5)      // Dirt
            return 2;
        if (blockID == 7)      // Water
            return 7;
        if (blockID == 8 || blockID == 34) // Sand
            return 8;
        if (blockID == 9)      // Flower
            return 9;
        if (blockID == 10)     // Dandelion
            return 10;
        if (blockID == 11)     // Iris
            return 11;
        if (blockID == 12)     // Leaves
            return 12;
        if (blockID == 13)     // Short Grass
            return 27;
        if (blockID == 14)     // Tall Grass
            return 28;
        if (blockID == 20)     // Small Wheel
            return (face == 4 || face == 5) ? 16 : 15;
        if (blockID == 21 || blockID == 23) // Large Wheel
            return (face == 4 || face == 5) ? 17 : 15;
        if (blockID == 22 || blockID == 26) // Propeller
            return (face == 0 || face == 1) ? 29 : 30;
        if (blockID == 24)     // Propeller Casing
            return 30;
        if (blockID == 25)     // Propeller Blade
            return 31;
        if (blockID == 50)     // Control Block
            return (face == 1) ? 14 : 13;
        if (blockID == 30)     // Coal Ore
            return 18;
        if (blockID == 31)     // Iron Ore
            return 19;
        if (blockID == 32)     // Gold Block
            return 20;
        if (blockID == 33)     // Iron Block
            return 21;
        if (blockID == 35)     // Glass
            return 22;
        if (blockID == 36)     // Crafting Table
            return (face == 2) ? 23 : (face == 3) ? 6 : 24;
        if (blockID == 37)     // Furnace
        {
            if (face == 2 || face == 3) return 3; // Top / Bottom (Stone)
            bool isFront = (facing != -1) ? (face == facing) : (face == 0); // default South (0)
            if (isFront) return isLit ? 26 : 25;
            return 3; // other sides are stone (3)
        }
        if (blockID == 38 || blockID == 40 || blockID == 41 || blockID == 42) // Wooden Stairs
            return 6;
        if (blockID == 39 || blockID == 43 || blockID == 44 || blockID == 45) // Stone Stairs
            return 3;
        if (blockID == 46)     // Wooden Slab
            return 6;
        if (blockID == 47)     // Stone Slab
            return 3;
        if (blockID == 48)     // Bedrock
            return 32;
        if (blockID == 49)     // Cactus
            return 33;
        if (blockID == 51)     // Birch Log
            return (face == 2 || face == 3) ? 35 : 34;
        if (blockID == 52)     // Birch Leaves
            return 36;
        if (blockID == 53)     // Spruce Log
            return (face == 2 || face == 3) ? 38 : 37;
        if (blockID == 54)     // Spruce Leaves
            return 39;
        if (blockID == 55)     // Diamond Ore
            return 40;
        if (blockID == 57)     // Gold Ore
            return 41;
        if (blockID == 56)     // Gravel
            return 3;
        
        // Grass (ID 4 or 6)
        return (face == 2) ? 0 : (face == 3) ? 2 : 1;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        DisposeNativeContainers();
        _registeredBlocks.Clear();
        byID.Clear();
        byName.Clear();
        faceTiles.Clear();
        activeLights.Clear();
    }

    public static void DisposeNativeContainers()
    {
        if (_nativeDefinitions.IsCreated) _nativeDefinitions.Dispose();
        if (_customMeshVertices.IsCreated) _customMeshVertices.Dispose();
        if (_customMeshIndices.IsCreated) _customMeshIndices.Dispose();
        if (_customMeshUVs.IsCreated) _customMeshUVs.Dispose();
        if (_voxelVerts.IsCreated) _voxelVerts.Dispose();
        if (_voxelTris.IsCreated) _voxelTris.Dispose();
        if (_faceChecks.IsCreated) _faceChecks.Dispose();
    }

    public static void RebuildBlittableDefinitions()
    {
        DisposeNativeContainers();

        _nativeDefinitions = new Unity.Collections.NativeArray<BlittableBlockDefinition>(256, Unity.Collections.Allocator.Persistent);

        // Initialize voxelVerts, voxelTris, faceChecks native arrays
        _voxelVerts = new Unity.Collections.NativeArray<Vector3>(8, Unity.Collections.Allocator.Persistent);
        for (int i = 0; i < 8; i++) _voxelVerts[i] = VoxelData.voxelVerts[i];

        _voxelTris = new Unity.Collections.NativeArray<int>(24, Unity.Collections.Allocator.Persistent);
        for (int f = 0; f < 6; f++)
        {
            for (int v = 0; v < 4; v++)
            {
                _voxelTris[f * 4 + v] = VoxelData.voxelTris[f, v];
            }
        }

        _faceChecks = new Unity.Collections.NativeArray<Vector3>(6, Unity.Collections.Allocator.Persistent);
        for (int i = 0; i < 6; i++) _faceChecks[i] = VoxelData.faceChecks[i];

        // Initialize with default air block definitions
        for (int i = 0; i < 256; i++)
        {
            _nativeDefinitions[i] = new BlittableBlockDefinition
            {
                blockID = (byte)i,
                isSolid = false,
                isTransparent = true,
                emitsLight = false,
                lightLevel = 0,
                isVehicleBlock = false,
                tileBack = -1,
                tileFront = -1,
                tileFrontLit = -1,
                tileTop = -1,
                tileBottom = -1,
                tileLeft = -1,
                tileRight = -1,
                hasCustomMesh = false
            };
        }

        // Count total vertices and indices for custom meshes
        int totalVertices = 0;
        int totalIndices = 0;
        foreach (var def in _registeredBlocks)
        {
            if (def != null && def.customMesh != null && def.cachedMeshVertices != null)
            {
                totalVertices += def.cachedMeshVertices.Length;
                totalIndices += def.cachedMeshTriangles.Length;
            }
        }

        _customMeshVertices = new Unity.Collections.NativeArray<Vector3>(totalVertices, Unity.Collections.Allocator.Persistent);
        _customMeshIndices = new Unity.Collections.NativeArray<int>(totalIndices, Unity.Collections.Allocator.Persistent);
        _customMeshUVs = new Unity.Collections.NativeArray<Vector2>(totalVertices, Unity.Collections.Allocator.Persistent);

        int currentVertexStart = 0;
        int currentIndexStart = 0;

        foreach (var def in _registeredBlocks)
        {
            if (def == null) continue;
            byte id = def.blockID;

            BlittableBlockDefinition blittable = new BlittableBlockDefinition
            {
                blockID = id,
                isSolid = def.isSolid,
                isTransparent = def.isTransparent,
                emitsLight = def.emitsLight,
                lightLevel = def.lightLevel,
                isVehicleBlock = def.isVehicleBlock,

                tileBack = GetTileIndex(id, 0, false, -1),
                tileFront = GetTileIndex(id, 1, false, -1),
                tileFrontLit = GetTileIndex(id, 1, true, -1),
                tileTop = GetTileIndex(id, 2, false, -1),
                tileBottom = GetTileIndex(id, 3, false, -1),
                tileLeft = GetTileIndex(id, 4, false, -1),
                tileRight = GetTileIndex(id, 5, false, -1),

                hasCustomMesh = false
            };

            if (def.customMesh != null && def.cachedMeshVertices != null)
            {
                blittable.hasCustomMesh = true;
                blittable.customMeshVertexStart = currentVertexStart;
                blittable.customMeshVertexCount = def.cachedMeshVertices.Length;
                blittable.customMeshIndexStart = currentIndexStart;
                blittable.customMeshIndexCount = def.cachedMeshTriangles.Length;

                for (int v = 0; v < def.cachedMeshVertices.Length; v++)
                {
                    _customMeshVertices[currentVertexStart + v] = def.cachedMeshVertices[v];
                    _customMeshUVs[currentVertexStart + v] = def.cachedMeshUVs[v];
                }

                for (int t = 0; t < def.cachedMeshTriangles.Length; t++)
                {
                    _customMeshIndices[currentIndexStart + t] = def.cachedMeshTriangles[t];
                }

                currentVertexStart += def.cachedMeshVertices.Length;
                currentIndexStart += def.cachedMeshTriangles.Length;
            }

            _nativeDefinitions[id] = blittable;
        }

        Debug.Log($"[BlockRegistry] Rebuilt native block definitions. Custom mesh vertices: {totalVertices}, indices: {totalIndices}");
    }
}
