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

                    // ── River Carving (Realistic Meandering & Dynamic Width) ──
                    float seaLevel = 14f;
                    
                    // Domain warp using noise to bend/wiggle the river coordinates
                    float warpX = Mathf.PerlinNoise(globalX * 0.015f + 10f, globalZ * 0.015f + 20f) * 60f;
                    float warpZ = Mathf.PerlinNoise(globalX * 0.015f + 30f, globalZ * 0.015f + 40f) * 60f;
                    
                    float riverNoise = Mathf.PerlinNoise((globalX + warpX) * 0.006f + 400f, (globalZ + warpZ) * 0.006f + 800f);
                    float riverCenterDist = Mathf.Abs(riverNoise - 0.5f);
                    
                    // Dynamic width variation
                    float widthNoise = Mathf.PerlinNoise(globalX * 0.01f + 150f, globalZ * 0.01f + 250f);
                    float riverWidth = 0.04f + widthNoise * 0.04f; // ranges from 0.04 to 0.08
                    
                    if (riverCenterDist < riverWidth)
                    {
                        float riverFactor = Mathf.Clamp01(riverCenterDist / riverWidth);
                        // S-curve interpolation for flatter riverbed and sloped banks
                        riverFactor = Mathf.SmoothStep(0f, 1f, riverFactor);
                        
                        float riverDepth = 3.5f; // slightly deeper river
                        float riverbedHeight = seaLevel - riverDepth;
                        
                        float carvedHeight = Mathf.Lerp(riverbedHeight, exactHeight, riverFactor);
                        exactHeight = Mathf.Min(exactHeight, carvedHeight);
                    }

                    int floorY = Mathf.FloorToInt(exactHeight);
                    bool isHalfBlock = (exactHeight - floorY) < 0.5f;

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
                            voxelMap[x, y, z] = isHalfBlock ? (byte)6 : (byte)4; // Grass Slab or Grass Full
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
                            voxelMap[x, y, z] = 9; // Flower
                        }
                    }
                }
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

    public void IgnorePlayerCollision()
    {
        if (foliageMeshCollider == null) return;
        if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
        {
            var cc = VoxelWorld.Instance.playerTransform.GetComponent<CharacterController>();
            if (cc != null)
            {
                Physics.IgnoreCollision(cc, foliageMeshCollider, true);
            }
        }
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
        if (blockType == 9)
        {
            AddFlowerQuads(pos);
            return;
        }

        bool isSlab  = (blockType == 6);
        bool isWater = (blockType == 7);
        byte uvBlockType = isSlab ? (byte)4 : blockType; // Use grass texture for slab

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
                    
                    // Squash the top vertices down if this is a slab or water
                    if (isSlab && vert.y > 0.5f)
                    {
                        vert.y = 0.5f;
                    }
                    else if (isWater && vert.y > 0.5f)
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
    void AddFlowerQuads(Vector3 pos)
    {
        Vector2[] uvs9 = GrassTextureGenerator.GetBlockUVs(0, 9); // flower tile UVs

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
        // Flowers are transparent billboards — treat like air for culling purposes
        bool neighborIsFlower = (neighbor == 9);

        if (currentIsWater)
        {
            // Water culls against solid blocks and other water
            return true;
        }
        else
        {
            // Solid blocks cull against solid blocks, but NOT water, air, or flowers
            if (neighborIsWater || neighborIsFlower) return false;

            // If neighbor is a full block, it culls our face
            if (neighbor != 6) return true;

            // If neighbor is a slab (ID = 6):
            // If we are looking UP at a slab above us (faceIndex = 2), its bottom rests on our top, culling it.
            if (faceIndex == 2) return true;

            // If we are a slab and neighbor is a slab, side faces match perfectly, culling each other.
            if (currentBlockType == 6 && (faceIndex == 0 || faceIndex == 1 || faceIndex == 4 || faceIndex == 5)) return true;

            // Otherwise (e.g. looking DOWN at a slab below us), don't cull.
            return false;
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
}
