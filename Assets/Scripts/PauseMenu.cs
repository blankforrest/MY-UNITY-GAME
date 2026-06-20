using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; } = false;

    private GameObject pauseCanvasGO;
    private GameObject pauseButtonGO;
    private GameObject pauseOverlayGO;
    private GameObject mainPanel;
    private GameObject settingsPanel;
    private GameObject helpPanel;

    private PlayerController playerController;
    private Camera playerCamera;

    void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    void Start()
    {
        FindPlayerComponents();
        CreatePauseUI();
        UpdatePauseState(false);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (InventoryUI.IsInventoryOpen || ConfirmationWindow.IsOpen)
            {
                return;
            }
            TogglePause();
        }
    }

    private void FindPlayerComponents()
    {
        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerCamera = playerController.GetComponentInChildren<Camera>();
        }
    }

    public void TogglePause()
    {
        UpdatePauseState(!IsPaused);
    }

    private void UpdatePauseState(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseOverlayGO != null) pauseOverlayGO.SetActive(paused);
        if (pauseButtonGO != null) pauseButtonGO.SetActive(!paused);

        if (paused)
        {
            SwitchToPanel("Main");
            FindPlayerComponents(); // Refresh references in case they changed
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void CreatePauseUI()
    {
        // 1. Find or create Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // 2. HUD Pause Button (floating top-right)
        pauseButtonGO = new GameObject("HUDPauseButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonEffects));
        pauseButtonGO.transform.SetParent(canvas.transform, false);

        RectTransform pbRT = pauseButtonGO.GetComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(1f, 1f);
        pbRT.anchorMax = new Vector2(1f, 1f);
        pbRT.pivot = new Vector2(1f, 1f);
        pbRT.sizeDelta = new Vector2(40, 40);
        pbRT.anchoredPosition = new Vector2(-20, -20);

        Image pbImg = pauseButtonGO.GetComponent<Image>();
        pbImg.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);

        GameObject pbTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        pbTxtGO.transform.SetParent(pauseButtonGO.transform, false);
        RectTransform pbTxtRT = pbTxtGO.GetComponent<RectTransform>();
        pbTxtRT.anchorMin = Vector2.zero;
        pbTxtRT.anchorMax = Vector2.one;
        pbTxtRT.sizeDelta = Vector2.zero;
        TextMeshProUGUI pbTmp = pbTxtGO.GetComponent<TextMeshProUGUI>();
        pbTmp.text = "<b>‖</b>";
        pbTmp.fontSize = 22;
        pbTmp.alignment = TextAlignmentOptions.Center;
        pbTmp.color = Color.white;

        Button pbBtn = pauseButtonGO.GetComponent<Button>();
        pbBtn.onClick.AddListener(() => TogglePause());

        // 3. Full-screen Pause Overlay
        pauseOverlayGO = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image));
        pauseOverlayGO.transform.SetParent(canvas.transform, false);
        RectTransform overlayRT = pauseOverlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;

        Image overlayImg = pauseOverlayGO.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.6f); // Semi-transparent black background dim

        // 4. Central Card Panel
        GameObject cardGO = new GameObject("PauseCard", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(pauseOverlayGO.transform, false);
        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(500, 600);
        cardRT.anchoredPosition = Vector2.zero;

        Image cardImg = cardGO.GetComponent<Image>();
        cardImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f); // Beautiful dark card panel

        // 5. Main Pause Panel
        mainPanel = new GameObject("MainPausePanel", typeof(RectTransform));
        mainPanel.transform.SetParent(cardGO.transform, false);
        RectTransform mainRT = mainPanel.GetComponent<RectTransform>();
        mainRT.anchorMin = Vector2.zero;
        mainRT.anchorMax = Vector2.one;
        mainRT.sizeDelta = Vector2.zero;

        // Title text for Main Panel
        GameObject mainTitleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        mainTitleGO.transform.SetParent(mainPanel.transform, false);
        RectTransform mtRT = mainTitleGO.GetComponent<RectTransform>();
        mtRT.anchorMin = new Vector2(0.5f, 1f);
        mtRT.anchorMax = new Vector2(0.5f, 1f);
        mtRT.pivot = new Vector2(0.5f, 1f);
        mtRT.sizeDelta = new Vector2(400, 80);
        mtRT.anchoredPosition = new Vector2(0, -30);
        TextMeshProUGUI mtText = mainTitleGO.GetComponent<TextMeshProUGUI>();
        mtText.text = "GAME PAUSED";
        mtText.fontSize = 36;
        mtText.fontStyle = FontStyles.Bold;
        mtText.alignment = TextAlignmentOptions.Center;
        mtText.color = Color.white;

        // Buttons Container
        GameObject btnContainer = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        btnContainer.transform.SetParent(mainPanel.transform, false);
        RectTransform bcRT = btnContainer.GetComponent<RectTransform>();
        bcRT.anchorMin = new Vector2(0.5f, 0.45f);
        bcRT.anchorMax = new Vector2(0.5f, 0.45f);
        bcRT.pivot = new Vector2(0.5f, 0.5f);
        bcRT.sizeDelta = new Vector2(300, 300);
        bcRT.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = btnContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        CreateMenuButton(btnContainer.transform, "RESUME GAME", () => TogglePause());
        CreateMenuButton(btnContainer.transform, "SETTINGS", () => SwitchToPanel("Settings"));
        CreateMenuButton(btnContainer.transform, "HELP & CONTROLS", () => SwitchToPanel("Help"));
        CreateMenuButton(btnContainer.transform, "SAVE & EXIT", () => ExitToMainMenu());

        // 6. Settings Panel
        CreateSettingsPanel(cardGO.transform);

        // 7. Help Panel
        CreateHelpPanel(cardGO.transform);
    }

    private void CreateSettingsPanel(Transform parent)
    {
        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
        settingsPanel.transform.SetParent(parent, false);
        RectTransform rt = settingsPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(settingsPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(400, 80);
        titleRT.anchoredPosition = new Vector2(0, -30);
        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SETTINGS";
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Sliders Container
        GameObject sliderContainer = new GameObject("Sliders", typeof(RectTransform), typeof(VerticalLayoutGroup));
        sliderContainer.transform.SetParent(settingsPanel.transform, false);
        RectTransform scRT = sliderContainer.GetComponent<RectTransform>();
        scRT.anchorMin = new Vector2(0.5f, 0.5f);
        scRT.anchorMax = new Vector2(0.5f, 0.5f);
        scRT.pivot = new Vector2(0.5f, 0.5f);
        scRT.sizeDelta = new Vector2(400, 320);
        scRT.anchoredPosition = new Vector2(0, 30);

        VerticalLayoutGroup vlg = sliderContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        // Load values
        float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 0.1f);
        float fov = PlayerPrefs.GetFloat("FOV", 70f);
        float volume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        float renderDist = PlayerPrefs.GetInt("RenderDistance", 6);

        CreateSlider(sliderContainer.transform, "Mouse Sensitivity", 0.01f, 0.5f, sensitivity, (val, label) => {
            label.text = $"Mouse Sensitivity: {val:F2}";
            PlayerPrefs.SetFloat("MouseSensitivity", val);
            if (playerController != null) playerController.mouseSensitivity = val;
        });

        CreateSlider(sliderContainer.transform, "Field of View (FOV)", 50f, 110f, fov, (val, label) => {
            label.text = $"Field of View (FOV): {val:F0}";
            PlayerPrefs.SetFloat("FOV", val);
            if (playerCamera != null) playerCamera.fieldOfView = val;
        });

        CreateSlider(sliderContainer.transform, "Master Volume", 0.0f, 1.0f, volume, (val, label) => {
            label.text = $"Master Volume: {val * 100:F0}%";
            PlayerPrefs.SetFloat("MasterVolume", val);
            AudioListener.volume = val;
        });

        CreateSlider(sliderContainer.transform, "Render Distance (Chunks)", 3f, 16f, renderDist, (val, label) => {
            int intVal = Mathf.RoundToInt(val);
            label.text = $"Render Distance: {intVal} Chunks";
            PlayerPrefs.SetInt("RenderDistance", intVal);
            if (VoxelWorld.Instance != null)
            {
                VoxelWorld.Instance.RefreshRenderDistance(intVal);
            }
        });

        // Back Button
        GameObject backGO = new GameObject("BackButtonContainer", typeof(RectTransform));
        backGO.transform.SetParent(settingsPanel.transform, false);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0.08f);
        backRT.anchorMax = new Vector2(0.5f, 0.08f);
        backRT.pivot = new Vector2(0.5f, 0.5f);
        backRT.sizeDelta = new Vector2(200, 50);
        backRT.anchoredPosition = Vector2.zero;

        CreateMenuButton(backGO.transform, "BACK", () => SwitchToPanel("Main"));
    }

    private void CreateHelpPanel(Transform parent)
    {
        helpPanel = new GameObject("HelpPanel", typeof(RectTransform));
        helpPanel.transform.SetParent(parent, false);
        RectTransform rt = helpPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(helpPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(400, 80);
        titleRT.anchoredPosition = new Vector2(0, -30);
        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "HELP / CONTROLS";
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Content
        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(TextMeshProUGUI));
        contentGO.transform.SetParent(helpPanel.transform, false);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax = new Vector2(0.5f, 0.5f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.sizeDelta = new Vector2(450, 350);
        contentRT.anchoredPosition = new Vector2(0, 10);

        TextMeshProUGUI infoText = contentGO.GetComponent<TextMeshProUGUI>();
        infoText.text = "<b>CONTROLS & SHORTCUTS</b>\n\n" +
                        "<b>Movement:</b> WASD | <b>Look around:</b> Mouse\n" +
                        "<b>Sneak Mode:</b> Left Control | <b>Show Cursor:</b> ` (Backquote)\n" +
                        "<b>Inventory / Crafting:</b> I | <b>Toggle Pause:</b> Escape / P\n" +
                        "<b>Enter Vehicle:</b> E | <b>Return to Boat (Rescue):</b> R\n\n" +
                        "<b>Save Progress:</b> K | <b>Load Progress:</b> L\n" +
                        "<b>Reset Save File:</b> Delete\n" +
                        "<b>Creative Mode (Flight):</b> C\n" +
                        "<b>Fly Up / Down:</b> Space / Left Shift";
        infoText.fontSize = 16;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        // Back Button
        GameObject backGO = new GameObject("BackButtonContainer", typeof(RectTransform));
        backGO.transform.SetParent(helpPanel.transform, false);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0.12f);
        backRT.anchorMax = new Vector2(0.5f, 0.12f);
        backRT.pivot = new Vector2(0.5f, 0.5f);
        backRT.sizeDelta = new Vector2(200, 50);
        backRT.anchoredPosition = Vector2.zero;

        CreateMenuButton(backGO.transform, "BACK", () => SwitchToPanel("Main"));
    }

    private void SwitchToPanel(string panelName)
    {
        if (mainPanel != null) mainPanel.SetActive(panelName == "Main");
        if (settingsPanel != null) settingsPanel.SetActive(panelName == "Settings");
        if (helpPanel != null) helpPanel.SetActive(panelName == "Help");
    }

    private void ExitToMainMenu()
    {
        UpdatePauseState(false);
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveGame();
        }
        SceneManager.LoadScene(0);
    }

    private void CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnGO = new GameObject(label + "_Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonEffects));
        btnGO.transform.SetParent(parent, false);

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
        tmp.fontMaterial.SetColor("_OutlineColor", Color.black);
        tmp.fontMaterial.SetFloat("_OutlineWidth", 0.2f);
        tmp.UpdateMeshPadding();
    }

    private Slider CreateSlider(Transform parent, string labelText, float min, float max, float current, System.Action<float, TextMeshProUGUI> onValueChanged)
    {
        GameObject container = new GameObject(labelText + "_SliderContainer", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        RectTransform containerRT = container.GetComponent<RectTransform>();
        containerRT.sizeDelta = new Vector2(400, 60);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(container.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.5f);
        labelRT.anchorMax = new Vector2(1f, 0.5f);
        labelRT.pivot = new Vector2(0f, 0.5f);
        labelRT.anchoredPosition = new Vector2(0, 15);
        labelRT.sizeDelta = new Vector2(0, 30);
        
        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.fontSize = 16;
        label.color = Color.white;
        label.text = labelText;

        GameObject sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(container.transform, false);
        RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(1f, 0f);
        sliderRT.pivot = new Vector2(0.5f, 0f);
        sliderRT.anchoredPosition = new Vector2(0, 5);
        sliderRT.sizeDelta = new Vector2(0, 20);

        Slider slider = sliderGO.GetComponent<Slider>();

        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.25f);
        bgRT.anchorMax = new Vector2(1f, 0.75f);
        bgRT.sizeDelta = Vector2.zero;
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.18f, 0.20f, 0.25f, 0.9f);

        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRT.sizeDelta = Vector2.zero;

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.sizeDelta = Vector2.zero;
        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.color = new Color(0.1f, 0.7f, 1.0f, 1f);

        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.sizeDelta = Vector2.zero;

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(15, 20);
        Image handleImg = handleGO.GetComponent<Image>();
        handleImg.color = Color.white;

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = current;

        slider.onValueChanged.AddListener((val) => {
            onValueChanged(val, label);
        });

        onValueChanged(current, label);

        return slider;
    }
}
