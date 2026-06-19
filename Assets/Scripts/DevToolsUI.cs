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

    private void Start()
    {
        BuildUI();
        UpdateMenuState();
    }

    private void Update()
    {
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
        _panel.SetActive(_isExpanded);
        _expandButtonGO.SetActive(!_isExpanded);
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
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DevToolsUI] No Canvas found in scene.");
            return;
        }

        // ── Expand Button (for when panel is collapsed) ───────────────────────
        _expandButtonGO = CreateUIObject("DevExpandButton", canvas.transform);
        var expRT = _expandButtonGO.GetComponent<RectTransform>();
        expRT.anchorMin = new Vector2(1f, 1f);
        expRT.anchorMax = new Vector2(1f, 1f);
        expRT.pivot     = new Vector2(1f, 1f);
        expRT.sizeDelta = new Vector2(110f, 30f);
        expRT.anchoredPosition = new Vector2(-10f, -10f);

        var expImg = _expandButtonGO.AddComponent<Image>();
        expImg.color = new Color(0.12f, 0.12f, 0.15f, 0.90f);
        var expBtn = _expandButtonGO.AddComponent<Button>();
        expBtn.onClick.AddListener(ToggleMenu);

        var expTextGO = CreateUIObject("Label", _expandButtonGO.transform);
        var expTextRT = expTextGO.GetComponent<RectTransform>();
        expTextRT.anchorMin = Vector2.zero;
        expTextRT.anchorMax = Vector2.one;
        expTextRT.sizeDelta = Vector2.zero;
        var expTmp = expTextGO.AddComponent<TextMeshProUGUI>();
        expTmp.text = "⚙️ Dev Tools";
        expTmp.fontSize = 12f;
        expTmp.alignment = TextAlignmentOptions.Center;
        expTmp.color = Color.yellow;

        // ── Main Dev Panel ────────────────────────────────────────────────────
        _panel = CreateUIObject("DevToolsPanel", canvas.transform);
        var panelRT = _panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 1f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot     = new Vector2(1f, 1f);
        panelRT.sizeDelta = new Vector2(210f, 225f);
        panelRT.anchoredPosition = new Vector2(-10f, -10f);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

        var layout = _panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // ── Header Row (Title & Collapse button) ─────────────────────────────
        var headerGO = CreateUIObject("HeaderRow", _panel.transform);
        SetLayoutHeight(headerGO, 24f);
        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = true;

        var titleGO = CreateUIObject("Title", headerGO.transform);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "⚙️ Dev Menu";
        titleTmp.fontSize = 14f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.yellow;
        titleTmp.alignment = TextAlignmentOptions.Left;

        var colBtnGO = CreateUIObject("CollapseButton", headerGO.transform);
        var colBtnImg = colBtnGO.AddComponent<Image>();
        colBtnImg.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        var colBtn = colBtnGO.AddComponent<Button>();
        colBtn.onClick.AddListener(ToggleMenu);
        var colTextGO = CreateUIObject("Label", colBtnGO.transform);
        var colTextRT = colTextGO.GetComponent<RectTransform>();
        colTextRT.anchorMin = Vector2.zero;
        colTextRT.anchorMax = Vector2.one;
        colTextRT.sizeDelta = Vector2.zero;
        var colTmp = colTextGO.AddComponent<TextMeshProUGUI>();
        colTmp.text = "[x]";
        colTmp.fontSize = 11f;
        colTmp.alignment = TextAlignmentOptions.Center;
        colTmp.color = Color.white;
        SetLayoutWidth(colBtnGO, 24f);

        // Divider
        var divGO = CreateUIObject("Divider", _panel.transform);
        SetLayoutHeight(divGO, 1f);
        divGO.AddComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        // Buttons
        CreateDevButton("SaveSpotBtn",  "📍 Save This Spot (F8)",       SaveCurrentSpot);
        CreateDevButton("SpawnHereBtn", "🚢 Spawn Boat Here (F9)",       SpawnBoatHere);
        CreateDevButton("TeleportBtn",  "🌊 Teleport & Spawn (F10)",     TeleportToSavedSpot);



        // Footer Hint Text
        var hintGO = CreateUIObject("Hint", _panel.transform);
        SetLayoutHeight(hintGO, 40f);
        var hintTmp = hintGO.AddComponent<TextMeshProUGUI>();
        hintTmp.text = "F8=Save Spot  F9=Spawn Here  F10=Teleport\n` (backtick) = unlock cursor";
        hintTmp.fontSize = 9f;
        hintTmp.color = new Color(0.7f, 0.7f, 0.7f);
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.wordSpacing = -0.5f;
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
