using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Run via  Tools → Assign Item Sprites
/// 1. Force-reimports all PNGs in Assets/Sprites/ as Sprite type.
/// 2. Assigns the correct sprite to each Item asset.
/// </summary>
public static class SpriteAssigner
{
    [MenuItem("Tools/Assign Item Sprites")]
    public static void AssignSprites()
    {
        // ── Step 1: Force-reimport every PNG in Assets/Sprites/ AND Resources/ as Sprite ────
        var foldersToFix = new[] { "Assets/Sprites", "Assets/Resources" };
        foreach (string folder in foldersToFix)
        {
            string absFolder = Path.Combine(Application.dataPath,
                                            folder.Replace("Assets/", ""));
            if (!Directory.Exists(absFolder)) continue;

            foreach (string abs in Directory.GetFiles(absFolder, "*.png"))
            {
                string rel = folder + "/" + Path.GetFileName(abs);
                TextureImporter ti = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType         = TextureImporterType.Sprite;
                    ti.spriteImportMode    = SpriteImportMode.Single;
                    ti.alphaIsTransparency = true;
                    ti.filterMode          = FilterMode.Point;
                    ti.maxTextureSize      = 128;
                    ti.SaveAndReimport();
                    Debug.Log($"[SpriteAssigner] Reimported as Sprite: {rel}");
                }
            }
        }

        AssetDatabase.Refresh();

        // ── Step 2: Map blockTypeID / name → sprite path ────────────────────
        var spriteMap = new Dictionary<int, string>
        {
            { 1, "Assets/Sprites/wood_block.png"  },
            { 2, "Assets/Sprites/plank_block.png" },
            { 3, "Assets/Sprites/stone_block.png" },
            { 4, "Assets/Sprites/dirt_block.png"  },
        };

        var nameMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "grass",   "Assets/Sprites/grass_block.png"  },
            { "wood",    "Assets/Sprites/wood_block.png"   },
            { "plank",   "Assets/Sprites/plank_block.png"  },
            { "stone",   "Assets/Sprites/stone_block.png"  },
            { "dirt",    "Assets/Sprites/dirt_block.png"   },
            { "iron",    "Assets/Sprites/iron_bar.png"     },
            { "stick",   "Assets/Sprites/stick.png"        },
            { "string",  "Assets/Sprites/string.png"       },
            { "wrench",  "Assets/Resources/WrenchIcon.png" },
        };

        // ── Step 3: Find all Item assets ─────────────────────────────────────
        string[] guids = AssetDatabase.FindAssets("t:Item");
        Debug.Log($"[SpriteAssigner] Found {guids.Length} Item asset(s).");

        int changed = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Item item   = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item == null) { Debug.LogWarning($"Could not load Item at {path}"); continue; }

            string spritePath = null;

            if (item.blockTypeID > 0 && spriteMap.TryGetValue(item.blockTypeID, out string bp))
                spritePath = bp;

            if (spritePath == null)
            {
                string combined = (item.itemName + " " + item.name).ToLower();
                foreach (var kvp in nameMap)
                {
                    if (combined.Contains(kvp.Key.ToLower()))
                    { spritePath = kvp.Value; break; }
                }
            }

            if (spritePath == null)
            {
                Debug.LogWarning($"[SpriteAssigner] No sprite for: '{item.name}' " +
                                 $"itemName='{item.itemName}' blockTypeID={item.blockTypeID}");
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[SpriteAssigner] Sprite not found: {spritePath}");
                continue;
            }

            item.icon = sprite;
            EditorUtility.SetDirty(item);
            changed++;
            Debug.Log($"[SpriteAssigner] ✓  {item.name}  →  {spritePath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"Done! Updated {changed} of {guids.Length} item(s).\n" +
                     "Check Console for details on any skipped items.";
        Debug.Log($"[SpriteAssigner] {msg}");
        EditorUtility.DisplayDialog("Sprite Assigner", msg, "OK");
    }
}
