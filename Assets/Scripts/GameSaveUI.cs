using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class GameSaveUI : MonoBehaviour
{
    public static GameSaveUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GameSaveUI");
        Instance = go.AddComponent<GameSaveUI>();
        DontDestroyOnLoad(go);
    }

    private GameObject toolbarPanel;
    private TextMeshProUGUI creativeText;
    private GameObject activeLoadingOverlay;

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
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        BuildUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BuildUI();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Prevent triggering hotkeys while inventory is open
        if (InventoryUI.IsInventoryOpen) return;

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            SaveGame();
        }
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            LoadGame();
        }
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            ToggleCreative();
        }
        if (Keyboard.current.deleteKey.wasPressedThisFrame)
        {
            ClearSave();
        }
    }

    private void BuildUI()
    {
        // Clean up previous toolbar if any
        if (toolbarPanel != null)
        {
            Destroy(toolbarPanel);
        }

        // Do not display the save/load toolbar in the Main Menu scene
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[GameSaveUI] No Canvas found in scene. Cannot build GameSave UI.");
            return;
        }

        // Create Toolbar Panel
        toolbarPanel = CreateUIObject("GameSaveToolbar", canvas.transform);
        var rt = toolbarPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(430f, 38f);
        // Positioned next to DevTools (-230f offset to the left)
        rt.anchoredPosition = new Vector2(-230f, -10f);

        var img = toolbarPanel.AddComponent<Image>();
        img.color = new Color(0.10f, 0.10f, 0.13f, 0.85f);

        var layout = toolbarPanel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 4, 4);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        // Add buttons with hotkey letters next to them
        CreateButton("SaveBtn", "💾 Save [K]", SaveGame);
        CreateButton("LoadBtn", "🔄 Load [L]", LoadGame);
        CreateButton("ResetBtn", "❌ Reset [Delete]", ClearSave);
        
        var creativeBtn = CreateButton("CreativeBtn", "🦄 Creative: OFF [C]", ToggleCreative);
        if (creativeBtn != null)
        {
            creativeText = creativeBtn.GetComponentInChildren<TextMeshProUGUI>();
            UpdateCreativeButtonLabel();
        }
    }

    private GameObject CreateButton(string name, string label, UnityEngine.Events.UnityAction callback)
    {
        var btnGO = CreateUIObject(name, toolbarPanel.transform);
        
        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.20f, 0.20f, 0.25f, 0.9f);

        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(callback);

        // Add subtle hover/pressed color transitions
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        var textGO = CreateUIObject("Label", btnGO.transform);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btnGO;
    }

    private void SaveGame()
    {
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveGame();
            ShowToast("💾 Game Saved!");
        }
    }

    private void LoadGame()
    {
        ShowLoading();
        StartCoroutine(LoadGameRoutine());
    }

    private System.Collections.IEnumerator LoadGameRoutine()
    {
        // Wait 1 frame so Unity renders the loading screen overlay first
        yield return null;

        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.LoadGame();
        }
        UpdateCreativeButtonLabel();

        // Load complete! Fade out and destroy loading screen
        StartCoroutine(HideLoadingRoutine());
    }

    private void ClearSave()
    {
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.ClearSave();
            ShowToast("❌ Save Reset!");
        }
    }

    private void ToggleCreative()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.isCreativeMode = !player.isCreativeMode;
            UpdateCreativeButtonLabel();
            Debug.Log($"[GameSaveUI] Creative Mode toggled: {player.isCreativeMode}");
        }
    }

    private void UpdateCreativeButtonLabel()
    {
        if (creativeText == null) return;
        var player = FindFirstObjectByType<PlayerController>();
        bool isCreative = player != null && player.isCreativeMode;
        creativeText.text = isCreative ? "🦄 Creative: ON [C]" : "🦄 Creative: OFF [C]";
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void ShowToast(string message)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject toastGO = new GameObject("SaveToast", typeof(RectTransform), typeof(CanvasGroup));
        toastGO.transform.SetParent(canvas.transform, false);
        
        var rt = toastGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.15f);
        rt.anchorMax = new Vector2(0.5f, 0.15f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(250f, 40f);
        rt.anchoredPosition = Vector2.zero;

        var bg = toastGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.10f, 0.90f);
        
        var outline = toastGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(toastGO.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        var tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        StartCoroutine(FadeOutAndDestroy(toastGO.GetComponent<CanvasGroup>(), 1.5f));
    }

    private void ShowLoading()
    {
        if (activeLoadingOverlay != null)
        {
            Destroy(activeLoadingOverlay);
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        activeLoadingOverlay = new GameObject("LoadOverlay", typeof(RectTransform), typeof(CanvasGroup));
        activeLoadingOverlay.transform.SetParent(canvas.transform, false);

        var rt = activeLoadingOverlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var bg = activeLoadingOverlay.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(activeLoadingOverlay.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.5f, 0.5f);
        txtRT.anchorMax = new Vector2(0.5f, 0.5f);
        txtRT.pivot = new Vector2(0.5f, 0.5f);
        txtRT.sizeDelta = new Vector2(400f, 100f);
        txtRT.anchoredPosition = Vector2.zero;

        var tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "🔄 LOADING WORLD...";
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }

    private System.Collections.IEnumerator HideLoadingRoutine()
    {
        if (activeLoadingOverlay == null) yield break;

        var cg = activeLoadingOverlay.GetComponent<CanvasGroup>();
        float elapsed = 0f;
        float duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        if (activeLoadingOverlay != null)
        {
            Destroy(activeLoadingOverlay);
            activeLoadingOverlay = null;
        }
    }

    private System.Collections.IEnumerator FadeOutAndDestroy(CanvasGroup cg, float delay)
    {
        yield return new WaitForSeconds(delay);
        float elapsed = 0f;
        float duration = 0.4f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        if (cg != null) Destroy(cg.gameObject);
    }
}
