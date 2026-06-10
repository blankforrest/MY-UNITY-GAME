using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    private byte[,,] voxelMap;
    public Vector2 chunkPos { get; private set; }

    int vertexIndex = 0;
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    // Water rendering lists
    private List<Vector3> waterVertices = new List<Vector3>();
    private List<int> waterTriangles = new List<int>();
    private List<Vector2> waterUvs = new List<Vector2>();
    private List<Color> waterColors = new List<Color>();
    private int waterVertexIndex = 0;

    private MeshFilter waterMeshFilter;
    private MeshRenderer waterMeshRenderer;

    // Foliage (flowers, etc.) rendering lists — separate child mesh, alpha-cutout
    private List<Vector3> foliageVertices  = new List<Vector3>();
    private List<int>     foliageTriangles = new List<int>();
    private List<Vector2> foliageUvs       = new List<Vector2>();
    private List<Color>   foliageColors    = new List<Color>();
    private int foliageVertexIndex = 0;

    private MeshFilter   foliageMeshFilter;
    private MeshRenderer foliageMeshRenderer;
    private MeshCollider foliageMeshCollider;

    public void Initialize(Vector2 pos, Material mat)
    {
        chunkPos = pos;
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = mat;

        PopulateVoxelMap();
        UpdateChunk();

        // Update neighboring chunks if they exist, so they can cull their boundary faces
        UpdateNeighbor(new Vector2(chunkPos.x + 1, chunkPos.y));
        UpdateNeighbor(new Vector2(chunkPos.x - 1, chunkPos.y));
        UpdateNeighbor(new Vector2(chunkPos.x, chunkPos.y + 1));
        UpdateNeighbor(new Vector2(chunkPos.x, chunkPos.y - 1));
    }

    void UpdateNeighbor(Vector2 pos)
    {
        if (VoxelWorld.Instance != null)
        {
            Chunk neighbor = VoxelWorld.Instance.GetChunkFromChunkPos(pos);
            if (neighbor != null)
            {
                neighbor.UpdateChunk();
            }
        }
    }

    private static void GetTreeNoise(float gX, float gZ, out float forestZone, out float clusterNoise, out float gapNoise)
    {
        forestZone   = Mathf.PerlinNoise(gX * 0.025f + 500f, gZ * 0.025f + 700f);
        clusterNoise = Mathf.PerlinNoise(gX * 0.10f + 900f,  gZ * 0.10f + 1100f);
        gapNoise     = Mathf.PerlinNoise(gX * 0.28f + 333f,  gZ * 0.28f + 444f);
    }

    private static bool WouldSpawnTree(float gX, float gZ, out float priority)
    {
        GetTreeNoise(gX, gZ, out float f, out float c, out float g);
        // High-frequency noise for local packing peaks
        priority = Mathf.PerlinNoise(gX * 0.75f + 123.4f, gZ * 0.75f + 567.8f);
        return (f >= 0.58f && c >= 0.52f && g <= 0.68f);
    }

    void PopulateVoxelMap()
    {
        voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    float globalX = x + chunkPos.x * VoxelData.ChunkWidth;
                    float globalZ = z + chunkPos.y * VoxelData.ChunkWidth;

                    // Base rolling noise (lower frequency = wider features)
                    float baseNoise = Mathf.PerlinNoise(globalX * 0.02f, globalZ * 0.02f);
                    // Exponentiate the noise to widen valleys (pushes mid-values lower)
                    baseNoise = Mathf.Pow(baseNoise, 2.5f);
                    
                    // Minor detail noise to prevent it from looking unnaturally smooth
                    float detailNoise = Mathf.PerlinNoise(globalX * 0.08f, globalZ * 0.08f) * 0.15f;

                    // Combine and lower the overall multiplier to make it less mountainy
                    float finalNoise = baseNoise + detailNoise;
                                       // Use float exact height to determine slabs
                    float exactHeight = finalNoise * VoxelData.ChunkHeight * 0.25f + (VoxelData.ChunkHeight / 5f);
 
                    // ── Macro Continent & Oceans ──
                    // continentNoise:
                    //   < 0.45f: Dry land (height boosted, no oceans)
                    //   > 0.55f: Ocean (height lowered, deep water)
                    //   0.45f - 0.55f: Transition/Coastline
                    float continentNoise = Mathf.PerlinNoise(globalX * 0.00015f + 1234.5f, globalZ * 0.00015f + 5678.9f);

                    // Smoothly blend the starting area around (0,0) towards dry land to guarantee solid ground on spawn
                    float startScale = Mathf.Clamp01(Mathf.Max(Mathf.Abs(globalX), Mathf.Abs(globalZ)) / 300f);
                    continentNoise = Mathf.Lerp(0.35f, continentNoise, startScale);

                    float heightOffset = 0f;
                    if (continentNoise < 0.45f)
                    {
                        // Dry land: boost height above sea level (up to 18 blocks)
                        float dryT = (0.45f - continentNoise) / 0.45f;
                        heightOffset = dryT * 18f;
                    }
                    else if (continentNoise > 0.55f)
                    {
                        // Ocean: pull height below sea level (down to 22 blocks)
                        float oceanT = (continentNoise - 0.55f) / 0.45f;
                        heightOffset = -oceanT * 22f;
                    }

                    exactHeight += heightOffset;
                    exactHeight = Mathf.Max(2f, exactHeight); // clamp to ensure we always have terrain at the bottom

                    // ── River Carving (Realistic Meandering & Dynamic Width) ──
                    float seaLevel = 14f;
                    
                    // Domain warp using noise to bend/wiggle the river coordinates
                    float warpX = Mathf.PerlinNoise(globalX * 0.015f + 10f, globalZ * 0.015f + 20f) * 60f;
                    float warpZ = Mathf.PerlinNoise(globalX * 0.015f + 30f, globalZ * 0.015f + 40f) * 60f;

                    // Lower frequency (0.002f) river noise creates long, continuous meandering rivers
                    float riverNoise = Mathf.PerlinNoise((globalX + warpX) * 0.002f + 400f, (globalZ + warpZ) * 0.002f + 800f);
                    float riverCenterDist = Mathf.Abs(riverNoise - 0.5f);
                    
                    // Dynamic width variation (scaled for the lower frequency)
                    float widthNoise = Mathf.PerlinNoise(globalX * 0.01f + 150f, globalZ * 0.01f + 250f);
                    float riverWidth = 0.01f + widthNoise * 0.008f; // clean, continuous channel width
                    
                    // Smoothly fade rivers in/out as they transition to desert/ocean
                    float riverStrength = 1f;
                    if (continentNoise < 0.45f)
                        riverStrength = Mathf.Clamp01((continentNoise - 0.35f) / 0.10f);
                    else if (continentNoise > 0.55f)
                        riverStrength = Mathf.Clamp01((0.65f - continentNoise) / 0.10f);

                    bool isRiver = (riverStrength > 0f) && (riverCenterDist < riverWidth);
                    bool isOcean = (continentNoise > 0.50f);

                    if (isRiver)
                    {
                        float riverFactor = Mathf.Clamp01(riverCenterDist / riverWidth);
                        // S-curve interpolation for flatter riverbed and sloped banks
                        riverFactor = Mathf.SmoothStep(0f, 1f, riverFactor);
                        
                        float riverDepth = 3.5f * riverStrength;
                        float riverbedHeight = seaLevel - riverDepth;
                        
                        float carvedHeight = Mathf.Lerp(riverbedHeight, exactHeight, riverFactor);
                        exactHeight = Mathf.Min(exactHeight, carvedHeight);
                    }
                    else if (!isOcean)
                    {
                        // Clean up any stray puddles by ensuring dry land is strictly above sea level
                        exactHeight = Mathf.Max(exactHeight, seaLevel + 1.5f);
                    }

                    int floorY = Mathf.FloorToInt(exactHeight);

                    // Determine dirt thickness dynamically using noise (between 3 and 5 blocks)
                    float dirtNoise = Mathf.PerlinNoise(globalX * 0.1f, globalZ * 0.1f);
                    int dirtThickness = 3 + Mathf.FloorToInt(dirtNoise * 3f); // 3, 4, or 5 layers of dirt

                    if (y > seaLevel && y > floorY)
                    {
                        voxelMap[x, y, z] = 0; // Air
                    }
                    else if (y <= seaLevel && y > floorY)
                    {
                        voxelMap[x, y, z] = 7; // Water
                    }
                    else if (y == floorY)
                    {
                        if (floorY <= seaLevel + 1)
                        {
                            voxelMap[x, y, z] = 8; // Sand beach/riverbed
                        }
                        else
                        {
                            voxelMap[x, y, z] = 4; // Grass
                        }
                    }
                    else if (y >= floorY - dirtThickness)
                    {
                        if (floorY <= seaLevel + 1)
                        {
                            voxelMap[x, y, z] = 8; // Sand deep beach
                        }
                        else
                        {
                            voxelMap[x, y, z] = 5; // Dirt
                        }
                    }
                    else
                    {
                        voxelMap[x, y, z] = 3; // Stone (deep)
                    }

                    // ── Flower spawning on full grass blocks ──────────────────────
                    // Only place flower one block above a *full* grass block (not slab)
                    // and only above sea level so flowers don't appear on the sea floor.
                    if (y == floorY + 1                       // cell directly above surface
                        && voxelMap[x, floorY, z] == 4        // surface is full grass (not slab/sand)
                        && floorY > seaLevel + 1)             // strictly above sea level
                    {
                        float flowerNoise = Mathf.PerlinNoise(
                            globalX * 0.18f + 77.3f,
                            globalZ * 0.18f + 53.1f);
                        // Second octave for clustering variety
                        float flowerNoise2 = Mathf.PerlinNoise(
                            globalX * 0.42f + 200f,
                            globalZ * 0.42f + 300f);
                        if (flowerNoise > 0.62f && flowerNoise2 > 0.45f)
                        {
                            float typeNoise = Mathf.PerlinNoise(globalX * 0.7f + 50f, globalZ * 0.7f + 90f);
                            if (typeNoise < 0.33f)
                                voxelMap[x, y, z] = 9; // Rose
                            else if (typeNoise < 0.66f)
                                voxelMap[x, y, z] = 10; // Dandelion
                            else
                                voxelMap[x, y, z] = 11; // Iris
                        }
                    }
                }
            }
        }

        // ── Tree spawning in grouped clusters ────────────────────────────────────
        // Layer 1 (forestZone, low freq):  picks large biome-scale forest blobs.
        // Layer 2 (clusterNoise, mid freq): high density inside the zone — forms groups.
        // Layer 3 (gapNoise, high freq):   punches natural small clearings between groups.
        // Spacing check (radius 3):        canopies stay separate, trunks 3+ blocks apart.
        for (int x = 1; x < VoxelData.ChunkWidth - 1; x++)
        {
            for (int z = 1; z < VoxelData.ChunkWidth - 1; z++)
            {
                float globalX = x + chunkPos.x * VoxelData.ChunkWidth;
                float globalZ = z + chunkPos.y * VoxelData.ChunkWidth;

                float myPriority;
                if (!WouldSpawnTree(globalX, globalZ, out myPriority)) continue;

                // Enforce boundary-proof spacing using local-maxima comparison on high-frequency priority.
                // Trunks must be at least 4 blocks apart so canopies (radius 2) don't touch.
                bool isLocalMax = true;
                for (int dx = -4; dx <= 4 && isLocalMax; dx++)
                {
                    for (int dz = -4; dz <= 4 && isLocalMax; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        if (dx * dx + dz * dz > 16) continue; // circular radius 4

                        float nGlobalX = globalX + dx;
                        float nGlobalZ = globalZ + dz;
                        float neighborPriority;
                        if (WouldSpawnTree(nGlobalX, nGlobalZ, out neighborPriority))
                        {
                            // Yield to neighbor with higher priority
                            if (neighborPriority > myPriority)
                            {
                                isLocalMax = false;
                            }
                            else if (Mathf.Approximately(neighborPriority, myPriority))
                            {
                                // Tie breaker
                                if (nGlobalX < globalX || (Mathf.Approximately(nGlobalX, globalX) && nGlobalZ < globalZ))
                                {
                                    isLocalMax = false;
                                }
                            }
                        }
                    }
                }
                if (!isLocalMax) continue;

                // Find the surface y for this (x,z) — walk down from top
                int surfaceY = -1;
                for (int sy = VoxelData.ChunkHeight - 1; sy > 0; sy--)
                {
                    byte b = voxelMap[x, sy, z];
                    if (b == 4) { surfaceY = sy; break; } // full grass block only
                }
                if (surfaceY < 0) continue; // no grass surface found
                if (surfaceY + 7 >= VoxelData.ChunkHeight) continue; // not enough vertical space

                // Randomised tree height between 4 and 6
                float heightNoise = Mathf.PerlinNoise(globalX * 0.9f + 888f, globalZ * 0.9f + 333f);
                int trunkHeight   = 4 + Mathf.FloorToInt(heightNoise * 3f); // 4, 5, or 6

                // ── Trunk ──
                for (int ty = 1; ty <= trunkHeight; ty++)
                {
                    int ty_abs = surfaceY + ty;
                    if (ty_abs >= VoxelData.ChunkHeight) break;
                    voxelMap[x, ty_abs, z] = 1; // Wood
                }

                // ── Canopy: a rough rounded blob of leaves ──
                int topY = surfaceY + trunkHeight;
                for (int lx = -2; lx <= 2; lx++)
                {
                    for (int lz = -2; lz <= 2; lz++)
                    {
                        for (int ly = -1; ly <= 2; ly++)
                        {
                            int cx = x + lx;
                            int cy = topY + ly;
                            int cz = z + lz;
                            if (cx < 0 || cx >= VoxelData.ChunkWidth ||
                                cz < 0 || cz >= VoxelData.ChunkWidth ||
                                cy < 0 || cy >= VoxelData.ChunkHeight) continue;

                            // Skip if already has trunk/wood
                            if (voxelMap[cx, cy, cz] == 1) continue;

                            // Round the canopy: exclude the far corner diagonals
                            int dist2 = lx * lx + lz * lz;
                            bool isTip = (ly == 2);
                            if (isTip && dist2 > 1) continue;    // top tier is just a cross
                            if (ly < -1 && dist2 > 1) continue;  // only directly under for low leaves
                            if (dist2 > 5) continue;             // trim outer corners

                            // Add slight natural noise to avoid a perfectly flat canopy
                            float leafNoise = Mathf.PerlinNoise((globalX + lx) * 0.4f + 22f, (globalZ + lz) * 0.4f + 55f);
                            if (dist2 == 5 && leafNoise < 0.45f) continue; // thin out outermost ring

                            voxelMap[cx, cy, cz] = 12; // Leaves
                        }
                    }
                }

                // Remove flower that got buried under trunk
                byte above = voxelMap[x, surfaceY + 1, z];
                if (above >= 9 && above <= 11)
                    voxelMap[x, surfaceY + 1, z] = 1; // replaced by trunk
            }
        }
    }

    public void EditVoxel(Vector3 localPosition, byte newID)
    {
        int x = Mathf.FloorToInt(localPosition.x);
        int y = Mathf.FloorToInt(localPosition.y);
        int z = Mathf.FloorToInt(localPosition.z);

        if (IsVoxelInChunk(x, y, z))
        {
            voxelMap[x, y, z] = newID;
            UpdateChunk();
        }
    }

    public byte GetVoxel(int x, int y, int z)
    {
        if (!IsVoxelInChunk(x, y, z))
            return 0;

        return voxelMap[x, y, z];
    }

    bool IsVoxelInChunk(int x, int y, int z)
    {
        if (x < 0 || x >= VoxelData.ChunkWidth || y < 0 || y >= VoxelData.ChunkHeight || z < 0 || z >= VoxelData.ChunkWidth)
            return false;
        else
            return true;
    }

    public void UpdateChunk()
    {
        ClearMeshData();

        for (int y = 0; y < VoxelData.ChunkHeight; y++)
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                    if (voxelMap[x, y, z] != 0)
                        UpdateVoxelMeshData(new Vector3(x, y, z), voxelMap[x, y, z]);

        CreateMesh();
    }

    void ClearMeshData()
    {
        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        waterVertexIndex = 0;
        waterVertices.Clear();
        waterTriangles.Clear();
        waterUvs.Clear();
        waterColors.Clear();

        foliageVertexIndex = 0;
        foliageVertices.Clear();
        foliageTriangles.Clear();
        foliageUvs.Clear();
        foliageColors.Clear();
    }

    void EnsureWaterChild()
    {
        Transform waterTrans = transform.Find("Water");
        GameObject waterGO;
        if (waterTrans == null)
        {
            waterGO = new GameObject("Water");
            waterGO.transform.SetParent(transform, false);
        }
        else
        {
            waterGO = waterTrans.gameObject;
        }

        waterMeshFilter = waterGO.GetComponent<MeshFilter>();
        if (waterMeshFilter == null) waterMeshFilter = waterGO.AddComponent<MeshFilter>();

        waterMeshRenderer = waterGO.GetComponent<MeshRenderer>();
        if (waterMeshRenderer == null) waterMeshRenderer = waterGO.AddComponent<MeshRenderer>();

        if (VoxelWorld.Instance != null && VoxelWorld.Instance.waterMaterial != null)
        {
            waterMeshRenderer.material = VoxelWorld.Instance.waterMaterial;
        }
        else
        {
            waterMeshRenderer.material = meshRenderer.material;
        }
    }

    void EnsureFoliageChild()
    {
        Transform t = transform.Find("Foliage");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("Foliage");
            go.transform.SetParent(transform, false);
        }
        else
        {
            go = t.gameObject;
        }

        foliageMeshFilter = go.GetComponent<MeshFilter>();
        if (foliageMeshFilter == null) foliageMeshFilter = go.AddComponent<MeshFilter>();

        foliageMeshRenderer = go.GetComponent<MeshRenderer>();
        if (foliageMeshRenderer == null) foliageMeshRenderer = go.AddComponent<MeshRenderer>();

        if (VoxelWorld.Instance != null && VoxelWorld.Instance.foliageMaterial != null)
            foliageMeshRenderer.material = VoxelWorld.Instance.foliageMaterial;
        else
            foliageMeshRenderer.material = meshRenderer.material;

        // Standard non-convex collider so raycasts hit flowers without trigger warnings
        foliageMeshCollider = go.GetComponent<MeshCollider>();
        if (foliageMeshCollider == null) foliageMeshCollider = go.AddComponent<MeshCollider>();
        foliageMeshCollider.convex     = false;
        foliageMeshCollider.isTrigger  = false;
    }

    /// <summary>Ignores physics collision between the foliage MeshCollider and all provided colliders.</summary>
    public void IgnoreFoliageCollisionWith(Collider[] others)
    {
        if (foliageMeshCollider == null) return;
        foreach (var col in others)
            if (col != null) Physics.IgnoreCollision(col, foliageMeshCollider, true);
    }

    public void IgnorePlayerCollision()
    {
        if (foliageMeshCollider == null) return;

        // Player CharacterController
        if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
        {
            var cc = VoxelWorld.Instance.playerTransform.GetComponent<CharacterController>();
            if (cc != null)
                Physics.IgnoreCollision(cc, foliageMeshCollider, true);
        }

        // Any active vehicle
        foreach (var vc in Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None))
            IgnoreFoliageCollisionWith(vc.GetComponentsInChildren<Collider>());
    }

    int GetWaterDepth(int x, int y, int z)
    {
        int depth = 0;
        for (int dy = y; dy >= 0; dy--)
        {
            byte block = voxelMap[x, dy, z];
            if (block == 7) // Water
            {
                depth++;
            }
            else if (block != 0) // Solid block
            {
                break;
            }
        }
        return Mathf.Max(1, depth);
    }

    void UpdateVoxelMeshData(Vector3 pos, byte blockType)
    {
        // ── Flower: render as two crossed quads (X-billboard) ──────────────────
        if (blockType == 9 || blockType == 10 || blockType == 11)
        {
            AddFlowerQuads(pos, blockType);
            return;
        }

        // ── Leaves: render as solid faces but on the foliage (alpha-cutout) mesh ─
        if (blockType == 12)
        {
            AddLeavesBlock(pos);
            return;
        }

        bool isWater = (blockType == 7);
        byte uvBlockType = blockType;

        // ── Dry interior: skip water mesh if this voxel sits inside any active vehicle hull ──
        if (isWater)
        {
            // Convert chunk-local voxel position to world space (centre of the voxel)
            Vector3 center = transform.position + pos + new Vector3(0.5f, 0.5f, 0.5f);
            // Check center and the 4 horizontal corners of the water block.
            // If any of these points are inside a vehicle's dry zone, suppress the voxel.
            if (VehicleController.IsWorldPosInsideVehicle(center) ||
                VehicleController.IsWorldPosInsideVehicle(center + new Vector3(-0.45f, 0f, -0.45f)) ||
                VehicleController.IsWorldPosInsideVehicle(center + new Vector3(0.45f, 0f, -0.45f)) ||
                VehicleController.IsWorldPosInsideVehicle(center + new Vector3(-0.45f, 0f, 0.45f)) ||
                VehicleController.IsWorldPosInsideVehicle(center + new Vector3(0.45f, 0f, 0.45f)))
            {
                return;
            }
        }

        int depth = 1;
        if (isWater)
        {
            depth = GetWaterDepth(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
        }

        for (int p = 0; p < 6; p++)
        {
            if (!CheckVoxelFace(pos, p, blockType))
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 vert = VoxelData.voxelVerts[VoxelData.voxelTris[p, i]];
                    
                    // Squash the top vertices down if this is water
                    if (isWater && vert.y > 0.5f)
                    {
                        vert.y = 0.85f; // Water is slightly lower
                    }
                    
                    if (isWater)
                    {
                        waterVertices.Add(pos + vert);
                        // Shallow water (depth 1) is transparent (low alpha); deeper water is opaque/reflective
                        float alpha;
                        if (depth == 1) alpha = 0.85f;
                        else if (depth == 2) alpha = 0.92f;
                        else if (depth == 3) alpha = 0.96f;
                        else alpha = 0.99f;

                        waterColors.Add(new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        vertices.Add(pos + vert);
                    }
                }

                // Per-face atlas UVs based on block type
                Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, uvBlockType);
                if (isWater)
                {
                    waterUvs.AddRange(faceUVs);

                    waterTriangles.Add(waterVertexIndex);
                    waterTriangles.Add(waterVertexIndex + 1);
                    waterTriangles.Add(waterVertexIndex + 2);
                    waterTriangles.Add(waterVertexIndex + 2);
                    waterTriangles.Add(waterVertexIndex + 1);
                    waterTriangles.Add(waterVertexIndex + 3);

                    waterVertexIndex += 4;
                }
                else
                {
                    uvs.AddRange(faceUVs);

                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 3);

                    vertexIndex += 4;
                }
            }
        }
    }

    /// <summary>
    /// Emits two crossed quads into the foliage mesh lists for a flower at <paramref name="pos"/>.
    /// Each quad spans the full block cell. Two quads at 90° give the classic SurvivalCraft X look.
    /// </summary>
    void AddFlowerQuads(Vector3 pos, byte blockType)
    {
        Vector2[] uvs9 = GrassTextureGenerator.GetBlockUVs(0, blockType); // flower tile UVs

        // Two diagonal crossed quads — SurvivalCraft X-billboard style
        Vector3[][] quads = new Vector3[2][];
        quads[0] = new Vector3[]
        {
            pos + new Vector3(0.05f, 0f, 0.05f),
            pos + new Vector3(0.05f, 1f, 0.05f),
            pos + new Vector3(0.95f, 0f, 0.95f),
            pos + new Vector3(0.95f, 1f, 0.95f),
        };
        quads[1] = new Vector3[]
        {
            pos + new Vector3(0.95f, 0f, 0.05f),
            pos + new Vector3(0.95f, 1f, 0.05f),
            pos + new Vector3(0.05f, 0f, 0.95f),
            pos + new Vector3(0.05f, 1f, 0.95f),
        };

        foreach (var quad in quads)
        {
            // Front winding
            foliageVertices.AddRange(quad);
            foliageUvs.AddRange(uvs9);
            for (int i = 0; i < 4; i++)
            {
                foliageColors.Add(Color.white);
            }
            foliageTriangles.Add(foliageVertexIndex);
            foliageTriangles.Add(foliageVertexIndex + 1);
            foliageTriangles.Add(foliageVertexIndex + 2);
            foliageTriangles.Add(foliageVertexIndex + 2);
            foliageTriangles.Add(foliageVertexIndex + 1);
            foliageTriangles.Add(foliageVertexIndex + 3);
            foliageVertexIndex += 4;
        }
    }

    /// <summary>
    /// Emits standard solid cube faces for a leaves block into the foliage
    /// (alpha-cutout) mesh so the chunky leaf texture renders with the gap pixels
    /// clipped out rather than opaque black squares.
    /// </summary>
    void AddLeavesBlock(Vector3 pos)
    {
        for (int p = 0; p < 6; p++)
        {
            // Cull against solid neighbours but not against other leaves / air
            Vector3 neighborPos = pos + VoxelData.faceChecks[p];
            int nx = Mathf.FloorToInt(neighborPos.x);
            int ny = Mathf.FloorToInt(neighborPos.y);
            int nz = Mathf.FloorToInt(neighborPos.z);

            byte neighbor = 0;
            if (!IsVoxelInChunk(nx, ny, nz))
            {
                if (VoxelWorld.Instance != null)
                    neighbor = VoxelWorld.Instance.GetBlock(neighborPos + transform.position);
            }
            else
            {
                neighbor = voxelMap[nx, ny, nz];
            }

            // Skip face if neighbour is another leaves block (cull) or any solid block
            if (neighbor == 12) continue;  // adjacent leaves cull each other
            // Skip if neighbour is fully opaque solid (not air/water/flowers/leaves)
            if (neighbor != 0 && neighbor != 7 && neighbor != 9 &&
                neighbor != 10 && neighbor != 11 && neighbor != 12) continue;

            Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, 12);

            for (int i = 0; i < 4; i++)
            {
                foliageVertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, i]]);
                foliageColors.Add(Color.white);
            }
            foliageUvs.AddRange(faceUVs);

            foliageTriangles.Add(foliageVertexIndex);
            foliageTriangles.Add(foliageVertexIndex + 1);
            foliageTriangles.Add(foliageVertexIndex + 2);
            foliageTriangles.Add(foliageVertexIndex + 2);
            foliageTriangles.Add(foliageVertexIndex + 1);
            foliageTriangles.Add(foliageVertexIndex + 3);
            foliageVertexIndex += 4;
        }
    }

    bool CheckVoxelFace(Vector3 pos, int faceIndex, byte currentBlockType)
    {
        Vector3 neighborPos = pos + VoxelData.faceChecks[faceIndex];
        int x = Mathf.FloorToInt(neighborPos.x);
        int y = Mathf.FloorToInt(neighborPos.y);
        int z = Mathf.FloorToInt(neighborPos.z);

        byte neighbor = 0;

        if (!IsVoxelInChunk(x, y, z))
        {
            if (VoxelWorld.Instance != null)
                neighbor = VoxelWorld.Instance.GetBlock(neighborPos + transform.position);
        }
        else
        {
            neighbor = voxelMap[x, y, z];
        }

        if (neighbor == 0) return false;

        bool currentIsWater  = (currentBlockType == 7);
        bool neighborIsWater = (neighbor == 7);
        // Flowers and leaves are transparent billboards — treat like air for culling purposes
        bool neighborIsFlower = (neighbor == 9 || neighbor == 10 || neighbor == 11 || neighbor == 12);

        if (currentIsWater)
        {
            // Water culls against solid blocks and other water
            return true;
        }
        else
        {
            // Solid blocks cull against solid blocks, but NOT water, air, or flowers
            if (neighborIsWater || neighborIsFlower) return false;

            // Any remaining solid neighbor culls our face
            return true;
        }
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices  = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv        = uvs.ToArray();
        mesh.RecalculateNormals();

        meshFilter.mesh        = mesh;
        meshCollider.sharedMesh = mesh;

        // ── Water mesh ────────────────────────────────────────────────────────
        if (waterVertices.Count > 0)
        {
            EnsureWaterChild();
            Mesh waterMesh = new Mesh();
            waterMesh.vertices  = waterVertices.ToArray();
            waterMesh.triangles = waterTriangles.ToArray();
            waterMesh.uv        = waterUvs.ToArray();
            waterMesh.colors    = waterColors.ToArray();
            waterMesh.RecalculateNormals();

            waterMeshFilter.mesh = waterMesh;
            waterMeshRenderer.gameObject.SetActive(true);
        }
        else
        {
            if (waterMeshRenderer != null)
                waterMeshRenderer.gameObject.SetActive(false);
        }

        // ── Foliage mesh (flowers) ─────────────────────────────────────────────
        if (foliageVertices.Count > 0)
        {
            EnsureFoliageChild();
            Mesh foliageMesh = new Mesh();
            foliageMesh.vertices  = foliageVertices.ToArray();
            foliageMesh.triangles = foliageTriangles.ToArray();
            foliageMesh.uv        = foliageUvs.ToArray();
            foliageMesh.colors    = foliageColors.ToArray();
            foliageMesh.RecalculateNormals();

            foliageMeshFilter.mesh   = foliageMesh;
            foliageMeshCollider.sharedMesh = foliageMesh; // allows raycast hits on flowers
            IgnorePlayerCollision(); // Ignore player physics collision so player walks through them
            foliageMeshRenderer.gameObject.SetActive(true);
        }
        else
        {
            if (foliageMeshRenderer != null)
                foliageMeshRenderer.gameObject.SetActive(false);
        }
    }
    /// <summary>
    /// Rebuilds ONLY the water (transparent) sub-mesh. Skips terrain and foliage geometry
    /// and does NOT rebake the MeshCollider — making it ~10x faster than UpdateChunk().
    /// Called by VehicleController when the boat moves to suppress interior water quickly.
    /// </summary>
    public void UpdateWaterMeshOnly()
    {
        // Clear only water lists
        waterVertexIndex = 0;
        waterVertices.Clear();
        waterTriangles.Clear();
        waterUvs.Clear();
        waterColors.Clear();

        // Iterate only water voxels (water only exists up to sea level which is 14; 40 is a safe maximum that saves massive CPU time)
        int scanHeight = Mathf.Min(VoxelData.ChunkHeight, 40);
        for (int y = 0; y < scanHeight; y++)
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                    if (voxelMap[x, y, z] == 7) // Water only
                        UpdateVoxelMeshData(new Vector3(x, y, z), 7);

        // Upload only the water mesh — no collider, no terrain touch
        EnsureWaterChild();
        if (waterVertices.Count > 0)
        {
            Mesh waterMesh = waterMeshFilter.mesh != null ? waterMeshFilter.mesh : new Mesh();
            waterMesh.Clear();
            waterMesh.vertices  = waterVertices.ToArray();
            waterMesh.triangles = waterTriangles.ToArray();
            waterMesh.uv        = waterUvs.ToArray();
            waterMesh.colors    = waterColors.ToArray();
            waterMesh.RecalculateNormals();
            waterMeshFilter.mesh = waterMesh;
            waterMeshRenderer.gameObject.SetActive(true);
        }
        else
        {
            if (waterMeshRenderer != null)
                waterMeshRenderer.gameObject.SetActive(false);
        }
    }
}
