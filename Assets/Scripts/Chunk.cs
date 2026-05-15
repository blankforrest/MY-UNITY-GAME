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

                    float noise = Mathf.PerlinNoise(globalX * 0.05f, globalZ * 0.05f);
                    int height = Mathf.FloorToInt(noise * VoxelData.ChunkHeight / 2)
                               + (VoxelData.ChunkHeight / 4);

                    if      (y >= height)        voxelMap[x, y, z] = 0; // Air
                    else if (y == height - 1)    voxelMap[x, y, z] = 1; // Grass (top)
                    else if (y >= height - 4)    voxelMap[x, y, z] = 2; // Dirt (3 layers)
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
        for (int p = 0; p < 6; p++)
        {
            if (!CheckVoxel(pos + VoxelData.faceChecks[p]))
            {
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 0]]);
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 1]]);
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 2]]);
                vertices.Add(pos + VoxelData.voxelVerts[VoxelData.voxelTris[p, 3]]);

                // Per-face atlas UVs based on block type
                Vector2[] faceUVs = GrassTextureGenerator.GetBlockUVs(p, blockType);
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

    bool CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (!IsVoxelInChunk(x, y, z))
        {
            if (VoxelWorld.Instance != null)
                return VoxelWorld.Instance.GetBlock(pos + transform.position) != 0;
            return false;
        }

        return voxelMap[x, y, z] != 0;
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
