using UnityEngine;

/// <summary>
/// Generates a pixel-art grass block texture atlas procedurally in code.
/// Atlas layout (left to right): [Grass Top | Grass Side | Dirt Bottom]
/// Each tile is TILE_SIZE x TILE_SIZE. Total atlas: (TILE_SIZE*3) x TILE_SIZE.
/// </summary>
public static class GrassTextureGenerator
{
    public const int TILE_SIZE  = 16;
    public const int TILE_COUNT = 23; // grass top, grass side, dirt, stone, wood top, wood side, plank, water, sand, flower, dandelion, iris, leaves, CB side, CB front, tread, small wheel, large wheel, coal ore, iron ore, gold block, iron block, glass

    public static Texture2D Create()
    {
        int w = TILE_SIZE * TILE_COUNT;
        int h = TILE_SIZE;

        Texture2D atlas = new Texture2D(w, h, TextureFormat.RGBA32, false);
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
                              : tile == 2 ? Dirt(lx, y)
                              : tile == 3 ? Stone(lx, y)
                              : tile == 4 ? WoodTop(lx, y)
                              : tile == 5 ? WoodSide(lx, y)
                              : tile == 6 ? Plank(lx, y)
                              : tile == 7 ? Water(lx, y)
                              : tile == 8 ? Sand(lx, y)
                              : tile == 9 ? Flower(lx, y)
                              : tile == 10 ? Dandelion(lx, y)
                              : tile == 11 ? Iris(lx, y)
                              : tile == 12 ? Leaves(lx, y)
                              : tile == 13 ? ControlBlockSide(lx, y)
                              : tile == 14 ? ControlBlockFront(lx, y)
                              : tile == 15 ? TireTread(lx, y)
                              : tile == 16 ? WheelSide(lx, y, false)
                              : tile == 17 ? WheelSide(lx, y, true)
                              : tile == 18 ? CoalOre(lx, y)
                              : tile == 19 ? IronOre(lx, y)
                              : tile == 20 ? GoldBlock(lx, y)
                              : tile == 21 ? IronBlock(lx, y)
                                           : MinecraftGlass(lx, y);
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

        // ── Soft edge shading (simulates Ambient Occlusion organically) ──
        int distToXEdge = Mathf.Min(x, 15 - x);
        int distToYEdge = Mathf.Min(y, 15 - y);
        int distToEdge = Mathf.Min(distToXEdge, distToYEdge);

        float edgeShadow = 0f;
        if (distToEdge < 3)
        {
            // Calculate a soft shadow factor (strongest at the very edge, fading to 0 by pixel 3)
            float fade = (3f - distToEdge) / 3f;
            
            // Mix in some high-frequency Perlin noise to make the shadow look irregular and natural
            float edgeNoise = Mathf.PerlinNoise(x * 0.8f + 15f, y * 0.8f + 25f);
            edgeShadow = fade * (0.04f + edgeNoise * 0.06f); // very subtle shadow (4% to 10% max)
        }

        return new Color(
            Mathf.Clamp01(0.28f + t - pd - edgeShadow),
            Mathf.Clamp01(0.62f + t * 0.4f - pd - edgeShadow * 0.8f),
            Mathf.Clamp01(0.10f + t * 0.2f - edgeShadow * 0.5f));
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

    static Color Stone(int x, int y)
    {
        float n  = Mathf.PerlinNoise(x * 0.4f + 12f, y * 0.4f + 7f);
        float n2 = Mathf.PerlinNoise(x * 1.1f + 3f,  y * 1.1f + 5f);
        float t  = n * 0.08f + n2 * 0.03f;

        bool crack = Mathf.PerlinNoise(x * 0.6f + 9f, y * 0.6f + 2f) > 0.80f;
        float cd = crack ? 0.07f : 0f;

        return new Color(0.52f + t - cd, 0.52f + t - cd, 0.54f + t - cd);
    }

    static Color WoodTop(int x, int y)
    {
        float dx = x - 7.5f;
        float dy = y - 7.5f;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);

        bool isRing = ((int)dist % 3 == 0);
        if (isRing)
            return new Color(0.40f, 0.28f, 0.15f); // darker brown ring
        else
            return new Color(0.60f, 0.46f, 0.30f); // lighter wood body
    }

