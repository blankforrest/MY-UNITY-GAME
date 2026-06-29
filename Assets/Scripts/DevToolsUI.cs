using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DevToolsUI : MonoBehaviour
{
    private static DevToolsUI Instance { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("DevToolsUI");
        Instance = go.AddComponent<DevToolsUI>();
        DontDestroyOnLoad(go);
    }

    public static bool IsCursorUnlocked { get; private set; } = false;

    private GameObject _panel;
    private GameObject _expandButtonGO;
    private bool _isExpanded = true;
    private string SavedBoatPath => System.IO.Path.Combine(Application.persistentDataPath, "SavedBoat.json");
    private string SavedSpotPath => System.IO.Path.Combine(Application.persistentDataPath, "SavedSpot.json");

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            if (_panel != null) _panel.SetActive(false);
            if (_expandButtonGO != null) _expandButtonGO.SetActive(false);
        }
        else
        {
            if (_panel == null)
            {
                BuildUI();
            }
            UpdateMenuState();
        }
    }

    private void Update()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
            return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        // Toggle cursor lock with Backquote/Tilde (`) — Tab is intentionally excluded
        // because Unity's EventSystem intercepts Tab for UI navigation, causing hangs.
        if (keyboard.backquoteKey.wasPressedThisFrame)
        {
            IsCursorUnlocked = !IsCursorUnlocked;
            Cursor.lockState = IsCursorUnlocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsCursorUnlocked;
        }

        // Hotkey shortcuts for fast testing
        if (keyboard.f8Key.wasPressedThisFrame)
        {
            SaveCurrentSpot();
        }
        else if (keyboard.f9Key.wasPressedThisFrame)
        {
            if (System.IO.File.Exists(SavedBoatPath))
                SpawnBoatHere();
            else
                Debug.LogWarning("[DevTools] No saved boat. Convert a structure and click 'Save' first!");
        }
        else if (keyboard.f10Key.wasPressedThisFrame)
        {
            if (!System.IO.File.Exists(SavedSpotPath))
                Debug.LogWarning("[DevTools] No saved spot. Stand where you want and press F8 first!");
            else if (!System.IO.File.Exists(SavedBoatPath))
                Debug.LogWarning("[DevTools] No saved boat. Convert a structure and click 'Save' first!");
            else
                TeleportToSavedSpot();
        }
    }

    private void UpdateMenuState()
    {
        if (_panel != null) _panel.SetActive(_isExpanded);
        if (_expandButtonGO != null) _expandButtonGO.SetActive(!_isExpanded);
    }

    private void ToggleMenu()
    {
        _isExpanded = !_isExpanded;
        UpdateMenuState();
    }

    // ── Spawning Actions ──────────────────────────────────────────────────────

    private void SpawnBoatHere()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        StructureBlueprint bp = BlueprintSerializer.LoadBlueprint(SavedBoatPath);
        if (bp == null)
        {
            Debug.LogError("[DevTools] Failed to load saved boat blueprint.");
            return;
        }

        Vector3 spawnPos = player.transform.position + player.transform.forward * (bp.dimensions.z * 0.5f + 3f);
        bp.worldOrigin = spawnPos - new Vector3(bp.dimensions.x * 0.5f, bp.dimensions.y * 0.5f, bp.dimensions.z * 0.5f);

        if (VehicleSpawner.Instance != null)
        {
            VehicleSpawner.Instance.SpawnVehicle(bp);
            Debug.Log($"[DevTools] Spawned saved boat in front of player at {bp.worldOrigin}");
        }
    }

    // ── F8: Save current spot ─────────────────────────────────────────────────

    private void SaveCurrentSpot()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogWarning("[DevTools] Player not found."); return; }

        Vector3 pos = player.transform.position;
        string json = JsonUtility.ToJson(new SavedSpotData { x = pos.x, y = pos.y, z = pos.z });
        System.IO.File.WriteAllText(SavedSpotPath, json);
        Debug.Log($"[DevTools] Spot saved: {pos}");
    }

    // ── F10: Teleport to saved spot and spawn boat ────────────────────────────

    private void TeleportToSavedSpot()
    {
        // Load saved position
        string json = System.IO.File.ReadAllText(SavedSpotPath);
        SavedSpotData data = JsonUtility.FromJson<SavedSpotData>(json);
        Vector3 savedPos = new Vector3(data.x, data.y, data.z);

        // Load blueprint
        StructureBlueprint bp = BlueprintSerializer.LoadBlueprint(SavedBoatPath);
        if (bp == null) { Debug.LogError("[DevTools] Failed to load saved boat."); return; }

        // Boat origin = min corner.
        // Place the boat so its BOTTOM is at savedPos.y, centered horizontally.
        Vector3 boatOrigin = new Vector3(
            savedPos.x - bp.dimensions.x * 0.5f,
            savedPos.y,                             // boat bottom at water surface
            savedPos.z - bp.dimensions.z * 0.5f
        );
        bp.worldOrigin = boatOrigin;

        // Place player ABOVE the boat top so they fall onto the deck, not inside the hull.
        Vector3 playerSpawn = new Vector3(
            savedPos.x,
            savedPos.y + bp.dimensions.y + 1.5f,   // 1.5 units above the roof
            savedPos.z
        );
        TeleportPlayer(playerSpawn);

        if (VehicleSpawner.Instance != null)
        {
            VehicleSpawner.Instance.SpawnVehicle(bp);
            Debug.Log($"[DevTools] Teleported above deck at {playerSpawn}, boat at {boatOrigin}.");
        }
    }

    private void TeleportPlayer(Vector3 targetPos)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            player.transform.position = targetPos;
            cc.enabled = true;
        }
        else
        {
            player.transform.position = targetPos;
        }
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Dev menu disabled — panel is intentionally not constructed.
    }


    private GameObject CreateDevButton(string name, string label, UnityEngine.Events.UnityAction callback)
    {
        var go = CreateUIObject(name, _panel.transform);
        SetLayoutHeight(go, 32f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.38f, 1f);
        colors.pressedColor     = new Color(0.15f, 0.15f, 0.18f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(callback);

        var labelGO = CreateUIObject("Label", go.transform);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go;
    }


    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetLayoutHeight(GameObject go, float height)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    private static void SetLayoutWidth(GameObject go, float width)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
    }
}

/// <summary>Simple JSON-serializable container for a saved world position.</summary>
[System.Serializable]
public class SavedSpotData
{
    public float x;
    public float y;
    public float z;
}
