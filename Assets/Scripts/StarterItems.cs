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
        { "Grass",  "Sprites/grass_block"  },
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
        // Wait one frame so Hotbar.Start() has built its slot UI first
        yield return null;

        if (Hotbar.Instance == null)
        {
            Debug.LogError("[StarterItems] Hotbar.Instance is null.");
            yield break;
        }

        GiveItem("Wood",  blockTypeID: 1, fallbackColor: new Color(0.55f, 0.38f, 0.17f), amount: 64);
        GiveItem("Plank", blockTypeID: 2, fallbackColor: new Color(0.72f, 0.58f, 0.37f), amount: 64);
        GiveItem("Stone", blockTypeID: 3, fallbackColor: new Color(0.52f, 0.52f, 0.54f), amount: 64);
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

        item.icon = loaded != null ? loaded : MakeBlockIcon(fallbackColor);

        bool added = Hotbar.Instance.TryAddItem(item, amount);
        if (!added)
            Debug.LogWarning($"[StarterItems] Hotbar full — could not add '{itemName}'.");
        else
            Debug.Log($"[StarterItems] Added {amount}x {itemName} " +
                      $"(icon={(loaded != null ? "sprite" : "procedural")}).");
    }

    // ── Fallback: procedural block icon ───────────────────────────────────────
    public static Sprite MakeBlockIcon(Color baseColor)
    {
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
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color Brighten(Color c, float amt) =>
        new Color(Mathf.Clamp01(c.r+amt), Mathf.Clamp01(c.g+amt), Mathf.Clamp01(c.b+amt), c.a);

    private static Color Darken(Color c, float amt) =>
        new Color(Mathf.Clamp01(c.r-amt), Mathf.Clamp01(c.g-amt), Mathf.Clamp01(c.b-amt), c.a);
}
