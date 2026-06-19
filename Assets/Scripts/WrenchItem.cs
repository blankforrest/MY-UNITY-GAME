using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// currentBlueprint is stored here after every successful wrench scan.

public class WrenchItem : MonoBehaviour
{
    public static WrenchItem Instance { get; private set; }

    [Header("Wrench Settings")]
    public int wrenchItemID = 99;
    public float reach = 5f;

    public static bool IsHoldingWrench { get; private set; } = false;

    private Camera _playerCam;
    private Item   _wrenchItem;
    public List<Vector3Int>    currentStructureBlocks { get; private set; } = new List<Vector3Int>();
    public StructureBlueprint currentBlueprint       { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        Instance   = this;
        _playerCam = GetComponentInChildren<Camera>();
        if (_playerCam == null)
            Debug.LogError("[WrenchItem] No Camera found as child of Player.");

        // Wait one frame so Hotbar.Start() has already run and built its slot UI
        yield return null;
        SetupWrenchItem();
    }

    private void Update()
    {
        IsHoldingWrench = IsWrenchSelected();
        if (!IsHoldingWrench) return;
        if (InventoryUI.IsInventoryOpen) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryScanStructure();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void SetupWrenchItem()
    {
        _wrenchItem          = ScriptableObject.CreateInstance<Item>();
        _wrenchItem.itemName = "Wrench";
        _wrenchItem.itemID   = wrenchItemID;

        Sprite loaded = Resources.Load<Sprite>("WrenchIcon");
        _wrenchItem.icon     = (loaded != null) ? loaded : DrawWrenchIcon();

        if (Hotbar.Instance != null)
        {
            bool added = Hotbar.Instance.TryAddItem(_wrenchItem, 1);
            Debug.Log(added ? "[WrenchItem] Wrench added to hotbar." : "[WrenchItem] Hotbar full.");
        }
        else
        {
            Debug.LogError("[WrenchItem] Hotbar.Instance is null.");
        }
    }

    // ── Procedural 64x64 pixel-art wrench icon ────────────────────────────────

    private Sprite DrawWrenchIcon()
    {
        const int SZ = 64;
        Color gold  = new Color(1f, 0.78f, 0.12f, 1f);
        Color shine = new Color(1f, 0.95f, 0.55f, 1f);
        Color[] px  = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Fill(int x, int y, int w, int h, Color c)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    int gx = x + dx, gy = y + dy;
                    if (gx >= 0 && gx < SZ && gy >= 0 && gy < SZ)
                        px[gy * SZ + gx] = c;
                }
        }

        // Handle — vertical bar
        Fill(28, 4, 8, 38, gold);
        Fill(30, 4, 3, 38, shine); // highlight

        // Head — top horizontal bar
        Fill(16, 44, 32, 10, gold);
        Fill(16, 46, 32, 3,  shine);

        // Left jaw
        Fill(16, 36, 12, 14, gold);
        Fill(18, 38,  3,  8, shine);

        // Right jaw
        Fill(36, 36, 12, 14, gold);

        // Clear mouth opening between jaws
        Fill(28, 36, 8, 8, Color.clear);

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    // ── Wrench logic ──────────────────────────────────────────────────────────

    private bool IsWrenchSelected()
    {
        if (Hotbar.Instance == null) return false;
        InventorySlot slot = Hotbar.Instance.GetSelectedSlot();
        return slot != null && slot.item != null && slot.item.itemID == wrenchItemID;
    }

