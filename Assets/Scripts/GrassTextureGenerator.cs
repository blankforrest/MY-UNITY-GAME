using UnityEngine;

/// <summary>
/// Generates a pixel-art grass block texture atlas procedurally in code.
/// Atlas layout (left to right): [Grass Top | Grass Side | Dirt Bottom]
/// Each tile is TILE_SIZE x TILE_SIZE. Total atlas: (TILE_SIZE*3) x TILE_SIZE.
/// </summary>
public static class GrassTextureGenerator
{
    public const int TILE_SIZE  = 16;
    public const int TILE_COUNT = 3;  // top, side, bottom

    public static Texture2D Create()
    {
        int w = TILE_SIZE * TILE_COUNT;
        int h = TILE_SIZE;

        Texture2D atlas = new Texture2D(w, h, TextureFormat.RGB24, false);
        atlas.filterMode = FilterMode.Point; // pixel-art crisp look
        atlas.wrapMode   = TextureWrapMode.Clamp;

        Color[] px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int tile = x / TILE_SIZE;
                int lx   = x % TILE_SIZE;
                px[y * w + x] = tile == 0 ? GrassTop(lx, y)
                              : tile == 1 ? GrassSide(lx, y)
                                          : Dirt(lx, y);
            }
        }

        atlas.SetPixels(px);
        atlas.Apply();
        return atlas;
    }

    // ── Tile samplers ─────────────────────────────────────────────────────────

    static Color GrassTop(int x, int y)
    {
        // Bright grassy green with Perlin variation — original colour (not Minecraft)
        float n  = Mathf.PerlinNoise(x * 0.55f + 0.13f, y * 0.55f + 0.17f);
        float n2 = Mathf.PerlinNoise(x * 1.1f  + 2.3f,  y * 1.1f  + 1.9f);
        float t  = n * 0.13f + n2 * 0.04f;

        // Occasional slightly darker patches for organic feel
        bool patch = Mathf.PerlinNoise(x * 0.25f + 5f, y * 0.25f + 3f) > 0.72f;
        float pd = patch ? 0.06f : 0f;

        return new Color(0.28f + t - pd, 0.62f + t * 0.4f - pd, 0.10f + t * 0.2f);
    }

    static Color GrassSide(int x, int y)
    {
        bool isGrassStrip = y >= TILE_SIZE - 3;
        if (isGrassStrip)
        {
            // Top 2-3 px: same green as the top face
            float n = Mathf.PerlinNoise(x * 0.45f + 1.1f, y * 0.6f + 0.4f) * 0.1f;
            return new Color(0.28f + n, 0.60f + n * 0.3f, 0.10f + n * 0.1f);
        }

        // Dirt body
        float dn = Mathf.PerlinNoise(x * 0.35f + 0.7f, y * 0.35f + 1.2f);
        float dn2= Mathf.PerlinNoise(x * 0.9f  + 3.1f, y * 0.9f  + 2.4f);
        float dt = dn * 0.10f + dn2 * 0.04f;

        // Small pebble-like dark spots
        bool spot = Mathf.PerlinNoise(x * 0.5f + 4.5f, y * 0.5f + 6.2f) > 0.78f;
        float sd = spot ? 0.07f : 0f;

        return new Color(0.50f + dt - sd, 0.34f + dt * 0.5f - sd, 0.17f + dt * 0.3f - sd);
    }

    static Color Dirt(int x, int y)
    {
        float n  = Mathf.PerlinNoise(x * 0.35f + 8.0f, y * 0.35f + 5.5f);
        float n2 = Mathf.PerlinNoise(x * 0.8f  + 1.7f, y * 0.8f  + 9.2f);
        float t  = n * 0.10f + n2 * 0.04f;

        bool spot = Mathf.PerlinNoise(x * 0.55f + 7.3f, y * 0.55f + 3.8f) > 0.76f;
        float sd = spot ? 0.06f : 0f;

        return new Color(0.48f + t - sd, 0.33f + t * 0.5f - sd, 0.15f + t * 0.3f - sd);
    }

    // ── UV helpers ────────────────────────────────────────────────────────────

    /// <summary>Returns the 4 atlas UVs for a given face and block type.</summary>
    /// <param name="face">0=back,1=front,2=top,3=bottom,4=left,5=right</param>
    /// <param name="blockType">1=Grass, 2=Dirt</param>
    public static Vector2[] GetBlockUVs(int face, byte blockType)
    {
        int tile;
        if (blockType == 2) // Dirt: all faces use the dirt tile
            tile = 2;
        else // Grass (blockType == 1)
            tile = (face == 2) ? 0   // top    → grass top
                 : (face == 3) ? 2   // bottom → dirt
                 :               1;  // sides  → grass side

        float u0 = tile        / (float)TILE_COUNT;
        float u1 = (tile + 1f) / (float)TILE_COUNT;

        return new Vector2[]
        {
            new Vector2(u0, 0f),
            new Vector2(u0, 1f),
            new Vector2(u1, 0f),
            new Vector2(u1, 1f),
        };
    }

    // Keep old name as alias so existing callers don't break
    public static Vector2[] GetGrassUVs(int face) => GetBlockUVs(face, 1);
}
