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

    public void Initialize(Vector2 pos, Material mat)
    {
        chunkPos = pos;
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = mat;

        PopulateVoxelMap();
        UpdateChunk();
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
                    int floorY = Mathf.FloorToInt(exactHeight);
                    bool isHalfBlock = (exactHeight - floorY) < 0.5f;

                    if      (y > floorY)         voxelMap[x, y, z] = 0; // Air
                    else if (y == floorY)        voxelMap[x, y, z] = isHalfBlock ? (byte)3 : (byte)1; // Grass Slab (3) or Grass Full (1)
                    else if (y >= floorY - 4)    voxelMap[x, y, z] = 2; // Dirt (3 layers)
                    else                         voxelMap[x, y, z] = 2; // Dirt (deep)
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
    }

    void UpdateVoxelMeshData(Vector3 pos, byte blockType)
    {
        bool isSlab = (blockType == 3);
        byte uvBlockType = isSlab ? (byte)1 : blockType; // Use grass texture for slab

        for (int p = 0; p < 6; p++)
        {
            if (!CheckVoxelFace(pos, p, blockType))
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 vert = VoxelData.voxelVerts[VoxelData.voxelTris[p, i]];
                    
                    // Squash the top vertices down if this is a slab
                    if (isSlab && vert.y > 0.5f)
                    {
                        vert.y = 0.5f;
                    }
                    
                    vertices.Add(pos + vert);
                }

                // Per-face atlas UVs based on block type
                Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, uvBlockType);
                uvs.Add(faceUVs[0]);
                uvs.Add(faceUVs[1]);
                uvs.Add(faceUVs[2]);
                uvs.Add(faceUVs[3]);

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

        // If neighbor is a full block, it culls our face
        if (neighbor != 3) return true;

        // If neighbor is a slab (ID = 3):
        // If we are looking UP at a slab above us (faceIndex = 2), its bottom rests on our top, culling it.
        if (faceIndex == 2) return true;

        // If we are a slab and neighbor is a slab, side faces match perfectly, culling each other.
        if (currentBlockType == 3 && (faceIndex == 0 || faceIndex == 1 || faceIndex == 4 || faceIndex == 5)) return true;

        // Otherwise (e.g. looking DOWN at a slab below us), don't cull.
        return false;
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
    }
}
