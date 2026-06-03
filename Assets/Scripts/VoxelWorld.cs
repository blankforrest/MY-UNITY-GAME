using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld Instance { get; private set; }

    public Material chunkMaterial;
    public Material waterMaterial;

    [Header("Streaming")]
    [Range(3, 16)]
    public int renderDistance = 6;   // chunks radius around player
    public Transform playerTransform; // assign in Inspector OR auto-found by tag

    private Dictionary<Vector2, Chunk> chunks     = new Dictionary<Vector2, Chunk>();
    private Queue<Vector2>             genQueue   = new Queue<Vector2>();
    private Vector2                    lastChunk  = new Vector2(int.MinValue, 0);
    private ChunkCuller                culler;

    [Header("Block Drops")]
    [Tooltip("Index = block ID. Assign Item asset for each droppable block.")]
    public Item[] blockDrops;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Auto-find material
        if (chunkMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            chunkMaterial = new Material(s);
        }

        // Apply procedurally generated grass texture atlas
        Texture2D grassAtlas = GrassTextureGenerator.Create();
        chunkMaterial.mainTexture = grassAtlas;
        chunkMaterial.color       = Color.white; // don't tint the texture

        // Initialize water material if not assigned
        if (waterMaterial == null)
        {
            // Try URP Particle shaders first as they are specifically built for vertex-colored transparency in URP
            Shader waterShader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit") ?? 
                                 Shader.Find("Universal Render Pipeline/Particles/Unlit");
            
            if (waterShader != null)
            {
                waterMaterial = new Material(waterShader);
                waterMaterial.SetFloat("_Surface", 1f); // Transparent
                waterMaterial.SetInt("_Blend", 0); // Alpha Blend
                waterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                waterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                waterMaterial.SetInt("_ZWrite", 0);
                waterMaterial.SetInt("_Cull", 0); // Render both sides (visible from underwater)
                waterMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                // Fallback to standard URP Lit with runtime transparency setup
                waterShader = Shader.Find("Universal Render Pipeline/Lit");
                if (waterShader != null)
                {
                    waterMaterial = new Material(waterShader);
                    waterMaterial.SetFloat("_Surface", 1f); // Transparent
                    waterMaterial.SetFloat("_Blend", 0f); // Alpha blend
                    waterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    waterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    waterMaterial.SetInt("_ZWrite", 0);
                    waterMaterial.SetInt("_Cull", 0); // Render both sides
                    waterMaterial.DisableKeyword("_ALPHATEST_ON");
                    waterMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    waterMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    waterMaterial.SetFloat("_Smoothness", 0.85f);
                }
                else
                {
                    // Built-in Render Pipeline fallback
                    waterShader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? 
                                  Shader.Find("Particles/Standard Unlit");
                    if (waterShader != null)
                    {
                        waterMaterial = new Material(waterShader);
                    }
                    else
                    {
                        waterShader = Shader.Find("Standard");
                        if (waterShader != null)
                        {
                            waterMaterial = new Material(waterShader);
                            waterMaterial.SetFloat("_Mode", 3f); // Transparent
                            waterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            waterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            waterMaterial.SetInt("_ZWrite", 0);
                            waterMaterial.DisableKeyword("_ALPHATEST_ON");
                            waterMaterial.EnableKeyword("_ALPHABLEND_ON");
                            waterMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            waterMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                            waterMaterial.SetFloat("_Glossiness", 0.85f);
                        }
                        else
                        {
                            waterMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default"));
                        }
                    }
                }
            }
        }
        waterMaterial.mainTexture = grassAtlas;
        waterMaterial.color       = Color.white;
        waterMaterial.SetInt("_Cull", 0); // Always ensure double-sided regardless of which shader was chosen

        // Auto-find player
        if (playerTransform == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go) playerTransform = go.transform;
        }

        culler = FindFirstObjectByType<ChunkCuller>();

        // Synchronously generate a small area so the player doesn't fall through
        Vector2 start = PlayerChunkCoord();
        for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
                CreateChunk(start + new Vector2(x, z));

        lastChunk = start;
        StartCoroutine(ChunkStreamLoop());
    }

    void Update()
    {
        if (playerTransform == null) return;

        Vector2 current = PlayerChunkCoord();
        if (current == lastChunk) return;

        lastChunk = current;
        EnqueueChunksAround(current);
        UnloadDistant(current);
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    Vector2 PlayerChunkCoord()
    {
        Vector3 p = playerTransform != null ? playerTransform.position : Vector3.zero;
        return new Vector2(
            Mathf.FloorToInt(p.x / VoxelData.ChunkWidth),
            Mathf.FloorToInt(p.z / VoxelData.ChunkWidth));
    }

    void EnqueueChunksAround(Vector2 center)
    {
        // Collect missing chunks sorted by distance (closest first)
        var needed = new List<(float d, Vector2 pos)>();
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                if (x * x + z * z > renderDistance * renderDistance) continue; // circle
                Vector2 pos = center + new Vector2(x, z);
                if (!chunks.ContainsKey(pos))
                    needed.Add((x * x + z * z, pos));
            }
        }
        needed.Sort((a, b) => a.d.CompareTo(b.d));

        genQueue.Clear();
        foreach (var n in needed) genQueue.Enqueue(n.pos);
    }

    void UnloadDistant(Vector2 center)
    {
        int limit = renderDistance + 3; // small buffer before unloading
        var toRemove = new List<Vector2>();
        foreach (var kvp in chunks)
            if (Vector2.Distance(kvp.Key, center) > limit)
                toRemove.Add(kvp.Key);

        foreach (var pos in toRemove)
        {
            Destroy(chunks[pos].gameObject);
            chunks.Remove(pos);
        }

        if (toRemove.Count > 0)
            culler?.Refresh();
    }

    IEnumerator ChunkStreamLoop()
    {
        while (true)
        {
            if (genQueue.Count > 0)
            {
                Vector2 pos = genQueue.Dequeue();
                if (!chunks.ContainsKey(pos))
                {
                    CreateChunk(pos);
                    culler?.Refresh();
                }
                yield return null; // one chunk per frame — smooth, no stutter
            }
            else
            {
                yield return new WaitForSeconds(0.05f);
            }
        }
    }

    // ── Chunk management ──────────────────────────────────────────────────────

    void CreateChunk(Vector2 pos)
    {
        if (chunks.ContainsKey(pos)) return;

        GameObject go = new GameObject($"Chunk {pos.x},{pos.y}");
        go.transform.position = new Vector3(pos.x * VoxelData.ChunkWidth, 0,
                                             pos.y * VoxelData.ChunkWidth);
        go.transform.parent = transform;

        Chunk c = go.AddComponent<Chunk>();
        chunks[pos] = c;
        c.Initialize(pos, chunkMaterial);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public Chunk GetChunkFromChunkPos(Vector2 pos)
    {
        chunks.TryGetValue(pos, out Chunk c);
        return c;
    }

    public Chunk GetChunkFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
        chunks.TryGetValue(new Vector2(x, z), out Chunk c);
        return c;
    }

    public byte GetBlock(Vector3 pos)
    {
        Chunk chunk = GetChunkFromVector3(pos);
        if (chunk == null) return 0;
        Vector3 local = pos - chunk.transform.position;
        return chunk.GetVoxel(
            Mathf.FloorToInt(local.x),
            Mathf.FloorToInt(local.y),
            Mathf.FloorToInt(local.z));
    }

    public void ModifyBlock(Vector3 pos, byte blockID)
    {
        Chunk chunk = GetChunkFromVector3(pos);
        if (chunk == null) return;

        Vector3 local = pos - chunk.transform.position;
        int lx = Mathf.FloorToInt(local.x);
        int ly = Mathf.FloorToInt(local.y);
        int lz = Mathf.FloorToInt(local.z);

        // Spawn drop when breaking — skip vehicle/special blocks (ID >= 10)
        if (blockID == 0)
        {
            byte existing = chunk.GetVoxel(lx, ly, lz);
            if (existing != 0 && existing < 10)
            {
                Item drop = null;
                if (blockDrops != null && existing < blockDrops.Length)
                {
                    drop = blockDrops[existing];
                }

                // Robust fallback for Wood (1), Plank (2), Stone (3)
                if (drop == null)
                {
                    if (existing == 1)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Wood";
                        drop.blockTypeID = 1;
                        drop.icon = Resources.Load<Sprite>("Sprites/wood_block");
                    }
                    else if (existing == 2)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Plank";
                        drop.blockTypeID = 2;
                        drop.icon = Resources.Load<Sprite>("Sprites/plank_block");
                    }
                    else if (existing == 3)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Stone";
                        drop.blockTypeID = 3;
                        drop.icon = Resources.Load<Sprite>("Sprites/stone_block");
                    }
                    else if (existing == 8)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Sand";
                        drop.blockTypeID = 8;
                        drop.icon = StarterItems.MakeBlockIcon(new Color(0.86f, 0.78f, 0.58f));
                    }
                }

                if (drop != null) DroppedItem.Spawn(drop, 1, pos, existing);
            }
        }

        chunk.EditVoxel(local, blockID);
        UpdateNeighbors(local, chunk.chunkPos);
    }

    void UpdateNeighbors(Vector3 local, Vector2 cp)
    {
        int x = Mathf.FloorToInt(local.x);
        int z = Mathf.FloorToInt(local.z);
        if (x == 0                       && chunks.TryGetValue(cp + Vector2.left,  out Chunk cl)) cl.UpdateChunk();
        if (x == VoxelData.ChunkWidth-1  && chunks.TryGetValue(cp + Vector2.right, out Chunk cr)) cr.UpdateChunk();
        if (z == 0                       && chunks.TryGetValue(cp + Vector2.down,  out Chunk cd)) cd.UpdateChunk();
        if (z == VoxelData.ChunkWidth-1  && chunks.TryGetValue(cp + Vector2.up,    out Chunk cu)) cu.UpdateChunk();
    }
}
