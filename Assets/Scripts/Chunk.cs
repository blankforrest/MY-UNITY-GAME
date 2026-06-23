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

    // Glass rendering lists — separate child mesh, ZWrite On + CullBack to fix back-face bleed
    private List<Vector3> glassVertices  = new List<Vector3>();
    private List<int>     glassTriangles = new List<int>();
    private List<Vector2> glassUvs       = new List<Vector2>();
    private int glassVertexIndex = 0;

    private MeshFilter   glassMeshFilter;
    private MeshRenderer glassMeshRenderer;

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
        float ox = gX + SaveLoadManager.worldSeedOffsetX;
        float oz = gZ + SaveLoadManager.worldSeedOffsetZ;
        forestZone   = Mathf.PerlinNoise(ox * 0.025f + 500f, oz * 0.025f + 700f);
        clusterNoise = Mathf.PerlinNoise(ox * 0.10f + 900f,  oz * 0.10f + 1100f);
        gapNoise     = Mathf.PerlinNoise(ox * 0.28f + 333f,  oz * 0.28f + 444f);
    }

    public static int GetBiome(float gX, float gZ)
    {
        float ox = gX + SaveLoadManager.worldSeedOffsetX;
        float oz = gZ + SaveLoadManager.worldSeedOffsetZ;

        float continentNoise = Mathf.PerlinNoise(ox * 0.00015f + 1234.5f, oz * 0.00015f + 5678.9f);
        float startScale = Mathf.Clamp01(Mathf.Max(Mathf.Abs(gX), Mathf.Abs(gZ)) / 300f);
        continentNoise = Mathf.Lerp(0.35f, continentNoise, startScale);

        if (continentNoise > 0.55f)
            return 0; // Ocean

        // River check
        float warpX = Mathf.PerlinNoise(ox * 0.015f + 10f, oz * 0.015f + 20f) * 60f;
        float warpZ = Mathf.PerlinNoise(ox * 0.015f + 30f, oz * 0.015f + 40f) * 60f;
        float riverNoise = Mathf.PerlinNoise((ox + warpX) * 0.002f + 400f, (oz + warpZ) * 0.002f + 800f);
        float riverCenterDist = Mathf.Abs(riverNoise - 0.5f);
        float widthNoise = Mathf.PerlinNoise(ox * 0.01f + 150f, oz * 0.01f + 250f);
        float riverWidth = 0.01f + widthNoise * 0.008f;
        float riverStrength = 1f;
        if (continentNoise < 0.45f)
            riverStrength = Mathf.Clamp01((continentNoise - 0.35f) / 0.10f);
        else if (continentNoise > 0.55f)
            riverStrength = Mathf.Clamp01((0.65f - continentNoise) / 0.10f);
        bool isRiver = (riverStrength > 0f) && (riverCenterDist < riverWidth);

        if (isRiver)
            return 4; // River

        float biomeNoise = Mathf.PerlinNoise(ox * 0.003f + 4000f, oz * 0.003f + 8000f);
        if (biomeNoise < 0.25f)
            return 1; // Desert
        else if (biomeNoise < 0.50f)
            return 2; // Plains
        else
            return 3; // Forest
    }

    private static bool WouldSpawnTree(float gX, float gZ, out float priority)
    {
        float ox = gX + SaveLoadManager.worldSeedOffsetX;
        float oz = gZ + SaveLoadManager.worldSeedOffsetZ;
        
        int biome = GetBiome(gX, gZ);
        if (biome == 0 || biome == 1 || biome == 4) // No trees in Ocean, Desert, or River
        {
            priority = 0f;
            return false;
        }

        GetTreeNoise(gX, gZ, out float f, out float c, out float g);
        priority = Mathf.PerlinNoise(ox * 0.75f + 123.4f, oz * 0.75f + 567.8f);

        if (biome == 2) // Plains: extremely sparse trees
        {
            return (f >= 0.72f && c >= 0.72f && g <= 0.45f);
        }
        else // Forest: standard high density
        {
            return (f >= 0.50f && c >= 0.48f && g <= 0.75f);
        }
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

                    float ox = globalX + SaveLoadManager.worldSeedOffsetX;
                    float oz = globalZ + SaveLoadManager.worldSeedOffsetZ;

                    // Base rolling noise
                    float baseNoise = Mathf.PerlinNoise(ox * 0.02f, oz * 0.02f);
                    // Exponentiate the noise to widen valleys (pushes mid-values lower)
                    baseNoise = Mathf.Pow(baseNoise, 2.5f);
                    
                    // Minor detail noise to prevent it from looking unnaturally smooth
                    float detailNoise = Mathf.PerlinNoise(ox * 0.08f, oz * 0.08f) * 0.15f;

                    // Combine and lower the overall multiplier to make it less mountainy
                    float finalNoise = baseNoise + detailNoise;
                                       // Use float exact height to determine slabs
                    float exactHeight = finalNoise * VoxelData.ChunkHeight * 0.25f + (VoxelData.ChunkHeight / 5f);
 
                    // ── Macro Continent & Oceans ──
                    // continentNoise:
                    //   < 0.45f: Dry land (height boosted, no oceans)
                    //   > 0.55f: Ocean (height lowered, deep water)
                    //   0.45f - 0.55f: Transition/Coastline
                    float continentNoise = Mathf.PerlinNoise(ox * 0.00015f + 1234.5f, oz * 0.00015f + 5678.9f);

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

                    // Calculate heights for each biome individually
                    float oceanFloorNoise = Mathf.PerlinNoise(ox * 0.015f, oz * 0.015f);
                    float oceanHeight = (oceanFloorNoise * VoxelData.ChunkHeight * 0.08f) + (VoxelData.ChunkHeight * 0.08f) + heightOffset;

                    float duneNoise = Mathf.PerlinNoise(ox * 0.012f, oz * 0.012f);
                    duneNoise = Mathf.Pow(duneNoise, 2f);
                    float desertHeight = (duneNoise * VoxelData.ChunkHeight * 0.12f) + (VoxelData.ChunkHeight * 0.22f) + heightOffset;

                    float plainsNoise = Mathf.PerlinNoise(ox * 0.01f, oz * 0.01f);
                    float plainsHeight = (plainsNoise * VoxelData.ChunkHeight * 0.03f) + (VoxelData.ChunkHeight * 0.22f) + heightOffset;

                    float forestHeight = finalNoise * VoxelData.ChunkHeight * 0.20f + (VoxelData.ChunkHeight * 0.21f) + heightOffset;

                    // Determine discrete biome index for surface block assignment
                    float biomeNoise = Mathf.PerlinNoise(ox * 0.003f + 4000f, oz * 0.003f + 8000f);
                    int biome = 2; // Plains default
                    if (continentNoise > 0.55f) biome = 0; // Ocean
                    else if (biomeNoise < 0.25f) biome = 1; // Desert
                    else if (biomeNoise < 0.50f) biome = 2; // Plains
                    else biome = 3; // Forest

                    // Smoothly blend land biome heights based on biomeNoise
                    float landHeight = 0f;
                    if (biomeNoise < 0.20f)
                    {
                        landHeight = desertHeight;
                    }
                    else if (biomeNoise < 0.30f)
                    {
                        float t = (biomeNoise - 0.20f) / 0.10f;
                        t = t * t * (3f - 2f * t); // Smoothstep
                        landHeight = Mathf.Lerp(desertHeight, plainsHeight, t);
                    }
                    else if (biomeNoise < 0.45f)
                    {
                        landHeight = plainsHeight;
                    }
                    else if (biomeNoise < 0.55f)
                    {
                        float t = (biomeNoise - 0.45f) / 0.10f;
                        t = t * t * (3f - 2f * t); // Smoothstep
                        landHeight = Mathf.Lerp(plainsHeight, forestHeight, t);
                    }
                    else
                    {
                        landHeight = forestHeight;
                    }

                    // Smoothly blend land heights with ocean heights near coastlines
                    float oceanWeight = Mathf.Clamp01((continentNoise - 0.45f) / 0.10f);
                    oceanWeight = oceanWeight * oceanWeight * (3f - 2f * oceanWeight); // Smoothstep
                    exactHeight = Mathf.Lerp(landHeight, oceanHeight, oceanWeight);

                    exactHeight = Mathf.Max(2f, exactHeight); // clamp to ensure we always have terrain at the bottom

                    // ── River Carving (Realistic Meandering & Dynamic Width) ──
                    float seaLevel = Mathf.Floor(VoxelData.ChunkHeight * 0.22f);
                    
                    // Domain warp using noise to bend/wiggle the river coordinates
                    float warpX = Mathf.PerlinNoise(ox * 0.015f + 10f, oz * 0.015f + 20f) * 60f;
                    float warpZ = Mathf.PerlinNoise(ox * 0.015f + 30f, oz * 0.015f + 40f) * 60f;

                    // Lower frequency (0.002f) river noise creates long, continuous meandering rivers
                    float riverNoise = Mathf.PerlinNoise((ox + warpX) * 0.002f + 400f, (oz + warpZ) * 0.002f + 800f);
                    float riverCenterDist = Mathf.Abs(riverNoise - 0.5f);
                    
                    // Dynamic width variation (scaled for the lower frequency)
                    float widthNoise = Mathf.PerlinNoise(ox * 0.01f + 150f, oz * 0.01f + 250f);
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
                    float dirtNoise = Mathf.PerlinNoise(ox * 0.1f, oz * 0.1f);
                    int dirtThickness = 3 + Mathf.FloorToInt(dirtNoise * 3f); // 3, 4, or 5 layers of dirt

                    // Determine cactus height at this column
                    float cactusNoise = Mathf.PerlinNoise(ox * 0.3f + 700f, oz * 0.3f + 900f);
                    int cactusHeight = 0;
                    if (biome == 1 && floorY > seaLevel + 1 && !isRiver)
                    {
                        if (cactusNoise > 0.94f)
                        {
                            float hNoise = Mathf.PerlinNoise(ox * 0.9f, oz * 0.9f);
                            cactusHeight = 2 + Mathf.FloorToInt(hNoise * 2f); // 2 or 3 block tall cactus
                        }
                    }

                    if (y == 0)
                    {
                        voxelMap[x, y, z] = 48; // Bedrock (indestructible bottom floor)
                    }
                    else if (y > floorY)
                    {
                        if (y <= floorY + cactusHeight)
                        {
                            voxelMap[x, y, z] = 49; // Cactus block
                        }
                        else if (y > seaLevel)
                        {
                            voxelMap[x, y, z] = 0; // Air
                        }
                        else
                        {
                            voxelMap[x, y, z] = 7; // Water
                        }
                    }
                    else if (y == floorY)
                    {
                        if (biome == 1) // Desert
                        {
                            voxelMap[x, y, z] = 8; // Sand
                        }
                        else if (floorY <= seaLevel + 1)
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
                        if (biome == 1) // Desert
                        {
                            voxelMap[x, y, z] = 8; // Sand deep
                        }
                        else if (floorY <= seaLevel + 1)
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

                    // ── Flower & Grass spawning on full grass blocks ──────────────────────
                    // Only place flower or grass one block above a *full* grass block (not slab)
                    // and only above sea level so they don't appear on the sea floor.
                    if (y == floorY + 1                       // cell directly above surface
                        && voxelMap[x, floorY, z] == 4        // surface is full grass (not slab/sand)
                        && floorY > seaLevel + 1)             // strictly above sea level
                    {
                        float foliageNoise = Mathf.PerlinNoise(
                            ox * 0.15f + 120.3f,
                            oz * 0.15f + 340.1f);
                        float threshold = (biome == 2) ? 0.32f : 0.45f;
                        if (foliageNoise > threshold) // Grass/Flower patches
                        {
                            float subNoise = Mathf.PerlinNoise(ox * 0.6f + 85f, oz * 0.6f + 15f);
                            if (subNoise < 0.12f)
                            {
                                // Flowers (Rose, Dandelion, Iris)
                                float typeNoise = Mathf.PerlinNoise(ox * 0.7f + 50f, oz * 0.7f + 90f);
                                if (typeNoise < 0.33f)
                                    voxelMap[x, y, z] = 9; // Rose
                                else if (typeNoise < 0.66f)
                                    voxelMap[x, y, z] = 10; // Dandelion
                                else
                                    voxelMap[x, y, z] = 11; // Iris
                            }
                            else
                            {
                                // Grass (Short Grass ID 13 or Tall Grass ID 14)
                                float grassTypeNoise = Mathf.PerlinNoise(ox * 0.8f + 144f, oz * 0.8f + 788f);
                                if (grassTypeNoise < 0.7f)
                                    voxelMap[x, y, z] = 13; // Short Grass
                                else
                                    voxelMap[x, y, z] = 14; // Tall Grass
                            }
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

                // Randomised tree height
                float hox = globalX + SaveLoadManager.worldSeedOffsetX;
                float hoz = globalZ + SaveLoadManager.worldSeedOffsetZ;
                float heightNoise = Mathf.PerlinNoise(hox * 0.9f + 888f, hoz * 0.9f + 333f);

                int logID = 1;
                int leafID = 12;
                bool isSpruce = false;

                int currentBiome = GetBiome(globalX, globalZ);
                float treeTypeNoise = Mathf.PerlinNoise(hox * 0.1f + 111f, hoz * 0.1f + 222f);

                if (currentBiome == 3) // Forest
                {
                    if (treeTypeNoise < 0.4f)
                    {
                        logID = 53; // Spruce Log
                        leafID = 54; // Spruce Leaves
                        isSpruce = true;
                    }
                    else if (treeTypeNoise < 0.7f)
                    {
                        logID = 51; // Birch Log
                        leafID = 52; // Birch Leaves
                    }
                    else
                    {
                        logID = 1; // Oak Log
                        leafID = 12; // Oak Leaves
                    }
                }
                else // Plains
                {
                    if (treeTypeNoise < 0.3f)
                    {
                        logID = 51; // Birch
                        leafID = 52;
                    }
                    else
                    {
                        logID = 1; // Oak
                        leafID = 12;
                    }
                }

                int trunkHeight = isSpruce ? (5 + Mathf.FloorToInt(heightNoise * 4f)) : (4 + Mathf.FloorToInt(heightNoise * 3f));

                // ── Trunk ──
                for (int ty = 1; ty <= trunkHeight; ty++)
                {
                    int ty_abs = surfaceY + ty;
                    if (ty_abs >= VoxelData.ChunkHeight) break;
                    voxelMap[x, ty_abs, z] = (byte)logID;
                }

                if (isSpruce)
                {
                    // Spruce canopy layers (pine tree look)
                    int topY = surfaceY + trunkHeight;
                    for (int ly = 2; ly <= trunkHeight + 1; ly++)
                    {
                        int cy = surfaceY + ly;
                        if (cy < 0 || cy >= VoxelData.ChunkHeight) continue;

                        int distFromTop = (trunkHeight + 1) - ly;
                        int radius = 1;
                        if (distFromTop == 0) radius = 0;
                        else if (distFromTop <= 2) radius = 1;
                        else if (distFromTop <= 4) radius = 2;
                        else radius = (distFromTop % 2 == 0) ? 2 : 3;

                        for (int lx = -radius; lx <= radius; lx++)
                        {
                            for (int lz = -radius; lz <= radius; lz++)
                            {
                                int cx = x + lx;
                                int cz = z + lz;
                                if (cx < 0 || cx >= VoxelData.ChunkWidth ||
                                    cz < 0 || cz >= VoxelData.ChunkWidth) continue;

                                if (cx == x && cz == z && cy <= topY) continue;
                                if (radius > 1 && (lx * lx + lz * lz > radius * radius)) continue;

                                byte currentVal = voxelMap[cx, cy, cz];
                                if (currentVal == 0 || currentVal == 13 || currentVal == 14 || (currentVal >= 9 && currentVal <= 11))
                                {
                                    voxelMap[cx, cy, cz] = (byte)leafID;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // ── Oak/Birch Canopy ──
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

                                if (voxelMap[cx, cy, cz] == logID || voxelMap[cx, cy, cz] == 1 || voxelMap[cx, cy, cz] == 51 || voxelMap[cx, cy, cz] == 53) continue;

                                int dist2 = lx * lx + lz * lz;
                                bool isTip = (ly == 2);
                                if (isTip && dist2 > 1) continue;
                                if (ly < -1 && dist2 > 1) continue;
                                if (dist2 > 5) continue;

                                float leafNoise = Mathf.PerlinNoise((hox + lx) * 0.4f + 22f, (hoz + lz) * 0.4f + 55f);
                                if (dist2 == 5 && leafNoise < 0.45f) continue;

                                voxelMap[cx, cy, cz] = (byte)leafID;
                            }
                        }
                    }
                }


                // Remove flower that got buried under trunk
                byte above = voxelMap[x, surfaceY + 1, z];
                if ((above >= 9 && above <= 11) || above == 13 || above == 14)
                    voxelMap[x, surfaceY + 1, z] = (byte)logID;
            }
        }

        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.ApplyChunkModifications(chunkPos, voxelMap);
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

        glassVertexIndex = 0;
        glassVertices.Clear();
        glassTriangles.Clear();
        glassUvs.Clear();
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
        if (blockType == 9 || blockType == 10 || blockType == 11 || blockType == 13 || blockType == 14)
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

        // ── Glass (ID 35): render in the solid mesh with face-culling (has collider, player can't pass through) ─
        if (blockType == 35)
        {
            AddGlassToSolidMesh(pos);
            return;
        }

        // ── Propeller (ID 22) ──────────────────
        if (blockType == 22)
        {
            AddPropellerBlock(pos);
            return;
        }

        // ── Large Propeller (ID 26) ──────────────────
        if (blockType == 26)
        {
            AddLargePropellerBlock(pos);
            return;
        }

        // ── Large Propeller Helper (ID 27) ──────────────────
        if (blockType == 27)
        {
            return;
        }

        // ── Wooden Stairs (IDs 38, 40, 41, 42) & Stone Stairs (IDs 39, 43, 44, 45) ──────────────────
        if (blockType == 38 || blockType == 40 || blockType == 41 || blockType == 42 ||
            blockType == 39 || blockType == 43 || blockType == 44 || blockType == 45)
        {
            AddStairsBlock(pos, blockType);
            return;
        }

        // ── Wooden Slab (ID 46) & Stone Slab (ID 47) ──────────────────
        if (blockType == 46 || blockType == 47)
        {
            AddSlabBlock(pos, blockType);
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
                bool isFurnaceLit = false;
                if (uvBlockType == 37 && FurnaceManager.Instance != null)
                {
                    Vector3Int worldPos = new Vector3Int(
                        Mathf.FloorToInt(transform.position.x + pos.x),
                        Mathf.FloorToInt(transform.position.y + pos.y),
                        Mathf.FloorToInt(transform.position.z + pos.z)
                    );
                    isFurnaceLit = FurnaceManager.Instance.IsFurnaceBurning(worldPos);
                }
                Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, uvBlockType, isFurnaceLit);
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

            // Back winding for double-sided collision and rendering
            foliageTriangles.Add(foliageVertexIndex + 2);
            foliageTriangles.Add(foliageVertexIndex + 1);
            foliageTriangles.Add(foliageVertexIndex);
            foliageTriangles.Add(foliageVertexIndex + 3);
            foliageTriangles.Add(foliageVertexIndex + 1);
            foliageTriangles.Add(foliageVertexIndex + 2);

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

    void AddStairsBlock(Vector3 pos, byte blockType)
    {
        // 38, 40, 41, 42 are Wooden (uses Plank ID 2), 39, 43, 44, 45 are Stone (uses Stone ID 3)
        bool isWood = (blockType == 38 || blockType == 40 || blockType == 41 || blockType == 42);
        byte textureBlockType = isWood ? (byte)2 : (byte)3;

        // Bottom box: (0, 0, 0) to (1, 0.5, 1)
        AddSubBox(pos, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f), textureBlockType);

        // Top box: depends on orientation
        Vector3 topMin = Vector3.zero;
        Vector3 topMax = Vector3.zero;

        if (blockType == 38 || blockType == 39) // South: step rises to +Z (back half is solid)
        {
            topMin = new Vector3(0f, 0.5f, 0.5f);
            topMax = new Vector3(1f, 1f, 1f);
        }
        else if (blockType == 40 || blockType == 43) // North: step rises to -Z (front half is solid)
        {
            topMin = new Vector3(0f, 0.5f, 0f);
            topMax = new Vector3(1f, 1f, 0.5f);
        }
        else if (blockType == 41 || blockType == 44) // West: step rises to +X (right half is solid)
        {
            topMin = new Vector3(0.5f, 0.5f, 0f);
            topMax = new Vector3(1f, 1f, 1f);
        }
        else if (blockType == 42 || blockType == 45) // East: step rises to -X (left half is solid)
        {
            topMin = new Vector3(0f, 0.5f, 0f);
            topMax = new Vector3(0.5f, 1f, 1f);
        }

        AddSubBox(pos, topMin, topMax, textureBlockType);
    }

    void AddSlabBlock(Vector3 pos, byte blockType)
    {
        // 46 is Wooden Slab (uses Plank ID 2), 47 is Stone Slab (uses Stone ID 3)
        byte textureBlockType = (blockType == 46) ? (byte)2 : (byte)3;

        // Slab box: (0, 0, 0) to (1, 0.5, 1)
        AddSubBox(pos, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f), textureBlockType);
    }

    void AddSubBox(Vector3 pos, Vector3 min, Vector3 max, byte textureBlockType)
    {
        for (int p = 0; p < 6; p++)
        {
            Vector3[] faceVerts = GetBoxFaceVertices(p, min, max);
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(pos + faceVerts[i]);
            }

            Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, textureBlockType);
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

    bool HasNeighborBlock(Vector3 pos, Vector3 direction)
    {
        Vector3 neighborPos = pos + direction;
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

        return neighbor != 0 && neighbor != 7 && neighbor != 9 && neighbor != 10 && neighbor != 11 && neighbor != 13 && neighbor != 14 && neighbor != 23 && neighbor != 27;
    }

    void AddPropellerBlock(Vector3 pos)
    {
        bool hasFront = HasNeighborBlock(pos, Vector3.forward);
        bool hasBack  = HasNeighborBlock(pos, Vector3.back);
        bool hasLeft  = HasNeighborBlock(pos, Vector3.left);
        bool hasRight = HasNeighborBlock(pos, Vector3.right);
        bool hasBottom = HasNeighborBlock(pos, Vector3.down);
        bool hasTop    = HasNeighborBlock(pos, Vector3.up);

        Quaternion blockRotation = Quaternion.identity;
        if (hasFront && !hasBack)
        {
            blockRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else if (hasBack && !hasFront)
        {
            blockRotation = Quaternion.identity;
        }
        else if (hasRight && !hasLeft)
        {
            blockRotation = Quaternion.Euler(0f, -90f, 0f);
        }
        else if (hasLeft && !hasRight)
        {
            blockRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        else if (hasBottom && !hasTop)
        {
            blockRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        else if (hasTop && !hasBottom)
        {
            blockRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            if (hasBottom)
                blockRotation = Quaternion.Euler(-90f, 0f, 0f);
            else
                blockRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        Vector3 centerOffset = new Vector3(0.5f, 0.5f, 0.5f);

        // 1. Hub (Extended to touch base block at Z = -0.5f)
        Vector3 hubSize = new Vector3(0.21f, 0.21f, 0.8f);
        Vector3 hubLocalPos = blockRotation * new Vector3(0f, 0f, -0.1f) + centerOffset;
        Quaternion hubRot = blockRotation;
        AddSubBoxRotated(pos, hubLocalPos, hubSize, hubRot, 24);

        // 2. Nose Cone
        Vector3 noseSize = new Vector3(0.15f, 0.15f, 0.15f);
        Vector3 noseLocalPos = blockRotation * new Vector3(0f, 0f, 0.3f) + centerOffset;
        Quaternion noseRot = blockRotation;
        AddSubBoxRotated(pos, noseLocalPos, noseSize, noseRot, 24);

        // 3. Three blades
        Vector3 bladeSize = new Vector3(0.132f, 0.48f, 0.03f);
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f;
            Quaternion radialRotation = Quaternion.Euler(0f, 0f, angle);
            Quaternion bladePitch = Quaternion.Euler(0f, 28f, 0f);
            
            Quaternion localBladeRot = radialRotation * bladePitch;
            Vector3 localBladePos = radialRotation * new Vector3(0f, 0.33f, 0f);

            Quaternion finalRot = blockRotation * localBladeRot;
            Vector3 finalPos = blockRotation * localBladePos + centerOffset;

            AddSubBoxRotated(pos, finalPos, bladeSize, finalRot, 25);
        }
    }

    void AddLargePropellerBlock(Vector3 pos)
    {
        bool hasFront = HasNeighborBlock(pos, Vector3.forward);
        bool hasBack  = HasNeighborBlock(pos, Vector3.back);
        bool hasLeft  = HasNeighborBlock(pos, Vector3.left);
        bool hasRight = HasNeighborBlock(pos, Vector3.right);
        bool hasBottom = HasNeighborBlock(pos, Vector3.down);
        bool hasTop    = HasNeighborBlock(pos, Vector3.up);

        Quaternion blockRotation = Quaternion.identity;
        if (hasFront && !hasBack)
        {
            blockRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else if (hasBack && !hasFront)
        {
            blockRotation = Quaternion.identity;
        }
        else if (hasRight && !hasLeft)
        {
            blockRotation = Quaternion.Euler(0f, -90f, 0f);
        }
        else if (hasLeft && !hasRight)
        {
            blockRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        else if (hasBottom && !hasTop)
        {
            blockRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        else if (hasTop && !hasBottom)
        {
            blockRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            if (hasBottom)
                blockRotation = Quaternion.Euler(-90f, 0f, 0f);
            else
                blockRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        Vector3 centerOffset = new Vector3(0.5f, 0.5f, 0.5f);

        // Cylindrical/thick hub running along Z-axis (oriented by blockRotation, extended to touch base block at Z = -0.5f)
        Vector3 hubSize = new Vector3(0.35f, 0.35f, 2.0f);
        Vector3 hubLocalPos = blockRotation * new Vector3(0f, 0f, 0.5f) + centerOffset;
        AddSubBoxRotated(pos, hubLocalPos, hubSize, blockRotation, 24);

        // Nose Cone at the front tip
        Vector3 noseSize = new Vector3(0.25f, 0.25f, 0.25f);
        Vector3 noseLocalPos = blockRotation * new Vector3(0f, 0f, 1.5f) + centerOffset;
        AddSubBoxRotated(pos, noseLocalPos, noseSize, blockRotation, 24);

        // Three blades at the upper part (z = 1.35f)
        Vector3 bladeSize = new Vector3(0.2f, 1.35f, 0.05f);
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f;
            Quaternion radialRotation = Quaternion.Euler(0f, 0f, angle);
            Quaternion bladePitch = Quaternion.Euler(0f, 28f, 0f);
            
            Quaternion localBladeRot = radialRotation * bladePitch;
            Vector3 localBladePos = radialRotation * new Vector3(0f, 0.8f, 0f);

            Quaternion finalRot = blockRotation * localBladeRot;
            Vector3 finalPos = blockRotation * (new Vector3(0f, 0f, 1.35f) + localBladePos) + centerOffset;

            AddSubBoxRotated(pos, finalPos, bladeSize, finalRot, 25);
        }
    }

    void AddSubBoxRotated(Vector3 pos, Vector3 localCenter, Vector3 size, Quaternion rotation, byte textureBlockType)
    {
        Vector3 min = -size * 0.5f;
        Vector3 max = size * 0.5f;

        for (int p = 0; p < 6; p++)
        {
            Vector3[] faceVerts = GetBoxFaceVertices(p, min, max);
            for (int i = 0; i < 4; i++)
            {
                Vector3 rotVert = rotation * faceVerts[i] + localCenter;
                vertices.Add(pos + rotVert);
            }

            Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, textureBlockType);
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

    Vector3[] GetBoxFaceVertices(int face, Vector3 min, Vector3 max)
    {
        Vector3[] verts = new Vector3[4];
        switch (face)
        {
            case 0: // Back
                verts[0] = new Vector3(min.x, min.y, min.z);
                verts[1] = new Vector3(min.x, max.y, min.z);
                verts[2] = new Vector3(max.x, min.y, min.z);
                verts[3] = new Vector3(max.x, max.y, min.z);
                break;
            case 1: // Front
                verts[0] = new Vector3(max.x, min.y, max.z);
                verts[1] = new Vector3(max.x, max.y, max.z);
                verts[2] = new Vector3(min.x, min.y, max.z);
                verts[3] = new Vector3(min.x, max.y, max.z);
                break;
            case 2: // Top
                verts[0] = new Vector3(min.x, max.y, min.z);
                verts[1] = new Vector3(min.x, max.y, max.z);
                verts[2] = new Vector3(max.x, max.y, min.z);
                verts[3] = new Vector3(max.x, max.y, max.z);
                break;
            case 3: // Bottom
                verts[0] = new Vector3(max.x, min.y, min.z);
                verts[1] = new Vector3(max.x, min.y, max.z);
                verts[2] = new Vector3(min.x, min.y, min.z);
                verts[3] = new Vector3(min.x, min.y, max.z);
                break;
            case 4: // Left
                verts[0] = new Vector3(min.x, min.y, max.z);
                verts[1] = new Vector3(min.x, max.y, max.z);
                verts[2] = new Vector3(min.x, min.y, min.z);
                verts[3] = new Vector3(min.x, max.y, min.z);
                break;
            case 5: // Right
                verts[0] = new Vector3(max.x, min.y, min.z);
                verts[1] = new Vector3(max.x, max.y, min.z);
                verts[2] = new Vector3(max.x, min.y, max.z);
                verts[3] = new Vector3(max.x, max.y, max.z);
                break;
        }
        return verts;
    }

    void EnsureGlassChild()
    {
        Transform t = transform.Find("Glass");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("Glass");
            go.transform.SetParent(transform, false);
        }
        else
        {
            go = t.gameObject;
        }

        glassMeshFilter = go.GetComponent<MeshFilter>();
        if (glassMeshFilter == null) glassMeshFilter = go.AddComponent<MeshFilter>();

        glassMeshRenderer = go.GetComponent<MeshRenderer>();
        if (glassMeshRenderer == null) glassMeshRenderer = go.AddComponent<MeshRenderer>();

        if (VoxelWorld.Instance != null && VoxelWorld.Instance.glassMaterial != null)
            glassMeshRenderer.material = VoxelWorld.Instance.glassMaterial;
        else
            glassMeshRenderer.material = meshRenderer.material;
    }

    /// <summary>
    /// Renders Glass (ID 35) visually into the dedicated glass mesh (ZWrite On + CullBack)
    /// to correctly occlude back faces. Also adds invisible collision geometry to the solid mesh.
    /// Adjacent-glass faces are culled. Faces next to solid opaque blocks are also culled.
    /// </summary>
    void AddGlassToSolidMesh(Vector3 pos)
    {
        float transU = 9f / (float)GrassTextureGenerator.TILE_COUNT;
        Vector2 transparentUV = new Vector2(transU, 0f);

        for (int p = 0; p < 6; p++)
        {
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

            // Cull face against: same glass=35 (cull), fully-solid blocks (cull)
            // Show face against: air (0), water (7), flowers (9-11), leaves (12)
            if (neighbor == 35) continue;  // adjacent glass panes share a face — hide it
            if (neighbor != 0 && neighbor != 7 && neighbor != 9 &&
                neighbor != 10 && neighbor != 11 && neighbor != 12) continue;

            Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, 35);

            // 1. Add to dedicated Glass mesh (ZWrite On + CullBack so back frames don't bleed through)
            for (int i = 0; i < 4; i++)
                glassVertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, i]]);
            glassUvs.AddRange(faceUVs);

            glassTriangles.Add(glassVertexIndex);
            glassTriangles.Add(glassVertexIndex + 1);
            glassTriangles.Add(glassVertexIndex + 2);
            glassTriangles.Add(glassVertexIndex + 2);
            glassTriangles.Add(glassVertexIndex + 1);
            glassTriangles.Add(glassVertexIndex + 3);
            glassVertexIndex += 4;

            // 2. Add invisible geometry to the solid mesh for MeshCollider (transparent UVs)
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, i]]);
                uvs.Add(transparentUV);
            }

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 3);
            vertexIndex += 4;
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
        // Flowers, leaves, glass (ID 35), and stairs are transparent — treat like air for solid-mesh culling purposes
        bool neighborIsFlower = (neighbor == 9 || neighbor == 10 || neighbor == 11 || neighbor == 12 || neighbor == 35 ||
                                 neighbor == 13 || neighbor == 14 ||
                                 neighbor == 38 || neighbor == 40 || neighbor == 41 || neighbor == 42 ||
                                 neighbor == 39 || neighbor == 43 || neighbor == 44 || neighbor == 45 ||
                                 neighbor == 46 || neighbor == 47 || neighbor == 22 || neighbor == 26 || neighbor == 27);

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
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
            waterMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
            foliageMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

        // ── Glass mesh (ZWrite On + CullBack to prevent back-face border bleed) ─
        if (glassVertices.Count > 0)
        {
            EnsureGlassChild();
            Mesh glassMesh = new Mesh();
            glassMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            glassMesh.vertices  = glassVertices.ToArray();
            glassMesh.triangles = glassTriangles.ToArray();
            glassMesh.uv        = glassUvs.ToArray();
            glassMesh.RecalculateNormals();

            glassMeshFilter.mesh = glassMesh;
            glassMeshRenderer.gameObject.SetActive(true);
        }
        else
        {
            if (glassMeshRenderer != null)
                glassMeshRenderer.gameObject.SetActive(false);
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
        int scanHeight = Mathf.Min(VoxelData.ChunkHeight, Mathf.FloorToInt(VoxelData.ChunkHeight * 0.35f));
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
            waterMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
