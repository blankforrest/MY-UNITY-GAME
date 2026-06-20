using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("SaveLoadManager");
        Instance = go.AddComponent<SaveLoadManager>();
        DontDestroyOnLoad(go);
    }

    public static int activeWorldSlot = 1;
    public static int activeWorldSeed = 0;
    public static float worldSeedOffsetX = 0f;
    public static float worldSeedOffsetZ = 0f;

    public void UpdateSeedOffsets()
    {
        Random.State oldState = Random.state;
        Random.InitState(activeWorldSeed);
        worldSeedOffsetX = Random.Range(-100000f, 100000f);
        Random.InitState(activeWorldSeed + 1);
        worldSeedOffsetZ = Random.Range(-100000f, 100000f);
        Random.state = oldState;
        Debug.Log($"[SaveLoadManager] Seed updated: {activeWorldSeed}, OffsetX: {worldSeedOffsetX}, OffsetZ: {worldSeedOffsetZ}");
    }

    private Dictionary<Vector3Int, byte> worldModifications = new Dictionary<Vector3Int, byte>();
    public string SaveFilePath => Path.Combine(Application.persistentDataPath, $"WorldSave_{activeWorldSlot}.json");

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Migrate legacy save if slot 1 doesn't exist yet
        string legacyPath = Path.Combine(Application.persistentDataPath, "WorldSave.json");
        string slot1Path = Path.Combine(Application.persistentDataPath, "WorldSave_1.json");
        if (File.Exists(legacyPath) && !File.Exists(slot1Path))
        {
            try { File.Move(legacyPath, slot1Path); } catch {}
        }

        // Pre-load modifications in Awake so chunk generation applies them on startup
        LoadModificationsOnly();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu")
        {
            if (HasSaveFile())
            {
                LoadGame();
            }
            else
            {
                PrepareNewWorld();

                // Initialize player creative mode based on MainMenu selectedGameMode for new world
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    player.isCreativeMode = (MainMenu.selectedGameMode == "Creative");
                    if (player.isCreativeMode)
                    {
                        if (Inventory.Instance != null)
                        {
                            Inventory.Instance.PopulateCreativeInventory();
                        }
                    }
                }
            }

            // Create PauseMenu dynamically if not already present
            if (FindFirstObjectByType<PauseMenu>() == null)
            {
                GameObject pmGO = new GameObject("PauseMenuContainer");
                pmGO.AddComponent<PauseMenu>();
            }

            // Start auto-save coroutine
            StopAllCoroutines();
            StartCoroutine(AutoSaveRoutine());
        }
    }

    private System.Collections.IEnumerator AutoSaveRoutine()
    {
        // Wait 2 seconds so chunk generation initializes, player is spawned,
        // and StarterItems has had a chance to run or bypass.
        yield return new WaitForSeconds(2.0f);

        SaveGame();

        while (true)
        {
            yield return new WaitForSeconds(30.0f);

            if (SceneManager.GetActiveScene().name != "MainMenu" && FindFirstObjectByType<PlayerController>() != null)
            {
                SaveGame();
            }
        }
    }

    public bool HasSaveFile() => File.Exists(SaveFilePath);

    public string GetSavedGameMode() => GetSavedGameMode(activeWorldSlot);

    public string GetSavedGameMode(int slot)
    {
        string path = Path.Combine(Application.persistentDataPath, $"WorldSave_{slot}.json");
        if (!File.Exists(path)) return "Survival";
        try
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            return data.isCreativeMode ? "Creative" : "Survival";
        }
        catch
        {
            return "Survival";
        }
    }

    public List<int> GetExistingSlots()
    {
        List<int> slots = new List<int>();
        string dir = Application.persistentDataPath;
        if (!Directory.Exists(dir)) return slots;
        
        string[] files = Directory.GetFiles(dir, "WorldSave_*.json");
        foreach (string file in files)
        {
            string filename = Path.GetFileNameWithoutExtension(file);
            string[] parts = filename.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[1], out int slot))
            {
                slots.Add(slot);
            }
        }
        slots.Sort();
        return slots;
    }

    public void RecordModification(Vector3 pos, byte blockID)
    {
        Vector3Int gridPos = new Vector3Int(
            Mathf.FloorToInt(pos.x),
            Mathf.FloorToInt(pos.y),
            Mathf.FloorToInt(pos.z)
        );
        worldModifications[gridPos] = blockID;
    }

    public void ApplyChunkModifications(Vector2 chunkPos, byte[,,] voxelMap)
    {
        int chunkWidth = VoxelData.ChunkWidth;
        int chunkHeight = VoxelData.ChunkHeight;

        int startX = Mathf.FloorToInt(chunkPos.x * chunkWidth);
        int startZ = Mathf.FloorToInt(chunkPos.y * chunkWidth);

        for (int x = 0; x < chunkWidth; x++)
        {
            for (int z = 0; z < chunkWidth; z++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    Vector3Int globalPos = new Vector3Int(startX + x, y, startZ + z);
                    if (worldModifications.TryGetValue(globalPos, out byte savedID))
                    {
                        voxelMap[x, y, z] = savedID;

                        // Re-register player-placed blocks (excluding air, water, and initial flowers)
                        if (savedID != 0 && savedID != 7 && savedID != 9 && savedID != 10 && savedID != 11)
                        {
                            PlacedBlockRegistry.Instance?.Register(globalPos);
                        }
                        else if (savedID == 0)
                        {
                            PlacedBlockRegistry.Instance?.Unregister(globalPos);
                        }
                    }
                }
            }
        }
    }

    public void SaveGame()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("[SaveLoadManager] Player not found. Cannot save.");
            return;
        }

        SaveData data = new SaveData();

        // Save Player position & rotation
        Vector3 pPos = player.transform.position;
        data.playerX = pPos.x;
        data.playerY = pPos.y;
        data.playerZ = pPos.z;

        Vector3 pRot = player.transform.eulerAngles;
        data.playerRotX = pRot.x;
        data.playerRotY = pRot.y;
        data.playerRotZ = pRot.z;

        data.isCreativeMode = player.isCreativeMode;
        data.seed = activeWorldSeed;

        // Save Block modifications
        data.modifications = new List<SavedBlock>();
        foreach (var kvp in worldModifications)
        {
            data.modifications.Add(new SavedBlock
            {
                x = kvp.Key.x,
                y = kvp.Key.y,
                z = kvp.Key.z,
                id = kvp.Value
            });
        }

        // Save Hotbar
        data.hotbar = new List<SavedItem>();
        if (Hotbar.Instance != null)
        {
            for (int i = 0; i < 8; i++)
            {
                var slot = Hotbar.Instance.GetSlotData(i);
                if (slot != null && slot.item != null)
                {
                    data.hotbar.Add(new SavedItem
                    {
                        itemName = slot.item.itemName,
                        blockTypeID = slot.item.blockTypeID,
                        amount = slot.amount,
                        isEmpty = false
                    });
                }
                else
                {
                    data.hotbar.Add(new SavedItem { isEmpty = true });
                }
            }
        }

        // Save Inventory
        data.inventory = new List<SavedItem>();
        if (Inventory.Instance != null && Inventory.Instance.slots != null)
        {
            int slotCount = Inventory.Instance.slots.Length;
            for (int i = 0; i < slotCount; i++)
            {
                var slot = Inventory.Instance.slots[i];
                if (slot != null && slot.item != null)
                {
                    data.inventory.Add(new SavedItem
                    {
                        itemName = slot.item.itemName,
                        blockTypeID = slot.item.blockTypeID,
                        amount = slot.amount,
                        isEmpty = false
                    });
                }
                else
                {
                    data.inventory.Add(new SavedItem { isEmpty = true });
                }
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SaveFilePath, json);
        Debug.Log($"[SaveLoadManager] Game saved to {SaveFilePath}");
    }

    public void LoadGame()
    {
        LoadModificationsOnly();
        RestorePlayerAndInventory();

        // Re-populate and rebuild active chunks to show loaded modifications
        if (VoxelWorld.Instance != null)
        {
            VoxelWorld.Instance.RebuildAllChunks();
        }

        Debug.Log("[SaveLoadManager] Game state loaded and chunks rebuilt.");
    }

    private void LoadModificationsOnly()
    {
        if (!File.Exists(SaveFilePath)) return;

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            activeWorldSeed = data.seed;
            UpdateSeedOffsets();

            worldModifications.Clear();
            if (data.modifications != null)
            {
                foreach (var b in data.modifications)
                {
                    worldModifications[new Vector3Int(b.x, b.y, b.z)] = b.id;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveLoadManager] Error loading modifications: {e}");
        }
    }

    private void RestorePlayerAndInventory()
    {
        if (!File.Exists(SaveFilePath)) return;

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // Load Player pos/rot
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                player.transform.position = new Vector3(data.playerX, data.playerY, data.playerZ);
                player.transform.eulerAngles = new Vector3(data.playerRotX, data.playerRotY, data.playerRotZ);
                player.isCreativeMode = data.isCreativeMode;

                if (cc != null) cc.enabled = true;
            }

            // Load Hotbar
            if (Hotbar.Instance != null && data.hotbar != null)
            {
                for (int i = 0; i < Mathf.Min(8, data.hotbar.Count); i++)
                {
                    var s = data.hotbar[i];
                    if (!s.isEmpty)
                    {
                        Item item = StarterItems.CreateItemInstance(s.itemName, s.blockTypeID, Color.white);
                        Hotbar.Instance.SetSlot(i, item, s.amount);
                    }
                    else
                    {
                        Hotbar.Instance.SetSlot(i, null, 0);
                    }
                }
            }

            // Load Inventory
            if (Inventory.Instance != null && data.inventory != null)
            {
                for (int i = 0; i < Mathf.Min(Inventory.MaxSlots, data.inventory.Count); i++)
                {
                    var s = data.inventory[i];
                    if (!s.isEmpty)
                    {
                        Item item = StarterItems.CreateItemInstance(s.itemName, s.blockTypeID, Color.white);
                        Inventory.Instance.SetSlot(i, item, s.amount);
                    }
                    else
                    {
                        Inventory.Instance.ClearSlot(i);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveLoadManager] Error restoring player and inventory: {e}");
        }
    }

    public void PrepareNewWorld()
    {
        worldModifications.Clear();
        PlacedBlockRegistry.Instance?.Clear();
    }

    public void ClearSave()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
        }
        PrepareNewWorld();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Debug.Log("[SaveLoadManager] Save cleared, scene reloaded.");
    }

    public void DeleteSave(int slot)
    {
        string path = Path.Combine(Application.persistentDataPath, $"WorldSave_{slot}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveLoadManager] Deleted world save file: {path}");
        }
    }
}

[System.Serializable]
public class SavedBlock
{
    public int x;
    public int y;
    public int z;
    public byte id;
}

[System.Serializable]
public class SavedItem
{
    public string itemName;
    public int blockTypeID;
    public int amount;
    public bool isEmpty;
}

[System.Serializable]
public class SaveData
{
    public float playerX;
    public float playerY;
    public float playerZ;

    public float playerRotX;
    public float playerRotY;
    public float playerRotZ;

    public List<SavedBlock> modifications;
    public List<SavedItem> hotbar;
    public List<SavedItem> inventory;
    public bool isCreativeMode;
    public int seed;
}
