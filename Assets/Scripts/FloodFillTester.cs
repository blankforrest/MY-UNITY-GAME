using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TEMPORARY DEBUG SCRIPT — delete when flood fill is verified working.
/// Attach to any empty GameObject in the scene.
/// Press F while looking at a player-placed block to trigger a flood fill.
/// Each block in the connected structure is highlighted with a temporary red overlay cube.
/// </summary>
public class FloodFillTester : MonoBehaviour
{
    [Header("Test Instructions")]
    [Tooltip("Place some touching blocks in the scene, press F while looking at one to test flood fill")]
    public float raycastDistance = 10f;

    [Header("Highlight Settings")]
    [Tooltip("Color of the overlay cubes shown on detected blocks.")]
    public Color highlightColor = new Color(1f, 0f, 0f, 0.5f);

    [Tooltip("How long (seconds) the highlight overlay stays before disappearing.")]
    public float highlightDuration = 2f;

    private Camera _mainCamera;
    private Material _overlayMat;

    // -----------------------------------------------------------------------

    private void Start()
    {
        _mainCamera = Camera.main;

        if (_mainCamera == null)
            Debug.LogError("[FloodFillTester] No Main Camera found. Tag your camera as 'MainCamera'.");

        if (PlacedBlockRegistry.Instance == null)
            Debug.LogError("[FloodFillTester] PlacedBlockRegistry not found in scene. " +
                           "Add PlacedBlockRegistry component to a GameObject.");

        // Build a shared semi-transparent material for overlay cubes
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _overlayMat = new Material(shader);
        _overlayMat.color = highlightColor;

        // Enable transparency
        _overlayMat.SetFloat("_Surface", 1);          // URP: Transparent surface
        _overlayMat.SetFloat("_Blend",   0);           // URP: Alpha blend
        _overlayMat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _overlayMat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _overlayMat.SetInt("_ZWrite",    0);
        _overlayMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _overlayMat.renderQueue = 3000;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            RunFloodFillTest();
    }

    // -----------------------------------------------------------------------

    private void RunFloodFillTest()
    {
        if (_mainCamera == null) return;

        Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
        {
            Debug.Log("[FloodFillTester] Ray hit nothing.");
            return;
        }

        // Convert the hit point (nudged inward) to an integer voxel grid position
        Vector3 p        = hit.point - hit.normal * 0.001f;
        Vector3Int gridPos = new Vector3Int(
            Mathf.FloorToInt(p.x),
            Mathf.FloorToInt(p.y),
            Mathf.FloorToInt(p.z));

        // Check registry — is this a player-placed block?
        if (PlacedBlockRegistry.Instance == null ||
            !PlacedBlockRegistry.Instance.IsPlayerPlaced(gridPos))
        {
            Debug.Log($"[FloodFillTester] No player-placed block at grid {gridPos}. " +
                      $"(Hit: '{hit.collider.gameObject.name}')");
            return;
        }

        // Run flood fill
        List<Vector3Int> structure = StructureScanner.FloodFillStructure(gridPos);
        Debug.Log($"[FloodFillTester] Flood fill found {structure.Count} block(s).");

        // Highlight each position with a temporary overlay cube
        foreach (Vector3Int pos in structure)
            StartCoroutine(ShowOverlay(pos, highlightDuration));
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawns a semi-transparent 1x1x1 cube at <paramref name="gridPos"/> for 
    /// <paramref name="duration"/> seconds, then destroys it.
    /// </summary>
    private IEnumerator ShowOverlay(Vector3Int gridPos, float duration)
    {
        // Place cube slightly inset (0.9x scale) so edges don't z-fight with the voxel face
        GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
        overlay.name = "FloodFillOverlay";
        overlay.transform.position = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, gridPos.z + 0.5f);
        overlay.transform.localScale = Vector3.one * 0.98f;

        // Remove collider so it doesn't interfere with raycasts or physics
        Destroy(overlay.GetComponent<Collider>());

        // Apply the shared semi-transparent overlay material
        overlay.GetComponent<MeshRenderer>().material = _overlayMat;

        yield return new WaitForSeconds(duration);

        Destroy(overlay);
    }
}
