using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives the player a starting set of placeable block items on first play.
/// Sprites are loaded from Assets/Resources/Sprites/ at runtime.
/// Attach to the Player GameObject.
/// </summary>
public class StarterItems : MonoBehaviour
{
    // Maps item name keyword → Resources path (no extension, no "Assets/Resources/" prefix)
    private static readonly Dictionary<string, string> SpriteMap =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Wood",       "Sprites/wood_block"       },
        { "Plank",      "Sprites/plank_block"      },
        { "Stone",      "Sprites/stone_block"      },
        { "Dirt",       "Sprites/dirt_block"       },
        { "Iron",       "Sprites/iron_bar"         },
        { "Stick",      "Sprites/stick"            },
        { "String",     "Sprites/string"           },
        { "Coal Ore",   "Sprites/coal_ore_block"   },
        { "Iron Ore",   "Sprites/iron_ore_block"   },
        { "Gold Block", "Sprites/gold_block"       },
        { "Iron Block", "Sprites/iron_block"       },
        { "Sand",       "Sprites/sand_block"       },
        { "Crafting Table", "Sprites/crafting_table" },
        { "Furnace",    "Sprites/furnace"          },
        { "Glass",      "Sprites/glass_block"      },
        { "Wooden Stairs", "Sprites/wooden_stairs" },
        { "Stone Stairs", "Sprites/stone_stairs" },
        { "Wooden Slab", "Sprites/wooden_slab" },
        { "Stone Slab", "Sprites/stone_slab" },
        { "Leaves",     "Sprites/leaves_block" },
    };

    private IEnumerator Start()
    {
        GenerateBlockSpritesIfMissing();

        // Wait one frame so Hotbar and Inventory have finished their Awake/Start
        yield return null;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && !player.isCreativeMode)
        {
            Debug.Log("[StarterItems] Player is in Survival Mode — starting with empty inventory.");
            yield break;
        }

        if (player != null && player.isCreativeMode)
        {
            Debug.Log("[StarterItems] Player is in Creative Mode — skipping giving starter items.");
            yield break;
        }

        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.HasSaveFile())
        {
            Debug.Log("[StarterItems] Save file detected — skipping giving starter items.");
            yield break;
        }

        if (Hotbar.Instance == null)
        {
            Debug.LogError("[StarterItems] Hotbar.Instance is null.");
            yield break;
        }

        // ── Hotbar: starts empty ────────────────────────────────────────────

        // ── Inventory bag: flowers & new blocks ───────────────────────────────
        GiveInventoryItem("Flower",    blockTypeID: 9,  fallbackColor: new Color(1.00f, 0.28f, 0.55f), amount: 64);
        GiveInventoryItem("Dandelion", blockTypeID: 10, fallbackColor: new Color(0.95f, 0.85f, 0.10f), amount: 64);
        GiveInventoryItem("Iris",      blockTypeID: 11, fallbackColor: new Color(0.40f, 0.20f, 0.90f), amount: 64);

        // Batch-added new blocks
        GiveInventoryItem("Coal Ore",   blockTypeID: 30, fallbackColor: new Color(0.20f, 0.20f, 0.20f), amount: 64);
        GiveInventoryItem("Iron Ore",   blockTypeID: 31, fallbackColor: new Color(0.85f, 0.65f, 0.52f), amount: 64);
        GiveInventoryItem("Gold Block", blockTypeID: 32, fallbackColor: new Color(0.98f, 0.82f, 0.15f), amount: 64);
        GiveInventoryItem("Iron Block", blockTypeID: 33, fallbackColor: new Color(0.88f, 0.93f, 0.98f), amount: 64);
        GiveInventoryItem("Sand",       blockTypeID: 34, fallbackColor: new Color(0.86f, 0.78f, 0.58f), amount: 64);
        GiveInventoryItem("Glass",      blockTypeID: 35, fallbackColor: new Color(0.80f, 0.90f, 0.95f), amount: 64);
        GiveInventoryItem("Furnace",    blockTypeID: 37, fallbackColor: new Color(0.50f, 0.50f, 0.50f), amount: 4);
        GiveInventoryItem("Wooden Slab", blockTypeID: 46, fallbackColor: new Color(0.72f, 0.58f, 0.37f), amount: 64);
        GiveInventoryItem("Stone Slab", blockTypeID: 47, fallbackColor: new Color(0.52f, 0.52f, 0.54f), amount: 64);
        GiveInventoryItem("Crafting Table", blockTypeID: 36, fallbackColor: new Color(0.72f, 0.58f, 0.37f), amount: 4);
        GiveInventoryItem("Stick",      blockTypeID: 0,  fallbackColor: new Color(0.48f, 0.31f, 0.16f), amount: 64);
        GiveInventoryItem("Iron",       blockTypeID: 0,  fallbackColor: new Color(0.85f, 0.85f, 0.85f), amount: 64);
        GiveInventoryItem("Diamond",    blockTypeID: 0,  fallbackColor: new Color(0.20f, 0.85f, 0.88f), amount: 64);
    }

    private void GenerateBlockSpritesIfMissing()
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "Resources/Sprites");
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        // Apply transparency key to coal, iron, and leaves AI-generated sprites if they exist
        ApplyTransparencyKey(System.IO.Path.Combine(dir, "coal_ore_block.png"));
        ApplyTransparencyKey(System.IO.Path.Combine(dir, "iron_ore_block.png"));
        ApplyTransparencyKey(System.IO.Path.Combine(dir, "leaves_block.png"));

        string[] names = { "gold_block", "iron_block", "sand_block", "glass_block", "crafting_table", "furnace", "wooden_slab", "stone_slab" };
        int[] ids = { 32, 33, 34, 35, 36, 37, 46, 47 };

        bool createdAny = false;
        // Keep forceGenerate as false so we do not overwrite the permanent, hand-crafted sprites
        bool forceGenerate = false;

        for (int i = 0; i < names.Length; i++)
        {
            string path = System.IO.Path.Combine(dir, names[i] + ".png");
            if (forceGenerate || !System.IO.File.Exists(path))
            {
                // Generate the sprite texture
                Sprite sprite = MakeIsometricBlock(ids[i], Color.white);
                Texture2D tex = sprite.texture;
                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(path, bytes);
                Debug.Log($"[StarterItems] Generated and saved sprite: {path}");
                createdAny = true;
            }
        }

