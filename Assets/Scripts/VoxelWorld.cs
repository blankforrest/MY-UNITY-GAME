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

    private Queue<Chunk> rebuildQueue = new Queue<Chunk>();
    private HashSet<Chunk> rebuildQueueSet = new HashSet<Chunk>();

    [Header("Block Drops")]
    [Tooltip("Index = block ID. Assign Item asset for each droppable block.")]
    public Item[] blockDrops;

    [Header("Block Database")]
    [Tooltip("Centralized database asset for configuring all blocks (default and custom).")]
    public BlockDatabase blockDatabase;

    [Header("Item Database")]
    [Tooltip("Centralized database asset for configuring all items (default and custom).")]
    public ItemDatabase itemDatabase;


    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (GetComponent<DayNightCycle>() == null)
        {
            gameObject.AddComponent<DayNightCycle>();
        }

        InitializeMaterials();
    }

    void InitializeMaterials()
    {
        // 1. Initialize the custom block definitions registry
        if (blockDatabase != null && blockDatabase.blocks != null)
        {
            Debug.Log($"[VoxelWorld] Loading Block Database with {blockDatabase.blocks.Count} blocks.");
            foreach (var b in blockDatabase.blocks)
            {
                if (b != null && (b.textureTop != null || b.textureSide != null || b.textureBottom != null))
                {
                    Debug.Log($"[VoxelWorld] Custom textures detected on block: {b.blockName} (ID: {b.blockID}). Top: {b.textureTop?.name}, Side: {b.textureSide?.name}, Bottom: {b.textureBottom?.name}");
                }
            }
            BlockRegistry.Initialize(blockDatabase.blocks);
        }
        // 1b. Initialize the custom item definitions registry
        if (itemDatabase != null && itemDatabase.items != null)
        {
            Debug.Log($"[VoxelWorld] Loading Item Database with {itemDatabase.items.Count} items.");
            ItemRegistry.Initialize(itemDatabase.items);
        }
        else
        {
            Debug.LogError("[VoxelWorld] itemDatabase is null! Please assign an ItemDatabase asset in the Inspector.");
            ItemRegistry.Initialize(new List<ItemDefinition>());
        }


        // 2. Apply procedurally generated grass texture atlas
        Texture2D grassAtlas = GrassTextureGenerator.Create();

        // 3. Append custom block textures dynamically
        Texture2D expandedAtlas = AppendCustomTextures(grassAtlas);

        // 4. Re-bake atlas as RGBA32 so magenta key pixels become alpha=0
        Texture2D rgbaAtlas = ConvertFlowerAtlasToRGBA(expandedAtlas);

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

        // Disable highlights/specular reflections so blocks are not shiny
        if (chunkMaterial.HasProperty("_Smoothness")) chunkMaterial.SetFloat("_Smoothness", 0f);
        if (chunkMaterial.HasProperty("_Glossiness")) chunkMaterial.SetFloat("_Glossiness", 0f);
        if (chunkMaterial.HasProperty("_Metallic")) chunkMaterial.SetFloat("_Metallic", 0f);
        if (chunkMaterial.HasProperty("_SpecularHighlights")) chunkMaterial.SetFloat("_SpecularHighlights", 0f);

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

        // Disable highlights/specular reflections on foliage
        if (foliageMaterial.HasProperty("_Smoothness")) foliageMaterial.SetFloat("_Smoothness", 0f);
        if (foliageMaterial.HasProperty("_Glossiness")) foliageMaterial.SetFloat("_Glossiness", 0f);
        if (foliageMaterial.HasProperty("_Metallic")) foliageMaterial.SetFloat("_Metallic", 0f);
        if (foliageMaterial.HasProperty("_SpecularHighlights")) foliageMaterial.SetFloat("_SpecularHighlights", 0f);

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

            // Disable highlights/specular reflections on glass blocks
            if (glassMaterial.HasProperty("_Smoothness")) glassMaterial.SetFloat("_Smoothness", 0f);
            if (glassMaterial.HasProperty("_Glossiness")) glassMaterial.SetFloat("_Glossiness", 0f);
            if (glassMaterial.HasProperty("_Metallic")) glassMaterial.SetFloat("_Metallic", 0f);
            if (glassMaterial.HasProperty("_SpecularHighlights")) glassMaterial.SetFloat("_SpecularHighlights", 0f);
        }
    }

    void Start()
    {
        if (PlayerPrefs.HasKey("RenderDistance"))
        {
            renderDistance = PlayerPrefs.GetInt("RenderDistance", 6);
        }

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

        ProcessAllDirtyChunksImmediately();

        lastChunk = start;
        StartCoroutine(ChunkStreamLoop());

        // Enable mob spawning
        gameObject.AddComponent<WolfSpawner>();
        gameObject.AddComponent<SheepSpawner>();
    }

    void Update()
    {
        if (playerTransform == null)
        {
            ProcessRebuildQueue(2);
            return;
        }

        Vector2 current = PlayerChunkCoord();
        if (current != lastChunk)
        {
            lastChunk = current;
            EnqueueChunksAround(current);
            UnloadDistant(current);
        }

        ProcessRebuildQueue(4);
    }

    public void RegisterDirtyChunk(Chunk chunk)
    {
        if (chunk == null) return;
        if (rebuildQueueSet.Add(chunk))
        {
            rebuildQueue.Enqueue(chunk);
        }
    }

    private void ProcessRebuildQueue(int maxCount)
    {
        int processed = 0;
        while (rebuildQueue.Count > 0 && processed < maxCount)
        {
            Chunk chunk = rebuildQueue.Dequeue();
            if (chunk != null)
            {
                rebuildQueueSet.Remove(chunk);
                if (chunk.isDirty)
                {
                    chunk.UpdateChunk();
                    processed++;
                }
            }
        }
    }

    public void ProcessAllDirtyChunksImmediately()
    {
        while (rebuildQueue.Count > 0)
        {
            Chunk chunk = rebuildQueue.Dequeue();
            if (chunk != null)
            {
                rebuildQueueSet.Remove(chunk);
                if (chunk.isDirty)
                {
                    chunk.UpdateChunkSync();
                }
            }
        }
    }

    public void RefreshRenderDistance(int newDistance)
    {
        renderDistance = newDistance;
        if (playerTransform == null) return;
        Vector2 current = PlayerChunkCoord();
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

        // Spawn drop when breaking — skip if suppressed (e.g. vehicle conversion)
        if (blockID == 0 && !suppressDrop)
        {
            byte existing = chunk.GetVoxel(lx, ly, lz);
            if (existing != 0)
            {
                Item drop = null;

                // 1. Check if the block has a definition in the Registry
                BlockDefinition def = BlockRegistry.GetDefinition(existing);
                
                // Get player mode
                var pc = FindFirstObjectByType<PlayerController>();
                bool isCreative = (pc != null && pc.isCreativeMode);

                if (!isCreative)
                {
                    // 2. Determine tool requirements
                    bool requiresPickaxe = false;
                    if (def != null)
                    {
                        requiresPickaxe = (def.preferredTool == ToolType.Pickaxe);
                    }
                    else
                    {
                        // Fallback pickaxe requirements if def is null
                        requiresPickaxe = (existing == 3 || existing == 30 || existing == 31 || existing == 32 || 
                                           existing == 33 || existing == 37 || existing == 47 || existing == 39 || 
                                           existing == 43 || existing == 44 || existing == 45 || existing == 55);
                    }

                    // Check if player is holding a pickaxe
                    InventorySlot selectedSlot = Hotbar.Instance != null ? Hotbar.Instance.GetSelectedSlot() : null;
                    Item heldItem = selectedSlot?.item;
                    bool hasPickaxe = (heldItem != null && heldItem.toolType == ToolType.Pickaxe);

                    if (requiresPickaxe && !hasPickaxe)
                    {
                        drop = null;
                        Debug.Log($"[ModifyBlock] Drop denied: block {existing} requires a pickaxe, but player is not holding one.");
                    }
                    else
                    {
                        // 3. Resolve the drop via Registry definition
                        if (def != null)
                        {
                            if (def.dropRule == DropRule.DropsSelf)
                            {
                                if (def.dropItem == null)
                                {
                                    def.dropItem = StarterItems.CreateItemInstance(def.blockName, def.blockID, Color.white);
                                }
                                drop = def.dropItem;
                            }
                            else if (def.dropRule == DropRule.DropsCustomItem)
                            {
                                drop = def.dropItem;
                            }
                        }

                        // 4. Legacy and Special Overrides (e.g. Coal Ore, Leaves, Glass, Vehicle components)
                        
                        // Water (7) or Bedrock (48) or Glass (35) -> no drop
                        if (existing == 7 || existing == 48 || existing == 35)
                        {
                            drop = null;
                        }
                        // Diamond Ore (55) -> always drops Diamond item (blockTypeID 0)
                        else if (existing == 55)
                        {
                            drop = Inventory.Instance != null ? Inventory.Instance.CreateItem("Diamond", 0) : null;
                            if (drop == null)
                            {
                                drop = ScriptableObject.CreateInstance<Item>();
                                drop.itemName = "Diamond";
                                drop.blockTypeID = 0;
                            }
                        }
                        // Coal Ore (30) -> 10% chance Diamond, 90% chance Coal Chunk
                        else if (existing == 30)
                        {
                            if (UnityEngine.Random.value < 0.10f)
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
                                drop = Inventory.Instance != null ? Inventory.Instance.CreateItem("Coal Chunk", 0) : null;
                                if (drop == null)
                                {
                                    drop = ScriptableObject.CreateInstance<Item>();
                                    drop.itemName = "Coal Chunk";
                                    drop.blockTypeID = 0;
                                }
                            }
                        }
                        // Leaves (12) -> 20% chance Apple
                        else if (existing == 12)
                        {
                            if (UnityEngine.Random.value < 0.20f)
                            {
                                drop = ScriptableObject.CreateInstance<Item>();
                                drop.itemName = "Apple";
                                drop.blockTypeID = 0;
                                drop.icon = MakeAppleIcon();
                            }
                            else
                            {
                                if (def != null && def.dropItem != null)
                                {
                                    drop = def.dropItem;
                                }
                                else
                                {
                                    drop = ScriptableObject.CreateInstance<Item>();
                                    drop.itemName = "Leaves";
                                    drop.blockTypeID = 12;
                                    drop.icon = StarterItems.MakeBlockIcon(new Color(0.20f, 0.50f, 0.10f));
                                }
                            }
                        }
                        // Flower (9)
                        else if (existing == 9)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Flower";
                            drop.blockTypeID = 9;
                            drop.icon = MakeFlowerIcon();
                        }
                        // Dandelion (10)
                        else if (existing == 10)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Dandelion";
                            drop.blockTypeID = 10;
                            drop.icon = MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.95f, 0.85f, 0.10f), new Color(0.95f, 0.65f, 0.05f));
                        }
                        // Iris (11)
                        else if (existing == 11)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Iris";
                            drop.blockTypeID = 11;
                            drop.icon = MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.40f, 0.20f, 0.90f), new Color(1.00f, 0.80f, 0.10f));
                        }
                        // Short Grass (13)
                        else if (existing == 13)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Short Grass";
                            drop.blockTypeID = 13;
                            drop.icon = StarterItems.MakeShortGrassIcon();
                        }
                        // Tall Grass (14)
                        else if (existing == 14)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Tall Grass";
                            drop.blockTypeID = 14;
                            drop.icon = StarterItems.MakeTallGrassIcon();
                        }
                        // Vehicle Small Wheel (20)
                        else if (existing == 20)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Small Wheel";
                            drop.blockTypeID = 20;
                            drop.icon = VehicleSpawner.CreateWheelIcon(false);
                        }
                        // Vehicle Large Wheel Anchor/Helper (21, 23)
                        else if (existing == 21 || existing == 23)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Large Wheel";
                            drop.blockTypeID = 21;
                            drop.icon = VehicleSpawner.CreateWheelIcon(true);
                        }
                        // Vehicle Propeller (22)
                        else if (existing == 22)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Propeller";
                            drop.blockTypeID = 22;
                            drop.icon = VehicleSpawner.CreatePropellerIcon();
                        }
                        // Vehicle Large Propeller Anchor/Helper (26, 27)
                        else if (existing == 26 || existing == 27)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Large Propeller";
                            drop.blockTypeID = 26;
                            drop.icon = VehicleSpawner.CreateLargePropellerIcon();
                        }
                        // Vehicle Control Block (50)
                        else if (existing == 50)
                        {
                            drop = ScriptableObject.CreateInstance<Item>();
                            drop.itemName = "Control Block";
                            drop.blockTypeID = 50;
                            drop.icon = VehicleSpawner.CreateControlBlockIcon();
                        }
                        // Fallback generator for standard or missing blocks
                        else if (drop == null)
                        {
                            if (def != null)
                            {
                                drop = ScriptableObject.CreateInstance<Item>();
                                drop.itemName = def.blockName;
                                drop.blockTypeID = def.blockID;
                                drop.icon = def.inventoryIcon;
                                
                                // Make sure standard/fallback block icons are populated
                                if (drop.icon == null)
                                {
                                    if (existing == 1) drop.icon = Resources.Load<Sprite>("Sprites/wood_block");
                                    else if (existing == 2) drop.icon = Resources.Load<Sprite>("Sprites/plank_block");
                                    else if (existing == 3) drop.icon = Resources.Load<Sprite>("Sprites/stone_block");
                                    else if (existing == 4) drop.icon = StarterItems.MakeGrassBlockIcon();
                                    else if (existing == 5) drop.icon = Resources.Load<Sprite>("Sprites/dirt_block") ?? StarterItems.MakeBlockIcon(new Color(0.45f, 0.30f, 0.18f));
                                    else if (existing == 13) drop.icon = StarterItems.MakeShortGrassIcon();
                                    else if (existing == 14) drop.icon = StarterItems.MakeTallGrassIcon();
                                    else if (existing == 36) drop.icon = Resources.Load<Sprite>("Sprites/crafting_table") ?? StarterItems.MakeBlockIcon(new Color(0.72f, 0.58f, 0.37f), 36);
                                    else if (existing == 37) drop.icon = Resources.Load<Sprite>("Sprites/furnace") ?? StarterItems.MakeBlockIcon(new Color(0.5f, 0.5f, 0.5f), 37);
                                    else if (existing == 38 || existing == 40 || existing == 41 || existing == 42) drop.icon = Resources.Load<Sprite>("Sprites/wooden_stairs") ?? StarterItems.MakeBlockIcon(new Color(0.72f, 0.58f, 0.37f), 38);
                                    else if (existing == 39 || existing == 43 || existing == 44 || existing == 45) drop.icon = Resources.Load<Sprite>("Sprites/stone_stairs") ?? StarterItems.MakeBlockIcon(new Color(0.52f, 0.52f, 0.54f), 39);
                                    else if (existing == 46) drop.icon = Resources.Load<Sprite>("Sprites/wooden_slab") ?? StarterItems.MakeBlockIcon(new Color(0.72f, 0.58f, 0.37f), 46);
                                    else if (existing == 47) drop.icon = Resources.Load<Sprite>("Sprites/stone_slab") ?? StarterItems.MakeBlockIcon(new Color(0.52f, 0.52f, 0.54f), 47);
                                    else if (existing == 56) drop.icon = Resources.Load<Sprite>("Sprites/gravel_block") ?? StarterItems.MakeBlockIcon(new Color(0.48f, 0.48f, 0.48f), 56);
                                }
                            }
                            else
                            {
                                // Legacy pure hardcoded fallback
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
                                else if (existing == 4)
                                {
                                    drop = ScriptableObject.CreateInstance<Item>();
                                    drop.itemName = "Grass Block";
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
                            }
                        }
                    }
                }

                if (drop != null)
                {
                    DroppedItem.Spawn(drop, 1, pos, existing);
                    Debug.Log($"[ModifyBlock] Successfully spawned drop item: {drop.itemName} for block ID {existing}");
                }
            }
        }
        if (blockID != 0)
        {
            var def = BlockRegistry.GetDefinition(blockID);
            if (def != null)
            {
                if (def.placeSound != null)
                {
                    AudioSource.PlayClipAtPoint(def.placeSound, pos + new Vector3(0.5f, 0.5f, 0.5f));
                }
                if (def.emitsLight && def.lightLevel > 0)
                {
                    BlockRegistry.AddLight(pos, def.lightLevel);
                }
            }
        }
        else
        {
            BlockRegistry.RemoveLight(pos);
        }

        chunk.EditVoxel(local, blockID);
        UpdateNeighbors(local, chunk.chunkPos);

        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.RecordModification(pos, blockID);
        }

        // If we broke a block, break any flower or grass sitting directly on top of it
        if (blockID == 0)
        {
            Vector3 abovePos = pos + Vector3.up;
            byte aboveBlock = GetBlock(abovePos);
            if (aboveBlock == 9 || aboveBlock == 10 || aboveBlock == 11 || aboveBlock == 13 || aboveBlock == 14) // Flower and grass block types
            {
                ModifyBlock(abovePos, 0);
            }
        }
    }

    private Texture2D AppendCustomTextures(Texture2D baseAtlas)
    {
        int baseWidth = baseAtlas.width;
        int baseHeight = baseAtlas.height;
        int tileSize = GrassTextureGenerator.TILE_SIZE;
        int baseTilesCount = baseWidth / tileSize;

        // Initialize TotalTilesCount to base atlas count first
        BlockRegistry.TotalTilesCount = baseTilesCount;

        // Find how many custom textures we actually need to append
        List<Texture2D> texturesToAppend = new List<Texture2D>();
        
        // Dictionary to store the assigned tile index for each texture asset
        Dictionary<Texture2D, int> textureTileIndices = new Dictionary<Texture2D, int>();

        int customCount = BlockRegistry.RegisteredBlocks.Count;
        if (customCount == 0) return baseAtlas;
        
        // First pass: collect all unique non-null textures to append
        for (int i = 0; i < customCount; i++)
        {
            BlockDefinition def = BlockRegistry.RegisteredBlocks[i];

#if UNITY_EDITOR
            MakeTextureReadable(def.textureTop);
            MakeTextureReadable(def.textureSide);
            MakeTextureReadable(def.textureBottom);
            MakeTextureReadable(def.textureFront);
            MakeTextureReadable(def.textureFrontLit);
#endif

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

        int extraTiles = texturesToAppend.Count;
        if (extraTiles == 0)
        {
            // Even if no new textures are appended, we still need to register faces for custom blocks (ID >= 60)
            // using default tiles so they don't render black/pink
            for (int i = 0; i < customCount; i++)
            {
                BlockDefinition def = BlockRegistry.RegisteredBlocks[i];
                def.resolvedTopTile = -1;
                def.resolvedSideTile = -1;
                def.resolvedBottomTile = -1;
                def.resolvedFrontTile = -1;
                def.resolvedFrontLitTile = -1;

                if (def.blockID >= 60)
                {
                    int topTile = BlockRegistry.GetDefaultTileIndex(def.blockID, 2);
                    int sideTile = BlockRegistry.GetDefaultTileIndex(def.blockID, 1);
                    int bottomTile = BlockRegistry.GetDefaultTileIndex(def.blockID, 3);
                    BlockRegistry.RegisterFaceTiles(def.blockID, topTile, sideTile, bottomTile);
                }
            }
            return baseAtlas;
        }

        int newWidth = baseWidth + extraTiles * tileSize;
        BlockRegistry.TotalTilesCount = baseTilesCount + extraTiles;

        Texture2D expandedAtlas = new Texture2D(newWidth, baseHeight, TextureFormat.RGBA32, false);
        expandedAtlas.filterMode = FilterMode.Point;
        expandedAtlas.wrapMode = TextureWrapMode.Clamp;

        // Copy the base atlas pixels first
        Color[] basePixels = baseAtlas.GetPixels();
        expandedAtlas.SetPixels(0, 0, baseWidth, baseHeight, basePixels);

        // Copy and assign tile indices
        for (int i = 0; i < texturesToAppend.Count; i++)
        {
            Texture2D tex = texturesToAppend[i];
            int tileIndex = baseTilesCount + i;
            textureTileIndices[tex] = tileIndex;

            Color[] px = GetResizedPixels(tex);
            expandedAtlas.SetPixels(tileIndex * tileSize, 0, tileSize, tileSize, px);
        }

        // Second pass: register face tiles for blocks
        for (int i = 0; i < customCount; i++)
        {
            BlockDefinition def = BlockRegistry.RegisteredBlocks[i];
            
            def.resolvedTopTile = def.textureTop != null ? textureTileIndices[def.textureTop] : -1;
            def.resolvedSideTile = def.textureSide != null ? textureTileIndices[def.textureSide] : -1;
            def.resolvedBottomTile = def.textureBottom != null ? textureTileIndices[def.textureBottom] : -1;
            def.resolvedFrontTile = def.textureFront != null ? textureTileIndices[def.textureFront] : def.resolvedSideTile;
            def.resolvedFrontLitTile = def.textureFrontLit != null ? textureTileIndices[def.textureFrontLit] : def.resolvedFrontTile;

            // Check if this block overrides any textures or is a custom block
            bool hasCustom = (def.textureTop != null || def.textureSide != null || def.textureBottom != null || def.textureFront != null || def.textureFrontLit != null);
            if (hasCustom || def.blockID >= 60)
            {
                int topTile = def.resolvedTopTile != -1 ? def.resolvedTopTile : BlockRegistry.GetDefaultTileIndex(def.blockID, 2);
                int sideTile = def.resolvedSideTile != -1 ? def.resolvedSideTile : BlockRegistry.GetDefaultTileIndex(def.blockID, 1);
                int bottomTile = def.resolvedBottomTile != -1 ? def.resolvedBottomTile : BlockRegistry.GetDefaultTileIndex(def.blockID, 3);

                BlockRegistry.RegisterFaceTiles(def.blockID, topTile, sideTile, bottomTile);
            }
        }

        expandedAtlas.Apply();
        return expandedAtlas;
    }



    private Color[] GetResizedPixels(Texture2D tex)
    {
        int tileSize = GrassTextureGenerator.TILE_SIZE;
        Color[] colors = new Color[tileSize * tileSize];
        if (tex == null)
        {
            // Fallback to magenta if texture is missing
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.magenta;
            return colors;
        }

        // Nearest-neighbor scaling for pixel art crispness
        for (int y = 0; y < tileSize; y++)
        {
            float v = (float)y / (tileSize - 1);
            int srcY = Mathf.Clamp(Mathf.RoundToInt(v * (tex.height - 1)), 0, tex.height - 1);
            for (int x = 0; x < tileSize; x++)
            {
                float u = (float)x / (tileSize - 1);
                int srcX = Mathf.Clamp(Mathf.RoundToInt(u * (tex.width - 1)), 0, tex.width - 1);
                colors[y * tileSize + x] = tex.GetPixel(srcX, srcY);
            }
        }
        return colors;
    }

    private static Sprite _cachedRoseIcon;
    private static Sprite _cachedDandelionIcon;
    private static Sprite _cachedIrisIcon;
    private static Sprite _cachedAppleIcon;
    private static Sprite _cachedWoolIcon;
    private static Sprite _cachedMuttonIcon;
    private static Sprite _cachedLeatherIcon;

    public static Sprite MakeAppleIcon()
    {
        if (_cachedAppleIcon != null) return _cachedAppleIcon;

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color red = new Color(0.85f, 0.15f, 0.15f, 1f);
        Color darkRed = new Color(0.6f, 0.08f, 0.08f, 1f);
        Color brown = new Color(0.45f, 0.3f, 0.15f, 1f);
        Color green = new Color(0.2f, 0.7f, 0.2f, 1f);

        // Stem (brown)
        for (int y = 40; y < 52; y++)
        {
            int x = 32 + (y - 40) / 3;
            Set(x, y, brown);
            Set(x + 1, y, brown);
        }

        // Leaf (green)
        for (int dx = 1; dx < 8; dx++)
        {
            for (int dy = 0; dy < 4; dy++)
            {
                if (dy <= dx && dx + dy < 10)
                {
                    Set(34 + dx, 46 + dy, green);
                }
            }
        }

        // Apple body (red with dark red shading/outlines)
        for (int dx = -16; dx <= 16; dx++)
        {
            for (int dy = -16; dy <= 16; dy++)
            {
                float d = dx * dx + dy * dy;
                float adjustedDist = d;
                if (dy > 4)
                {
                    float topDip = Mathf.Abs(dx) * 0.4f;
                    adjustedDist = dx * dx + (dy + topDip) * (dy + topDip);
                }
                else if (dy < -10)
                {
                    float bottomDip = Mathf.Abs(dx) * 0.3f;
                    adjustedDist = dx * dx + (dy - bottomDip) * (dy - bottomDip);
                }

                if (adjustedDist <= 220f)
                {
                    Set(32 + dx, 28 + dy, red);
                }
                else if (adjustedDist <= 256f)
                {
                    Set(32 + dx, 28 + dy, darkRed);
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        _cachedAppleIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedAppleIcon;
    }

    public static Sprite MakeWoolIcon()
    {
        if (_cachedWoolIcon != null) return _cachedWoolIcon;

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color woolColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        Color shadeColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        Color outlineColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        // Draw a fluffy cloud/wool shape
        for (int dx = -20; dx <= 20; dx++)
        {
            for (int dy = -16; dy <= 16; dy++)
            {
                // Fluffy lobes
                float d1 = (dx - 4) * (dx - 4) + (dy - 4) * (dy - 4); // main center
                float d2 = (dx + 10) * (dx + 10) + (dy + 2) * (dy + 2); // left lobe
                float d3 = (dx - 12) * (dx - 12) + (dy - 2) * (dy - 2); // right lobe
                float d4 = (dx - 2) * (dx - 2) + (dy + 8) * (dy + 8); // top lobe
                float d5 = (dx + 2) * (dx + 2) + (dy - 10) * (dy - 10); // bottom lobe

                float val = Mathf.Min(d1 / 180f, d2 / 120f, d3 / 120f, d4 / 100f, d5 / 100f);

                if (val <= 0.85f)
                {
                    // Inside
                    Color col = woolColor;
                    if (val > 0.65f || dy < -6) col = shadeColor;
                    Set(32 + dx, 32 + dy, col);
                }
                else if (val <= 1.0f)
                {
                    // Outline
                    Set(32 + dx, 32 + dy, outlineColor);
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        _cachedWoolIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedWoolIcon;
    }

    public static Sprite MakeMuttonIcon()
    {
        if (_cachedMuttonIcon != null) return _cachedMuttonIcon;

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color meatColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        Color fatColor = new Color(0.95f, 0.90f, 0.90f, 1f);
        Color boneColor = new Color(0.92f, 0.92f, 0.88f, 1f);
        Color meatOutline = new Color(0.55f, 0.12f, 0.12f, 1f);
        Color boneOutline = new Color(0.6f, 0.6f, 0.55f, 1f);

        // Draw a meat chop (angled bone from bottom-left to center, meat surrounding top-right)
        // Bone
        for (int i = -16; i <= 2; i++)
        {
            // Draw bone along diagonal x = y
            int bx = 32 + i;
            int by = 32 + i;
            Set(bx, by, boneColor);
            Set(bx - 1, by + 1, boneOutline);
            Set(bx + 1, by - 1, boneOutline);
        }
        // Bone tip (rounded end at bottom-left)
        Set(15, 15, boneOutline);
        Set(16, 15, boneOutline);
        Set(15, 16, boneOutline);

        // Meat body (oval shape around center/top-right)
        for (int dx = -14; dx <= 18; dx++)
        {
            for (int dy = -14; dy <= 18; dy++)
            {
                // Offset oval
                float ox = dx - 4;
                float oy = dy - 4;
                float d = ox * ox + oy * oy - ox * oy * 0.8f; // slanted oval

                if (d <= 140f)
                {
                    // Draw meat
                    Color col = meatColor;
                    // Draw fat marbling/stripes
                    if (Mathf.Abs(ox - oy) < 2f || Mathf.Abs(ox - oy + 8f) < 2f)
                    {
                        col = fatColor;
                    }
                    Set(32 + dx, 32 + dy, col);
                }
                else if (d <= 165f)
                {
                    // Meat Outline
                    Set(32 + dx, 32 + dy, meatOutline);
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        _cachedMuttonIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedMuttonIcon;
    }

    public static Sprite MakeLeatherIcon()
    {
        if (_cachedLeatherIcon != null) return _cachedLeatherIcon;

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color leatherColor = new Color(0.62f, 0.42f, 0.28f, 1f);
        Color leatherShade = new Color(0.52f, 0.35f, 0.22f, 1f);
        Color leatherOutline = new Color(0.35f, 0.20f, 0.10f, 1f);

        // Draw a stretched animal hide shape (like a rough cross/star or a rect with indented sides)
        for (int dx = -18; dx <= 18; dx++)
        {
            for (int dy = -22; dy <= 22; dy++)
            {
                // Compute distance from center with indentations
                float absX = Mathf.Abs(dx);
                float absY = Mathf.Abs(dy);

                // Indentation formula (larger x/y in corners)
                float boundary = 16f;
                if (absY < 6) boundary = 13f; // pinch the waist
                if (absX < 6) boundary = 20f; // stretch head/tail
                
                // Corner check (indents corners)
                float cornerVal = absX + absY * 0.7f;

                if (cornerVal <= 26f && absX <= boundary + 2 && absY <= 21)
                {
                    bool isOutline = (cornerVal > 24.5f || absX >= boundary || absY >= 20);
                    if (isOutline)
                    {
                        Set(32 + dx, 32 + dy, leatherOutline);
                    }
                    else
                    {
                        Color col = leatherColor;
                        if (dy < -4 || dx > 6) col = leatherShade;
                        Set(32 + dx, 32 + dy, col);
                    }
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        _cachedLeatherIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedLeatherIcon;
    }

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
                // Preserve the exact alpha generated by MinecraftGlass
                out_[i] = c;
            }
            else
            {
                // Convert magenta key pixels in procedural flower/grass tiles to alpha=0
                bool inFlowerOrGrassTile = (px >= 9 * tileSize && px < 12 * tileSize)
                                        || (px >= 27 * tileSize && px < 29 * tileSize);
                bool isMagenta = (c.r > 0.8f && c.g < 0.2f && c.b > 0.8f);

                if (inFlowerOrGrassTile && isMagenta)
                {
                    // Transparent key colour used by procedural foliage
                    out_[i] = new Color(c.r, c.g, c.b, 0f);
                }
                else if (c.a < 1f)
                {
                    // Custom PNG tile already has a real alpha channel — preserve it.
                    // This makes background-removed textures (e.g. leaves, gravel) show
                    // transparent pixels instead of rendering them white/opaque.
                    out_[i] = c;
                }
                else
                {
                    // Fully opaque procedural tile — keep alpha=1
                    out_[i] = new Color(c.r, c.g, c.b, 1f);
                }
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

#if UNITY_EDITOR
    private void MakeTextureReadable(Texture2D tex)
    {
        if (tex == null) return;
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(assetPath)) return;

        UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"[VoxelWorld] Automatically set Read/Write Enabled on texture: {tex.name}");
        }
    }
#endif

    public void RebuildAllChunks()
    {
        foreach (var kvp in chunks)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Initialize(kvp.Key, chunkMaterial);
            }
        }
        ProcessAllDirtyChunksImmediately();
    }
}
