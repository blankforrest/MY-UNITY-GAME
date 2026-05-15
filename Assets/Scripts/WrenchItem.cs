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
        _wrenchItem.icon     = DrawWrenchIcon();

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
