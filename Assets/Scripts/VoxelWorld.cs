using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld Instance { get; private set; }

    public Material chunkMaterial;
    public Material waterMaterial;
    public Material foliageMaterial;
    [HideInInspector] public Material glassMaterial;

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

        InitializeMaterials();
    }

    void InitializeMaterials()
    {
        // Apply procedurally generated grass texture atlas
        Texture2D grassAtlas = GrassTextureGenerator.Create();
        // Re-bake atlas as RGBA32 so magenta key pixels become alpha=0
        Texture2D rgbaAtlas = ConvertFlowerAtlasToRGBA(grassAtlas);

        // Auto-find material
        if (chunkMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            chunkMaterial = new Material(s);
        }

        chunkMaterial.mainTexture = rgbaAtlas;
        chunkMaterial.SetTexture("_BaseMap", rgbaAtlas);
        chunkMaterial.color       = Color.white; // don't tint the texture
        chunkMaterial.SetColor("_BaseColor", Color.white);

        // Configure chunkMaterial as Cutout (Alpha Test) so transparent blocks like Glass work in the solid mesh
        if (chunkMaterial.shader.name.Contains("Standard"))
        {
            chunkMaterial.SetFloat("_Mode", 1f); // 1 = Cutout
            chunkMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            chunkMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            chunkMaterial.SetInt("_ZWrite", 1);
            chunkMaterial.EnableKeyword("_ALPHATEST_ON");
            chunkMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        else
        {
            chunkMaterial.SetFloat("_Surface", 0f);       // 0 = Opaque
            chunkMaterial.SetFloat("_AlphaClip", 1f);     // 1 = On
            chunkMaterial.SetFloat("_Cutoff", 0.5f);      // Threshold
            chunkMaterial.EnableKeyword("_ALPHATEST_ON");
            chunkMaterial.SetInt("_ZWrite", 1);           // Write to depth
            chunkMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

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
        // Use rgbaAtlas so glass (tile 22) alpha channels are properly sampled
        waterMaterial.mainTexture = rgbaAtlas;
        waterMaterial.SetTexture("_BaseMap", rgbaAtlas); // URP shaders use _BaseMap
        waterMaterial.color       = Color.white;
        waterMaterial.SetColor("_BaseColor", Color.white);
        waterMaterial.SetInt("_Cull", 0); // Always ensure double-sided regardless of which shader was chosen

        // ── Foliage material (alpha-cutout, double-sided) ─────────────────────
        // Configured as Opaque with Alpha Clipping (Cutout) to avoid transparent depth sorting issues
        // and eliminate the visible square borders/shadows.
        if (foliageMaterial == null)
        {
            // Try URP Unlit first so flowers are vibrant pixel art unaffected by blue ambient sky light
            Shader foliageShader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? chunkMaterial.shader
                                ?? Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard");

            foliageMaterial = new Material(foliageShader);
            
            if (foliageShader.name.Contains("Standard"))
            {
                foliageMaterial.SetFloat("_Mode", 1f); // 1 = Cutout
                foliageMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                foliageMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                foliageMaterial.SetInt("_ZWrite", 1);
                foliageMaterial.EnableKeyword("_ALPHATEST_ON");
                foliageMaterial.DisableKeyword("_ALPHABLEND_ON");
                foliageMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                foliageMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                foliageMaterial.SetFloat("_Surface", 0f);       // 0 = Opaque
                foliageMaterial.SetFloat("_AlphaClip", 1f);     // 1 = On
                foliageMaterial.SetFloat("_Cutoff", 0.5f);      // Threshold
                foliageMaterial.EnableKeyword("_ALPHATEST_ON");
                foliageMaterial.SetInt("_Cull", 0);             // Double-sided
                foliageMaterial.SetInt("_ZWrite", 1);           // Write to depth
                foliageMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
        }
        foliageMaterial.mainTexture = rgbaAtlas;           // sets _MainTex
        foliageMaterial.SetTexture("_BaseMap", rgbaAtlas); // URP Particles/Lit use _BaseMap
        foliageMaterial.color       = Color.white;
        foliageMaterial.SetColor("_BaseColor", Color.white);
        foliageMaterial.SetInt("_Cull", 0);                 // Ensure culling is off (double-sided rendering) on all shaders

        // ── Glass material (ZWrite On + Cull Back) ────────────────────────────
        // ZWrite On: front faces write depth → back faces are Z-rejected and never show through.
        // Cull Back: only front-facing fragments are rasterized. This is the key fix for the
        // bug where the back-face border frames were bleeding through the front faces.
        {
            Shader glassShader = Shader.Find("Universal Render Pipeline/Lit")
                              ?? Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Standard");
            glassMaterial = new Material(glassShader);

            if (glassShader.name.Contains("Standard"))
            {
                glassMaterial.SetFloat("_Mode", 3f); // Transparent
                glassMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glassMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glassMaterial.SetInt("_ZWrite", 1);  // ON — front faces occlude back faces
                glassMaterial.SetInt("_Cull", 2);    // 2 = Back — only render front faces
                glassMaterial.DisableKeyword("_ALPHATEST_ON");
                glassMaterial.EnableKeyword("_ALPHABLEND_ON");
                glassMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                // URP path
                glassMaterial.SetFloat("_Surface", 1f);      // 1 = Transparent
                glassMaterial.SetFloat("_Blend", 0f);        // Alpha blend
                glassMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glassMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glassMaterial.SetInt("_ZWrite", 1);          // ON — front faces occlude back faces
                glassMaterial.SetInt("_Cull", 2);            // 2 = Back — only render front faces
                glassMaterial.DisableKeyword("_ALPHATEST_ON");
                glassMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glassMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            glassMaterial.mainTexture = rgbaAtlas;
            glassMaterial.SetTexture("_BaseMap", rgbaAtlas);
            glassMaterial.color = Color.white;
            glassMaterial.SetColor("_BaseColor", Color.white);
        }
    }

    void Start()
    {
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

    public void ModifyBlock(Vector3 pos, byte blockID, bool suppressDrop = false)
    {
        Chunk chunk = GetChunkFromVector3(pos);
        if (chunk == null) return;

        Vector3 local = pos - chunk.transform.position;
        int lx = Mathf.FloorToInt(local.x);
        int ly = Mathf.FloorToInt(local.y);
        int lz = Mathf.FloorToInt(local.z);

        // Spawn drop when breaking — skip if suppressed (e.g. vehicle conversion) or vehicle/special blocks
        if (blockID == 0 && !suppressDrop)
        {
            byte existing = chunk.GetVoxel(lx, ly, lz);
            if (existing != 0 && (existing <= 12 || existing == 20 || existing == 21 || existing == 22 || existing == 23 || existing == 50 || existing == 36 || (existing >= 30 && existing <= 47)))
            {
                Item drop = null;
                if (blockDrops != null && existing < blockDrops.Length)
                {
                    drop = blockDrops[existing];
                }

                // Pickaxe requirement check
                bool isPickaxeRequired = (existing == 3 || existing == 30 || existing == 31 || existing == 32 || existing == 33 || existing == 37 || existing == 47 || existing == 39 || existing == 43 || existing == 44 || existing == 45);
                InventorySlot selectedSlot = Hotbar.Instance != null ? Hotbar.Instance.GetSelectedSlot() : null;
                Item heldItem = selectedSlot?.item;
                bool hasPickaxe = (heldItem != null && heldItem.toolType == ToolType.Pickaxe);

                // Robust fallback for Wood (1), Plank (2), Stone (3)
                if (drop == null)
                {
                    if (isPickaxeRequired && !hasPickaxe)
                    {
                        // Drops nothing!
                        drop = null;
                    }
                    else if (existing == 1)
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
                    else if (existing == 4)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Grass";
                        drop.blockTypeID = 4;
                        drop.icon = StarterItems.MakeGrassBlockIcon();
                    }
                    else if (existing == 5)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Dirt";
                        drop.blockTypeID = 5;
                        Sprite loaded = Resources.Load<Sprite>("Sprites/dirt_block");
                        drop.icon = loaded != null ? loaded : StarterItems.MakeBlockIcon(new Color(0.45f, 0.30f, 0.18f));
                    }
                    else if (existing == 8 || existing == 34)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Sand";
                        drop.blockTypeID = existing;
                        drop.icon = StarterItems.MakeBlockIcon(new Color(0.86f, 0.78f, 0.58f), existing);
                    }
                    else if (existing == 30)
                    {
                        if (hasPickaxe && UnityEngine.Random.value < 0.10f)
                        {
                            drop = Inventory.Instance != null ? Inventory.Instance.CreateItem("Diamond", 0) : null;
                            if (drop == null)
                            {
                                drop = ScriptableObject.CreateInstance<Item>();
                                drop.itemName = "Diamond";
                                drop.blockTypeID = 0;
                            }
                        }
                        else
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Coal Ore";
                            drop.blockTypeID = 30;
                            drop.icon = StarterItems.MakeBlockIcon(new Color(0.2f, 0.2f, 0.2f), 30);
                        }
                    }
                    else if (existing == 31)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Iron Ore";
                        drop.blockTypeID = 31;
                        drop.icon = StarterItems.MakeBlockIcon(new Color(0.85f, 0.65f, 0.52f), 31);
                    }
                    else if (existing == 32)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Gold Block";
                        drop.blockTypeID = 32;
                        drop.icon = StarterItems.MakeBlockIcon(new Color(0.98f, 0.82f, 0.15f), 32);
                    }
                    else if (existing == 33)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Iron Block";
                        drop.blockTypeID = 33;
                        drop.icon = StarterItems.MakeBlockIcon(new Color(0.88f, 0.93f, 0.98f), 33);
                    }
                    else if (existing == 35)
                    {
                        // Glass shatters — no item drop (drop stays null)
                    }
                    else if (existing == 36)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Crafting Table";
                        drop.blockTypeID = 36;
                        Sprite loaded = Resources.Load<Sprite>("Sprites/crafting_table");
                        drop.icon = loaded != null ? loaded : StarterItems.MakeBlockIcon(new Color(0.72f, 0.58f, 0.37f), 36);
                    }
                    else if (existing == 37)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Furnace";
                        drop.blockTypeID = 37;
                        Sprite loaded = Resources.Load<Sprite>("Sprites/furnace");
                        drop.icon = loaded != null ? loaded : StarterItems.MakeBlockIcon(new Color(0.5f, 0.5f, 0.5f), 37);
                    }
                    else if (existing == 38 || existing == 40 || existing == 41 || existing == 42)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Wooden Stairs";
                        drop.blockTypeID = 38;
                        Sprite loaded = Resources.Load<Sprite>("Sprites/wooden_stairs");
                        drop.icon = loaded != null ? loaded : StarterItems.MakeBlockIcon(new Color(0.72f, 0.58f, 0.37f), 38);
                    }
                    else if (existing == 39 || existing == 43 || existing == 44 || existing == 45)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Stone Stairs";
                        drop.blockTypeID = 39;
                        Sprite loaded = Resources.Load<Sprite>("Sprites/stone_stairs");
                        drop.icon = loaded != null ? loaded : StarterItems.MakeBlockIcon(new Color(0.52f, 0.52f, 0.54f), 39);
                    }
                    else if (existing == 46)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Wooden Slab";
                        drop.blockTypeID = 46;
                        Sprite loaded = Resources.Load<Sprite>("Sprites/wooden_slab");
                        drop.icon = loaded != null ? loaded : StarterItems.MakeBlockIcon(new Color(0.72f, 0.58f, 0.37f), 46);
                    }
                    else if (existing == 47)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Stone Slab";
                        drop.blockTypeID = 47;
                        Sprite loaded = Resources.Load<Sprite>("Sprites/stone_slab");
                        drop.icon = loaded != null ? loaded : StarterItems.MakeBlockIcon(new Color(0.52f, 0.52f, 0.54f), 47);
                    }
                    else if (existing == 9)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Flower";
                        drop.blockTypeID = 9;
                        drop.icon = MakeFlowerIcon();
                    }
                    else if (existing == 10)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Dandelion";
                        drop.blockTypeID = 10;
                        drop.icon = MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.95f, 0.85f, 0.10f), new Color(0.95f, 0.65f, 0.05f));
                    }
                    else if (existing == 11)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Iris";
                        drop.blockTypeID = 11;
                        drop.icon = MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.40f, 0.20f, 0.90f), new Color(1.00f, 0.80f, 0.10f));
                    }
                    else if (existing == 12)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Leaves";
                        drop.blockTypeID = 12;
                        drop.icon = StarterItems.MakeBlockIcon(new Color(0.20f, 0.50f, 0.10f));
                    }
                    else if (existing == 20)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Small Wheel";
                        drop.blockTypeID = 20;
                        drop.icon = VehicleSpawner.CreateWheelIcon(false);
                    }
                    else if (existing == 21 || existing == 23)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Large Wheel";
                        drop.blockTypeID = 21;
                        drop.icon = VehicleSpawner.CreateWheelIcon(true);
                    }
                    else if (existing == 22)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Propeller";
                        drop.blockTypeID = 22;
                        drop.icon = VehicleSpawner.CreatePropellerIcon();
                    }
                    else if (existing == 50)
                    {
                        drop = ScriptableObject.CreateInstance<Item>();
                        drop.itemName = "Control Block";
                        drop.blockTypeID = 50;
                        drop.icon = VehicleSpawner.CreateControlBlockIcon();
                    }
                }

                if (drop != null) DroppedItem.Spawn(drop, 1, pos, existing);
            }
        }
        chunk.EditVoxel(local, blockID);
        UpdateNeighbors(local, chunk.chunkPos);

        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.RecordModification(pos, blockID);
        }

        // If we broke a block, break any flower sitting directly on top of it
        if (blockID == 0)
        {
            Vector3 abovePos = pos + Vector3.up;
            byte aboveBlock = GetBlock(abovePos);
            if (aboveBlock == 9 || aboveBlock == 10 || aboveBlock == 11) // Flower block types
            {
                ModifyBlock(abovePos, 0);
            }
        }
    }

    private static Sprite _cachedRoseIcon;
    private static Sprite _cachedDandelionIcon;
    private static Sprite _cachedIrisIcon;

    /// <summary>Procedurally generates a small sprite icon representing a flower.</summary>
    public static Sprite MakeFlowerIcon()
    {
        return MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(1.00f, 0.28f, 0.55f), new Color(1.00f, 0.92f, 0.20f));
    }

    /// <summary>Procedurally generates a small sprite icon representing a flower with custom colors.</summary>
    public static Sprite MakeFlowerIcon(Color stem, Color petal, Color centre)
    {
        // Rose-pink/red
        if (petal.r > 0.9f && petal.g < 0.4f && petal.b > 0.4f)
        {
            if (_cachedRoseIcon == null) _cachedRoseIcon = GenerateFlowerIcon(stem, petal, centre);
            return _cachedRoseIcon;
        }
        // Dandelion yellow
        if (petal.r > 0.9f && petal.g > 0.8f && petal.b < 0.2f)
        {
            if (_cachedDandelionIcon == null) _cachedDandelionIcon = GenerateFlowerIcon(stem, petal, centre);
            return _cachedDandelionIcon;
        }
        // Iris violet
        if (petal.r > 0.3f && petal.r < 0.5f && petal.g < 0.3f && petal.b > 0.8f)
        {
            if (_cachedIrisIcon == null) _cachedIrisIcon = GenerateFlowerIcon(stem, petal, centre);
            return _cachedIrisIcon;
        }

        return GenerateFlowerIcon(stem, petal, centre);
    }

    private static Sprite GenerateFlowerIcon(Color stem, Color petal, Color centre)
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        // Stem
        for (int y = 4; y < 30; y++) { Set(31, y, stem); Set(32, y, stem); }
        // Petals
        for (int dx = -10; dx <= 10; dx++)
            for (int dy = -10; dy <= 10; dy++)
            {
                float d = dx * dx + dy * dy;
                if (d <= 110 && d >= 50)
                    Set(32 + dx, 40 + dy, petal);
            }
        // Centre
        for (int dx = -5; dx <= 5; dx++)
            for (int dy = -5; dy <= 5; dy++)
                if (dx * dx + dy * dy <= 30)
                    Set(32 + dx, 40 + dy, centre);

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Procedurally generates a half-height slab block sprite icon.</summary>
    public static Sprite MakeSlabIcon(Color baseColor)
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        float r = baseColor.r, g = baseColor.g, b = baseColor.b, a = baseColor.a;
        Color top     = new Color(Mathf.Clamp01(r + 0.25f), Mathf.Clamp01(g + 0.25f), Mathf.Clamp01(b + 0.25f), a);
        Color front   = baseColor;
        Color side    = new Color(Mathf.Clamp01(r - 0.20f), Mathf.Clamp01(g - 0.20f), Mathf.Clamp01(b - 0.20f), a);
        Color outline = new Color(Mathf.Clamp01(r - 0.45f), Mathf.Clamp01(g - 0.45f), Mathf.Clamp01(b - 0.45f), a);

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        void FillRect(int x, int y, int w, int h, Color c)
        { for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++) Set(x+dx, y+dy, c); }

        // Top is lowered from y=38 to y=26 (38 - 12)
        FillRect(16, 26, 32, 14, top);
        // Front/side height is 12 instead of 24
        FillRect(8,  14, 24, 12, front);
        FillRect(32, 14, 24, 12, side);

        // Outlines
        for (int x = 8;  x < 40; x++) Set(x, 13, outline);
        for (int x = 32; x < 56; x++) Set(x, 13, outline);
        for (int y = 13; y < 26; y++) Set(7,  y,  outline);
        for (int y = 13; y < 26; y++) Set(56, y,  outline);
        for (int x = 8;  x < 32; x++) Set(x, 26, outline);
        for (int x = 32; x < 56; x++) Set(x, 26, outline);
        for (int y = 26; y < 40; y++) Set(16, y,  outline);
        for (int y = 26; y < 40; y++) Set(47, y,  outline);

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Returns a new RGBA32 copy of the atlas where magenta (R≈1, G≈0, B≈1) pixels
    /// in the flower tile (tile 9) have alpha=0 so the alpha-cutout shader clips them.
    /// All other pixels keep alpha=1.
    /// </summary>
    static Texture2D ConvertFlowerAtlasToRGBA(Texture2D source)
    {
        int w = source.width;
        int h = source.height;
        Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.filterMode = FilterMode.Point;
        dst.wrapMode   = TextureWrapMode.Clamp;

        Color[] src = source.GetPixels();
        Color[] out_ = new Color[src.Length];
        int tileSize  = GrassTextureGenerator.TILE_SIZE;
        int flowerTileXStart = 9 * tileSize; // pixel x start of flower tiles
        int flowerTileXEnd   = 12 * tileSize; // end of flower tiles (9, 10, 11) — flowers only
        int glassTileXStart  = 22 * tileSize; // glass tile is 22nd (index 22)
        int glassTileXEnd    = 23 * tileSize;

        for (int i = 0; i < src.Length; i++)
        {
            int px = i % w;
            Color c = src[i];
            
            bool inGlassTile = (px >= glassTileXStart && px < glassTileXEnd);
            if (inGlassTile)
            {
                out_[i] = c; // Preserve the exact alpha generated by MinecraftGlass
            }
            else
            {
                // Convert magenta key pixels in flower tiles to alpha=0
                bool inFlowerTile = (px >= flowerTileXStart && px < flowerTileXEnd);
                bool isMagenta    = (c.r > 0.8f && c.g < 0.2f && c.b > 0.8f);
                out_[i] = inFlowerTile && isMagenta
                    ? new Color(c.r, c.g, c.b, 0f)  // transparent
                    : new Color(c.r, c.g, c.b, 1f);  // opaque
            }
        }

        dst.SetPixels(out_);
        dst.Apply();
        return dst;
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

    public void RebuildAllChunks()
    {
        foreach (var kvp in chunks)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Initialize(kvp.Key, chunkMaterial);
            }
        }
    }
}
