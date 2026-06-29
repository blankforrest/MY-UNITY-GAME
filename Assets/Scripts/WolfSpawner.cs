using UnityEngine;

public class WolfSpawner : MonoBehaviour
{
    public float spawnInterval = 10f;
    public int maxWolves = 6;
    public float minSpawnRadius = 18f;
    public float maxSpawnRadius = 32f;

    private float timer = 0f;

    void Start()
    {
        // Initial delay before first spawn check
        timer = 5f;
    }

    void Update()
    {
        if (VoxelWorld.Instance == null || VoxelWorld.Instance.playerTransform == null)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = spawnInterval;
            CheckAndSpawnWolf();
        }
    }

    private void CheckAndSpawnWolf()
    {
        // Count existing wolves
        var wolves = FindObjectsByType<WolfAI>(FindObjectsSortMode.None);
        if (wolves.Length >= maxWolves)
            return;

        // Pick random position around the player
        Vector3 playerPos = VoxelWorld.Instance.playerTransform.position;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(minSpawnRadius, maxSpawnRadius);

        float x = playerPos.x + Mathf.Cos(angle) * radius;
        float z = playerPos.z + Mathf.Sin(angle) * radius;

        // Find top block height
        float y = WolfAI.GetTopSolidBlockY(x, z);

        if (y > 0f && y < VoxelData.ChunkHeight)
        {
            // Verify block below is solid (not water) and block at feet is not water
            byte blockBelow = VoxelWorld.Instance.GetBlock(new Vector3(x, y - 0.5f, z));
            byte blockAtFeet = VoxelWorld.Instance.GetBlock(new Vector3(x, y, z));
            if (blockBelow != 0 && blockBelow != 7 && blockAtFeet != 7)
            {
                SpawnWolf(new Vector3(x, y + 0.1f, z));
            }
        }
    }

    private void SpawnWolf(Vector3 position)
    {
        GameObject wolfGO = new GameObject("Wolf_AI");
        wolfGO.transform.position = position;
        wolfGO.AddComponent<WolfAI>();
        Debug.Log($"[WolfSpawner] Spawned new wolf at {position}");
    }
}