    static Color WoodSide(int x, int y)
    {
        float n = Mathf.PerlinNoise(x * 0.9f, y * 0.1f);
        float stripe = Mathf.PerlinNoise(x * 1.5f, 0.5f);
        bool isDark = stripe > 0.6f || (x % 4 == 0 && n > 0.4f);

        if (isDark)
            return new Color(0.28f, 0.18f, 0.08f); // dark brown bark
        else
            return new Color(0.38f, 0.26f, 0.14f); // lighter bark
    }

    static Color Plank(int x, int y)
    {
        bool isLine = (y % 4 == 0);
        bool isSeam = (y / 4 % 2 == 0) ? (x == 4 || x == 12) : (x == 0 || x == 8);

        if (isLine || isSeam)
            return new Color(0.48f, 0.35f, 0.18f); // dark seams
        else
        {
            float n = Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.06f;
            return new Color(0.72f + n, 0.58f + n * 0.8f, 0.37f + n * 0.5f); // nice warm plank color
        }
    }

    static Color Water(int x, int y)
    {
        // Dark blue base with lighter cyan ripples
        float n = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
        float ripple = Mathf.PerlinNoise(x * 0.8f + 5f, y * 0.8f + 10f);
        
        float r = 0.12f + n * 0.05f;
        float g = 0.42f + n * 0.08f + ripple * 0.05f;
        float b = 0.78f + n * 0.12f + ripple * 0.08f;
        
        return new Color(r, g, b);
    }

    static Color Sand(int x, int y)
    {
        // Sandy beige with slight noise for graininess
        float n = Mathf.PerlinNoise(x * 0.6f + 3f, y * 0.6f + 6f);
        float grain = Mathf.PerlinNoise(x * 1.5f + 12f, y * 1.5f + 8f);
        
        float r = 0.86f + n * 0.05f;
        float g = 0.78f + n * 0.04f;
        float b = 0.58f + n * 0.06f - grain * 0.03f;
        
        // ── Soft edge shading (simulates Ambient Occlusion organically) ──
        int distToXEdge = Mathf.Min(x, 15 - x);
        int distToYEdge = Mathf.Min(y, 15 - y);
        int distToEdge = Mathf.Min(distToXEdge, distToYEdge);

        float edgeShadow = 0f;
        if (distToEdge < 3)
        {
            float fade = (3f - distToEdge) / 3f;
            float edgeNoise = Mathf.PerlinNoise(x * 0.7f + 33f, y * 0.7f + 44f);
            edgeShadow = fade * (0.03f + edgeNoise * 0.05f); // very subtle shadow (3% to 8% max)
        }

        return new Color(
            Mathf.Clamp01(r - edgeShadow),
            Mathf.Clamp01(g - edgeShadow * 0.9f),
            Mathf.Clamp01(b - edgeShadow * 0.8f));
    }

    static Color Flower(int x, int y)
    {
        // SurvivalCraft-style: thin green stem, coloured cross petals, transparent background.
        // Background is pure magenta (1,0,1) → alpha=0 in RGBA conversion.
        Color key    = new Color(1f,    0f,    1f   );  // pure magenta → alpha=0 (transparent)
        Color stem   = new Color(0.20f, 0.55f, 0.10f);  // dark green stem
        Color leaf   = new Color(0.28f, 0.68f, 0.15f);  // lighter green leaf
        Color petal  = new Color(0.95f, 0.25f, 0.50f);  // rose-pink petal
        Color petal2 = new Color(1.00f, 0.50f, 0.10f);  // warm orange side petals
        Color centre = new Color(1.00f, 0.90f, 0.15f);  // bright yellow centre

        // ── Stem: 2 pixels wide, centre of tile, y=0..8 ──
        bool isStem = (x == 7 || x == 8) && y <= 8;
        if (isStem) return stem;

        // ── Leaf: small bump off the stem at y=4-5 ──
        if ((x == 9 || x == 10) && (y == 5 || y == 4)) return leaf;
        if ((x == 6 || x == 5) && (y == 3 || y == 2)) return leaf;

        // ── Petals: 5-pixel cross centred at (7,11) ──
        //  top arm:   (7,14)(8,14)
        //  bottom arm:(7,9) (8,9)
        //  left arm:  (5,11)(5,12)
        //  right arm: (10,11)(10,12)
        //  diagonals (orange accent):
        //    top-left (6,13), top-right (9,13), bot-left (6,10), bot-right (9,10)
        //  centre fill: x 6..9, y 10..13
        bool topArm    = (x == 7 || x == 8) && (y == 14 || y == 15);
        bool botArm    = (x == 7 || x == 8) && (y == 8  || y == 9 );
        bool leftArm   = (x == 4 || x == 5) && (y == 11 || y == 12);
        bool rightArm  = (x == 10|| x == 11) && (y == 11 || y == 12);
        bool centFill  = x >= 6 && x <= 9 && y >= 10 && y <= 13;
        bool diag      = ((x == 5 || x == 10) && (y == 13 || y == 10));
        bool isCentre  = (x == 7 || x == 8) && (y == 11 || y == 12);

        if (isCentre)  return centre;
        if (centFill)  return petal;
        if (topArm || botArm || leftArm || rightArm) return petal;
        if (diag)      return petal2;

        return key;
    }

