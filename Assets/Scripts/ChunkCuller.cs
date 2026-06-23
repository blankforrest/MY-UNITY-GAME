using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

/// <summary>
/// Disables MeshRenderer on chunks and their children (Water, Foliage, Glass) 
/// when they are fully outside the camera's view frustum.
/// </summary>
public class ChunkCuller : MonoBehaviour
{
    public static ChunkCuller Instance { get; private set; }

    [Tooltip("Culling checks per second (lower = cheaper).")]
    public float updatesPerSecond = 20f;

    private Camera mainCam;
    private float timer;
    private readonly List<Chunk> chunks = new List<Chunk>();
    private readonly List<float3> chunkCenters = new List<float3>();
    private readonly List<float3> chunkExtents = new List<float3>();
    private readonly Plane[] planes = new Plane[6]; // Pre-allocated to prevent GC allocation spikes

    // Persistent NativeArrays to completely avoid allocations in the Update loop
    private NativeArray<float4> nativePlanes;
    private NativeArray<float3> nativeCenters;
    private NativeArray<float3> nativeExtents;
    private NativeArray<bool> nativeResults;
    private int allocatedCapacity = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            nativePlanes = new NativeArray<float4>(6, Allocator.Persistent);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (mainCam == null) return;

        int count = chunks.Count;
        if (count == 0) return;

        // Throttle culling checks to save CPU cycles
        timer += Time.deltaTime;
        if (timer < 1f / updatesPerSecond) return;
        timer = 0f;

        CullChunks();
    }

    private void OnDestroy()
    {
        if (nativePlanes.IsCreated) nativePlanes.Dispose();
        if (nativeCenters.IsCreated) nativeCenters.Dispose();
        if (nativeExtents.IsCreated) nativeExtents.Dispose();
        if (nativeResults.IsCreated) nativeResults.Dispose();
    }

    public void RegisterChunk(Chunk chunk)
    {
        if (chunk != null && !chunks.Contains(chunk))
        {
            chunks.Add(chunk);

            // Pre-calculate and cache chunk bounds coordinates once
            Vector2 chunkPos = chunk.chunkPos;
            float3 center = new float3(
                chunkPos.x * VoxelData.ChunkWidth + VoxelData.ChunkWidth * 0.5f,
                VoxelData.ChunkHeight * 0.5f,
                chunkPos.y * VoxelData.ChunkWidth + VoxelData.ChunkWidth * 0.5f
            );
            float3 extents = new float3(
                VoxelData.ChunkWidth * 0.5f,
                VoxelData.ChunkHeight * 0.5f,
                VoxelData.ChunkWidth * 0.5f
            );

            chunkCenters.Add(center);
            chunkExtents.Add(extents);
        }
    }

    public void UnregisterChunk(Chunk chunk)
    {
        if (chunk != null)
        {
            int idx = chunks.IndexOf(chunk);
            if (idx >= 0)
            {
                chunks.RemoveAt(idx);
                chunkCenters.RemoveAt(idx);
                chunkExtents.RemoveAt(idx);
            }
        }
    }

    public void Refresh()
    {
        // No-op compatibility
    }

    private void CullChunks()
    {
        // Remove any null references from the list (defensive check)
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            if (chunks[i] == null)
            {
                chunks.RemoveAt(i);
                chunkCenters.RemoveAt(i);
                chunkExtents.RemoveAt(i);
            }
        }

        int count = chunks.Count;
        if (count == 0) return;

        // Non-allocating frustum planes check
        GeometryUtility.CalculateFrustumPlanes(mainCam, planes);

        // Ensure persistent arrays are large enough
        if (!nativeCenters.IsCreated || count > allocatedCapacity)
        {
            if (nativeCenters.IsCreated) nativeCenters.Dispose();
            if (nativeExtents.IsCreated) nativeExtents.Dispose();
            if (nativeResults.IsCreated) nativeResults.Dispose();

            allocatedCapacity = Mathf.Max(128, count * 2);
            nativeCenters = new NativeArray<float3>(allocatedCapacity, Allocator.Persistent);
            nativeExtents = new NativeArray<float3>(allocatedCapacity, Allocator.Persistent);
            nativeResults = new NativeArray<bool>(allocatedCapacity, Allocator.Persistent);
        }

        for (int i = 0; i < 6; i++)
        {
            nativePlanes[i] = new float4(planes[i].normal, planes[i].distance);
        }

        for (int i = 0; i < count; i++)
        {
            nativeCenters[i] = chunkCenters[i];
            nativeExtents[i] = chunkExtents[i];
        }

        CullChunksJob job = new CullChunksJob
        {
            Planes = nativePlanes,
            Centers = nativeCenters,
            Extents = nativeExtents,
            Results = nativeResults
        };

        JobHandle handle = job.Schedule(count, 64);
        handle.Complete();

        for (int i = 0; i < count; i++)
        {
            if (chunks[i] != null)
            {
                chunks[i].SetRenderersEnabled(nativeResults[i]);
            }
        }
    }

    [BurstCompile]
    private struct CullChunksJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> Planes;
        [ReadOnly] public NativeArray<float3> Centers;
        [ReadOnly] public NativeArray<float3> Extents;
        public NativeArray<bool> Results;

        public void Execute(int index)
        {
            float3 center = Centers[index];
            float3 extents = Extents[index];

            bool inside = true;
            for (int i = 0; i < 6; i++)
            {
                float4 plane = Planes[i];
                float3 normal = plane.xyz;
                float distance = plane.w;

                float r = math.dot(extents, math.abs(normal));
                float s = math.dot(normal, center) + distance;

                if (s < -r)
                {
                    inside = false;
                    break;
                }
            }
            Results[index] = inside;
        }
    }
}
