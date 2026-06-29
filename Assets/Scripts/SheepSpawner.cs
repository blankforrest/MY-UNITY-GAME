using UnityEngine;

public class SheepSpawner : MonoBehaviour
{
    public float spawnInterval = 12f;
    public int maxSheep = 8;
    public float minSpawnRadius = 16f;
    public float maxSpawnRadius = 32f;

    private float timer = 0f;

    void Start()
    {
        timer = 6f; // Delay slightly after start
    }

    void Update()
    {
        if (VoxelWorld.Instance == null || VoxelWorld.Instance.playerTransform == null)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = spawnInterval;
            CheckAndSpawnSheep();
        }
    }

    private void CheckAndSpawnSheep()
    {
        var sheepList = FindObjectsByType<SheepAI>(FindObjectsSortMode.None);
        if (sheepList.Length >= maxSheep)
            return;

        Vector3 playerPos = VoxelWorld.Instance.playerTransform.position;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(minSpawnRadius, maxSpawnRadius);

        float x = playerPos.x + Mathf.Cos(angle) * radius;
        float z = playerPos.z + Mathf.Sin(angle) * radius;

        float y = SheepAI.GetTopSolidBlockY(x, z);

        if (y > 0f && y < VoxelData.ChunkHeight)
        {
            // Verify block below is solid (not water) and block at feet is not water
            byte blockBelow = VoxelWorld.Instance.GetBlock(new Vector3(x, y - 0.5f, z));
            byte blockAtFeet = VoxelWorld.Instance.GetBlock(new Vector3(x, y, z));
            if (blockBelow != 0 && blockBelow != 7 && blockAtFeet != 7)
            {
                SpawnSheep(new Vector3(x, y + 0.1f, z));
            }
        }
    }

    private void SpawnSheep(Vector3 position)
    {
        GameObject sheepGO = new GameObject("Sheep_AI");
        sheepGO.transform.position = position;
        sheepGO.AddComponent<SheepAI>();
        Debug.Log($"[SheepSpawner] Spawned new sheep at {position}");
    }
}
