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
        { "Glass",      "Sprites/glass_block"      },
    };

    private IEnumerator Start()
    {
        GenerateBlockSpritesIfMissing();

        // Wait one frame so Hotbar and Inventory have finished their Awake/Start
        yield return null;

        if (Hotbar.Instance == null)
        {
            Debug.LogError("[StarterItems] Hotbar.Instance is null.");
            yield break;
        }

        // ── Hotbar: building blocks (quick-access) ────────────────────────────
        GiveItem("Wood",  blockTypeID: 1, fallbackColor: new Color(0.55f, 0.38f, 0.17f), amount: 64);
        GiveItem("Plank", blockTypeID: 2, fallbackColor: new Color(0.72f, 0.58f, 0.37f), amount: 64);
        GiveItem("Stone", blockTypeID: 3, fallbackColor: new Color(0.52f, 0.52f, 0.54f), amount: 64);

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
    }

    private void GenerateBlockSpritesIfMissing()
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "Resources/Sprites");
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        string[] names = { "coal_ore_block", "iron_ore_block", "gold_block", "iron_block", "sand_block", "glass_block" };
        int[] ids = { 30, 31, 32, 33, 34, 35 };

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

    private void GiveItem(string itemName, int blockTypeID, Color fallbackColor, int amount)
    {
        Item item        = ScriptableObject.CreateInstance<Item>();
        item.itemName    = itemName;
        item.itemID      = 0;
        item.blockTypeID = blockTypeID;

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

        if (itemName.Equals("Flower", System.StringComparison.OrdinalIgnoreCase))
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
        else if (itemName.Equals("Grass", System.StringComparison.OrdinalIgnoreCase))
        {
            item.icon = MakeGrassBlockIcon();
        }
        else
        {
            item.icon = loaded != null ? loaded : MakeBlockIcon(fallbackColor, blockTypeID);
        }

        bool added = Hotbar.Instance.TryAddItem(item, amount);
        if (!added)
            Debug.LogWarning($"[StarterItems] Hotbar full — could not add '{itemName}'.");
        else
            Debug.Log($"[StarterItems] Added {amount}x {itemName} to hotbar " +
                      $"(icon={(loaded != null ? "sprite" : "procedural")}).");
    }

    /// <summary>Adds an item directly to the Inventory bag (not the hotbar).</summary>
    private void GiveInventoryItem(string itemName, int blockTypeID, Color fallbackColor, int amount)
    {
        if (Inventory.Instance == null)
        {
            Debug.LogWarning($"[StarterItems] Inventory.Instance is null — cannot add '{itemName}'.");
            return;
        }

        Item item        = ScriptableObject.CreateInstance<Item>();
        item.itemName    = itemName;
        item.itemID      = 0;
        item.blockTypeID = blockTypeID;

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

        if (itemName.Equals("Flower", System.StringComparison.OrdinalIgnoreCase))
            item.icon = VoxelWorld.MakeFlowerIcon();
        else if (itemName.Equals("Dandelion", System.StringComparison.OrdinalIgnoreCase))
            item.icon = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.95f, 0.85f, 0.10f), new Color(0.95f, 0.65f, 0.05f));
        else if (itemName.Equals("Iris", System.StringComparison.OrdinalIgnoreCase))
            item.icon = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.40f, 0.20f, 0.90f), new Color(1.00f, 0.80f, 0.10f));
        else if (itemName.Equals("Grass", System.StringComparison.OrdinalIgnoreCase))
            item.icon = MakeGrassBlockIcon();
        else if (loaded != null)
            item.icon = loaded;
        else
            item.icon = MakeBlockIcon(fallbackColor, blockTypeID);

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

    private static bool IsNearEdge(int x, int y, int dist)
    {
        int myFace = GetIsometricFace(x, y);
        if (myFace == 0) return false;
        
        for (int dy = -dist; dy <= dist; dy++)
        {
            for (int dx = -dist; dx <= dist; dx++)
            {
                if (GetIsometricFace(x + dx, y + dy) != myFace) return true;
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

        // Draw pixel-by-pixel
        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                int face = GetIsometricFace(x, y);
                if (face == 0) continue; // background

                // 1. Outline detection (any 4-neighbor belongs to a different face)
                bool isOutline = false;
                if (x == 0 || x == SZ - 1 || y == 0 || y == SZ - 1)
                {
                    isOutline = true;
                }
                else
                {
                    int nL = GetIsometricFace(x - 1, y);
                    int nR = GetIsometricFace(x + 1, y);
                    int nU = GetIsometricFace(x, y + 1);
                    int nD = GetIsometricFace(x, y - 1);
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
                    u = 0.5f * ((x - 14) / 1.2f + (y - 35) / 0.6f);
                    v = 0.5f * ((x - 14) / 1.2f - (y - 35) / 0.6f);
                }
                else if (face == 2) // Left Face
                {
                    u = (x - 14) / 1.2f;
                    v = 15f * (y - 21 + 0.5f * (x - 14)) / 14f;
                }
                else if (face == 3) // Right Face
                {
                    u = (x - 32) / 1.2f;
                    v = 15f * (y - 12 - 0.5f * (x - 32)) / 14f;
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
                else if (blockTypeID == 35) tileIndex = 22; // Glass
                else if (blockTypeID == 1) tileIndex = (face == 1) ? 4 : 5; // Wood
                else if (blockTypeID == 4 || blockTypeID == 6) tileIndex = (face == 1) ? 0 : 1; // Grass
                else if (blockTypeID == 5) tileIndex = 2; // Dirt
                else if (blockTypeID == 2) tileIndex = 6; // Plank
                else if (blockTypeID == 3) tileIndex = 3; // Stone

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
