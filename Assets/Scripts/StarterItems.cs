using System.Collections;
using UnityEngine;

/// <summary>
/// Gives the player a starting set of placeable block items on first play.
/// Attach to the Player GameObject.
/// Items are created at runtime — no ScriptableObject assets needed.
/// </summary>
public class StarterItems : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Wait one frame so Hotbar.Start() has built its slot UI first
        yield return null;

        if (Hotbar.Instance == null)
        {
            Debug.LogError("[StarterItems] Hotbar.Instance is null.");
            yield break;
        }

        // Give one stack of each placeable block type
        GiveItem("Wood",  blockTypeID: 1, iconColor: new Color(0.35f, 0.65f, 0.15f), amount: 64); // grass-green
        GiveItem("Plank", blockTypeID: 2, iconColor: new Color(0.55f, 0.38f, 0.17f), amount: 64); // dirt-brown
        GiveItem("Stone", blockTypeID: 3, iconColor: new Color(0.52f, 0.52f, 0.54f), amount: 64); // grey
    }

    private void GiveItem(string itemName, int blockTypeID, Color iconColor, int amount)
    {
        Item item          = ScriptableObject.CreateInstance<Item>();
        item.itemName      = itemName;
        item.itemID        = 0;           // block items don't need a unique tool ID
        item.blockTypeID   = blockTypeID;
        item.icon          = MakeBlockIcon(iconColor);

        bool added = Hotbar.Instance.TryAddItem(item, amount);
        if (!added)
            Debug.LogWarning($"[StarterItems] Hotbar full — could not add '{itemName}'.");
        else
            Debug.Log($"[StarterItems] Added {amount}x {itemName} (blockTypeID={blockTypeID}) to hotbar.");
    }

    // ── Procedural block icon ──────────────────────────────────────────────────

    /// <summary>
    /// Draws a simple isometric-style block face icon (64×64) in the given color.
    /// </summary>
    private static Sprite MakeBlockIcon(Color baseColor)
    {
        const int SZ = 64;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        Color top     = Brighten(baseColor,  0.25f); // lighter top face
        Color front   = baseColor;                   // mid front face
        Color side    = Darken(baseColor,    0.20f); // darker right face
        Color outline = Darken(baseColor,    0.45f); // dark border

        void Set(int x, int y, Color c)
        {
            if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c;
        }

        void FillRect(int x, int y, int w, int h, Color c)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    Set(x + dx, y + dy, c);
        }

        // Isometric block layout in 64x64:
        //   Top face:    center-top
        //   Front face:  lower-left
        //   Right face:  lower-right

        // Top face (parallelogram approximation using rectangles)
        FillRect(16, 38, 32, 14, top);

        // Front face
        FillRect(8,  14, 24, 24, front);

        // Right face
        FillRect(32, 14, 24, 24, side);

        // Simple 1-px outline on visible edges
        for (int x = 8;  x < 40; x++) Set(x, 13, outline); // top-left edge
        for (int x = 32; x < 56; x++) Set(x, 13, outline); // top-right edge
        for (int y = 13; y < 38; y++) Set(7,  y,  outline); // left edge
        for (int y = 13; y < 38; y++) Set(56, y,  outline); // right edge
        for (int x = 8;  x < 32; x++) Set(x, 38, outline); // front-bottom
        for (int x = 32; x < 56; x++) Set(x, 38, outline); // side-bottom
        for (int y = 38; y < 52; y++) Set(16, y,  outline); // centre divider
        for (int y = 38; y < 52; y++) Set(47, y,  outline); // right divider

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color Brighten(Color c, float amt) =>
        new Color(Mathf.Clamp01(c.r + amt), Mathf.Clamp01(c.g + amt), Mathf.Clamp01(c.b + amt), c.a);

    private static Color Darken(Color c, float amt) =>
        new Color(Mathf.Clamp01(c.r - amt), Mathf.Clamp01(c.g - amt), Mathf.Clamp01(c.b - amt), c.a);
}