    static Color Dandelion(int x, int y)
    {
        Color key    = new Color(1f,    0f,    1f   );  // pure magenta
        Color stem   = new Color(0.18f, 0.50f, 0.08f);  // slightly different dark green stem
        Color leaf   = new Color(0.25f, 0.62f, 0.12f);  // jagged leaves
        Color yellow = new Color(0.95f, 0.85f, 0.10f);  // bright yellow dandelion petals
        Color gold   = new Color(0.95f, 0.65f, 0.05f);  // golden/orange shading
        Color center = new Color(1.00f, 0.95f, 0.40f);  // bright center highlight

        // ── Stem: center of tile, y=0..9 ──
        bool isStem = (x == 7 || x == 8) && y <= 9;
        if (isStem) return stem;

        // ── Jagged Leaves (toothed, dandelion style) ──
        if (x == 9 && (y == 3 || y == 4)) return leaf;
        if (x == 10 && y == 4) return leaf;
        if (x == 6 && (y == 2 || y == 3)) return leaf;
        if (x == 5 && y == 3) return leaf;

        // ── Flower Head: circular puff at (7.5, 11.5) ──
        float dx = x - 7.5f;
        float dy = y - 11.5f;
        float distSq = dx * dx + dy * dy;

        if (distSq <= 2.5f) return center;      // tight center
        if (distSq <= 8.5f) return yellow;      // yellow puff body
        if (distSq <= 16.5f)                    // outer petals/fluff
        {
            // add some jaggedness
            bool isPetalPattern = (x + y) % 2 == 0;
            if (isPetalPattern) return gold;
        }

        return key;
    }

    static Color Iris(int x, int y)
    {
        Color key    = new Color(1f,    0f,    1f   );  // pure magenta
        Color stem   = new Color(0.22f, 0.52f, 0.15f);  // tall green stem
        Color leaf   = new Color(0.28f, 0.65f, 0.20f);  // sword-like leaves
        Color violet = new Color(0.40f, 0.20f, 0.90f);  // rich violet/purple
        Color blue   = new Color(0.20f, 0.40f, 0.95f);  // deep blue accent
        Color yellow = new Color(1.00f, 0.80f, 0.10f);  // yellow beard/center

        // ── Stem: center of tile, y=0..7 ──
        bool isStem = (x == 7 || x == 8) && y <= 7;
        if (isStem) return stem;

        // ── Sword Leaves: tall, growing upwards from bottom-sides ──
        if (x == 5 && y >= 2 && y <= 8) return leaf;
        if (x == 6 && y >= 4 && y <= 6) return leaf;
        if (x == 10 && y >= 1 && y <= 6) return leaf;
        if (x == 9 && y >= 3 && y <= 5) return leaf;

        // ── Iris Flower Head: at (7.5, 11) ──
        // Standards (upper petals, vertical/pointing up):
        bool upperPetals = (x >= 6 && x <= 9) && (y >= 12 && y <= 15);
        // Falls (lower petals, drooping down to the sides):
        bool lowerLeft   = (x >= 3 && x <= 5) && (y >= 8 && y <= 10);
        bool lowerRight  = (x >= 10 && x <= 12) && (y >= 8 && y <= 10);
        // Center/Beard:
        bool isCenter    = (x == 7 || x == 8) && (y == 10 || y == 11);
        bool isBeard     = (x == 6 || x == 9) && (y == 9 || y == 10);

        if (isCenter) return yellow;
        if (isBeard) return yellow;
        if (upperPetals)
        {
            // Shader effect: blue highlights on top edges
            if (y == 15 || x == 6 || x == 9) return blue;
            return violet;
        }
        if (lowerLeft || lowerRight)
        {
            // Shading on drooping petals
            if (y == 8) return blue;
            return violet;
        }

        return key;
    }

