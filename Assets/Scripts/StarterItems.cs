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
        { "Wood",   "Sprites/wood_block"   },
        { "Plank",  "Sprites/plank_block"  },
        { "Stone",  "Sprites/stone_block"  },
        { "Dirt",   "Sprites/dirt_block"   },
        { "Iron",   "Sprites/iron_bar"     },
        { "Stick",  "Sprites/stick"        },
        { "String", "Sprites/string"       },
    };

    private IEnumerator Start()
    {
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

        // ── Inventory bag: flowers (decorative items) ─────────────────────────
        GiveInventoryItem("Flower",    blockTypeID: 9,  fallbackColor: new Color(1.00f, 0.28f, 0.55f), amount: 64);
        GiveInventoryItem("Dandelion", blockTypeID: 10, fallbackColor: new Color(0.95f, 0.85f, 0.10f), amount: 64);
        GiveInventoryItem("Iris",      blockTypeID: 11, fallbackColor: new Color(0.40f, 0.20f, 0.90f), amount: 64);
    }

    private void GiveItem(string itemName, int blockTypeID, Color fallbackColor, int amount)
    {
        Item item        = ScriptableObject.CreateInstance<Item>();
        item.itemName    = itemName;
        item.itemID      = 0;
        item.blockTypeID = blockTypeID;

        // Try to load the real sprite from Resources first
        Sprite loaded = null;
        foreach (var kvp in SpriteMap)
        {
            if (itemName.ToLower().Contains(kvp.Key.ToLower()))
            {
                loaded = Resources.Load<Sprite>(kvp.Value);
                break;
            }
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
            item.icon = loaded != null ? loaded : MakeBlockIcon(fallbackColor);
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

        if (itemName.Equals("Flower", System.StringComparison.OrdinalIgnoreCase))
            item.icon = VoxelWorld.MakeFlowerIcon();
        else if (itemName.Equals("Dandelion", System.StringComparison.OrdinalIgnoreCase))
            item.icon = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.95f, 0.85f, 0.10f), new Color(0.95f, 0.65f, 0.05f));
        else if (itemName.Equals("Iris", System.StringComparison.OrdinalIgnoreCase))
            item.icon = VoxelWorld.MakeFlowerIcon(new Color(0.22f, 0.58f, 0.12f), new Color(0.40f, 0.20f, 0.90f), new Color(1.00f, 0.80f, 0.10f));
        else if (itemName.Equals("Grass", System.StringComparison.OrdinalIgnoreCase))
            item.icon = MakeGrassBlockIcon();
        else
            item.icon = MakeBlockIcon(fallbackColor);

        bool added = Inventory.Instance.Add(item, amount);
        if (!added)
            Debug.LogWarning($"[StarterItems] Inventory full — could not add '{itemName}'.");
        else
            Debug.Log($"[StarterItems] Added {amount}x {itemName} to inventory bag.");
    }

    private static Dictionary<Color, Sprite> _blockIconCache = new Dictionary<Color, Sprite>();

    // ── Fallback: procedural block icon ───────────────────────────────────────
    public static Sprite MakeBlockIcon(Color baseColor)
    {
        if (_blockIconCache.TryGetValue(baseColor, out Sprite cached))
        {
            return cached;
        }

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color top     = Brighten(baseColor,  0.25f);
        Color front   = baseColor;
        Color side    = Darken(baseColor,    0.20f);
        Color outline = Darken(baseColor,    0.45f);

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        void FillRect(int x, int y, int w, int h, Color c)
        { for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++) Set(x+dx, y+dy, c); }

        FillRect(16, 38, 32, 14, top);
        FillRect(8,  14, 24, 24, front);
        FillRect(32, 14, 24, 24, side);
        for (int x = 8;  x < 40; x++) Set(x, 13, outline);
        for (int x = 32; x < 56; x++) Set(x, 13, outline);
        for (int y = 13; y < 38; y++) Set(7,  y,  outline);
        for (int y = 13; y < 38; y++) Set(56, y,  outline);
        for (int x = 8;  x < 32; x++) Set(x, 38, outline);
        for (int x = 32; x < 56; x++) Set(x, 38, outline);
        for (int y = 38; y < 52; y++) Set(16, y,  outline);
        for (int y = 38; y < 52; y++) Set(47, y,  outline);

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        Sprite result = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
        _blockIconCache[baseColor] = result;
        return result;
    }

    public static Sprite MakeGrassBlockIcon()
    {
        // Load the generated Minecraft-style grass block PNG from Resources
        Sprite loaded = Resources.Load<Sprite>("Sprites/grass_block");
        if (loaded != null) return loaded;

        // Fallback: procedural grass block using the same style as other blocks
        Color grassGreen = new Color(0.35f, 0.65f, 0.25f, 1f);
        Color dirtBrown  = new Color(0.45f, 0.30f, 0.18f, 1f);

        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color top     = Brighten(grassGreen, 0.15f);
        Color front   = dirtBrown;
        Color side    = Darken(dirtBrown, 0.20f);
        Color outline = Darken(dirtBrown, 0.45f);

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        void FillRect(int x, int y, int w, int h, Color c)
        { for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++) Set(x+dx, y+dy, c); }

        FillRect(16, 38, 32, 14, top);
        FillRect(8,  14, 24, 24, front);
        FillRect(32, 14, 24, 24, side);
        for (int x = 8;  x < 40; x++) Set(x, 13, outline);
        for (int x = 32; x < 56; x++) Set(x, 13, outline);
        for (int y = 13; y < 38; y++) Set(7,  y,  outline);
        for (int y = 13; y < 38; y++) Set(56, y,  outline);
        for (int x = 8;  x < 32; x++) Set(x, 38, outline);
        for (int x = 32; x < 56; x++) Set(x, 38, outline);
        for (int y = 38; y < 52; y++) Set(16, y,  outline);
        for (int y = 38; y < 52; y++) Set(47, y,  outline);

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
