#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility that generates 5 block prefabs in Assets/Prefab/.
/// Run it once from the Unity menu: VoxelGame → Create Block Prefabs
///
/// Prefabs created (index = blockTypeID used in VehicleSpawner):
///   0 = Air      (empty GameObject, placeholder — not actually placed)
///   1 = Wood     (brown cube)
///   2 = Plank    (light wood cube)
///   3 = Stone    (grey cube)
///   4 = Dirt     (dark brown cube)
///
/// To add Iron (type 5): increase array size in VehicleSpawner Inspector to 6,
/// then add a 6th entry here and re-run.
/// </summary>
public static class BlockPrefabCreator
{
    private const string PrefabFolder = "Assets/Prefab";

    // Block definitions: (display name, URP/Standard material color)
    private static readonly (string name, Color color, bool isEmpty)[] BlockDefs =
    {
        ("BlockPrefab_0_Air",   Color.clear,                      true  ), // index 0 — unused
        ("BlockPrefab_1_Wood",  new Color(0.55f, 0.33f, 0.12f),  false ), // index 1 — wood
        ("BlockPrefab_2_Plank", new Color(0.78f, 0.58f, 0.28f),  false ), // index 2 — plank
        ("BlockPrefab_3_Stone", new Color(0.52f, 0.52f, 0.52f),  false ), // index 3 — stone
        ("BlockPrefab_4_Dirt",  new Color(0.42f, 0.27f, 0.10f),  false ), // index 4 — dirt
    };

    [MenuItem("VoxelGame/Create Block Prefabs")]
    public static void CreateBlockPrefabs()
    {
        // Ensure the Prefab folder exists
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefab");

        for (int i = 0; i < BlockDefs.Length; i++)
        {
            var (blockName, color, isEmpty) = BlockDefs[i];
            string prefabPath = $"{PrefabFolder}/{blockName}.prefab";

            // Skip if prefab already exists (don't overwrite)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log($"[BlockPrefabCreator] Skipping '{blockName}' — already exists.");
                continue;
            }

            GameObject go;

            if (isEmpty)
            {
                // Air block — just an empty GO with no mesh or collider
                go      = new GameObject(blockName);
            }
            else
            {
                // Solid block — 1×1×1 cube with colored material
                go      = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = blockName;

                // Make sure the BoxCollider is already on the prefab
                // (CreatePrimitive adds one automatically)

                // Create and save a named material for this block type
                string matPath = $"{PrefabFolder}/{blockName}_Mat.mat";
                Material mat;

                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                {
                    mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                }
                else
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                 ?? Shader.Find("Standard");
                    mat       = new Material(shader);
                    mat.color = color;
                    mat.name  = blockName + "_Mat";
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            // Save as prefab asset
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            if (saved != null)
                Debug.Log($"[BlockPrefabCreator] Created: {prefabPath}");
            else
                Debug.LogError($"[BlockPrefabCreator] Failed to create: {prefabPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BlockPrefabCreator] Done! Go to Assets/Prefab/ and assign them to " +
                  "VehicleSpawner → Block Prefabs in the Inspector.");
    }
}
#endif
