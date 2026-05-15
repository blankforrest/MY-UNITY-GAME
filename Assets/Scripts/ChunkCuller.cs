using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Disables MeshRenderer on chunks fully outside the camera's view frustum.
/// Attach to the Player GameObject.
/// </summary>
public class ChunkCuller : MonoBehaviour
{
    [Tooltip("Culling checks per second (lower = cheaper).")]
    public float updatesPerSecond = 20f;

    private Camera            mainCam;
    private float             timer;
    private List<MeshRenderer> renderers = new List<MeshRenderer>();

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (mainCam == null) return;

        // Auto-populate: keep trying until we find chunks
        if (renderers.Count == 0)
        {
            CollectRenderers();
            if (renderers.Count == 0) return;
        }

        // Throttle culling checks
        timer += Time.deltaTime;
        if (timer < 1f / updatesPerSecond) return;
        timer = 0f;

        CullChunks();
    }

    void CollectRenderers()
    {
        renderers.Clear();
        Chunk[] chunks = FindObjectsByType<Chunk>(FindObjectsSortMode.None);
        foreach (var c in chunks)
        {
            MeshRenderer mr = c.GetComponent<MeshRenderer>();
            if (mr != null) renderers.Add(mr);
        }
    }

    void CullChunks()
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCam);

        for (int i = renderers.Count - 1; i >= 0; i--)
        {
            if (renderers[i] == null) { renderers.RemoveAt(i); continue; }

            // Use the renderer's own up-to-date bounds
            bool visible = GeometryUtility.TestPlanesAABB(planes, renderers[i].bounds);
            renderers[i].enabled = visible;
        }
    }

    /// <summary>Call when new chunks are spawned at runtime.</summary>
    public void Refresh() => renderers.Clear();
}