    private void TryScanStructure()
    {
        if (_playerCam == null) return;
        Ray ray = new Ray(_playerCam.transform.position, _playerCam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            return;

        // Check if we hit an active vehicle
        VehicleController hitVehicle = hit.collider.GetComponentInParent<VehicleController>();
        if (hitVehicle != null)
        {
            DeconvertVehicleToStructure(hitVehicle);
            return;
        }

        Vector3 p = hit.point - hit.normal * 0.001f;
        Vector3Int gridPos = new Vector3Int(
            Mathf.FloorToInt(p.x),
            Mathf.FloorToInt(p.y),
            Mathf.FloorToInt(p.z));

        if (PlacedBlockRegistry.Instance == null || !PlacedBlockRegistry.Instance.IsPlayerPlaced(gridPos))
            return;

        currentStructureBlocks = StructureScanner.FloodFillStructure(gridPos);
        Debug.Log($"[WrenchItem] Structure found with {currentStructureBlocks.Count} blocks.");

        // Generate blueprint (computes mass, durability, dimensions) and log summary.
        currentBlueprint = BlueprintGenerator.GenerateBlueprint(currentStructureBlocks);

        TriggerConfirmationWindow(currentBlueprint);
    }

    private void DeconvertVehicleToStructure(VehicleController vehicle)
    {
        if (VoxelWorld.Instance == null) return;

        Debug.Log($"[WrenchItem] De-converting vehicle '{vehicle.gameObject.name}' back to structure.");

        // If the player is currently riding this vehicle, exit control
        if (vehicle.isBeingControlled)
        {
            vehicle.isBeingControlled = false;
        }

        // Align the vehicle to the world voxel grid to prevent block distortion.
        // 1. Snap rotation to the nearest 90 degrees around the Y axis, clearing pitch and roll.
        float snapY = Mathf.Round(vehicle.transform.eulerAngles.y / 90f) * 90f;
        vehicle.transform.rotation = Quaternion.Euler(0f, snapY, 0f);

        // 2. Snap position to the nearest half-grid coordinates (matching the 0.5f voxel-center offset).
        vehicle.transform.position = new Vector3(
            Mathf.Round(vehicle.transform.position.x - 0.5f) + 0.5f,
            Mathf.Round(vehicle.transform.position.y - 0.5f) + 0.5f,
            Mathf.Round(vehicle.transform.position.z - 0.5f) + 0.5f
        );

        // Collect all child blocks
        List<Transform> childrenToProcess = new List<Transform>();
        foreach (Transform child in vehicle.transform)
        {
            if (child.name.StartsWith("Block_") || child.name.StartsWith("SpecialBlock_"))
            {
                childrenToProcess.Add(child);
            }
        }

        foreach (Transform child in childrenToProcess)
        {
            string[] parts = child.name.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int typeID))
            {
                if (typeID == 21) // Large Wheel (anchor)
                {
                    Vector3 localPos = child.localPosition;
                    // Determine if side offset is X or Z
                    // fractional part of localPos.x is 0.5f if sideOffset is X
                    float fracX = Mathf.Abs(localPos.x - Mathf.Floor(localPos.x));
                    Vector3Int localSide;
                    Vector3Int anchorLocal;
                    
                    if (Mathf.Abs(fracX - 0.5f) < 0.1f)
                    {
                        localSide = Vector3Int.right;
                        anchorLocal = new Vector3Int(
                            Mathf.RoundToInt(localPos.x - 0.5f),
                            Mathf.RoundToInt(localPos.y - 0.5f),
                            Mathf.RoundToInt(localPos.z)
                        );
                    }
                    else
                    {
                        localSide = Vector3Int.forward;
                        anchorLocal = new Vector3Int(
                            Mathf.RoundToInt(localPos.x),
                            Mathf.RoundToInt(localPos.y - 0.5f),
                            Mathf.RoundToInt(localPos.z - 0.5f)
                        );
                    }

                    // The 4 local positions for the 2x2 wheel footprint
                    Vector3Int[] localPositions = new Vector3Int[]
                    {
                        anchorLocal,
                        anchorLocal + Vector3Int.up,
                        anchorLocal + localSide,
                        anchorLocal + localSide + Vector3Int.up
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 worldPos = vehicle.transform.TransformPoint((Vector3)localPositions[i]);
                        Vector3Int worldGrid = new Vector3Int(
                            Mathf.RoundToInt(worldPos.x - 0.5f),
                            Mathf.RoundToInt(worldPos.y - 0.5f),
                            Mathf.RoundToInt(worldPos.z - 0.5f)
                        );

                        Vector3 voxelCentre = new Vector3(
                            worldGrid.x + 0.5f,
                            worldGrid.y + 0.5f,
                            worldGrid.z + 0.5f
                        );

                        byte blockToPlace = (i == 0) ? (byte)21 : (byte)23;
                        VoxelWorld.Instance.ModifyBlock(voxelCentre, blockToPlace, suppressDrop: true);
                        PlacedBlockRegistry.Instance?.Register(worldGrid);
                    }
                }
                else
                {
                    Vector3 childPos = child.position;
                    Vector3Int worldGrid = new Vector3Int(
                        Mathf.RoundToInt(childPos.x - 0.5f),
                        Mathf.RoundToInt(childPos.y - 0.5f),
                        Mathf.RoundToInt(childPos.z - 0.5f)
                    );

                    Vector3 voxelCentre = new Vector3(
                        worldGrid.x + 0.5f,
                        worldGrid.y + 0.5f,
                        worldGrid.z + 0.5f
                    );

                    // Re-place the voxel block
                    VoxelWorld.Instance.ModifyBlock(voxelCentre, (byte)typeID, suppressDrop: true);

                    // Register as player placed
                    PlacedBlockRegistry.Instance?.Register(worldGrid);
                }
            }
        }