    static Color Leaves(int x, int y)
    {
        // Organic leaf texture: shades of green with small dark gaps
        float n = Mathf.PerlinNoise(x * 0.7f + 8.1f, y * 0.7f + 9.3f);
        float n2 = Mathf.PerlinNoise(x * 1.4f + 3.2f, y * 1.4f + 1.1f);
        
        bool gap = (n2 > 0.82f); // small transparent/dark gaps
        if (gap)
            return new Color(0.12f, 0.28f, 0.08f); // dark gap color
            
        float t = n * 0.15f + n2 * 0.05f;
        Color c = new Color(0.18f + t, 0.48f + t * 0.6f, 0.08f + t * 0.2f);
        
        // Soft edge shading to match the other blocks
        int distToXEdge = Mathf.Min(x, 15 - x);
        int distToYEdge = Mathf.Min(y, 15 - y);
        int distToEdge = Mathf.Min(distToXEdge, distToYEdge);
        if (distToEdge < 2)
        {
            c = new Color(c.r * 0.82f, c.g * 0.82f, c.b * 0.82f);
        }
        
        return c;
    }

    static Color ControlBlockSide(int x, int y)
    {
        Color baseYellow = new Color(0.95f, 0.82f, 0.10f);
        Color stripeDark = new Color(0.12f, 0.12f, 0.12f);
        Color borderGray = new Color(0.35f, 0.35f, 0.35f);

        if (x == 0 || x == 15 || y == 0 || y == 15)
            return borderGray;

        bool stripe = ((x + y) / 2) % 2 == 0;
        return stripe ? baseYellow : stripeDark;
    }

    static Color ControlBlockFront(int x, int y)
    {
        Color baseYellow = new Color(0.95f, 0.82f, 0.10f);
        Color stripeDark = new Color(0.12f, 0.12f, 0.12f);
        Color borderGray = new Color(0.35f, 0.35f, 0.35f);
        Color lightGray  = new Color(0.6f, 0.6f, 0.62f);
        Color screenBlue = new Color(0.1f, 0.6f, 0.95f);
        Color coreWhite  = new Color(0.9f, 0.95f, 1.0f);

        if (x == 0 || x == 15 || y == 0 || y == 15)
            return borderGray;

        // Screen area
        if (x >= 3 && x <= 12 && y >= 4 && y <= 11)
        {
            if (x == 3 || x == 12 || y == 4 || y == 11)
                return lightGray;
            
            bool isCenter = (x == 7 || x == 8) && (y == 7 || y == 8);
            return isCenter ? coreWhite : screenBlue;
        }

        bool stripe = ((x + y) / 2) % 2 == 0;
        return stripe ? baseYellow : stripeDark;
    }

    static Color TireTread(int x, int y)
    {
        Color darkTire  = new Color(0.15f, 0.15f, 0.15f);
        Color treadLine = new Color(0.08f, 0.08f, 0.08f);
        Color grayDust  = new Color(0.22f, 0.22f, 0.22f);

        bool isTread = (x % 4 == 0 && y >= 2 && y <= 13) || (y % 4 == 0 && x >= 2 && x <= 13);
        if (isTread) return treadLine;

        float n = Mathf.PerlinNoise(x * 0.9f, y * 0.9f);
        return Color.Lerp(darkTire, grayDust, n * 0.3f);
    }

    static Color WheelSide(int x, int y, bool isLarge)
    {
        Color darkTire   = new Color(0.15f, 0.15f, 0.15f);
        Color metallicRim= new Color(0.7f, 0.7f, 0.72f);
        Color darkRim    = new Color(0.4f, 0.4f, 0.42f);
        Color highlight  = new Color(0.9f, 0.9f, 0.95f);
        Color centerCap  = new Color(0.1f, 0.1f, 0.1f);

        float centerX = 7.5f;
        float centerY = 7.5f;
        float tireRadius = isLarge ? 7.2f : 6.0f;
        float rimRadius  = isLarge ? 4.5f : 3.5f;
        float hubRadius  = isLarge ? 1.8f : 1.2f;

        float dx = x - centerX;
        float dy = y - centerY;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);

        if (dist > tireRadius)
        {
            return new Color(0.1f, 0.1f, 0.1f);
        }

