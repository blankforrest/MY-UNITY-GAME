using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    private NativeArray<byte> _voxelMap;
    public NativeArray<byte> voxelMap => _voxelMap;
    public Vector2 chunkPos { get; private set; }
    private bool renderersEnabled = true;

    private int maxVoxelHeight = 0;

    private bool _isDirty = false;
    public bool isDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                if (_isDirty && VoxelWorld.Instance != null)
                {
                    VoxelWorld.Instance.RegisterDirtyChunk(this);
                }
            }
        }
    }

    // Cached neighbor references to avoid dictionary lookups on boundaries
    private Chunk neighborNorth;
    private Chunk neighborSouth;
    private Chunk neighborEast;
    private Chunk neighborWest;

    private Vector3 chunkWorldPos;

    public static bool enableProfilingLogs = true;
    private float _updateStartTime;
    private bool isUpdating = false;
    private bool needsReupdate = false;
    private int currentUpdateVersion = 0;
    private JobHandle _meshJobHandle;


    // Vertex index counters â€” track how many vertices have been written to each NativeList
    private int vertexIndex = 0;
    private int waterVertexIndex = 0;
    private int foliageVertexIndex = 0;
    private int foliageSolidVertexIndex = 0;
    private int glassVertexIndex = 0;

    private NativeList<Vector3> vertices;
    private NativeList<int> triangles;
    private NativeList<Vector2> uvs;

    // Water rendering lists
    private NativeList<Vector3> waterVertices;
    private NativeList<int> waterTriangles;
    private NativeList<Vector2> waterUvs;
    private NativeList<Color> waterColors;

    private MeshFilter waterMeshFilter;
    private MeshRenderer waterMeshRenderer;

    // Foliage (flowers, etc.) rendering lists â€” separate child mesh, alpha-cutout
    private NativeList<Vector3> foliageVertices;
    private NativeList<int>     foliageTriangles;
    private NativeList<Vector2> foliageUvs;
    private NativeList<Color>   foliageColors;

    private MeshFilter   foliageMeshFilter;
    private MeshRenderer foliageMeshRenderer;
    private MeshCollider foliageMeshCollider;

    // Foliage solid (leaves, solid custom blocks) collider lists
    private NativeList<Vector3> foliageSolidVertices;
    private NativeList<int>     foliageSolidTriangles;
    private MeshCollider  foliageSolidCollider;

    // Glass rendering lists â€” separate child mesh, ZWrite On + CullBack to fix back-face bleed
    private NativeList<Vector3> glassVertices;
    private NativeList<int>     glassTriangles;
    private NativeList<Vector2> glassUvs;

    private MeshFilter   glassMeshFilter;
    private MeshRenderer glassMeshRenderer;

    public void Initialize(Vector2 pos, Material mat)
    {
        chunkPos = pos;
        chunkWorldPos = transform.position;
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = mat;

        EnsureNativeListsCreated();

        PopulateVoxelMap();

        // Resolve neighboring chunks references
        ResolveNeighbors();

        isDirty = true;

        if (ChunkCuller.Instance != null)
        {
            ChunkCuller.Instance.RegisterChunk(this);
        }

        // Update neighboring chunks if they exist, so they can cull their boundary faces
        UpdateNeighbor(new Vector2(chunkPos.x + 1, chunkPos.y));
        UpdateNeighbor(new Vector2(chunkPos.x - 1, chunkPos.y));
        UpdateNeighbor(new Vector2(chunkPos.x, chunkPos.y + 1));
        UpdateNeighbor(new Vector2(chunkPos.x, chunkPos.y - 1));
    }

    private void OnDestroy()
    {
        if (ChunkCuller.Instance != null)
        {
            ChunkCuller.Instance.UnregisterChunk(this);
        }

        ClearNeighborReferences();

        // Wait for any in-flight mesh job to finish before disposing native memory
        if (isUpdating)
        {
            _meshJobHandle.Complete();
        }


        if (voxelMap.IsCreated) voxelMap.Dispose();

        DisposeNativeLists();
    }

    private void EnsureNativeListsCreated()
    {
        if (!vertices.IsCreated) vertices = new NativeList<Vector3>(4096, Allocator.Persistent);
        if (!triangles.IsCreated) triangles = new NativeList<int>(6144, Allocator.Persistent);
        if (!uvs.IsCreated) uvs = new NativeList<Vector2>(4096, Allocator.Persistent);

        if (!waterVertices.IsCreated) waterVertices = new NativeList<Vector3>(2048, Allocator.Persistent);
        if (!waterTriangles.IsCreated) waterTriangles = new NativeList<int>(3072, Allocator.Persistent);
        if (!waterUvs.IsCreated) waterUvs = new NativeList<Vector2>(2048, Allocator.Persistent);
        if (!waterColors.IsCreated) waterColors = new NativeList<Color>(2048, Allocator.Persistent);

        if (!foliageVertices.IsCreated) foliageVertices = new NativeList<Vector3>(2048, Allocator.Persistent);
        if (!foliageTriangles.IsCreated) foliageTriangles = new NativeList<int>(3072, Allocator.Persistent);
        if (!foliageUvs.IsCreated) foliageUvs = new NativeList<Vector2>(2048, Allocator.Persistent);
        if (!foliageColors.IsCreated) foliageColors = new NativeList<Color>(2048, Allocator.Persistent);

        if (!foliageSolidVertices.IsCreated) foliageSolidVertices = new NativeList<Vector3>(1024, Allocator.Persistent);
        if (!foliageSolidTriangles.IsCreated) foliageSolidTriangles = new NativeList<int>(1536, Allocator.Persistent);

        if (!glassVertices.IsCreated) glassVertices = new NativeList<Vector3>(1024, Allocator.Persistent);
        if (!glassTriangles.IsCreated) glassTriangles = new NativeList<int>(1536, Allocator.Persistent);
        if (!glassUvs.IsCreated) glassUvs = new NativeList<Vector2>(1024, Allocator.Persistent);
    }

    private void DisposeNativeLists()
    {
        if (vertices.IsCreated) vertices.Dispose();
        if (triangles.IsCreated) triangles.Dispose();
        if (uvs.IsCreated) uvs.Dispose();

        if (waterVertices.IsCreated) waterVertices.Dispose();
        if (waterTriangles.IsCreated) waterTriangles.Dispose();
        if (waterUvs.IsCreated) waterUvs.Dispose();
        if (waterColors.IsCreated) waterColors.Dispose();

        if (foliageVertices.IsCreated) foliageVertices.Dispose();
        if (foliageTriangles.IsCreated) foliageTriangles.Dispose();
        if (foliageUvs.IsCreated) foliageUvs.Dispose();
        if (foliageColors.IsCreated) foliageColors.Dispose();

        if (foliageSolidVertices.IsCreated) foliageSolidVertices.Dispose();
        if (foliageSolidTriangles.IsCreated) foliageSolidTriangles.Dispose();

        if (glassVertices.IsCreated) glassVertices.Dispose();
        if (glassTriangles.IsCreated) glassTriangles.Dispose();
        if (glassUvs.IsCreated) glassUvs.Dispose();
    }

    private void ResolveNeighbors()
    {
        if (VoxelWorld.Instance == null) return;

        neighborEast  = VoxelWorld.Instance.GetChunkFromChunkPos(new Vector2(chunkPos.x + 1, chunkPos.y));
        neighborWest  = VoxelWorld.Instance.GetChunkFromChunkPos(new Vector2(chunkPos.x - 1, chunkPos.y));
        neighborNorth = VoxelWorld.Instance.GetChunkFromChunkPos(new Vector2(chunkPos.x, chunkPos.y + 1));
        neighborSouth = VoxelWorld.Instance.GetChunkFromChunkPos(new Vector2(chunkPos.x, chunkPos.y - 1));

        if (neighborEast != null) neighborEast.neighborWest = this;
        if (neighborWest != null) neighborWest.neighborEast = this;
        if (neighborNorth != null) neighborNorth.neighborSouth = this;
        if (neighborSouth != null) neighborSouth.neighborNorth = this;
    }

    private void ClearNeighborReferences()
    {
        if (neighborEast != null) { neighborEast.neighborWest = null; neighborEast.isDirty = true; }
        if (neighborWest != null) { neighborWest.neighborEast = null; neighborWest.isDirty = true; }
        if (neighborNorth != null) { neighborNorth.neighborSouth = null; neighborNorth.isDirty = true; }
        if (neighborSouth != null) { neighborSouth.neighborNorth = null; neighborSouth.isDirty = true; }
    }

    private void RecalculateMaxVoxelHeight()
    {
        maxVoxelHeight = 0;
        for (int y = VoxelData.ChunkHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    int flatIndex = VoxelData.GetFlatIndex(x, y, z);
                    if (voxelMap[flatIndex] != 0)
                    {
                        maxVoxelHeight = y;
                        return;
                    }
                }
            }
        }
    }

    public void SetRenderersEnabled(bool enabled)
    {
        if (renderersEnabled == enabled) return;
        renderersEnabled = enabled;
        if (meshRenderer != null) meshRenderer.enabled = enabled;
        if (waterMeshRenderer != null) waterMeshRenderer.enabled = enabled;
        if (foliageMeshRenderer != null) foliageMeshRenderer.enabled = enabled;
        if (glassMeshRenderer != null) glassMeshRenderer.enabled = enabled;
    }

    void UpdateNeighbor(Vector2 pos)
    {
        if (VoxelWorld.Instance != null)
        {
            Chunk neighbor = VoxelWorld.Instance.GetChunkFromChunkPos(pos);
            if (neighbor != null)
            {
                neighbor.isDirty = true;
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
        int elementCount = VoxelData.ChunkWidth * VoxelData.ChunkHeight * VoxelData.ChunkWidth;
        _voxelMap = new NativeArray<byte>(elementCount, Allocator.Persistent);

        PopulateVoxelMapJob job = new PopulateVoxelMapJob
        {
            ChunkPos = new float2(chunkPos.x, chunkPos.y),
            WorldSeedOffsetX = SaveLoadManager.worldSeedOffsetX,
            WorldSeedOffsetZ = SaveLoadManager.worldSeedOffsetZ,
            ChunkWidth = VoxelData.ChunkWidth,
            ChunkHeight = VoxelData.ChunkHeight,
            VoxelMapOut = _voxelMap,
            Modifications = SaveLoadManager.Instance != null ? SaveLoadManager.Instance.NativeModifications : default
        };

        job.Run();
        RecalculateMaxVoxelHeight();
    }

    [BurstCompile]
    private struct PopulateVoxelMapJob : IJob
    {
        public float2 ChunkPos;
        public float WorldSeedOffsetX;
        public float WorldSeedOffsetZ;
        public int ChunkWidth;
        public int ChunkHeight;

        public NativeArray<byte> VoxelMapOut;
        [Unity.Collections.ReadOnly]
        public Unity.Collections.NativeParallelHashMap<Vector3Int, byte> Modifications;

        private float Noise2D(float x, float y)
        {
            float val = noise.cnoise(new float2(x, y)) * 0.5f + 0.5f;
            return math.clamp(val, 0f, 1f);
        }

        private void GetTreeNoise(float gX, float gZ, out float forestZone, out float clusterNoise, out float gapNoise)
        {
            float ox = gX + WorldSeedOffsetX;
            float oz = gZ + WorldSeedOffsetZ;
            forestZone   = Noise2D(ox * 0.025f + 500f, oz * 0.025f + 700f);
            clusterNoise = Noise2D(ox * 0.10f + 900f,  oz * 0.10f + 1100f);
            gapNoise     = Noise2D(ox * 0.28f + 333f,  oz * 0.28f + 444f);
        }

        private int GetBiome(float gX, float gZ)
        {
            float ox = gX + WorldSeedOffsetX;
            float oz = gZ + WorldSeedOffsetZ;

            float continentNoise = Noise2D(ox * 0.00015f + 1234.5f, oz * 0.00015f + 5678.9f);
            float startScale = math.clamp(math.max(math.abs(gX), math.abs(gZ)) / 300f, 0f, 1f);
            continentNoise = math.lerp(0.35f, continentNoise, startScale);

            if (continentNoise > 0.55f)
                return 0; // Ocean

            // River check
            float warpX = Noise2D(ox * 0.015f + 10f, oz * 0.015f + 20f) * 60f;
            float warpZ = Noise2D(ox * 0.015f + 30f, oz * 0.015f + 40f) * 60f;
            float riverNoise = Noise2D((ox + warpX) * 0.002f + 400f, (oz + warpZ) * 0.002f + 800f);
            float riverCenterDist = math.abs(riverNoise - 0.5f);
            float widthNoise = Noise2D(ox * 0.01f + 150f, oz * 0.01f + 250f);
            float riverWidth = 0.01f + widthNoise * 0.008f;
            float riverStrength = 1f;
            if (continentNoise < 0.45f)
                riverStrength = math.clamp((continentNoise - 0.35f) / 0.10f, 0f, 1f);
            else if (continentNoise > 0.55f)
                riverStrength = math.clamp((0.65f - continentNoise) / 0.10f, 0f, 1f);
            bool isRiver = (riverStrength > 0f) && (riverCenterDist < riverWidth);

            if (isRiver)
                return 4; // River

            float biomeNoise = Noise2D(ox * 0.003f + 4000f, oz * 0.003f + 8000f);
            if (biomeNoise < 0.25f)
                return 1; // Desert
            else if (biomeNoise < 0.50f)
                return 2; // Plains
            else
                return 3; // Forest
        }

        private bool WouldSpawnTree(float gX, float gZ, out float priority)
        {
            float ox = gX + WorldSeedOffsetX;
            float oz = gZ + WorldSeedOffsetZ;
            
            int biome = GetBiome(gX, gZ);
            if (biome == 0 || biome == 1 || biome == 4) // No trees in Ocean, Desert, or River
            {
                priority = 0f;
                return false;
            }

            GetTreeNoise(gX, gZ, out float f, out float c, out float g);
            priority = Noise2D(ox * 0.75f + 123.4f, oz * 0.75f + 567.8f);

            if (biome == 2) // Plains: extremely sparse trees
            {
                return (f >= 0.72f && c >= 0.72f && g <= 0.45f);
            }
            else // Forest: standard high density
            {
                return (f >= 0.50f && c >= 0.48f && g <= 0.75f);
            }
        }

        private int GetFlatIndex(int x, int y, int z)
        {
            return x * (ChunkHeight * ChunkWidth) + y * ChunkWidth + z;
        }

        public void Execute()
        {
            // Main generation loop
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int x = 0; x < ChunkWidth; x++)
                {
                    for (int z = 0; z < ChunkWidth; z++)
                    {
                        float globalX = x + ChunkPos.x * ChunkWidth;
                        float globalZ = z + ChunkPos.y * ChunkWidth;

                        float ox = globalX + WorldSeedOffsetX;
                        float oz = globalZ + WorldSeedOffsetZ;

                        // Base rolling noise
                        float baseNoise = Noise2D(ox * 0.02f, oz * 0.02f);
                        baseNoise = math.pow(baseNoise, 2.5f);
                        
                        float detailNoise = Noise2D(ox * 0.08f, oz * 0.08f) * 0.15f;
                        float finalNoise = baseNoise + detailNoise;
                        float exactHeight = finalNoise * ChunkHeight * 0.25f + (ChunkHeight / 5f);
     
                        float continentNoise = Noise2D(ox * 0.00015f + 1234.5f, oz * 0.00015f + 5678.9f);
                        float startScale = math.clamp(math.max(math.abs(globalX), math.abs(globalZ)) / 300f, 0f, 1f);
                        continentNoise = math.lerp(0.35f, continentNoise, startScale);

                        float heightOffset = 0f;
                        if (continentNoise < 0.45f)
                        {
                            float dryT = (0.45f - continentNoise) / 0.45f;
                            heightOffset = dryT * 18f;
                        }
                        else if (continentNoise > 0.55f)
                        {
                            float oceanT = (continentNoise - 0.55f) / 0.45f;
                            heightOffset = -oceanT * 22f;
                        }

                        float oceanFloorNoise = Noise2D(ox * 0.015f, oz * 0.015f);
                        float oceanDetail = Noise2D(ox * 0.06f, oz * 0.06f) * 2.5f;
                        float oceanHeight = 12f + (oceanFloorNoise * 7f) + oceanDetail;

                        // Multi-octave desert dune generator
                        float duneNoise1 = Noise2D(ox * 0.01f, oz * 0.01f);
                        float duneNoise2 = Noise2D(ox * 0.03f, oz * 0.03f) * 0.25f;
                        float duneNoise = (duneNoise1 + duneNoise2) / 1.25f;
                        duneNoise = math.pow(duneNoise, 2f);
                        float desertHeight = (duneNoise * ChunkHeight * 0.26f) + (ChunkHeight * 0.22f) + heightOffset;

                        float plainsNoise = Noise2D(ox * 0.01f, oz * 0.01f);
                        float plainsHeight = (plainsNoise * ChunkHeight * 0.03f) + (ChunkHeight * 0.22f) + heightOffset;

                        float forestHeight = finalNoise * ChunkHeight * 0.20f + (ChunkHeight * 0.21f) + heightOffset;

                        float biomeNoise = Noise2D(ox * 0.003f + 4000f, oz * 0.003f + 8000f);
                        int biome = 2; // Plains default
                        if (continentNoise > 0.55f) biome = 0; // Ocean
                        else if (biomeNoise < 0.25f) biome = 1; // Desert
                        else if (biomeNoise < 0.50f) biome = 2; // Plains
                        else biome = 3; // Forest

                        float landHeight = 0f;
                        if (biomeNoise < 0.20f)
                        {
                            landHeight = desertHeight;
                        }
                        else if (biomeNoise < 0.30f)
                        {
                            float t = (biomeNoise - 0.20f) / 0.10f;
                            t = t * t * (3f - 2f * t); // Smoothstep
                            landHeight = math.lerp(desertHeight, plainsHeight, t);
                        }
                        else if (biomeNoise < 0.45f)
                        {
                            landHeight = plainsHeight;
                        }
                        else if (biomeNoise < 0.55f)
                        {
                            float t = (biomeNoise - 0.45f) / 0.10f;
                            t = t * t * (3f - 2f * t); // Smoothstep
                            landHeight = math.lerp(plainsHeight, forestHeight, t);
                        }
                        else
                        {
                            landHeight = forestHeight;
                        }

                        // We use a slightly wider window (0.12f instead of 0.10f) for a more gradual slope into deep ocean
                        float oceanWeight = math.clamp((continentNoise - 0.45f) / 0.12f, 0f, 1f);
                        oceanWeight = oceanWeight * oceanWeight * (3f - 2f * oceanWeight); // Smoothstep
                        exactHeight = math.lerp(landHeight, oceanHeight, oceanWeight);
                        exactHeight = math.max(2f, exactHeight);

                        float seaLevel = math.floor(ChunkHeight * 0.22f);
                        
                        float warpX = Noise2D(ox * 0.015f + 10f, oz * 0.015f + 20f) * 60f;
                        float warpZ = Noise2D(ox * 0.015f + 30f, oz * 0.015f + 40f) * 60f;

                        float riverNoise = Noise2D((ox + warpX) * 0.002f + 400f, (oz + warpZ) * 0.002f + 800f);
                        float riverCenterDist = math.abs(riverNoise - 0.5f);
                        
                        float widthNoise = Noise2D(ox * 0.01f + 150f, oz * 0.01f + 250f);
                        float riverWidth = 0.01f + widthNoise * 0.008f;
                        
                        float riverStrength = 1f;
                        if (continentNoise < 0.45f)
                            riverStrength = math.clamp((continentNoise - 0.35f) / 0.10f, 0f, 1f);
                        else if (continentNoise > 0.55f)
                            riverStrength = math.clamp((0.65f - continentNoise) / 0.10f, 0f, 1f);

                        bool isRiver = (riverStrength > 0f) && (riverCenterDist < riverWidth);

                        if (isRiver)
                        {
                            float riverFactor = math.clamp(riverCenterDist / riverWidth, 0f, 1f);
                            riverFactor = math.smoothstep(0f, 1f, riverFactor);
                            
                            float riverDepth = 3.5f * riverStrength;
                            float riverbedHeight = seaLevel - riverDepth;
                            
                            float carvedHeight = math.lerp(riverbedHeight, exactHeight, riverFactor);
                            exactHeight = math.min(exactHeight, carvedHeight);
                        }
                        else
                        {
                            // Smoothly phase out the land height clamp as the continent transitions into the ocean
                            float minHeight = math.lerp(seaLevel + 1.5f, 2f, oceanWeight);
                            exactHeight = math.max(minHeight, exactHeight);
                        }

                        // Lower beach height by 1 block using perlin noise
                        float beachNoise = Noise2D(ox * 0.1f, oz * 0.1f);
                        if (exactHeight <= seaLevel + 1.6f && exactHeight >= seaLevel && beachNoise > 0.0f)
                        {
                            exactHeight -= 1.0f;
                        }

                        int floorY = (int)math.floor(exactHeight);

                        float dirtNoise = Noise2D(ox * 0.1f, oz * 0.1f);
                        int dirtThickness = 3 + (int)math.floor(dirtNoise * 3f);

                        float cactusNoise = Noise2D(ox * 0.3f + 700f, oz * 0.3f + 900f);
                        int cactusHeight = 0;
                        if (biome == 1 && floorY > seaLevel + 1 && !isRiver)
                        {
                            if (cactusNoise > 0.94f)
                            {
                                float hNoise = Noise2D(ox * 0.9f, oz * 0.9f);
                                cactusHeight = 2 + (int)math.floor(hNoise * 2f);
                            }
                        }

                        int flatIndex = GetFlatIndex(x, y, z);

                        if (y == 0)
                        {
                            VoxelMapOut[flatIndex] = 48; // Bedrock
                        }
                        else if (y > floorY)
                        {
                            if (y <= floorY + cactusHeight)
                            {
                                VoxelMapOut[flatIndex] = 49; // Cactus
                            }
                            else if (y > seaLevel)
                            {
                                VoxelMapOut[flatIndex] = 0; // Air
                            }
                            else
                            {
                                VoxelMapOut[flatIndex] = 7; // Water
                            }
                        }
                        else if (y == floorY)
                        {
                            if (biome == 1)
                            {
                                VoxelMapOut[flatIndex] = 8; // Sand
                            }
                            else if (floorY <= seaLevel + 1)
                            {
                                VoxelMapOut[flatIndex] = 8; // Sand beach/riverbed
                            }
                            else
                            {
                                VoxelMapOut[flatIndex] = 4; // Grass
                            }
                        }
                        else if (y >= floorY - dirtThickness)
                        {
                            if (biome == 1)
                            {
                                VoxelMapOut[flatIndex] = 8; // Sand deep
                            }
                            else if (floorY <= seaLevel + 1)
                            {
                                VoxelMapOut[flatIndex] = 8; // Sand deep beach
                            }
                            else
                            {
                                VoxelMapOut[flatIndex] = 5; // Dirt
                            }
                        }
                        else
                        {
                            VoxelMapOut[flatIndex] = 3; // Stone
                        }

                        // Flower & Grass spawning
                        if (y == floorY + 1
                            && VoxelMapOut[GetFlatIndex(x, floorY, z)] == 4
                            && floorY > seaLevel + 1)
                        {
                            float foliageNoise = Noise2D(ox * 0.15f + 120.3f, oz * 0.15f + 340.1f);
                            float threshold = (biome == 2) ? 0.32f : 0.45f;
                            if (foliageNoise > threshold)
                            {
                                float subNoise = Noise2D(ox * 0.6f + 85f, oz * 0.6f + 15f);
                                if (subNoise < 0.12f)
                                {
                                    float typeNoise = Noise2D(ox * 0.7f + 50f, oz * 0.7f + 90f);
                                    if (typeNoise < 0.33f)
                                        VoxelMapOut[flatIndex] = 9; // Rose
                                    else if (typeNoise < 0.66f)
                                        VoxelMapOut[flatIndex] = 10; // Dandelion
                                    else
                                        VoxelMapOut[flatIndex] = 11; // Iris
                                }
                                else
                                {
                                    float grassTypeNoise = Noise2D(ox * 0.8f + 144f, oz * 0.8f + 788f);
                                    if (grassTypeNoise < 0.7f)
                                        VoxelMapOut[flatIndex] = 13; // Short Grass
                                    else
                                        VoxelMapOut[flatIndex] = 14; // Tall Grass
                                }
                            }
                        }
                    }
                }
            }

            // Tree Spawning loop inside Job
            for (int x = 1; x < ChunkWidth - 1; x++)
            {
                for (int z = 1; z < ChunkWidth - 1; z++)
                {
                    float globalX = x + ChunkPos.x * ChunkWidth;
                    float globalZ = z + ChunkPos.y * ChunkWidth;

                    float myPriority;
                    if (!WouldSpawnTree(globalX, globalZ, out myPriority)) continue;

                    bool isLocalMax = true;
                    for (int dx = -4; dx <= 4 && isLocalMax; dx++)
                    {
                        for (int dz = -4; dz <= 4 && isLocalMax; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            if (dx * dx + dz * dz > 16) continue;

                            float nGlobalX = globalX + dx;
                            float nGlobalZ = globalZ + dz;
                            float neighborPriority;
                            if (WouldSpawnTree(nGlobalX, nGlobalZ, out neighborPriority))
                            {
                                if (neighborPriority > myPriority)
                                {
                                    isLocalMax = false;
                                }
                                else if (math.abs(neighborPriority - myPriority) < 0.0001f)
                                {
                                    if (nGlobalX < globalX || (math.abs(nGlobalX - globalX) < 0.0001f && nGlobalZ < globalZ))
                                    {
                                        isLocalMax = false;
                                    }
                                }
                            }
                        }
                    }

                    if (!isLocalMax) continue;

                    int surfaceY = -1;
                    for (int sy = ChunkHeight - 1; sy > 0; sy--)
                    {
                        byte b = VoxelMapOut[GetFlatIndex(x, sy, z)];
                        if (b == 4) { surfaceY = sy; break; }
                    }
                    if (surfaceY < 0) continue;
                    if (surfaceY + 7 >= ChunkHeight) continue;

                    float hox = globalX + WorldSeedOffsetX;
                    float hoz = globalZ + WorldSeedOffsetZ;
                    float heightNoise = Noise2D(hox * 0.9f + 888f, hoz * 0.9f + 333f);

                    int logID = 1;
                    int leafID = 12;
                    bool isSpruce = false;

                    int currentBiome = GetBiome(globalX, globalZ);
                    float treeTypeNoise = Noise2D(hox * 0.1f + 111f, hoz * 0.1f + 222f);

                    if (currentBiome == 3)
                    {
                        if (treeTypeNoise < 0.4f)
                        {
                            logID = 53;
                            leafID = 54;
                            isSpruce = true;
                        }
                        else if (treeTypeNoise < 0.7f)
                        {
                            logID = 51;
                            leafID = 52;
                        }
                        else
                        {
                            logID = 1;
                            leafID = 12;
                        }
                    }
                    else
                    {
                        if (treeTypeNoise < 0.3f)
                        {
                            logID = 51;
                            leafID = 52;
                        }
                        else
                        {
                            logID = 1;
                            leafID = 12;
                        }
                    }

                    int trunkHeight = isSpruce ? (5 + (int)math.floor(heightNoise * 4f)) : (4 + (int)math.floor(heightNoise * 3f));

                    for (int ty = 1; ty <= trunkHeight; ty++)
                    {
                        int ty_abs = surfaceY + ty;
                        if (ty_abs >= ChunkHeight) break;
                        VoxelMapOut[GetFlatIndex(x, ty_abs, z)] = (byte)logID;
                    }

                    if (isSpruce)
                    {
                        int topY = surfaceY + trunkHeight;
                        for (int ly = 2; ly <= trunkHeight + 1; ly++)
                        {
                            int cy = surfaceY + ly;
                            if (cy < 0 || cy >= ChunkHeight) continue;

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
                                    if (cx < 0 || cx >= ChunkWidth || cz < 0 || cz >= ChunkWidth) continue;

                                    if (cx == x && cz == z && cy <= topY) continue;
                                    if (radius > 1 && (lx * lx + lz * lz > radius * radius)) continue;

                                    byte currentVal = VoxelMapOut[GetFlatIndex(cx, cy, cz)];
                                    if (currentVal == 0 || currentVal == 13 || currentVal == 14 || (currentVal >= 9 && currentVal <= 11))
                                    {
                                        VoxelMapOut[GetFlatIndex(cx, cy, cz)] = (byte)leafID;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
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
                                    if (cx < 0 || cx >= ChunkWidth || cz < 0 || cz >= ChunkWidth || cy < 0 || cy >= ChunkHeight) continue;

                                    byte currentVal = VoxelMapOut[GetFlatIndex(cx, cy, cz)];
                                    if (currentVal == logID || currentVal == 1 || currentVal == 51 || currentVal == 53) continue;

                                    int dist2 = lx * lx + lz * lz;
                                    bool isTip = (ly == 2);
                                    if (isTip && dist2 > 1) continue;
                                    if (ly < -1 && dist2 > 1) continue;
                                    if (dist2 > 5) continue;

                                    float leafNoise = Noise2D((hox + lx) * 0.4f + 22f, (hoz + lz) * 0.4f + 55f);
                                    if (dist2 == 5 && leafNoise < 0.45f) continue;

                                    VoxelMapOut[GetFlatIndex(cx, cy, cz)] = (byte)leafID;
                                }
                            }
                        }
                    }

                    byte above = VoxelMapOut[GetFlatIndex(x, surfaceY + 1, z)];
                    if ((above >= 9 && above <= 11) || above == 13 || above == 14)
                        VoxelMapOut[GetFlatIndex(x, surfaceY + 1, z)] = (byte)logID;
                }
            }

            // Apply loaded/recorded modifications
            if (Modifications.IsCreated)
            {
                int startX = (int)math.floor(ChunkPos.x * ChunkWidth);
                int startZ = (int)math.floor(ChunkPos.y * ChunkWidth);

                for (int x = 0; x < ChunkWidth; x++)
                {
                    for (int z = 0; z < ChunkWidth; z++)
                    {
                        for (int y = 0; y < ChunkHeight; y++)
                        {
                            Vector3Int globalPos = new Vector3Int(startX + x, y, startZ + z);
                            if (Modifications.TryGetValue(globalPos, out byte savedID))
                            {
                                int flatIndex = GetFlatIndex(x, y, z);
                                VoxelMapOut[flatIndex] = savedID;
                            }
                        }
                    }
                }
            }
        }
    }

    public void EditVoxel(int x, int y, int z, byte newID)
    {
        if (IsVoxelInChunk(x, y, z))
        {
            int flatIndex = VoxelData.GetFlatIndex(x, y, z);
            _voxelMap[flatIndex] = newID;
            RecalculateMaxVoxelHeight();
            UpdateChunk();
        }
    }

    public void EditVoxel(Vector3 localPosition, byte newID)
    {
        int x = Mathf.FloorToInt(localPosition.x);
        int y = Mathf.FloorToInt(localPosition.y);
        int z = Mathf.FloorToInt(localPosition.z);
        EditVoxel(x, y, z, newID);
    }

    public byte GetVoxel(int x, int y, int z)
    {
        if (!IsVoxelInChunk(x, y, z))
            return 0;

        return voxelMap[VoxelData.GetFlatIndex(x, y, z)];
    }

    bool IsVoxelInChunk(int x, int y, int z)
    {
        if (x < 0 || x >= VoxelData.ChunkWidth || y < 0 || y >= VoxelData.ChunkHeight || z < 0 || z >= VoxelData.ChunkWidth)
            return false;
        else
            return true;
    }


    private void Update()
    {
        if (isUpdating && _meshJobHandle.IsCompleted)
        {
            float jobEndTime = Time.realtimeSinceStartup;
            float jobDurationMs = (jobEndTime - _updateStartTime) * 1000f;

            _meshJobHandle.Complete();
            RecalculateMaxVoxelHeight();

            float mainThreadStartTime = Time.realtimeSinceStartup;
            CreateMesh();
            float mainThreadEndTime = Time.realtimeSinceStartup;
            float mainThreadDurationMs = (mainThreadEndTime - mainThreadStartTime) * 1000f;

            isUpdating = false;

            if (enableProfilingLogs)
            {
                Debug.Log($"[Profiler] Async Chunk ({chunkPos.x}, {chunkPos.y}) Rebuilt. Job Duration: {jobDurationMs:F2} ms, Main-Thread CreateMesh: {mainThreadDurationMs:F2} ms.");
            }

            if (needsReupdate)
            {
                UpdateChunk();
            }
        }
    }

    public void UpdateChunk()
    {
        if (isUpdating) { needsReupdate = true; return; }

        _updateStartTime = Time.realtimeSinceStartup;
        isUpdating    = true;
        needsReupdate = false;
        currentUpdateVersion++;

        chunkWorldPos = transform.position;
        _isDirty = false;

        // Reset mesh list capacities and clear them
        ClearMeshData();

        // Gather furnaces in this chunk
        var activeFurnaces = new List<UnmanagedFurnaceData>();
        var burning = FurnaceManager.Instance != null ? FurnaceManager.GetBurningFurnaces() : null;
        var facings = FurnaceManager.Instance != null ? FurnaceManager.GetFurnaceFacings() : null;
        
        if (burning != null)
        {
            foreach (var worldPos in burning)
            {
                Vector3Int localPos = worldPos - Vector3Int.FloorToInt(chunkWorldPos);
                if (localPos.x >= 0 && localPos.x < VoxelData.ChunkWidth &&
                    localPos.y >= 0 && localPos.y < VoxelData.ChunkHeight &&
                    localPos.z >= 0 && localPos.z < VoxelData.ChunkWidth)
                {
                    int facing = -1;
                    if (facings != null) facings.TryGetValue(worldPos, out facing);
                    activeFurnaces.Add(new UnmanagedFurnaceData
                    {
                        localPos = new int3(localPos.x, localPos.y, localPos.z),
                        isLit = true,
                        facing = facing
                    });
                }
            }
        }
        if (facings != null)
        {
            foreach (var kvp in facings)
            {
                var worldPos = kvp.Key;
                Vector3Int localPos = worldPos - Vector3Int.FloorToInt(chunkWorldPos);
                if (localPos.x >= 0 && localPos.x < VoxelData.ChunkWidth &&
                    localPos.y >= 0 && localPos.y < VoxelData.ChunkHeight &&
                    localPos.z >= 0 && localPos.z < VoxelData.ChunkWidth)
                {
                    bool alreadyAdded = false;
                    for (int i = 0; i < activeFurnaces.Count; i++)
                    {
                        if (activeFurnaces[i].localPos.x == localPos.x &&
                            activeFurnaces[i].localPos.y == localPos.y &&
                            activeFurnaces[i].localPos.z == localPos.z)
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }
                    if (!alreadyAdded)
                    {
                        activeFurnaces.Add(new UnmanagedFurnaceData
                        {
                            localPos = new int3(localPos.x, localPos.y, localPos.z),
                            isLit = false,
                            facing = kvp.Value
                        });
                    }
                }
            }
        }

        var furnaceDataArray = new NativeArray<UnmanagedFurnaceData>(activeFurnaces.Count, Allocator.TempJob);
        for (int i = 0; i < activeFurnaces.Count; i++) furnaceDataArray[i] = activeFurnaces[i];

        // Gather vehicles for water suppression
        var activeVehicles = VehicleController.GetCachedVehicles();
        int totalBounds = 0;
        for (int i = 0; i < activeVehicles.Count; i++)
        {
            if (activeVehicles[i].dryFilters != null)
                totalBounds += activeVehicles[i].dryFilters.Count;
        }

        var vehicleFilters = new NativeArray<Bounds>(totalBounds, Allocator.TempJob);
        var unmanagedVehicles = new NativeArray<UnmanagedVehicleData>(activeVehicles.Count, Allocator.TempJob);

        int boundIndex = 0;
        for (int i = 0; i < activeVehicles.Count; i++)
        {
            var av = activeVehicles[i];
            int start = boundIndex;
            int count = 0;
            if (av.dryFilters != null)
            {
                for (int j = 0; j < av.dryFilters.Count; j++)
                {
                    vehicleFilters[boundIndex++] = av.dryFilters[j];
                    count++;
                }
            }
            unmanagedVehicles[i] = new UnmanagedVehicleData
            {
                position = av.position,
                inverseRotation = av.inverseRotation,
                filterStart = start,
                filterCount = count
            };
        }

        var emptyMap = new NativeArray<byte>(0, Allocator.TempJob);

        var job = new MeshGenerationJob
        {
            chunkWorldPos = chunkWorldPos,
            scanHeight = VoxelData.ChunkHeight,
            totalTilesCount = BlockRegistry.TotalTilesCount,
            waterOnly = false,

            VoxelMap = voxelMap,
            WestMap = (neighborWest != null && neighborWest.voxelMap.IsCreated) ? neighborWest.voxelMap : emptyMap,
            EastMap = (neighborEast != null && neighborEast.voxelMap.IsCreated) ? neighborEast.voxelMap : emptyMap,
            SouthMap = (neighborSouth != null && neighborSouth.voxelMap.IsCreated) ? neighborSouth.voxelMap : emptyMap,
            NorthMap = (neighborNorth != null && neighborNorth.voxelMap.IsCreated) ? neighborNorth.voxelMap : emptyMap,

            NativeDefinitions = BlockRegistry.NativeDefinitions,
            CustomMeshVertices = BlockRegistry.CustomMeshVertices,
            CustomMeshIndices = BlockRegistry.CustomMeshIndices,
            CustomMeshUVs = BlockRegistry.CustomMeshUVs,

            VoxelVerts = BlockRegistry.VoxelVerts,
            VoxelTris = BlockRegistry.VoxelTris,
            FaceChecks = BlockRegistry.FaceChecks,

            Furnaces = furnaceDataArray,
            Vehicles = unmanagedVehicles,
            VehicleFilters = vehicleFilters,

            vertices = vertices,
            triangles = triangles,
            uvs = uvs,

            waterVertices = waterVertices,
            waterTriangles = waterTriangles,
            waterUvs = waterUvs,
            waterColors = waterColors,

            foliageVertices = foliageVertices,
            foliageTriangles = foliageTriangles,
            foliageUvs = foliageUvs,
            foliageColors = foliageColors,

            foliageSolidVertices = foliageSolidVertices,
            foliageSolidTriangles = foliageSolidTriangles,

            glassVertices = glassVertices,
            glassTriangles = glassTriangles,
            glassUvs = glassUvs
        };

        _meshJobHandle = job.Schedule();

        // Schedule NativeArray disposals automatically when job completes
        furnaceDataArray.Dispose(_meshJobHandle);
        unmanagedVehicles.Dispose(_meshJobHandle);
        vehicleFilters.Dispose(_meshJobHandle);
        emptyMap.Dispose(_meshJobHandle);
    }

    public void UpdateChunkSync()
    {
        float syncStartTime = Time.realtimeSinceStartup;
        currentUpdateVersion++;
        if (isUpdating)
        {
            _meshJobHandle.Complete();
            isUpdating = false;
        }



        isUpdating = false;
        needsReupdate = false;
        chunkWorldPos = transform.position;

        ClearMeshData();

        // Gather furnaces in this chunk
        var activeFurnaces = new List<UnmanagedFurnaceData>();
        var burning = FurnaceManager.Instance != null ? FurnaceManager.GetBurningFurnaces() : null;
        var facings = FurnaceManager.Instance != null ? FurnaceManager.GetFurnaceFacings() : null;
        
        if (burning != null)
        {
            foreach (var worldPos in burning)
            {
                Vector3Int localPos = worldPos - Vector3Int.FloorToInt(chunkWorldPos);
                if (localPos.x >= 0 && localPos.x < VoxelData.ChunkWidth &&
                    localPos.y >= 0 && localPos.y < VoxelData.ChunkHeight &&
                    localPos.z >= 0 && localPos.z < VoxelData.ChunkWidth)
                {
                    int facing = -1;
                    if (facings != null) facings.TryGetValue(worldPos, out facing);
                    activeFurnaces.Add(new UnmanagedFurnaceData
                    {
                        localPos = new int3(localPos.x, localPos.y, localPos.z),
                        isLit = true,
                        facing = facing
                    });
                }
            }
        }
        if (facings != null)
        {
            foreach (var kvp in facings)
            {
                var worldPos = kvp.Key;
                Vector3Int localPos = worldPos - Vector3Int.FloorToInt(chunkWorldPos);
                if (localPos.x >= 0 && localPos.x < VoxelData.ChunkWidth &&
                    localPos.y >= 0 && localPos.y < VoxelData.ChunkHeight &&
                    localPos.z >= 0 && localPos.z < VoxelData.ChunkWidth)
                {
                    bool alreadyAdded = false;
                    for (int i = 0; i < activeFurnaces.Count; i++)
                    {
                        if (activeFurnaces[i].localPos.x == localPos.x &&
                            activeFurnaces[i].localPos.y == localPos.y &&
                            activeFurnaces[i].localPos.z == localPos.z)
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }
                    if (!alreadyAdded)
                    {
                        activeFurnaces.Add(new UnmanagedFurnaceData
                        {
                            localPos = new int3(localPos.x, localPos.y, localPos.z),
                            isLit = false,
                            facing = kvp.Value
                        });
                    }
                }
            }
        }

        var furnaceDataArray = new NativeArray<UnmanagedFurnaceData>(activeFurnaces.Count, Allocator.TempJob);
        for (int i = 0; i < activeFurnaces.Count; i++) furnaceDataArray[i] = activeFurnaces[i];

        // Gather vehicles
        var activeVehicles = VehicleController.GetCachedVehicles();
        int totalBounds = 0;
        for (int i = 0; i < activeVehicles.Count; i++)
        {
            if (activeVehicles[i].dryFilters != null)
                totalBounds += activeVehicles[i].dryFilters.Count;
        }

        var vehicleFilters = new NativeArray<Bounds>(totalBounds, Allocator.TempJob);
        var unmanagedVehicles = new NativeArray<UnmanagedVehicleData>(activeVehicles.Count, Allocator.TempJob);

        int boundIndex = 0;
        for (int i = 0; i < activeVehicles.Count; i++)
        {
            var av = activeVehicles[i];
            int start = boundIndex;
            int count = 0;
            if (av.dryFilters != null)
            {
                for (int j = 0; j < av.dryFilters.Count; j++)
                {
                    vehicleFilters[boundIndex++] = av.dryFilters[j];
                    count++;
                }
            }
            unmanagedVehicles[i] = new UnmanagedVehicleData
            {
                position = av.position,
                inverseRotation = av.inverseRotation,
                filterStart = start,
                filterCount = count
            };
        }

        var emptyMap = new NativeArray<byte>(0, Allocator.TempJob);

        var job = new MeshGenerationJob
        {
            chunkWorldPos = chunkWorldPos,
            scanHeight = VoxelData.ChunkHeight,
            totalTilesCount = BlockRegistry.TotalTilesCount,
            waterOnly = false,

            VoxelMap = voxelMap,
            WestMap = (neighborWest != null && neighborWest.voxelMap.IsCreated) ? neighborWest.voxelMap : emptyMap,
            EastMap = (neighborEast != null && neighborEast.voxelMap.IsCreated) ? neighborEast.voxelMap : emptyMap,
            SouthMap = (neighborSouth != null && neighborSouth.voxelMap.IsCreated) ? neighborSouth.voxelMap : emptyMap,
            NorthMap = (neighborNorth != null && neighborNorth.voxelMap.IsCreated) ? neighborNorth.voxelMap : emptyMap,

            NativeDefinitions = BlockRegistry.NativeDefinitions,
            CustomMeshVertices = BlockRegistry.CustomMeshVertices,
            CustomMeshIndices = BlockRegistry.CustomMeshIndices,
            CustomMeshUVs = BlockRegistry.CustomMeshUVs,

            VoxelVerts = BlockRegistry.VoxelVerts,
            VoxelTris = BlockRegistry.VoxelTris,
            FaceChecks = BlockRegistry.FaceChecks,

            Furnaces = furnaceDataArray,
            Vehicles = unmanagedVehicles,
            VehicleFilters = vehicleFilters,

            vertices = vertices,
            triangles = triangles,
            uvs = uvs,

            waterVertices = waterVertices,
            waterTriangles = waterTriangles,
            waterUvs = waterUvs,
            waterColors = waterColors,

            foliageVertices = foliageVertices,
            foliageTriangles = foliageTriangles,
            foliageUvs = foliageUvs,
            foliageColors = foliageColors,

            foliageSolidVertices = foliageSolidVertices,
            foliageSolidTriangles = foliageSolidTriangles,

            glassVertices = glassVertices,
            glassTriangles = glassTriangles,
            glassUvs = glassUvs
        };

        float jobRunStartTime = Time.realtimeSinceStartup;
        // Run synchronously
        job.Run();
        float jobRunEndTime = Time.realtimeSinceStartup;

        furnaceDataArray.Dispose();
        unmanagedVehicles.Dispose();
        vehicleFilters.Dispose();
        emptyMap.Dispose();

        RecalculateMaxVoxelHeight();

        float mainThreadStartTime = Time.realtimeSinceStartup;
        CreateMesh();
        float mainThreadEndTime = Time.realtimeSinceStartup;

        if (enableProfilingLogs)
        {
            float totalDurationMs = (mainThreadEndTime - syncStartTime) * 1000f;
            float jobDurationMs = (jobRunEndTime - jobRunStartTime) * 1000f;
            float meshDurationMs = (mainThreadEndTime - mainThreadStartTime) * 1000f;
            Debug.Log($"[Profiler] Sync Chunk ({chunkPos.x}, {chunkPos.y}) Rebuilt. Total time: {totalDurationMs:F2} ms (Job: {jobDurationMs:F2} ms, CreateMesh: {meshDurationMs:F2} ms).");
        }
    }

    void ClearMeshData()
    {
        // Clear NativeLists â€” retains their allocated capacity, avoiding per-build GC pressure.
        // EnsureNativeListsCreated() is called in Initialize(), so lists are always valid here.
        vertexIndex  = 0;
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

        foliageSolidVertexIndex = 0;
        foliageSolidVertices.Clear();
        foliageSolidTriangles.Clear();

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
        waterMeshRenderer.enabled = renderersEnabled;

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
        foliageMeshRenderer.enabled = renderersEnabled;

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

    /// <summary>Ignores physics collision between the foliage MeshColliders and all provided colliders.</summary>
    public void IgnoreFoliageCollisionWith(Collider[] others)
    {
        if (foliageMeshCollider != null)
        {
            foreach (var col in others)
                if (col != null) Physics.IgnoreCollision(col, foliageMeshCollider, true);
        }
        if (foliageSolidCollider != null)
        {
            foreach (var col in others)
                if (col != null) Physics.IgnoreCollision(col, foliageSolidCollider, true);
        }
    }

    public void IgnorePlayerCollision()
    {
        // 1. Trigger foliage collider (flowers, tall grass) - player and vehicles always ignore this
        if (foliageMeshCollider != null)
        {
            if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
            {
                var cc = VoxelWorld.Instance.playerTransform.GetComponent<CharacterController>();
                if (cc != null)
                    Physics.IgnoreCollision(cc, foliageMeshCollider, true);
            }

            foreach (var vc in Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None))
                IgnoreFoliageCollisionWith(vc.GetComponentsInChildren<Collider>());

            foreach (var sheep in Object.FindObjectsByType<SheepAI>(FindObjectsSortMode.None))
            {
                var animalCC = sheep.GetComponent<CharacterController>();
                if (animalCC != null) Physics.IgnoreCollision(animalCC, foliageMeshCollider, true);
            }
            foreach (var wolf in Object.FindObjectsByType<WolfAI>(FindObjectsSortMode.None))
            {
                var animalCC = wolf.GetComponent<CharacterController>();
                if (animalCC != null) Physics.IgnoreCollision(animalCC, foliageMeshCollider, true);
            }
        }

        // 2. Solid foliage collider (leaves, custom solid models) - vehicles always ignore it.
        // Player only ignores it when riding a vehicle!
        if (foliageSolidCollider != null)
        {
            foreach (var vc in Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None))
            {
                var colliders = vc.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                    if (col != null) Physics.IgnoreCollision(col, foliageSolidCollider, true);
            }

            bool playerIsRiding = (VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen);
            if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
            {
                var cc = VoxelWorld.Instance.playerTransform.GetComponent<CharacterController>();
                if (cc != null)
                    Physics.IgnoreCollision(cc, foliageSolidCollider, playerIsRiding);
            }

            // Animals do NOT ignore solid foliage (they can walk on leaves!)
            foreach (var sheep in Object.FindObjectsByType<SheepAI>(FindObjectsSortMode.None))
            {
                var animalCC = sheep.GetComponent<CharacterController>();
                if (animalCC != null) Physics.IgnoreCollision(animalCC, foliageSolidCollider, false);
            }
            foreach (var wolf in Object.FindObjectsByType<WolfAI>(FindObjectsSortMode.None))
            {
                var animalCC = wolf.GetComponent<CharacterController>();
                if (animalCC != null) Physics.IgnoreCollision(animalCC, foliageSolidCollider, false);
            }
        }
    }

    public void UpdatePlayerFoliageSolidCollision(bool ignore)
    {
        if (foliageSolidCollider == null) return;
        if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
        {
            var cc = VoxelWorld.Instance.playerTransform.GetComponent<CharacterController>();
            if (cc != null)
                Physics.IgnoreCollision(cc, foliageSolidCollider, ignore);
        }
    }

    void EnsureFoliageSolidChild()
    {
        Transform t = transform.Find("FoliageSolid");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("FoliageSolid");
            go.transform.SetParent(transform, false);
        }
        else
        {
            go = t.gameObject;
        }

        foliageSolidCollider = go.GetComponent<MeshCollider>();
        if (foliageSolidCollider == null) foliageSolidCollider = go.AddComponent<MeshCollider>();
        foliageSolidCollider.convex = false;
        foliageSolidCollider.isTrigger = false;
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
        glassMeshRenderer.enabled = renderersEnabled;

        if (VoxelWorld.Instance != null && VoxelWorld.Instance.glassMaterial != null)
            glassMeshRenderer.material = VoxelWorld.Instance.glassMaterial;
        else
            glassMeshRenderer.material = meshRenderer.material;
    }

    private void UploadMeshDirect(Mesh mesh, NativeList<Vector3> verts, NativeList<int> tris, NativeList<Vector2> uvs)
    {
        mesh.Clear();
        int vertexCount = verts.Length;
        if (vertexCount == 0) return;

        var layout = new[]
        {
            new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Position, UnityEngine.Rendering.VertexAttributeFormat.Float32, 3, stream: 0),
            new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.TexCoord0, UnityEngine.Rendering.VertexAttributeFormat.Float32, 2, stream: 1)
        };
        mesh.SetVertexBufferParams(vertexCount, layout);
        mesh.SetVertexBufferData(verts.AsArray(), 0, 0, vertexCount, 0, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
        mesh.SetVertexBufferData(uvs.AsArray(), 0, 0, vertexCount, 1, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        int indexCount = tris.Length;
        mesh.SetIndexBufferParams(indexCount, UnityEngine.Rendering.IndexFormat.UInt32);
        mesh.SetIndexBufferData(tris.AsArray(), 0, 0, indexCount, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        var subMesh = new UnityEngine.Rendering.SubMeshDescriptor(0, indexCount, MeshTopology.Triangles);
        subMesh.vertexCount = vertexCount;
        mesh.SetSubMesh(0, subMesh, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private void UploadMeshDirect(Mesh mesh, NativeList<Vector3> verts, NativeList<int> tris, NativeList<Vector2> uvs, NativeList<Color> colors)
    {
        mesh.Clear();
        int vertexCount = verts.Length;
        if (vertexCount == 0) return;

        var layout = new[]
        {
            new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Position, UnityEngine.Rendering.VertexAttributeFormat.Float32, 3, stream: 0),
            new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.TexCoord0, UnityEngine.Rendering.VertexAttributeFormat.Float32, 2, stream: 1),
            new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Color, UnityEngine.Rendering.VertexAttributeFormat.Float32, 4, stream: 2)
        };
        mesh.SetVertexBufferParams(vertexCount, layout);
        mesh.SetVertexBufferData(verts.AsArray(), 0, 0, vertexCount, 0, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
        mesh.SetVertexBufferData(uvs.AsArray(), 0, 0, vertexCount, 1, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
        mesh.SetVertexBufferData(colors.AsArray(), 0, 0, vertexCount, 2, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        int indexCount = tris.Length;
        mesh.SetIndexBufferParams(indexCount, UnityEngine.Rendering.IndexFormat.UInt32);
        mesh.SetIndexBufferData(tris.AsArray(), 0, 0, indexCount, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        var subMesh = new UnityEngine.Rendering.SubMeshDescriptor(0, indexCount, MeshTopology.Triangles);
        subMesh.vertexCount = vertexCount;
        mesh.SetSubMesh(0, subMesh, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private void UploadMeshDirect(Mesh mesh, NativeList<Vector3> verts, NativeList<int> tris)
    {
        mesh.Clear();
        int vertexCount = verts.Length;
        if (vertexCount == 0) return;

        var layout = new[]
        {
            new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Position, UnityEngine.Rendering.VertexAttributeFormat.Float32, 3, stream: 0)
        };
        mesh.SetVertexBufferParams(vertexCount, layout);
        mesh.SetVertexBufferData(verts.AsArray(), 0, 0, vertexCount, 0, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        int indexCount = tris.Length;
        mesh.SetIndexBufferParams(indexCount, UnityEngine.Rendering.IndexFormat.UInt32);
        mesh.SetIndexBufferData(tris.AsArray(), 0, 0, indexCount, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        var subMesh = new UnityEngine.Rendering.SubMeshDescriptor(0, indexCount, MeshTopology.Triangles);
        subMesh.vertexCount = vertexCount;
        mesh.SetSubMesh(0, subMesh, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    void CreateMesh()
    {
        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }

        UploadMeshDirect(mesh, vertices, triangles, uvs);

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;

        // ── Water mesh ────────────────────────────────────────────────────────
        if (waterVertices.Length > 0)
        {
            EnsureWaterChild();
            Mesh waterMesh = waterMeshFilter.sharedMesh;
            if (waterMesh == null)
            {
                waterMesh = new Mesh();
                waterMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                waterMeshFilter.sharedMesh = waterMesh;
            }
            UploadMeshDirect(waterMesh, waterVertices, waterTriangles, waterUvs, waterColors);
            waterMeshRenderer.gameObject.SetActive(true);
        }
        else
        {
            if (waterMeshRenderer != null)
                waterMeshRenderer.gameObject.SetActive(false);
        }

        // ── Foliage mesh (flowers) ─────────────────────────────────────────────
        if (foliageVertices.Length > 0)
        {
            EnsureFoliageChild();
            Mesh foliageMesh = foliageMeshFilter.sharedMesh;
            if (foliageMesh == null)
            {
                foliageMesh = new Mesh();
                foliageMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                foliageMeshFilter.sharedMesh = foliageMesh;
            }
            UploadMeshDirect(foliageMesh, foliageVertices, foliageTriangles, foliageUvs, foliageColors);

            foliageMeshCollider.sharedMesh = null;
            foliageMeshCollider.sharedMesh = foliageMesh; // allows raycast hits on flowers

            // Build the solid foliage collider if we have solid foliage geometry
            if (foliageSolidVertices.Length > 0)
            {
                EnsureFoliageSolidChild();
                Mesh foliageSolidMesh = foliageSolidCollider.sharedMesh;
                if (foliageSolidMesh == null)
                {
                    foliageSolidMesh = new Mesh();
                    foliageSolidMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    foliageSolidCollider.sharedMesh = foliageSolidMesh;
                }
                UploadMeshDirect(foliageSolidMesh, foliageSolidVertices, foliageSolidTriangles);

                foliageSolidCollider.sharedMesh = null;
                foliageSolidCollider.sharedMesh = foliageSolidMesh;
                foliageSolidCollider.gameObject.SetActive(true);
            }
            else
            {
                if (foliageSolidCollider != null)
                    foliageSolidCollider.gameObject.SetActive(false);
            }

            IgnorePlayerCollision(); // Set up proper collision and ignore behaviors for player, vehicles, and animals
            foliageMeshRenderer.gameObject.SetActive(true);
        }
        else
        {
            if (foliageMeshRenderer != null)
                foliageMeshRenderer.gameObject.SetActive(false);
            if (foliageSolidCollider != null)
                foliageSolidCollider.gameObject.SetActive(false);
        }

        // ── Glass mesh (ZWrite On + CullBack to prevent back-face border bleed) ─
        if (glassVertices.Length > 0)
        {
            EnsureGlassChild();
            Mesh glassMesh = glassMeshFilter.sharedMesh;
            if (glassMesh == null)
            {
                glassMesh = new Mesh();
                glassMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                glassMeshFilter.sharedMesh = glassMesh;
            }
            UploadMeshDirect(glassMesh, glassVertices, glassTriangles, glassUvs);

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
        waterVertices.Clear();
        waterTriangles.Clear();
        waterUvs.Clear();
        waterColors.Clear();

        // Furnaces are not needed for water-only meshing, but we can pass empty arrays
        var furnaceDataArray = new NativeArray<UnmanagedFurnaceData>(0, Allocator.TempJob);
        
        // Gather vehicles for water suppression
        var activeVehicles = VehicleController.GetCachedVehicles();
        int totalBounds = 0;
        for (int i = 0; i < activeVehicles.Count; i++)
        {
            if (activeVehicles[i].dryFilters != null)
                totalBounds += activeVehicles[i].dryFilters.Count;
        }

        var vehicleFilters = new NativeArray<Bounds>(totalBounds, Allocator.TempJob);
        var unmanagedVehicles = new NativeArray<UnmanagedVehicleData>(activeVehicles.Count, Allocator.TempJob);

        int boundIndex = 0;
        for (int i = 0; i < activeVehicles.Count; i++)
        {
            var av = activeVehicles[i];
            int start = boundIndex;
            int count = 0;
            if (av.dryFilters != null)
            {
                for (int j = 0; j < av.dryFilters.Count; j++)
                {
                    vehicleFilters[boundIndex++] = av.dryFilters[j];
                    count++;
                }
            }
            unmanagedVehicles[i] = new UnmanagedVehicleData
            {
                position = av.position,
                inverseRotation = av.inverseRotation,
                filterStart = start,
                filterCount = count
            };
        }

        var emptyMap = new NativeArray<byte>(0, Allocator.TempJob);

        var job = new MeshGenerationJob
        {
            chunkWorldPos = transform.position,
            scanHeight = VoxelData.ChunkHeight,
            totalTilesCount = BlockRegistry.TotalTilesCount,
            waterOnly = true,

            VoxelMap = voxelMap,
            WestMap = (neighborWest != null && neighborWest.voxelMap.IsCreated) ? neighborWest.voxelMap : emptyMap,
            EastMap = (neighborEast != null && neighborEast.voxelMap.IsCreated) ? neighborEast.voxelMap : emptyMap,
            SouthMap = (neighborSouth != null && neighborSouth.voxelMap.IsCreated) ? neighborSouth.voxelMap : emptyMap,
            NorthMap = (neighborNorth != null && neighborNorth.voxelMap.IsCreated) ? neighborNorth.voxelMap : emptyMap,

            NativeDefinitions = BlockRegistry.NativeDefinitions,
            CustomMeshVertices = BlockRegistry.CustomMeshVertices,
            CustomMeshIndices = BlockRegistry.CustomMeshIndices,
            CustomMeshUVs = BlockRegistry.CustomMeshUVs,

            VoxelVerts = BlockRegistry.VoxelVerts,
            VoxelTris = BlockRegistry.VoxelTris,
            FaceChecks = BlockRegistry.FaceChecks,

            Furnaces = furnaceDataArray,
            Vehicles = unmanagedVehicles,
            VehicleFilters = vehicleFilters,

            vertices = vertices,
            triangles = triangles,
            uvs = uvs,

            waterVertices = waterVertices,
            waterTriangles = waterTriangles,
            waterUvs = waterUvs,
            waterColors = waterColors,

            foliageVertices = foliageVertices,
            foliageTriangles = foliageTriangles,
            foliageUvs = foliageUvs,
            foliageColors = foliageColors,

            foliageSolidVertices = foliageSolidVertices,
            foliageSolidTriangles = foliageSolidTriangles,

            glassVertices = glassVertices,
            glassTriangles = glassTriangles,
            glassUvs = glassUvs
        };

        // Run synchronously since this is called on-demand to hide water instantly
        job.Run();

        furnaceDataArray.Dispose();
        unmanagedVehicles.Dispose();
        vehicleFilters.Dispose();
        emptyMap.Dispose();

        // Upload only the water mesh — no collider, no terrain touch
        EnsureWaterChild();
        if (waterVertices.Length > 0)
        {
            Mesh waterMesh = waterMeshFilter.sharedMesh;
            if (waterMesh == null)
            {
                waterMesh = new Mesh();
                waterMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                waterMeshFilter.sharedMesh = waterMesh;
            }
            UploadMeshDirect(waterMesh, waterVertices, waterTriangles, waterUvs, waterColors);
            waterMeshFilter.sharedMesh = waterMesh;
            waterMeshRenderer.gameObject.SetActive(true);
        }
        else
        {
            if (waterMeshRenderer != null)
                waterMeshRenderer.gameObject.SetActive(false);
        }
    }
}

public struct UnmanagedVehicleData
{
    public Vector3 position;
    public Quaternion inverseRotation;
    public int filterStart;
    public int filterCount;
}

public struct UnmanagedFurnaceData
{
    public int3 localPos;
    public bool isLit;
    public int facing;
}

// [ignoring loop detection]
[BurstCompile]
public struct MeshGenerationJob : IJob
{
    public Vector3 chunkWorldPos;
    public int scanHeight;
    public int totalTilesCount;
    public bool waterOnly;

    [Unity.Collections.ReadOnly]
    [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
    public NativeArray<byte> VoxelMap;
    [Unity.Collections.ReadOnly]
    [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
    public NativeArray<byte> WestMap;
    [Unity.Collections.ReadOnly]
    [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
    public NativeArray<byte> EastMap;
    [Unity.Collections.ReadOnly]
    [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
    public NativeArray<byte> SouthMap;
    [Unity.Collections.ReadOnly]
    [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
    public NativeArray<byte> NorthMap;

    [Unity.Collections.ReadOnly]
    public NativeArray<BlittableBlockDefinition> NativeDefinitions;
    [Unity.Collections.ReadOnly]
    public NativeArray<Vector3> CustomMeshVertices;
    [Unity.Collections.ReadOnly]
    public NativeArray<int> CustomMeshIndices;
    [Unity.Collections.ReadOnly]
    public NativeArray<Vector2> CustomMeshUVs;

    [Unity.Collections.ReadOnly]
    public NativeArray<Vector3> VoxelVerts;
    [Unity.Collections.ReadOnly]
    public NativeArray<int> VoxelTris;
    [Unity.Collections.ReadOnly]
    public NativeArray<Vector3> FaceChecks;

    [Unity.Collections.ReadOnly]
    public NativeArray<UnmanagedFurnaceData> Furnaces;
    [Unity.Collections.ReadOnly]
    public NativeArray<UnmanagedVehicleData> Vehicles;
    [Unity.Collections.ReadOnly]
    public NativeArray<Bounds> VehicleFilters;

    // Outputs
    public NativeList<Vector3> vertices;
    public NativeList<int> triangles;
    public NativeList<Vector2> uvs;

    public NativeList<Vector3> waterVertices;
    public NativeList<int> waterTriangles;
    public NativeList<Vector2> waterUvs;
    public NativeList<Color> waterColors;

    public NativeList<Vector3> foliageVertices;
    public NativeList<int> foliageTriangles;
    public NativeList<Vector2> foliageUvs;
    public NativeList<Color> foliageColors;

    public NativeList<Vector3> foliageSolidVertices;
    public NativeList<int> foliageSolidTriangles;

    public NativeList<Vector3> glassVertices;
    public NativeList<int> glassTriangles;
    public NativeList<Vector2> glassUvs;

    public void Execute()
    {
        if (waterOnly)
        {
            int scanHeightLimit = math.min(scanHeight, 40);
            for (int y = 0; y < scanHeightLimit; y++)
            {
                for (int x = 0; x < VoxelData.ChunkWidth; x++)
                {
                    for (int z = 0; z < VoxelData.ChunkWidth; z++)
                    {
                        byte val = VoxelMap[VoxelData.GetFlatIndex(x, y, z)];
                        if (val == 7) // Water only
                        {
                            UpdateVoxelMeshData(new Vector3(x, y, z), 7);
                        }
                    }
                }
            }
        }
        else
        {
            for (int y = 0; y < scanHeight; y++)
            {
                for (int x = 0; x < VoxelData.ChunkWidth; x++)
                {
                    for (int z = 0; z < VoxelData.ChunkWidth; z++)
                    {
                        byte val = VoxelMap[VoxelData.GetFlatIndex(x, y, z)];
                        if (val != 0)
                        {
                            UpdateVoxelMeshData(new Vector3(x, y, z), val);
                        }
                    }
                }
            }
        }
    }

    private void UpdateVoxelMeshData(Vector3 pos, byte blockType)
    {
        byte baseStairID = 0;
        if (blockType == 38 || blockType == 40 || blockType == 41 || blockType == 42)
        {
            baseStairID = 38;
        }
        else if (blockType == 39 || blockType == 43 || blockType == 44 || blockType == 45)
        {
            baseStairID = 39;
        }

        BlittableBlockDefinition def = default;
        bool hasDef = false;
        if (NativeDefinitions.IsCreated)
        {
            byte lookupID = baseStairID != 0 ? baseStairID : blockType;
            if (lookupID < NativeDefinitions.Length)
            {
                def = NativeDefinitions[lookupID];
                hasDef = true;
            }
        }

        if (hasDef && def.hasCustomMesh)
        {
            if (baseStairID != 0)
            {
                AddCustomStairMeshBlock(pos, def, blockType);
            }
            else
            {
                AddCustomMeshBlock(pos, def);
            }
            return;
        }

        // Flowers
        if (blockType == 9 || blockType == 10 || blockType == 11 || blockType == 13 || blockType == 14)
        {
            AddFlowerQuads(pos, blockType);
            return;
        }

        // Leaves
        if (blockType == 12 || blockType == 52 || blockType == 54)
        {
            AddLeavesBlock(pos, blockType);
            return;
        }

        // Glass
        if (blockType == 35)
        {
            AddGlassToSolidMesh(pos);
            return;
        }

        // Propeller
        if (blockType == 22)
        {
            AddPropellerBlock(pos);
            return;
        }

        // Large Propeller
        if (blockType == 26)
        {
            AddLargePropellerBlock(pos);
            return;
        }

        if (blockType == 27)
        {
            return;
        }

        // Stairs
        if (blockType == 38 || blockType == 40 || blockType == 41 || blockType == 42 ||
            blockType == 39 || blockType == 43 || blockType == 44 || blockType == 45)
        {
            AddStairsBlock(pos, blockType);
            return;
        }

        // Slab
        if (blockType == 46 || blockType == 47)
        {
            AddSlabBlock(pos, blockType);
            return;
        }

        bool isWater = (blockType == 7);

        // Dry interior check
        if (isWater)
        {
            Vector3 center = chunkWorldPos + pos + new Vector3(0.5f, 0.5f, 0.5f);
            bool insideVehicle = IsWorldPosInsideVehicleCachedBurst(center) ||
                                IsWorldPosInsideVehicleCachedBurst(center + new Vector3(-0.45f, 0f, -0.45f)) ||
                                IsWorldPosInsideVehicleCachedBurst(center + new Vector3(0.45f, 0f, -0.45f)) ||
                                IsWorldPosInsideVehicleCachedBurst(center + new Vector3(-0.45f, 0f, 0.45f)) ||
                                IsWorldPosInsideVehicleCachedBurst(center + new Vector3(0.45f, 0f, 0.45f));
            if (insideVehicle)
            {
                return;
            }
        }

        int depth = 1;
        bool isWaterSurface = false;
        if (isWater)
        {
            depth = GetWaterDepth((int)pos.x, (int)pos.y, (int)pos.z);
            byte blockAbove = GetVoxelFromNeighborOrWorld((int)pos.x, (int)pos.y + 1, (int)pos.z);
            isWaterSurface = (blockAbove != 7);
        }

        for (int p = 0; p < 6; p++)
        {
            if (!CheckVoxelFace(pos, p, blockType))
            {
                int baseIndex = isWater ? waterVertices.Length : vertices.Length;

                for (int i = 0; i < 4; i++)
                {
                    Vector3 vert = VoxelVerts[VoxelTris[p * 4 + i]];

                    if (isWater && isWaterSurface && vert.y > 0.5f)
                    {
                        vert.y = 0.85f;
                    }

                    if (isWater)
                    {
                        waterVertices.Add(pos + vert);
                    }
                    else
                    {
                        vertices.Add(pos + vert);
                    }
                }

                if (isWater)
                {
                    float alpha;
                    if (depth == 1) alpha = 0.85f;
                    else if (depth == 2) alpha = 0.92f;
                    else if (depth == 3) alpha = 0.96f;
                    else alpha = 0.99f;

                    for (int i = 0; i < 4; i++)
                    {
                        waterColors.Add(new Color(1f, 1f, 1f, alpha));
                    }

                    int tile = GetTileIndexNativeBurst(blockType, p, false, -1, NativeDefinitions);
                    float u0 = tile / (float)totalTilesCount;
                    float u1 = (tile + 1f) / (float)totalTilesCount;

                    waterUvs.Add(new Vector2(u0, 0f));
                    waterUvs.Add(new Vector2(u0, 1f));
                    waterUvs.Add(new Vector2(u1, 0f));
                    waterUvs.Add(new Vector2(u1, 1f));

                    waterTriangles.Add(baseIndex);
                    waterTriangles.Add(baseIndex + 1);
                    waterTriangles.Add(baseIndex + 2);
                    waterTriangles.Add(baseIndex + 2);
                    waterTriangles.Add(baseIndex + 1);
                    waterTriangles.Add(baseIndex + 3);
                }
                else
                {
                    bool isFurnaceLit = false;
                    int furnaceFacing = -1;
                    if (blockType == 37)
                    {
                        int3 localIntPos = new int3((int)pos.x, (int)pos.y, (int)pos.z);
                        for (int f = 0; f < Furnaces.Length; f++)
                        {
                            if (Furnaces[f].localPos.x == localIntPos.x &&
                                Furnaces[f].localPos.y == localIntPos.y &&
                                Furnaces[f].localPos.z == localIntPos.z)
                            {
                                isFurnaceLit = Furnaces[f].isLit;
                                furnaceFacing = Furnaces[f].facing;
                                break;
                            }
                        }
                    }

                    int tile = GetTileIndexNativeBurst(blockType, p, isFurnaceLit, furnaceFacing, NativeDefinitions);
                    float u0 = tile / (float)totalTilesCount;
                    float u1 = (tile + 1f) / (float)totalTilesCount;

                    uvs.Add(new Vector2(u0, 0f));
                    uvs.Add(new Vector2(u0, 1f));
                    uvs.Add(new Vector2(u1, 0f));
                    uvs.Add(new Vector2(u1, 1f));

                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 3);
                }
            }
        }
    }

    private int GetWaterDepth(int x, int y, int z)
    {
        int depth = 0;
        for (int dy = y; dy >= 0; dy--)
        {
            byte block = VoxelMap[VoxelData.GetFlatIndex(x, dy, z)];
            if (block == 7) // Water
            {
                depth++;
            }
            else if (block != 0) // Solid block
            {
                break;
            }
        }
        return math.max(1, depth);
    }

    private byte GetVoxelFromNeighborOrWorld(int x, int y, int z)
    {
        if (x >= 0 && x < VoxelData.ChunkWidth && y >= 0 && y < VoxelData.ChunkHeight && z >= 0 && z < VoxelData.ChunkWidth)
        {
            return VoxelMap[VoxelData.GetFlatIndex(x, y, z)];
        }

        if (y < 0 || y >= VoxelData.ChunkHeight)
        {
            return 0;
        }

        int targetX = x;
        int targetZ = z;
        NativeArray<byte> neighborMap = default;

        if (x < 0)
        {
            neighborMap = WestMap;
            targetX += VoxelData.ChunkWidth;
        }
        else if (x >= VoxelData.ChunkWidth)
        {
            neighborMap = EastMap;
            targetX -= VoxelData.ChunkWidth;
        }

        if (z < 0)
        {
            neighborMap = SouthMap;
            targetZ += VoxelData.ChunkWidth;
        }
        else if (z >= VoxelData.ChunkWidth)
        {
            neighborMap = NorthMap;
            targetZ -= VoxelData.ChunkWidth;
        }

        if (neighborMap.IsCreated && neighborMap.Length > 0)
        {
            return neighborMap[VoxelData.GetFlatIndex(targetX, y, targetZ)];
        }

        return 0;
    }

    private bool CheckVoxelFace(Vector3 pos, int faceIndex, byte currentBlockType)
    {
        Vector3 neighborPos = pos + FaceChecks[faceIndex];
        int x = (int)math.floor(neighborPos.x);
        int y = (int)math.floor(neighborPos.y);
        int z = (int)math.floor(neighborPos.z);

        byte neighbor = GetVoxelFromNeighborOrWorld(x, y, z);

        if (neighbor == 0) return false;

        bool currentIsWater  = (currentBlockType == 7);
        bool neighborIsWater = (neighbor == 7);

        bool neighborIsTransparentBlock = false;
        if (NativeDefinitions.IsCreated && neighbor < NativeDefinitions.Length)
        {
            neighborIsTransparentBlock = NativeDefinitions[neighbor].isTransparent;
        }

        bool neighborIsFlower = (neighbor == 9 || neighbor == 10 || neighbor == 11 || neighbor == 12 || neighbor == 35 ||
                                 neighbor == 13 || neighbor == 14 ||
                                 neighbor == 38 || neighbor == 40 || neighbor == 41 || neighbor == 42 ||
                                 neighbor == 39 || neighbor == 43 || neighbor == 44 || neighbor == 45 ||
                                 neighbor == 46 || neighbor == 47 || neighbor == 22 || neighbor == 26 || neighbor == 27 ||
                                 neighborIsTransparentBlock);

        if (currentIsWater)
        {
            return true;
        }
        else
        {
            if (neighborIsWater || neighborIsFlower) return false;
            return true;
        }
    }

    private bool IsWorldPosInsideVehicleCachedBurst(Vector3 worldPos)
    {
        for (int i = 0; i < Vehicles.Length; i++)
        {
            var cv = Vehicles[i];
            Vector3 localPos = cv.inverseRotation * (worldPos - cv.position);
            
            for (int j = 0; j < cv.filterCount; j++)
            {
                Bounds filter = VehicleFilters[cv.filterStart + j];
                if (filter.Contains(localPos))
                    return true;
            }
        }
        return false;
    }

    private void AddFlowerQuads(Vector3 pos, byte blockType)
    {
        int tile = GetTileIndexNativeBurst(blockType, 0, false, -1, NativeDefinitions);
        float u0 = tile / (float)totalTilesCount;
        float u1 = (tile + 1f) / (float)totalTilesCount;

        // Quad 1
        int startIdx1 = foliageVertices.Length;
        foliageVertices.Add(pos + new Vector3(0.05f, 0f, 0.05f));
        foliageVertices.Add(pos + new Vector3(0.05f, 1f, 0.05f));
        foliageVertices.Add(pos + new Vector3(0.95f, 0f, 0.95f));
        foliageVertices.Add(pos + new Vector3(0.95f, 1f, 0.95f));

        for (int i = 0; i < 4; i++) foliageColors.Add(Color.white);
        foliageUvs.Add(new Vector2(u0, 0f));
        foliageUvs.Add(new Vector2(u0, 1f));
        foliageUvs.Add(new Vector2(u1, 0f));
        foliageUvs.Add(new Vector2(u1, 1f));

        foliageTriangles.Add(startIdx1);
        foliageTriangles.Add(startIdx1 + 1);
        foliageTriangles.Add(startIdx1 + 2);
        foliageTriangles.Add(startIdx1 + 2);
        foliageTriangles.Add(startIdx1 + 1);
        foliageTriangles.Add(startIdx1 + 3);

        foliageTriangles.Add(startIdx1 + 2);
        foliageTriangles.Add(startIdx1 + 1);
        foliageTriangles.Add(startIdx1);
        foliageTriangles.Add(startIdx1 + 3);
        foliageTriangles.Add(startIdx1 + 1);
        foliageTriangles.Add(startIdx1 + 2);

        // Quad 2
        int startIdx2 = foliageVertices.Length;
        foliageVertices.Add(pos + new Vector3(0.95f, 0f, 0.05f));
        foliageVertices.Add(pos + new Vector3(0.95f, 1f, 0.05f));
        foliageVertices.Add(pos + new Vector3(0.05f, 0f, 0.95f));
        foliageVertices.Add(pos + new Vector3(0.05f, 1f, 0.95f));

        for (int i = 0; i < 4; i++) foliageColors.Add(Color.white);
        foliageUvs.Add(new Vector2(u0, 0f));
        foliageUvs.Add(new Vector2(u0, 1f));
        foliageUvs.Add(new Vector2(u1, 0f));
        foliageUvs.Add(new Vector2(u1, 1f));

        foliageTriangles.Add(startIdx2);
        foliageTriangles.Add(startIdx2 + 1);
        foliageTriangles.Add(startIdx2 + 2);
        foliageTriangles.Add(startIdx2 + 2);
        foliageTriangles.Add(startIdx2 + 1);
        foliageTriangles.Add(startIdx2 + 3);

        foliageTriangles.Add(startIdx2 + 2);
        foliageTriangles.Add(startIdx2 + 1);
        foliageTriangles.Add(startIdx2);
        foliageTriangles.Add(startIdx2 + 3);
        foliageTriangles.Add(startIdx2 + 1);
        foliageTriangles.Add(startIdx2 + 2);
    }

    private void AddLeavesBlock(Vector3 pos, byte blockType)
    {
        for (int p = 0; p < 6; p++)
        {
            Vector3 neighborPos = pos + FaceChecks[p];
            int nx = (int)math.floor(neighborPos.x);
            int ny = (int)math.floor(neighborPos.y);
            int nz = (int)math.floor(neighborPos.z);

            byte neighbor = GetVoxelFromNeighborOrWorld(nx, ny, nz);

            if (neighbor == 12 || neighbor == 52 || neighbor == 54) continue;
            if (neighbor != 0 && neighbor != 7 && neighbor != 9 &&
                neighbor != 10 && neighbor != 11 &&
                neighbor != 12 && neighbor != 52 && neighbor != 54) continue;

            int baseIndex = foliageVertices.Length;
            int baseSolidIndex = foliageSolidVertices.Length;

            for (int i = 0; i < 4; i++)
            {
                Vector3 vert = pos + VoxelVerts[VoxelTris[p * 4 + i]];
                foliageVertices.Add(vert);
                foliageSolidVertices.Add(vert);
                foliageColors.Add(Color.white);
            }

            int tile = GetTileIndexNativeBurst(blockType, p, false, -1, NativeDefinitions);
            float u0 = tile / (float)totalTilesCount;
            float u1 = (tile + 1f) / (float)totalTilesCount;

            foliageUvs.Add(new Vector2(u0, 0f));
            foliageUvs.Add(new Vector2(u0, 1f));
            foliageUvs.Add(new Vector2(u1, 0f));
            foliageUvs.Add(new Vector2(u1, 1f));

            foliageTriangles.Add(baseIndex);
            foliageTriangles.Add(baseIndex + 1);
            foliageTriangles.Add(baseIndex + 2);
            foliageTriangles.Add(baseIndex + 2);
            foliageTriangles.Add(baseIndex + 1);
            foliageTriangles.Add(baseIndex + 3);

            foliageSolidTriangles.Add(baseSolidIndex);
            foliageSolidTriangles.Add(baseSolidIndex + 1);
            foliageSolidTriangles.Add(baseSolidIndex + 2);
            foliageSolidTriangles.Add(baseSolidIndex + 2);
            foliageSolidTriangles.Add(baseSolidIndex + 1);
            foliageSolidTriangles.Add(baseSolidIndex + 3);
        }
    }

    private void AddGlassToSolidMesh(Vector3 pos)
    {
        float transU = 9f / (float)totalTilesCount;
        Vector2 transparentUV = new Vector2(transU, 0f);

        for (int p = 0; p < 6; p++)
        {
            Vector3 neighborPos = pos + FaceChecks[p];
            int nx = (int)math.floor(neighborPos.x);
            int ny = (int)math.floor(neighborPos.y);
            int nz = (int)math.floor(neighborPos.z);

            byte neighbor = GetVoxelFromNeighborOrWorld(nx, ny, nz);

            if (neighbor == 35) continue;
            if (neighbor != 0 && neighbor != 7 && neighbor != 9 &&
                neighbor != 10 && neighbor != 11 && neighbor != 12) continue;

            int baseIndex = glassVertices.Length;
            int baseSolidIndex = vertices.Length;

            for (int i = 0; i < 4; i++)
            {
                glassVertices.Add(pos + VoxelVerts[VoxelTris[p * 4 + i]]);
            }

            int tile = GetTileIndexNativeBurst(35, p, false, -1, NativeDefinitions);
            float u0 = tile / (float)totalTilesCount;
            float u1 = (tile + 1f) / (float)totalTilesCount;

            glassUvs.Add(new Vector2(u0, 0f));
            glassUvs.Add(new Vector2(u0, 1f));
            glassUvs.Add(new Vector2(u1, 0f));
            glassUvs.Add(new Vector2(u1, 1f));

            glassTriangles.Add(baseIndex);
            glassTriangles.Add(baseIndex + 1);
            glassTriangles.Add(baseIndex + 2);
            glassTriangles.Add(baseIndex + 2);
            glassTriangles.Add(baseIndex + 1);
            glassTriangles.Add(baseIndex + 3);

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(pos + VoxelVerts[VoxelTris[p * 4 + i]]);
                uvs.Add(transparentUV);
            }

            triangles.Add(baseSolidIndex);
            triangles.Add(baseSolidIndex + 1);
            triangles.Add(baseSolidIndex + 2);
            triangles.Add(baseSolidIndex + 2);
            triangles.Add(baseSolidIndex + 1);
            triangles.Add(baseSolidIndex + 3);
        }
    }

    private bool HasNeighborBlock(Vector3 pos, Vector3 direction)
    {
        Vector3 neighborPos = pos + direction;
        int nx = (int)math.floor(neighborPos.x);
        int ny = (int)math.floor(neighborPos.y);
        int nz = (int)math.floor(neighborPos.z);

        byte neighbor = GetVoxelFromNeighborOrWorld(nx, ny, nz);

        return neighbor != 0 && neighbor != 7 && neighbor != 9 && neighbor != 10 && neighbor != 11 && neighbor != 13 && neighbor != 14 && neighbor != 23 && neighbor != 27;
    }

    private void AddPropellerBlock(Vector3 pos)
    {
        bool hasFront = HasNeighborBlock(pos, Vector3.forward);
        bool hasBack  = HasNeighborBlock(pos, Vector3.back);
        bool hasLeft  = HasNeighborBlock(pos, Vector3.left);
        bool hasRight = HasNeighborBlock(pos, Vector3.right);
        bool hasBottom = HasNeighborBlock(pos, Vector3.down);
        bool hasTop    = HasNeighborBlock(pos, Vector3.up);

        Quaternion blockRotation = Quaternion.identity;
        if (hasFront && !hasBack) blockRotation = Quaternion.Euler(0f, 180f, 0f);
        else if (hasBack && !hasFront) blockRotation = Quaternion.identity;
        else if (hasRight && !hasLeft) blockRotation = Quaternion.Euler(0f, -90f, 0f);
        else if (hasLeft && !hasRight) blockRotation = Quaternion.Euler(0f, 90f, 0f);
        else if (hasBottom && !hasTop) blockRotation = Quaternion.Euler(-90f, 0f, 0f);
        else if (hasTop && !hasBottom) blockRotation = Quaternion.Euler(90f, 0f, 0f);
        else
        {
            if (hasBottom) blockRotation = Quaternion.Euler(-90f, 0f, 0f);
            else blockRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        Vector3 centerOffset = new Vector3(0.5f, 0.5f, 0.5f);

        Vector3 hubSize = new Vector3(0.21f, 0.21f, 0.8f);
        Vector3 hubLocalPos = blockRotation * new Vector3(0f, 0f, -0.1f) + centerOffset;
        AddSubBoxRotated(pos, hubLocalPos, hubSize, blockRotation, 24);

        Vector3 noseSize = new Vector3(0.15f, 0.15f, 0.15f);
        Vector3 noseLocalPos = blockRotation * new Vector3(0f, 0f, 0.3f) + centerOffset;
        AddSubBoxRotated(pos, noseLocalPos, noseSize, blockRotation, 24);

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

    private void AddLargePropellerBlock(Vector3 pos)
    {
        bool hasFront = HasNeighborBlock(pos, Vector3.forward);
        bool hasBack  = HasNeighborBlock(pos, Vector3.back);
        bool hasLeft  = HasNeighborBlock(pos, Vector3.left);
        bool hasRight = HasNeighborBlock(pos, Vector3.right);
        bool hasBottom = HasNeighborBlock(pos, Vector3.down);
        bool hasTop    = HasNeighborBlock(pos, Vector3.up);

        Quaternion blockRotation = Quaternion.identity;
        if (hasFront && !hasBack) blockRotation = Quaternion.Euler(0f, 180f, 0f);
        else if (hasBack && !hasFront) blockRotation = Quaternion.identity;
        else if (hasRight && !hasLeft) blockRotation = Quaternion.Euler(0f, -90f, 0f);
        else if (hasLeft && !hasRight) blockRotation = Quaternion.Euler(0f, 90f, 0f);
        else if (hasBottom && !hasTop) blockRotation = Quaternion.Euler(-90f, 0f, 0f);
        else if (hasTop && !hasBottom) blockRotation = Quaternion.Euler(90f, 0f, 0f);
        else
        {
            if (hasBottom) blockRotation = Quaternion.Euler(-90f, 0f, 0f);
            else blockRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        Vector3 centerOffset = new Vector3(0.5f, 0.5f, 0.5f);

        Vector3 hubSize = new Vector3(0.35f, 0.35f, 2.0f);
        Vector3 hubLocalPos = blockRotation * new Vector3(0f, 0f, 0.5f) + centerOffset;
        AddSubBoxRotated(pos, hubLocalPos, hubSize, blockRotation, 24);

        Vector3 noseSize = new Vector3(0.25f, 0.25f, 0.25f);
        Vector3 noseLocalPos = blockRotation * new Vector3(0f, 0f, 1.5f) + centerOffset;
        AddSubBoxRotated(pos, noseLocalPos, noseSize, blockRotation, 24);

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

    private void AddStairsBlock(Vector3 pos, byte blockType)
    {
        bool isWood = (blockType == 38 || blockType == 40 || blockType == 41 || blockType == 42);
        byte textureBlockType = isWood ? (byte)2 : (byte)3;

        AddSubBox(pos, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f), textureBlockType);

        Vector3 topMin = Vector3.zero;
        Vector3 topMax = Vector3.zero;

        if (blockType == 38 || blockType == 39)
        {
            topMin = new Vector3(0f, 0.5f, 0.5f);
            topMax = new Vector3(1f, 1f, 1f);
        }
        else if (blockType == 40 || blockType == 43)
        {
            topMin = new Vector3(0f, 0.5f, 0f);
            topMax = new Vector3(1f, 1f, 0.5f);
        }
        else if (blockType == 41 || blockType == 44)
        {
            topMin = new Vector3(0.5f, 0.5f, 0f);
            topMax = new Vector3(1f, 1f, 1f);
        }
        else if (blockType == 42 || blockType == 45)
        {
            topMin = new Vector3(0f, 0.5f, 0f);
            topMax = new Vector3(0.5f, 1f, 1f);
        }

        AddSubBox(pos, topMin, topMax, textureBlockType);
    }

    private void AddSlabBlock(Vector3 pos, byte blockType)
    {
        byte textureBlockType = (blockType == 46) ? (byte)2 : (byte)3;
        AddSubBox(pos, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f), textureBlockType);
    }

    private void AddSubBox(Vector3 pos, Vector3 min, Vector3 max, byte textureBlockType)
    {
        for (int p = 0; p < 6; p++)
        {
            int baseIndex = vertices.Length;
            WriteBoxFaceVertices(p, min, max, ref vertices, pos, Quaternion.identity, Vector3.zero);

            int tile = GetTileIndexNativeBurst(textureBlockType, p, false, -1, NativeDefinitions);
            float u0 = tile / (float)totalTilesCount;
            float u1 = (tile + 1f) / (float)totalTilesCount;

            uvs.Add(new Vector2(u0, 0f));
            uvs.Add(new Vector2(u0, 1f));
            uvs.Add(new Vector2(u1, 0f));
            uvs.Add(new Vector2(u1, 1f));

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 3);
        }
    }

    private void AddSubBoxRotated(Vector3 pos, Vector3 localCenter, Vector3 size, Quaternion rotation, byte textureBlockType)
    {
        Vector3 min = -size * 0.5f;
        Vector3 max = size * 0.5f;

        for (int p = 0; p < 6; p++)
        {
            int baseIndex = vertices.Length;
            WriteBoxFaceVertices(p, min, max, ref vertices, pos, rotation, localCenter);

            int tile = GetTileIndexNativeBurst(textureBlockType, p, false, -1, NativeDefinitions);
            float u0 = tile / (float)totalTilesCount;
            float u1 = (tile + 1f) / (float)totalTilesCount;

            uvs.Add(new Vector2(u0, 0f));
            uvs.Add(new Vector2(u0, 1f));
            uvs.Add(new Vector2(u1, 0f));
            uvs.Add(new Vector2(u1, 1f));

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 3);
        }
    }

    private void WriteBoxFaceVertices(int face, Vector3 min, Vector3 max, ref NativeList<Vector3> targetList, Vector3 pos, Quaternion rotation, Vector3 localCenter)
    {
        Vector3 v0 = default, v1 = default, v2 = default, v3 = default;
        switch (face)
        {
            case 0: // Back
                v0 = new Vector3(min.x, min.y, min.z);
                v1 = new Vector3(min.x, max.y, min.z);
                v2 = new Vector3(max.x, min.y, min.z);
                v3 = new Vector3(max.x, max.y, min.z);
                break;
            case 1: // Front
                v0 = new Vector3(max.x, min.y, max.z);
                v1 = new Vector3(max.x, max.y, max.z);
                v2 = new Vector3(min.x, min.y, max.z);
                v3 = new Vector3(min.x, max.y, max.z);
                break;
            case 2: // Top
                v0 = new Vector3(min.x, max.y, min.z);
                v1 = new Vector3(min.x, max.y, max.z);
                v2 = new Vector3(max.x, max.y, min.z);
                v3 = new Vector3(max.x, max.y, max.z);
                break;
            case 3: // Bottom
                v0 = new Vector3(max.x, min.y, min.z);
                v1 = new Vector3(max.x, min.y, max.z);
                v2 = new Vector3(min.x, min.y, min.z);
                v3 = new Vector3(min.x, min.y, max.z);
                break;
            case 4: // Left
                v0 = new Vector3(min.x, min.y, max.z);
                v1 = new Vector3(min.x, max.y, max.z);
                v2 = new Vector3(min.x, min.y, min.z);
                v3 = new Vector3(min.x, max.y, min.z);
                break;
            case 5: // Right
                v0 = new Vector3(max.x, min.y, min.z);
                v1 = new Vector3(max.x, max.y, min.z);
                v2 = new Vector3(max.x, min.y, max.z);
                v3 = new Vector3(max.x, max.y, max.z);
                break;
        }

        if (rotation.Equals(Quaternion.identity) && localCenter.Equals(Vector3.zero))
        {
            targetList.Add(pos + v0);
            targetList.Add(pos + v1);
            targetList.Add(pos + v2);
            targetList.Add(pos + v3);
        }
        else
        {
            targetList.Add(pos + (rotation * v0 + localCenter));
            targetList.Add(pos + (rotation * v1 + localCenter));
            targetList.Add(pos + (rotation * v2 + localCenter));
            targetList.Add(pos + (rotation * v3 + localCenter));
        }
    }

    private void AddCustomStairMeshBlock(Vector3 pos, BlittableBlockDefinition def, byte blockType)
    {
        if (!def.hasCustomMesh) return;

        float angle = 0f;
        if (blockType == 40 || blockType == 43) angle = 180f;
        else if (blockType == 41 || blockType == 44) angle = 90f;
        else if (blockType == 42 || blockType == 45) angle = 270f;

        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        Vector3 pivot = new Vector3(0.5f, 0f, 0.5f);

        int tile = def.tileLeft;
        float u0 = tile / (float)totalTilesCount;
        float u1 = (tile + 1f) / (float)totalTilesCount;

        int vertCount = def.customMeshVertexCount;
        int triCount = def.customMeshIndexCount;
        int vertStart = def.customMeshVertexStart;
        int triStart = def.customMeshIndexStart;

        if (def.isTransparent)
        {
            int startVert = foliageVertices.Length;
            for (int i = 0; i < vertCount; i++)
            {
                Vector3 localVert = CustomMeshVertices[vertStart + i];
                Vector3 rotatedVert = rot * (localVert - pivot) + pivot;
                foliageVertices.Add(pos + rotatedVert);

                Vector2 origUV = CustomMeshUVs[vertStart + i];
                float u = math.lerp(u0, u1, origUV.x);
                foliageUvs.Add(new Vector2(u, origUV.y));
                foliageColors.Add(Color.white);
            }
            for (int i = 0; i < triCount; i++)
            {
                foliageTriangles.Add(startVert + CustomMeshIndices[triStart + i]);
            }

            if (def.isSolid)
            {
                int startSolidVert = foliageSolidVertices.Length;
                for (int i = 0; i < vertCount; i++)
                {
                    Vector3 localVert = CustomMeshVertices[vertStart + i];
                    Vector3 rotatedVert = rot * (localVert - pivot) + pivot;
                    foliageSolidVertices.Add(pos + rotatedVert);
                }
                for (int i = 0; i < triCount; i++)
                {
                    foliageSolidTriangles.Add(startSolidVert + CustomMeshIndices[triStart + i]);
                }
            }
        }
        else
        {
            int startVert = vertices.Length;
            for (int i = 0; i < vertCount; i++)
            {
                Vector3 localVert = CustomMeshVertices[vertStart + i];
                Vector3 rotatedVert = rot * (localVert - pivot) + pivot;
                vertices.Add(pos + rotatedVert);

                Vector2 origUV = CustomMeshUVs[vertStart + i];
                float u = math.lerp(u0, u1, origUV.x);
                uvs.Add(new Vector2(u, origUV.y));
            }
            for (int i = 0; i < triCount; i++)
            {
                triangles.Add(startVert + CustomMeshIndices[triStart + i]);
            }
        }
    }

    private void AddCustomMeshBlock(Vector3 pos, BlittableBlockDefinition def)
    {
        if (!def.hasCustomMesh) return;

        int tile = def.tileLeft;
        float u0 = tile / (float)totalTilesCount;
        float u1 = (tile + 1f) / (float)totalTilesCount;

        int vertCount = def.customMeshVertexCount;
        int triCount = def.customMeshIndexCount;
        int vertStart = def.customMeshVertexStart;
        int triStart = def.customMeshIndexStart;

        if (def.isTransparent)
        {
            int startVert = foliageVertices.Length;
            for (int i = 0; i < vertCount; i++)
            {
                foliageVertices.Add(pos + CustomMeshVertices[vertStart + i]);
                Vector2 origUV = CustomMeshUVs[vertStart + i];
                float u = math.lerp(u0, u1, origUV.x);
                foliageUvs.Add(new Vector2(u, origUV.y));
                foliageColors.Add(Color.white);
            }
            for (int i = 0; i < triCount; i++)
            {
                foliageTriangles.Add(startVert + CustomMeshIndices[triStart + i]);
            }

            if (def.isSolid)
            {
                int startSolidVert = foliageSolidVertices.Length;
                for (int i = 0; i < vertCount; i++)
                {
                    foliageSolidVertices.Add(pos + CustomMeshVertices[vertStart + i]);
                }
                for (int i = 0; i < triCount; i++)
                {
                    foliageSolidTriangles.Add(startSolidVert + CustomMeshIndices[triStart + i]);
                }
            }
        }
        else
        {
            int startVert = vertices.Length;
            for (int i = 0; i < vertCount; i++)
            {
                vertices.Add(pos + CustomMeshVertices[vertStart + i]);
                Vector2 origUV = CustomMeshUVs[vertStart + i];
                float u = math.lerp(u0, u1, origUV.x);
                uvs.Add(new Vector2(u, origUV.y));
            }
            for (int i = 0; i < triCount; i++)
            {
                triangles.Add(startVert + CustomMeshIndices[triStart + i]);
            }
        }
    }

    private int GetTileIndexNativeBurst(byte blockID, int face, bool isLit, int facing, NativeArray<BlittableBlockDefinition> nativeDefs)
    {
        if (nativeDefs.IsCreated && blockID < nativeDefs.Length)
        {
            var def = nativeDefs[blockID];
            if (face == 2) return def.tileTop;
            if (face == 3) return def.tileBottom;

            bool isFront = (facing != -1) ? (face == facing) : (blockID == 37 ? face == 0 : face == 1);
            if (isFront)
            {
                return isLit ? def.tileFrontLit : def.tileFront;
            }
            return def.tileLeft;
        }
        return GetDefaultTileIndexBurst(blockID, face, isLit, facing);
    }

    private int GetDefaultTileIndexBurst(byte blockID, int face, bool isLit, int facing)
    {
        if (blockID == 1) return (face == 2 || face == 3) ? 4 : 5;
        if (blockID == 2) return 6;
        if (blockID == 3) return 3;
        if (blockID == 5) return 2;
        if (blockID == 7) return 7;
        if (blockID == 8 || blockID == 34) return 8;
        if (blockID == 9) return 9;
        if (blockID == 10) return 10;
        if (blockID == 11) return 11;
        if (blockID == 12) return 12;
        if (blockID == 13) return 27;
        if (blockID == 14) return 28;
        if (blockID == 20) return (face == 4 || face == 5) ? 16 : 15;
        if (blockID == 21 || blockID == 23) return (face == 4 || face == 5) ? 17 : 15;
        if (blockID == 22 || blockID == 26) return (face == 0 || face == 1) ? 29 : 30;
        if (blockID == 24) return 30;
        if (blockID == 25) return 31;
        if (blockID == 50) return (face == 1) ? 14 : 13;
        if (blockID == 30) return 18;
        if (blockID == 31) return 19;
        if (blockID == 32) return 20;
        if (blockID == 33) return 21;
        if (blockID == 35) return 22;
        if (blockID == 36) return (face == 2) ? 23 : (face == 3) ? 6 : 24;
        if (blockID == 37)
        {
            if (face == 2 || face == 3) return 3;
            bool isFront = (facing != -1) ? (face == facing) : (face == 0);
            if (isFront) return isLit ? 26 : 25;
            return 3;
        }
        if (blockID == 38 || blockID == 40 || blockID == 41 || blockID == 42) return 6;
        if (blockID == 39 || blockID == 43 || blockID == 44 || blockID == 45) return 3;
        if (blockID == 46) return 6;
        if (blockID == 47) return 3;
        if (blockID == 48) return 32;
        if (blockID == 49) return 33;
        if (blockID == 51) return (face == 2 || face == 3) ? 35 : 34;
        if (blockID == 52) return 36;
        if (blockID == 53) return (face == 2 || face == 3) ? 38 : 37;
        if (blockID == 54) return 39;
        if (blockID == 55) return 40;
        if (blockID == 57) return 41;
        if (blockID == 56) return 3;

        return (face == 2) ? 0 : (face == 3) ? 2 : 1;
    }
}