#if UNITY_EDITOR
        if (createdAny)
        {
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.EditorPrefs.SetBool("StarterItems_SpritesGenerated_v3", true);
        }
#endif
    }

    private void ApplyTransparencyKey(string path)
    {
        if (!System.IO.File.Exists(path)) return;
        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                Color[] pixels = tex.GetPixels();
                bool modified = false;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    // If pixel is black or near-black
                    if (c.r < 0.12f && c.g < 0.12f && c.b < 0.12f)
                    {
                        pixels[i] = Color.clear;
                        modified = true;
                    }
                }
                if (modified)
                {
                    tex.SetPixels(pixels);
                    tex.Apply();
                    byte[] outBytes = tex.EncodeToPNG();
                    System.IO.File.WriteAllBytes(path, outBytes);
                    Debug.Log($"[StarterItems] Keyed transparency for: {path}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StarterItems] Error applying transparency key: {e}");
        }
    }

    public static Color GetBlockColor(int blockTypeID)
    {
        switch (blockTypeID)
        {
            case 1: return new Color(0.55f, 0.38f, 0.17f); // Wood
            case 2: return new Color(0.72f, 0.58f, 0.37f); // Plank
            case 3: return new Color(0.52f, 0.52f, 0.54f); // Stone
            case 4: return new Color(0.48f, 0.35f, 0.24f); // Dirt
            case 9: return new Color(1.00f, 0.28f, 0.55f); // Flower
            case 10: return new Color(0.95f, 0.85f, 0.10f); // Dandelion
            case 11: return new Color(0.40f, 0.20f, 0.90f); // Iris
            case 12: return new Color(0.22f, 0.55f, 0.18f); // Leaves
            case 13: return new Color(0.22f, 0.55f, 0.18f); // Short Grass
            case 14: return new Color(0.20f, 0.50f, 0.12f); // Tall Grass
            case 30: return new Color(0.20f, 0.20f, 0.20f); // Coal Ore
            case 31: return new Color(0.85f, 0.65f, 0.52f); // Iron Ore
            case 32: return new Color(0.98f, 0.82f, 0.15f); // Gold Block
            case 33: return new Color(0.88f, 0.93f, 0.98f); // Iron Block
            case 34: return new Color(0.86f, 0.78f, 0.58f); // Sand
            case 35: return new Color(0.80f, 0.90f, 0.95f); // Glass
            case 37: return new Color(0.50f, 0.50f, 0.50f); // Furnace
            case 46: return new Color(0.72f, 0.58f, 0.37f); // Wooden Slab
            case 47: return new Color(0.52f, 0.52f, 0.54f); // Stone Slab
            default: return Color.white;
        }
    }

    public static Item CreateItemInstance(string itemName, int blockTypeID, Color fallbackColor)
    {
        Item item        = ScriptableObject.CreateInstance<Item>();
        item.itemName    = itemName;
        item.itemID      = 0;

        ToolType tType;
        ToolTier tTier;
        Inventory.ParseToolName(itemName, out tType, out tTier);
        if (tType != ToolType.None)
        {
            item.toolType = tType;
            item.toolTier = tTier;
            item.icon = Inventory.CreateToolIcon(tType, tTier);
            return item;
        }
        
        if (itemName.Equals("Wrench", System.StringComparison.OrdinalIgnoreCase))
        {
            item.itemID = 99;
        }

        item.blockTypeID = blockTypeID;

        Color actualColor = fallbackColor;
        if (actualColor == Color.white && blockTypeID != 0)
        {
            actualColor = GetBlockColor(blockTypeID);
        }

        // Try to load the real sprite from Resources first (exact match wins over partial)
        Sprite loaded = null;
        {
            string resPath = null;
            foreach (var kvp in SpriteMap)
                if (string.Equals(itemName, kvp.Key, System.StringComparison.OrdinalIgnoreCase))
                { resPath = kvp.Value; break; }
            if (resPath == null)
                foreach (var kvp in SpriteMap)
                    if (itemName.ToLower().Contains(kvp.Key.ToLower()))
                    { resPath = kvp.Value; break; }
            if (resPath != null) loaded = Resources.Load<Sprite>(resPath);
        }

        if (itemName.Equals("Wrench", System.StringComparison.OrdinalIgnoreCase))
        {
            Sprite wrenchLoaded = Resources.Load<Sprite>("WrenchIcon");
            item.icon = (wrenchLoaded != null) ? wrenchLoaded : MakeBlockIcon(actualColor, blockTypeID);
        }
        else if (itemName.Equals("Control Block", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 50)
        {
            item.icon = VehicleSpawner.CreateControlBlockIcon();
        }
        else if (itemName.Equals("Small Wheel", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 20)
        {
            item.icon = VehicleSpawner.CreateWheelIcon(false);
        }
        else if (itemName.Equals("Large Wheel", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 21)
        {
            item.icon = VehicleSpawner.CreateWheelIcon(true);
        }
        else if (itemName.Equals("Propeller", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 22)
        {
            item.icon = VehicleSpawner.CreatePropellerIcon();
        }
        else if (itemName.Equals("Flower", System.StringComparison.OrdinalIgnoreCase))
        {
            item.icon = VoxelWorld.MakeFlowerIcon();
        }
        else if (itemName.Equals("Dandelion", System.StringComparison.OrdinalIgnoreCase))
        {
            item.icon = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.95f, 0.85f, 0.10f), new Color(0.95f, 0.65f, 0.05f));
        }
        else if (itemName.Equals("Iris", System.StringComparison.OrdinalIgnoreCase))
        {
            item.icon = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.40f, 0.20f, 0.90f), new Color(1.00f, 0.80f, 0.10f));
        }
        else if (itemName.Equals("Grass Block", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 4)
        {
            item.icon = MakeGrassBlockIcon();
        }
        else if (itemName.Equals("Short Grass", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 13)
        {
            item.icon = MakeShortGrassIcon();
        }
        else if (itemName.Equals("Tall Grass", System.StringComparison.OrdinalIgnoreCase) || blockTypeID == 14)
        {
            item.icon = MakeTallGrassIcon();
        }
        else
        {
            item.icon = loaded != null ? loaded : MakeBlockIcon(actualColor, blockTypeID);
        }

        return item;
    }

    private void GiveItem(string itemName, int blockTypeID, Color fallbackColor, int amount)
    {
        Item item = CreateItemInstance(itemName, blockTypeID, fallbackColor);
        bool added = Hotbar.Instance.TryAddItem(item, amount);
        if (!added)
            Debug.LogWarning($"[StarterItems] Hotbar full — could not add '{itemName}'.");
        else
            Debug.Log($"[StarterItems] Added {amount}x {itemName} to hotbar.");
    }

    /// <summary>Adds an item directly to the Inventory bag (not the hotbar).</summary>
    private void GiveInventoryItem(string itemName, int blockTypeID, Color fallbackColor, int amount)
    {
        if (Inventory.Instance == null)
        {
            Debug.LogWarning($"[StarterItems] Inventory.Instance is null — cannot add '{itemName}'.");
            return;
        }

        Item item = CreateItemInstance(itemName, blockTypeID, fallbackColor);
        bool added = Inventory.Instance.Add(item, amount);
        if (!added)
            Debug.LogWarning($"[StarterItems] Inventory full — could not add '{itemName}'.");
        else
            Debug.Log($"[StarterItems] Added {amount}x {itemName} to inventory bag.");
    }

    private static Dictionary<Color, Sprite> _blockIconCache = new Dictionary<Color, Sprite>();

    // ── Fallback: procedural block icon ───────────────────────────────────────
    private static Dictionary<int, Sprite> _blockIconCacheByID = new Dictionary<int, Sprite>();

    // ── Fallback: procedural block icon ───────────────────────────────────────
    public static Sprite MakeBlockIcon(Color baseColor, int blockTypeID = -1)
    {
        if (blockTypeID != -1)
        {
            if (_blockIconCacheByID.TryGetValue(blockTypeID, out Sprite cached))
                return cached;

            Sprite generated = MakeIsometricBlock(blockTypeID, baseColor);
            _blockIconCacheByID[blockTypeID] = generated;
            return generated;
        }

        if (_blockIconCache.TryGetValue(baseColor, out Sprite cachedFallback))
        {
            return cachedFallback;
        }

        Sprite fallback = MakeIsometricBlock(-1, baseColor);
        _blockIconCache[baseColor] = fallback;
        return fallback;
    }

    public static Sprite MakeGrassBlockIcon()
    {
        // Load the generated Minecraft-style grass block PNG from Resources
        Sprite loaded = Resources.Load<Sprite>("Sprites/grass_block");
        if (loaded != null) return loaded;

        // Fallback: procedural grass block using the unified isometric system
        return MakeIsometricBlock(4, Color.white);
    }

    private static Sprite _cachedGrassFoliageIcon;

    public static Sprite MakeShortGrassIcon()
    {
        if (_cachedGrassFoliageIcon != null) return _cachedGrassFoliageIcon;

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color darkGreen = new Color(0.18f, 0.48f, 0.08f);
        Color midGreen  = new Color(0.22f, 0.55f, 0.18f);
        Color lightGreen = new Color(0.28f, 0.68f, 0.15f);

        // Draw multiple blades of grass starting from the bottom-center/bottom-sides
        // Blade 1 (tall center, slightly left-leaning)
        for (int y = 4; y < 48; y++)
        {
            int x = 32 - (y - 4) / 6;
            Set(x, y, midGreen);
            Set(x + 1, y, midGreen);
        }
        // Blade 2 (shorter left, leaning left)
        for (int y = 4; y < 32; y++)
        {
            int x = 24 - (y - 4) / 2;
            Set(x, y, darkGreen);
            Set(x + 1, y, darkGreen);
        }
        // Blade 3 (shorter right, leaning right)
        for (int y = 4; y < 36; y++)
        {
            int x = 40 + (y - 4) / 3;
            Set(x, y, lightGreen);
            Set(x - 1, y, lightGreen);
        }
        // Blade 4 (mid-tall center-right, straight-ish)
        for (int y = 4; y < 42; y++)
        {
            int x = 35 + (y - 4) / 10;
            Set(x, y, lightGreen);
            Set(x + 1, y, lightGreen);
        }
        // Blade 5 (short center-left, straight-ish)
        for (int y = 4; y < 24; y++)
        {
            int x = 28 - (y - 4) / 8;
            Set(x, y, darkGreen);
            Set(x + 1, y, darkGreen);
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();

        _cachedGrassFoliageIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedGrassFoliageIcon;
    }

    private static Sprite _cachedTallGrassFoliageIcon;

    public static Sprite MakeTallGrassIcon()
    {
        if (_cachedTallGrassFoliageIcon != null) return _cachedTallGrassFoliageIcon;

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color darkGreen = new Color(0.15f, 0.42f, 0.05f);
        Color midGreen  = new Color(0.20f, 0.50f, 0.12f);
        Color lightGreen = new Color(0.25f, 0.62f, 0.15f);

        // Draw multiple tall blades of grass
        // Blade 1 (tall center-left, leaning left)
        for (int y = 4; y < 58; y++)
        {
            int x = 28 - (y - 4) / 4;
            Set(x, y, lightGreen);
            Set(x + 1, y, lightGreen);
        }
        // Blade 2 (tall center-right, leaning right)
        for (int y = 4; y < 56; y++)
        {
            int x = 36 + (y - 4) / 5;
            Set(x, y, lightGreen);
            Set(x - 1, y, lightGreen);
        }
        // Blade 3 (straight center)
        for (int y = 4; y < 52; y++)
        {
            int x = 32;
            Set(x, y, midGreen);
            Set(x + 1, y, midGreen);
        }
        // Blade 4 (shorter left)
        for (int y = 4; y < 38; y++)
        {
            int x = 20 - (y - 4) / 2;
            Set(x, y, darkGreen);
            Set(x + 1, y, darkGreen);
        }
        // Blade 5 (shorter right)
        for (int y = 4; y < 40; y++)
        {
            int x = 44 + (y - 4) / 2;
            Set(x, y, darkGreen);
            Set(x - 1, y, darkGreen);
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();

        _cachedTallGrassFoliageIcon = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        return _cachedTallGrassFoliageIcon;
    }

    private static Sprite _cachedGlassIcon;

    public static Sprite MakeGlassBlockIcon()
    {
        if (_cachedGlassIcon != null) return _cachedGlassIcon;
        _cachedGlassIcon = MakeIsometricBlock(35, Color.white);
        return _cachedGlassIcon;
    }

    private static int GetIsometricFace(int x, int y)
    {
        if (x < 14 || x > 50) return 0;
        
        float dx = Mathf.Abs(x - 32);
        float topLow = 26f + dx * 0.5f;
        float topHigh = 44f - dx * 0.5f;
        
        if (y >= topLow && y <= topHigh)
        {
            return 1; // Top face
        }
        
        if (x < 32)
        {
            float leftLow = 12f + (32 - x) * 0.5f;
            if (y >= leftLow && y < topLow) return 2; // Left face
        }
        else
        {
            float rightLow = 12f + (x - 32) * 0.5f;
            if (y >= rightLow && y < topLow) return 3; // Right face
        }
        
        return 0;
    }

    private static int GetIsometricFaceForSlab(int x, int y)
    {
        if (x < 14 || x > 50) return 0;
        
        float dx = Mathf.Abs(x - 32);
        // Shift top face down by 9 pixels
        float topLow = 17f + dx * 0.5f;
        float topHigh = 35f - dx * 0.5f;
        
        if (y >= topLow && y <= topHigh)
        {
            return 1; // Top face
        }
        
        if (x < 32)
        {
            float leftLow = 12f + (32 - x) * 0.5f;
            if (y >= leftLow && y < topLow) return 2; // Left face
        }
        else
        {
            float rightLow = 12f + (x - 32) * 0.5f;
            if (y >= rightLow && y < topLow) return 3; // Right face
        }
        
        return 0;
    }

    private static bool IsNearEdge(int x, int y, int dist, bool isSlab)
    {
        int myFace = isSlab ? GetIsometricFaceForSlab(x, y) : GetIsometricFace(x, y);
        if (myFace == 0) return false;
        
        for (int dy = -dist; dy <= dist; dy++)
        {
            for (int dx = -dist; dx <= dist; dx++)
            {
                int faceAt = isSlab ? GetIsometricFaceForSlab(x + dx, y + dy) : GetIsometricFace(x + dx, y + dy);
                if (faceAt != myFace) return true;
            }
        }
        return false;
    }

    public static Sprite MakeIsometricBlock(int blockTypeID, Color baseColor)
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        // Base outline color based on blockTypeID
        Color outlineColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        if (blockTypeID == 32) outlineColor = new Color(0.35f, 0.28f, 0.05f, 1f); // Gold
        else if (blockTypeID == 33) outlineColor = new Color(0.30f, 0.32f, 0.35f, 1f); // Iron
        else if (blockTypeID == 8 || blockTypeID == 34) outlineColor = new Color(0.40f, 0.35f, 0.25f, 1f); // Sand
        else if (blockTypeID == 35) outlineColor = new Color(0.15f, 0.22f, 0.28f, 1.0f); // Glass
        else if (blockTypeID == 36) outlineColor = new Color(0.25f, 0.15f, 0.05f, 1f);   // Crafting Table

        bool isSlab = (blockTypeID == 46 || blockTypeID == 47);
        float slabHeight = isSlab ? 7f : 14f;

        // Draw pixel-by-pixel
        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                int face = isSlab ? GetIsometricFaceForSlab(x, y) : GetIsometricFace(x, y);
                if (face == 0) continue; // background

                // 1. Outline detection (any 4-neighbor belongs to a different face)
                bool isOutline = false;
                if (x == 0 || x == SZ - 1 || y == 0 || y == SZ - 1)
                {
                    isOutline = true;
                }
                else
                {
                    int nL = isSlab ? GetIsometricFaceForSlab(x - 1, y) : GetIsometricFace(x - 1, y);
                    int nR = isSlab ? GetIsometricFaceForSlab(x + 1, y) : GetIsometricFace(x + 1, y);
                    int nU = isSlab ? GetIsometricFaceForSlab(x, y + 1) : GetIsometricFace(x, y + 1);
                    int nD = isSlab ? GetIsometricFaceForSlab(x, y - 1) : GetIsometricFace(x, y - 1);
                    if (nL != face || nR != face || nU != face || nD != face)
                    {
                        isOutline = true;
                    }
                }

                if (isOutline)
                {
                    px[y * SZ + x] = outlineColor;
                    continue;
                }

                // 2. Map (x, y) to texture coordinates (u, v) from 0 to 15
                float u = 0f, v = 0f;
                if (face == 1) // Top Face
                {
                    float yOffset = isSlab ? 9f : 0f;
                    u = 0.5f * ((x - 14) / 1.2f + (y + yOffset - 35) / 0.6f);
                    v = 0.5f * ((x - 14) / 1.2f - (y + yOffset - 35) / 0.6f);
                }
                else if (face == 2) // Left Face
                {
                    u = (x - 14) / 1.2f;
                    v = 15f * (y - (12f + (32f - x) * 0.5f)) / slabHeight;
                }
                else if (face == 3) // Right Face
                {
                    u = (x - 32) / 1.2f;
                    v = 15f * (y - (12f + (x - 32f) * 0.5f)) / slabHeight;
                }

                int tu = Mathf.Clamp(Mathf.RoundToInt(u), 0, 15);
                int tv = Mathf.Clamp(Mathf.RoundToInt(v), 0, 15);

                // Find tile index
                int tileIndex = 3; // default stone
                if (blockTypeID == 30) tileIndex = 18;      // Coal Ore
                else if (blockTypeID == 31) tileIndex = 19; // Iron Ore
                else if (blockTypeID == 32) tileIndex = 20; // Gold Block
                else if (blockTypeID == 33) tileIndex = 21; // Iron Block
                else if (blockTypeID == 8 || blockTypeID == 34) tileIndex = 8; // Sand
                else if (blockTypeID == 12) tileIndex = 12; // Leaves
                else if (blockTypeID == 35) tileIndex = 22; // Glass
                else if (blockTypeID == 36) tileIndex = (face == 1) ? 23 : 24; // Crafting Table
                else if (blockTypeID == 37) tileIndex = (face == 3) ? 25 : 3;  // Furnace front is 25, others are stone (3)
                else if (blockTypeID == 1) tileIndex = (face == 1) ? 4 : 5; // Wood
                else if (blockTypeID == 4 || blockTypeID == 6) tileIndex = (face == 1) ? 0 : 1; // Grass
                else if (blockTypeID == 5) tileIndex = 2; // Dirt
                else if (blockTypeID == 2 || blockTypeID == 46) tileIndex = 6; // Plank / Wooden Slab
                else if (blockTypeID == 3 || blockTypeID == 47) tileIndex = 3; // Stone / Stone Slab

                Color col = GrassTextureGenerator.GetPixel(tileIndex, tu, tv);

                // Apply lighting based on face
                if (face == 1) // Top
                {
                    col.r = Mathf.Clamp01(col.r * 1.05f);
                    col.g = Mathf.Clamp01(col.g * 1.05f);
                    col.b = Mathf.Clamp01(col.b * 1.05f);
                }
                else if (face == 2) // Left
                {
                    col.r = Mathf.Clamp01(col.r * 0.85f);
                    col.g = Mathf.Clamp01(col.g * 0.85f);
                    col.b = Mathf.Clamp01(col.b * 0.85f);
                }
                else if (face == 3) // Right
                {
                    col.r = Mathf.Clamp01(col.r * 0.70f);
                    col.g = Mathf.Clamp01(col.g * 0.70f);
                    col.b = Mathf.Clamp01(col.b * 0.70f);
                }

                // Handle transparency for Glass (Survivalcraft/Minecraft Style)
                if (blockTypeID == 35)
                {
                    // Check if sampled color is the interior light-blue tint (which we want transparent)
                    // The interior of MinecraftGlass in GrassTextureGenerator is new Color(0.80f, 0.90f, 0.95f)
                    bool isInterior = (Mathf.Abs(col.r - 0.80f) < 0.05f && Mathf.Abs(col.g - 0.90f) < 0.05f && Mathf.Abs(col.b - 0.95f) < 0.05f);
                    if (isInterior)
                    {
                        col = new Color(0.80f, 0.90f, 0.95f, 0.15f); // semi-transparent inside
                        
                        // Add white diagonal reflections inside the transparent center
                        if ((face == 2 || face == 3 || face == 1) && (tu + tv == 13 || tu + tv == 14) && tu >= 4 && tu <= 11)
                        {
                            col = new Color(1.0f, 1.0f, 1.0f, 0.90f); // bright white glint
                        }
                    }
                    else
                    {
                        col.a = 1.0f;
                    }
                }

                px[y * SZ + x] = col;
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color Brighten(Color c, float amt) =>
        new Color(Mathf.Clamp01(c.r+amt), Mathf.Clamp01(c.g+amt), Mathf.Clamp01(c.b+amt), c.a);

    private static Color Darken(Color c, float amt) =>
        new Color(Mathf.Clamp01(c.r-amt), Mathf.Clamp01(c.g-amt), Mathf.Clamp01(c.b-amt), c.a);
}