        if (dist > rimRadius)
        {
            float shade = Mathf.Clamp01((dx - dy) / 10f);
            return Color.Lerp(new Color(0.25f, 0.25f, 0.25f), darkTire, shade);
        }
        else if (dist > hubRadius)
        {
            float shade = Mathf.Clamp01((dx - dy) / 6f);
            Color baseRim = Color.Lerp(highlight, darkRim, shade);

            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            int spokes = isLarge ? 6 : 4;
            float angleStep = 360f / spokes;
            bool onSpoke = Mathf.Abs((angle + 180f) % angleStep) < (isLarge ? 12f : 16f);
            if (onSpoke && dist > hubRadius + 0.5f)
            {
                return baseRim;
            }
            return new Color(0.18f, 0.18f, 0.2f);
        }
        else
        {
            return centerCap;
        }
    }

    // ── UV helpers ────────────────────────────────────────────────────────────

    /// <summary>Returns the 4 atlas UVs for a given face and block type.</summary>
    /// <param name="face">0=back,1=front,2=top,3=bottom,4=left,5=right</param>
    /// <param name="blockType">1=Wood, 2=Plank, 3=Stone, 4=Grass, 5=Dirt, 6=Grass Slab, 7=Water, 8=Sand, 9=Flower</param>
    public static Vector2[] GetBlockUVs(int face, byte blockType)
    {
        int tile;
        if (blockType == 1)      // Wood: top/bottom uses WoodTop, sides use WoodSide
            tile = (face == 2 || face == 3) ? 4 : 5;
        else if (blockType == 2) // Plank: all faces planks
            tile = 6;
        else if (blockType == 3) // Stone: all faces stone
            tile = 3;
        else if (blockType == 5) // Dirt: all faces dirt
            tile = 2;
        else if (blockType == 7) // Water: all faces water
            tile = 7;
        else if (blockType == 8 || blockType == 34) // Sand: all faces sand
            tile = 8;
        else if (blockType == 9) // Flower: all quads use flower tile
            tile = 9;
        else if (blockType == 10) // Dandelion: all quads use dandelion tile
            tile = 10;
        else if (blockType == 11) // Iris: all quads use iris tile
            tile = 11;
        else if (blockType == 12) // Leaves: all faces leaves
            tile = 12;
        else if (blockType == 20) // Small Wheel: sides are wheel side, others are tire tread
            tile = (face == 4 || face == 5) ? 16 : 15;
        else if (blockType == 21 || blockType == 23) // Large Wheel & Helper: sides are wheel side, others are tire tread
            tile = (face == 4 || face == 5) ? 17 : 15;
        else if (blockType == 22) // Propeller Block: render as wood planks in the world
            tile = 6;
        else if (blockType == 50) // Control Block: front is screen, others are striped sides
            tile = (face == 1) ? 14 : 13;
        else if (blockType == 30) // Coal Ore
            tile = 18;
        else if (blockType == 31) // Iron Ore
            tile = 19;
        else if (blockType == 32) // Gold Block
            tile = 20;
        else if (blockType == 33) // Iron Block
            tile = 21;
        else if (blockType == 35) // Glass
            tile = 22;
        else                     // Grass (blockType == 4 or 6)
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

    // ── New Block Samplers ───────────────────────────────────────────────────

    static Color CoalOre(int x, int y)
    {
        Color baseCol = Stone(x, y);
        float n = Mathf.PerlinNoise(x * 1.8f + 50f, y * 1.8f + 60f);
        bool isCoal = n > 0.65f && (x + y) % 3 != 0;
        if (isCoal)
        {
            return new Color(0.12f, 0.12f, 0.12f); // charcoal black
        }
        return baseCol;
    }

    static Color IronOre(int x, int y)
    {
        Color baseCol = Stone(x, y);
        float n = Mathf.PerlinNoise(x * 1.6f + 80f, y * 1.6f + 90f);
        bool isIron = n > 0.68f;
        if (isIron)
        {
            return new Color(0.85f, 0.65f, 0.52f); // light peach/rusty iron color
        }
        return baseCol;
    }

    static Color GoldBlock(int x, int y)
    {
        float n = Mathf.PerlinNoise(x * 0.7f + 110f, y * 0.7f + 120f);
        float shiny = Mathf.PerlinNoise(x * 1.5f + 10f, y * 1.5f + 20f);
        Color baseGold = new Color(0.98f, 0.82f, 0.15f);
        Color shadowGold = new Color(0.80f, 0.62f, 0.05f);
        Color shine = new Color(1.00f, 0.95f, 0.60f);

        Color col = Color.Lerp(shadowGold, baseGold, n);
        if (shiny > 0.7f) col = Color.Lerp(col, shine, 0.6f);

        int distToXEdge = Mathf.Min(x, 15 - x);
        int distToYEdge = Mathf.Min(y, 15 - y);
        int distToEdge = Mathf.Min(distToXEdge, distToYEdge);
        if (distToEdge == 0)
        {
            col = shadowGold * 0.7f;
        }
        else if (distToEdge == 1)
        {
            col = shine;
        }
        return col;
    }

    /// <summary>Renamed from Glass — the frosted texture resembles iron. Used for Iron Block (ID 33).</summary>
    static Color IronBlock(int x, int y)
    {
        int distToXEdge = Mathf.Min(x, 15 - x);
        int distToYEdge = Mathf.Min(y, 15 - y);
        int distToEdge  = Mathf.Min(distToXEdge, distToYEdge);

        // Outermost pixel: thin light gray border
        if (distToEdge == 0)
            return new Color(0.65f, 0.70f, 0.75f);

        // Inner border: slightly lighter
        if (distToEdge == 1)
            return new Color(0.80f, 0.85f, 0.90f);

        // Interior: soft icy blue-white base with subtle noise
        float n = Mathf.PerlinNoise(x * 1.2f + 7f, y * 1.2f + 13f);
        Color interior = Color.Lerp(new Color(0.88f, 0.93f, 0.98f), new Color(0.94f, 0.97f, 1.00f), n);

        // Bright diagonal glint lines
        if (x + y == 8 || x + y == 9)
            return new Color(0.98f, 0.99f, 1.00f);
        if (x + y == 20 || x + y == 21)
            return new Color(0.96f, 0.98f, 1.00f);

        return interior;
    }

    /// <summary>Minecraft/Survivalcraft-style Glass (ID 35): thin dark border, white interior with subtle tint.</summary>
    static Color MinecraftGlass(int x, int y)
    {
        int distToXEdge = Mathf.Min(x, 15 - x);
        int distToYEdge = Mathf.Min(y, 15 - y);
        int distToEdge  = Mathf.Min(distToXEdge, distToYEdge);

        // Outermost border: black, as requested by the user
        if (distToEdge == 0)
            return new Color(0.0f, 0.0f, 0.0f, 1.0f);

        // 1-pixel inner border highlight
        if (distToEdge == 1)
            return new Color(0.55f, 0.68f, 0.75f, 1.0f);

        // Bright white diagonal glint lines
        if (x + y == 8 || x + y == 9 || x + y == 20 || x + y == 21)
            return new Color(1.0f, 1.0f, 1.0f, 0.9f);

        // Interior: soft semi-transparent blue-white fill with alpha=0.15
        return new Color(0.80f, 0.90f, 0.95f, 0.15f);
    }

    // Keep old name as alias so existing callers don't break
    public static Vector2[] GetGrassUVs(int face) => GetBlockUVs(face, 1);

    public static Color GetPixel(int tile, int lx, int ly)
    {
        lx = Mathf.Clamp(lx, 0, 15);
        ly = Mathf.Clamp(ly, 0, 15);
        
        switch (tile)
        {
            case 0: return GrassTop(lx, ly);
            case 1: return GrassSide(lx, ly);
            case 2: return Dirt(lx, ly);
            case 3: return Stone(lx, ly);
            case 4: return WoodTop(lx, ly);
            case 5: return WoodSide(lx, ly);
            case 6: return Plank(lx, ly);
            case 7: return Water(lx, ly);
            case 8: return Sand(lx, ly);
            case 9: return Flower(lx, ly);
            case 10: return Dandelion(lx, ly);
            case 11: return Iris(lx, ly);
            case 12: return Leaves(lx, ly);
            case 13: return ControlBlockSide(lx, ly);
            case 14: return ControlBlockFront(lx, ly);
            case 15: return TireTread(lx, ly);
            case 16: return WheelSide(lx, ly, false);
            case 17: return WheelSide(lx, ly, true);
            case 18: return CoalOre(lx, ly);
            case 19: return IronOre(lx, ly);
            case 20: return GoldBlock(lx, ly);
            case 21: return IronBlock(lx, ly);
            case 22: return MinecraftGlass(lx, ly);
            default: return Color.clear;
        }
    }
}