        // Destroy the vehicle GameObject
        Destroy(vehicle.gameObject);
    }


    private void TriggerConfirmationWindow(StructureBlueprint blueprint)
    {
        if (blueprint == null) return;

        if (ConfirmationWindow.Instance != null)
            ConfirmationWindow.Instance.ShowWindow(blueprint);
        else
            Debug.LogWarning("[WrenchItem] ConfirmationWindow.Instance is null — " +
                             "add ConfirmationWindow component to a GameObject in the scene.");
    }

    // ── Procedural wrench 3D mesh (for dropped item) ──────────────────────────

    /// <summary>
    /// Returns a procedural Mesh shaped like a wrench.
    /// Composed of box primitives: handle + two jaws + top bar.
    /// </summary>
    public static Mesh BuildWrenchMesh()
    {
        var verts = new List<Vector3>();
        var tris  = new List<int>();
        var norms = new List<Vector3>();

        AddBox(verts, tris, norms, new Vector3(0f, -0.08f, 0f),  new Vector3(0.035f, 0.16f, 0.035f)); // handle
        AddBox(verts, tris, norms, new Vector3(0f,  0.10f, 0f),  new Vector3(0.13f,  0.03f, 0.035f)); // top bar
        AddBox(verts, tris, norms, new Vector3(-0.05f, 0.07f, 0f), new Vector3(0.03f, 0.06f, 0.035f)); // left jaw
        AddBox(verts, tris, norms, new Vector3( 0.05f, 0.07f, 0f), new Vector3(0.03f, 0.06f, 0.035f)); // right jaw

        Mesh mesh = new Mesh();
        mesh.name = "WrenchMesh";
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(norms);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddBox(List<Vector3> verts, List<int> tris, List<Vector3> norms,
                                Vector3 center, Vector3 size)
    {
        float hx = size.x / 2f, hy = size.y / 2f, hz = size.z / 2f;

        // 6 faces: front, back, left, right, top, bottom
        (Vector3[] quad, Vector3 normal)[] faces =
        {
            (new[]{center+new Vector3(-hx,-hy, hz),center+new Vector3( hx,-hy, hz),center+new Vector3( hx, hy, hz),center+new Vector3(-hx, hy, hz)}, Vector3.forward),
            (new[]{center+new Vector3( hx,-hy,-hz),center+new Vector3(-hx,-hy,-hz),center+new Vector3(-hx, hy,-hz),center+new Vector3( hx, hy,-hz)}, Vector3.back),
            (new[]{center+new Vector3(-hx,-hy,-hz),center+new Vector3(-hx,-hy, hz),center+new Vector3(-hx, hy, hz),center+new Vector3(-hx, hy,-hz)}, Vector3.left),
            (new[]{center+new Vector3( hx,-hy, hz),center+new Vector3( hx,-hy,-hz),center+new Vector3( hx, hy,-hz),center+new Vector3( hx, hy, hz)}, Vector3.right),
            (new[]{center+new Vector3(-hx, hy, hz),center+new Vector3( hx, hy, hz),center+new Vector3( hx, hy,-hz),center+new Vector3(-hx, hy,-hz)}, Vector3.up),
            (new[]{center+new Vector3(-hx,-hy,-hz),center+new Vector3( hx,-hy,-hz),center+new Vector3( hx,-hy, hz),center+new Vector3(-hx,-hy, hz)}, Vector3.down),
        };

        foreach (var (quad, normal) in faces)
        {
            int b = verts.Count;
            verts.AddRange(quad);
            norms.Add(normal); norms.Add(normal); norms.Add(normal); norms.Add(normal);
            tris.Add(b); tris.Add(b+1); tris.Add(b+2);
            tris.Add(b); tris.Add(b+2); tris.Add(b+3);
        }
    }
}
