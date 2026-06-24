using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FurnaceState
{
    public Vector3Int position;
    public InventorySlot inputSlot;
    public InventorySlot fuelSlot;
    public InventorySlot outputSlot;

    public float fuelBurnTimeLeft = 0f;
    public float maxFuelBurnTime = 0f;
    public float smeltProgress = 0f;
    public float smeltTimeRequired = 5f; // 5 seconds per sand
}

public class FurnaceManager : MonoBehaviour
{
    private static FurnaceManager _instance;
    public static FurnaceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("FurnaceManager");
                _instance = go.AddComponent<FurnaceManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    public static FurnaceState ActiveFurnace;

    private readonly Dictionary<Vector3Int, FurnaceState> _furnaces = new Dictionary<Vector3Int, FurnaceState>();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        bool stateChanged = false;

        foreach (var kvp in _furnaces)
        {
            FurnaceState state = kvp.Value;
            bool wasBurning = state.fuelBurnTimeLeft > 0f;

            // 1. Tick fuel burn time down
            if (wasBurning)
            {
                state.fuelBurnTimeLeft -= Time.deltaTime;
                if (state.fuelBurnTimeLeft < 0f) state.fuelBurnTimeLeft = 0f;
                stateChanged = true;
            }

            // 2. Determine if we can smelt
            bool canSmelt = CanSmelt(state);

            // 3. Consume fuel if needed and we have ingredients
            if (canSmelt && state.fuelBurnTimeLeft <= 0f)
            {
                if (state.fuelSlot != null && state.fuelSlot.item != null && state.fuelSlot.amount > 0)
                {
                    float burnTime = GetFuelBurnTime(state.fuelSlot.item);
                    if (burnTime > 0f)
                    {
                        state.fuelBurnTimeLeft = burnTime;
                        state.maxFuelBurnTime = burnTime;
                        
                        // Consume 1 fuel
                        state.fuelSlot.amount--;
                        if (state.fuelSlot.amount <= 0)
                        {
                            state.fuelSlot = null;
                        }
                        stateChanged = true;
                    }
                }
            }

            // 4. Progress smelting if burning and can smelt
            if (state.fuelBurnTimeLeft > 0f && canSmelt)
            {
                state.smeltProgress += Time.deltaTime;
                if (state.smeltProgress >= state.smeltTimeRequired)
                {
                    SmeltItem(state);
                    state.smeltProgress = 0f;
                }
                stateChanged = true;
            }
            else
            {
                // Cool down progress if not actively smelting
                if (state.smeltProgress > 0f)
                {
                    state.smeltProgress -= Time.deltaTime * 2f; // cool down faster than heating
                    if (state.smeltProgress < 0f) state.smeltProgress = 0f;
                    stateChanged = true;
                }
            }

            bool isBurningNow = state.fuelBurnTimeLeft > 0f;
            if (wasBurning != isBurningNow)
            {
                TriggerChunkRebuildAt(state.position);
            }
        }

        // If the UI is currently open for the active furnace, refresh the UI
        if (stateChanged && ActiveFurnace != null && InventoryUI.Instance != null && InventoryUI.Instance.isFurnaceActive)
        {
            InventoryUI.Instance.RefreshFurnaceUI();
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public FurnaceState GetOrCreateFurnace(Vector3Int position)
    {
        if (!_furnaces.TryGetValue(position, out FurnaceState state))
        {
            state = new FurnaceState { position = position };
            _furnaces[position] = state;
        }
        return state;
    }

    public void RemoveFurnace(Vector3Int position)
    {
        if (_furnaces.ContainsKey(position))
        {
            // Drop items stored inside furnace when it is broken
            FurnaceState state = _furnaces[position];
            DropItemAt(state.inputSlot, position);
            DropItemAt(state.fuelSlot, position);
            DropItemAt(state.outputSlot, position);
            _furnaces.Remove(position);
        }
    }

    public static bool IsFuel(Item item)
    {
        if (item == null) return false;
        return GetFuelBurnTime(item) > 0f;
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    private static float GetFuelBurnTime(Item item)
    {
        if (item == null) return 0f;
        if (item.itemName == "Coal Ore" || item.itemName == "Coal Chunk") return 16f; // burns for 16s (smelts ~3 items)
        if (item.itemName == "Wood") return 8f;     // burns for 8s
        if (item.itemName == "Plank") return 4f;    // burns for 4s
        if (item.itemName == "Stick") return 2f;    // burns for 2s
        return 0f;
    }

    private bool CanSmelt(FurnaceState state)
    {
        // Must have Sand or Iron Ore in the input slot
        if (state.inputSlot == null || state.inputSlot.item == null || state.inputSlot.amount <= 0)
            return false;

        string inputName = state.inputSlot.item.itemName;
        if (inputName != "Sand" && inputName != "Iron Ore")
            return false;

        // Output slot checks: must be empty, or matching result and have room (max stack 64)
        if (state.outputSlot == null || state.outputSlot.item == null)
            return true;

        string outputName = state.outputSlot.item.itemName;
        if (inputName == "Sand" && outputName == "Glass" && state.outputSlot.amount < 64)
            return true;

        if (inputName == "Iron Ore" && outputName == "Iron Ingot" && state.outputSlot.amount < 64)
            return true;

        return false;
    }

    private void SmeltItem(FurnaceState state)
    {
        if (!CanSmelt(state)) return;

        string inputName = state.inputSlot.item.itemName;

        // Consume 1 input
        state.inputSlot.amount--;
        if (state.inputSlot.amount <= 0)
        {
            state.inputSlot = null;
        }

        string resultName = (inputName == "Sand") ? "Glass" : "Iron Ingot";
        int resultTypeID = (inputName == "Sand") ? 35 : 0;

        // Add 1 output
        if (state.outputSlot == null || state.outputSlot.item == null)
        {
            Item outputItem = Inventory.Instance?.CreateItem(resultName, resultTypeID);
            if (outputItem == null)
            {
                outputItem = ScriptableObject.CreateInstance<Item>();
                outputItem.itemName = resultName;
                outputItem.blockTypeID = resultTypeID;
            }
            state.outputSlot = new InventorySlot(outputItem, 1);
        }
        else
        {
            state.outputSlot.amount++;
        }
    }

    private void DropItemAt(InventorySlot slot, Vector3Int position)
    {
        if (slot == null || slot.item == null || slot.amount <= 0) return;
        
        Vector3 spawnPos = new Vector3(position.x + 0.5f, position.y + 0.5f, position.z + 0.5f);
        DroppedItem.Spawn(slot.item, slot.amount, spawnPos, (byte)slot.item.blockTypeID);
    }

    public bool IsFurnaceBurning(Vector3Int position)
    {
        if (_furnaces.TryGetValue(position, out FurnaceState state))
        {
            return state.fuelBurnTimeLeft > 0f;
        }
        return false;
    }

    private void TriggerChunkRebuildAt(Vector3Int position)
    {
        if (VoxelWorld.Instance != null)
        {
            Vector3 worldPos = new Vector3(position.x + 0.5f, position.y + 0.5f, position.z + 0.5f);
            Chunk chunk = VoxelWorld.Instance.GetChunkFromVector3(worldPos);
            if (chunk != null)
            {
                chunk.isDirty = true;
            }
        }
    }
}
