using UnityEngine;
using UnityEditor;

/// <summary>
/// Automatically run on model import — enables Read/Write for any 3D mesh model in Assets/Blocks/,
/// so the voxel engine can access their vertex and triangle data at runtime for rendering and physics.
/// </summary>
public class MeshImportFixer : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Blocks/")) return;

        ModelImporter modelImporter = (ModelImporter)assetImporter;
        if (modelImporter != null && !modelImporter.isReadable)
        {
            modelImporter.isReadable = true;
            Debug.Log($"[MeshImportFixer] Automatically enabled Read/Write on model: {assetPath}");
        }
    }
}
