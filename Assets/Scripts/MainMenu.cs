using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Programmatic Main Menu system.
/// Attach this script to an empty GameObject in a new scene.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public static string selectedGameMode = "Survival";

    private GameObject menuCanvasGO;
    private GameObject mainPanel;
    private GameObject settingsPanel;
    private GameObject helpPanel;
    private GameObject createWorldPanel;
    private GameObject worldSelectionPanel;
    private GameObject confirmationPanel;

    private string chosenMode = "Survival";
    private TextMeshProUGUI modeDescriptionText;
    private TextMeshProUGUI survivalBtnText;
    private TextMeshProUGUI creativeBtnText;
    private TMP_InputField seedInputField;
    private TMP_InputField worldNameInputField;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        CreateMainMenuUI();
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }
    }

    private void CreateMainMenuUI()
    {
        // 1. Create Canvas
        menuCanvasGO = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = menuCanvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = menuCanvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Ensure EventSystem exists in the scene and works with the New Input System
        UnityEngine.EventSystems.EventSystem eventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            eventSystem = esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        else
        {
            // If it exists but has the legacy StandaloneInputModule, replace it to avoid errors
            UnityEngine.EventSystems.StandaloneInputModule legacyModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyModule != null)
            {
                DestroyImmediate(legacyModule);
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        // 2. Background Panel (Rich dark gradient color)
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(menuCanvasGO.transform, false);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.09f, 0.12f, 1f); // Sleek dark midnight theme

        mainPanel = new GameObject("MainPanel", typeof(RectTransform));
        mainPanel.transform.SetParent(menuCanvasGO.transform, false);
        RectTransform mainRT = mainPanel.GetComponent<RectTransform>();
        mainRT.anchorMin = new Vector2(0.5f, 0.5f);
        mainRT.anchorMax = new Vector2(0.5f, 0.5f);
        mainRT.pivot = new Vector2(0.5f, 0.5f);
        mainRT.sizeDelta = new Vector2(1500, 950);
        mainRT.anchoredPosition = Vector2.zero;

        // 4. Title Text
        GameObject titleGO = new GameObject("GameTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(mainPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(1500, 250);
        titleRT.anchoredPosition = new Vector2(0, -50);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "MY UNITY GAME";
        titleText.fontSize = 140;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Add uniform outline to title for readability
        titleText.fontMaterial.EnableKeyword("OUTLINE_ON");
        titleText.fontMaterial.SetColor("_OutlineColor", Color.black);
        titleText.fontMaterial.SetFloat("_OutlineWidth", 0.2f);
        titleText.UpdateMeshPadding();

        // 5. Button Container (Vertical layout for buttons)
        GameObject btnContainer = new GameObject("ButtonContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        btnContainer.transform.SetParent(mainPanel.transform, false);
        RectTransform containerRT = btnContainer.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.35f);
        containerRT.anchorMax = new Vector2(0.5f, 0.35f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(900, 500);
        containerRT.anchoredPosition = new Vector2(0, -80);

        VerticalLayoutGroup vlg = btnContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 45;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        // Create Play, Settings, Help, and Quit buttons
        CreateMenuButton(btnContainer.transform, "PLAY GAME", () => SwitchToPanel("WorldSelection"));
        CreateMenuButton(btnContainer.transform, "SETTINGS", () => SwitchToPanel("Settings"));
        CreateMenuButton(btnContainer.transform, "HELP", () => SwitchToPanel("Help"));
        CreateMenuButton(btnContainer.transform, "QUIT GAME", () => QuitGame());

        // 6. Create Panels
        CreateSettingsPanel();
        CreateHelpPanel();
        CreateWorldSelectionPanel();
        CreateWorldPanelCreation();

        // Initially show only the Main menu panel
        SwitchToPanel("Main");
    }

    private void CreateMenuButton(Transform parent, string label, UnityAction onClickAction)
    {
        GameObject btnGO = new GameObject(label + "_Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonEffects));
        btnGO.transform.SetParent(parent, false);

        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = Vector2.zero;
        btnRT.anchorMax = Vector2.one;
        btnRT.sizeDelta = Vector2.zero;

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        // Text child
        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 60;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Apply clean border to button labels
        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
        tmp.fontMaterial.SetColor("_OutlineColor", Color.black);
        tmp.fontMaterial.SetFloat("_OutlineWidth", 0.2f);
        tmp.UpdateMeshPadding();
    }

    private void CreateSettingsPanel()
    {
        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        settingsPanel.transform.SetParent(menuCanvasGO.transform, false);
        RectTransform setRT = settingsPanel.GetComponent<RectTransform>();
        setRT.anchorMin = new Vector2(0.5f, 0.5f);
        setRT.anchorMax = new Vector2(0.5f, 0.5f);
        setRT.pivot = new Vector2(0.5f, 0.5f);
        setRT.sizeDelta = new Vector2(1500, 950);
        setRT.anchoredPosition = Vector2.zero;

        Image setImg = settingsPanel.GetComponent<Image>();
        setImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f); // Slightly lighter background card

        // Settings Title
        GameObject titleGO = new GameObject("SettingsTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(settingsPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(1200, 200);
        titleRT.anchoredPosition = new Vector2(0, -50);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SETTINGS";
        titleText.fontSize = 90;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Sliders Container
        GameObject sliderContainer = new GameObject("SettingsSliderContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        sliderContainer.transform.SetParent(settingsPanel.transform, false);
        RectTransform scRT = sliderContainer.GetComponent<RectTransform>();
        scRT.anchorMin = new Vector2(0.5f, 0.48f);
        scRT.anchorMax = new Vector2(0.5f, 0.48f);
        scRT.pivot = new Vector2(0.5f, 0.5f);
        scRT.sizeDelta = new Vector2(1200, 550);
        scRT.anchoredPosition = new Vector2(0, -20);

        VerticalLayoutGroup vlg = sliderContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 30;
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
            PlayerPrefs.Save();
        });

        CreateSlider(sliderContainer.transform, "Field of View (FOV)", 50f, 110f, fov, (val, label) => {
            label.text = $"Field of View (FOV): {val:F0}";
            PlayerPrefs.SetFloat("FOV", val);
            PlayerPrefs.Save();
        });

        CreateSlider(sliderContainer.transform, "Master Volume", 0.0f, 1.0f, volume, (val, label) => {
            label.text = $"Master Volume: {val * 100:F0}%";
            PlayerPrefs.SetFloat("MasterVolume", val);
            AudioListener.volume = val;
            PlayerPrefs.Save();
        });

        CreateSlider(sliderContainer.transform, "Render Distance (Chunks)", 3f, 16f, renderDist, (val, label) => {
            int intVal = Mathf.RoundToInt(val);
            label.text = $"Render Distance: {intVal} Chunks";
            PlayerPrefs.SetInt("RenderDistance", intVal);
            PlayerPrefs.Save();
        });

        // Back Button
        GameObject backGO = new GameObject("BackButtonContainer", typeof(RectTransform));
        backGO.transform.SetParent(settingsPanel.transform, false);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0.12f);
        backRT.anchorMax = new Vector2(0.5f, 0.12f);
        backRT.pivot = new Vector2(0.5f, 0.5f);
        backRT.sizeDelta = new Vector2(600, 120);
        backRT.anchoredPosition = Vector2.zero;

        CreateMenuButton(backGO.transform, "BACK", () => SwitchToPanel("Main"));
    }

    private void SwitchToPanel(string panelName)
    {
        if (confirmationPanel != null) Destroy(confirmationPanel);

        // Deactivate all panels first
        if (mainPanel) mainPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (helpPanel) helpPanel.SetActive(false);
        if (worldSelectionPanel) worldSelectionPanel.SetActive(false);
        if (createWorldPanel) createWorldPanel.SetActive(false);

        // Activate the target panel
        if (panelName == "Main")
        {
            if (mainPanel) mainPanel.SetActive(true);
        }
        else if (panelName == "Settings")
        {
            if (settingsPanel) settingsPanel.SetActive(true);
        }
        else if (panelName == "Help")
        {
            if (helpPanel) helpPanel.SetActive(true);
        }
        else if (panelName == "WorldSelection")
        {
            // Recreate world selection panel to refresh save state
            Destroy(worldSelectionPanel);
            CreateWorldSelectionPanel();
            if (worldSelectionPanel) worldSelectionPanel.SetActive(true);
        }
        else if (panelName == "CreateWorld")
        {
            if (createWorldPanel) createWorldPanel.SetActive(true);
        }
    }

    private void CreateHelpPanel()
    {
        helpPanel = new GameObject("HelpPanel", typeof(RectTransform), typeof(Image));
        helpPanel.transform.SetParent(menuCanvasGO.transform, false);
        RectTransform helpRT = helpPanel.GetComponent<RectTransform>();
        helpRT.anchorMin = new Vector2(0.5f, 0.5f);
        helpRT.anchorMax = new Vector2(0.5f, 0.5f);
        helpRT.pivot = new Vector2(0.5f, 0.5f);
        helpRT.sizeDelta = new Vector2(1500, 950);
        helpRT.anchoredPosition = Vector2.zero;

        Image helpImg = helpPanel.GetComponent<Image>();
        helpImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        // Help Title
        GameObject titleGO = new GameObject("HelpTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(helpPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(1200, 200);
        titleRT.anchoredPosition = new Vector2(0, -50);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "HELP / CONTROLS";
        titleText.fontSize = 90;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Info Text containing all the shortcut instructions
        GameObject infoGO = new GameObject("HelpInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
        infoGO.transform.SetParent(helpPanel.transform, false);
        RectTransform infoRT = infoGO.GetComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.5f, 0.5f);
        infoRT.anchorMax = new Vector2(0.5f, 0.5f);
        infoRT.pivot = new Vector2(0.5f, 0.5f);
        infoRT.sizeDelta = new Vector2(1350, 600);
        infoRT.anchoredPosition = new Vector2(0, 20);

        TextMeshProUGUI infoText = infoGO.GetComponent<TextMeshProUGUI>();
        infoText.text = "<b>CONTROLS & SHORTCUTS</b>\n\n" +
                        "<b>Movement:</b> WASD | <b>Look around:</b> Mouse\n" +
                        "<b>Sneak Mode:</b> Left Control | <b>Show Cursor:</b> ` (Backquote)\n" +
                        "<b>Inventory / Crafting:</b> I | <b>Toggle Pause:</b> Escape / P\n" +
                        "<b>Enter Vehicle:</b> E | <b>Return to Boat (Rescue):</b> R\n\n" +
                        "<b>Save Progress:</b> K | <b>Load Progress:</b> L\n" +
                        "<b>Reset Save File:</b> Delete\n" +
                        "<b>Creative Mode (Flight):</b> C\n" +
                        "<b>Fly Up / Down:</b> Space / Left Shift";
        infoText.fontSize = 42;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        // Back Button
        GameObject backGO = new GameObject("BackButtonContainer", typeof(RectTransform));
        backGO.transform.SetParent(helpPanel.transform, false);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0.12f);
        backRT.anchorMax = new Vector2(0.5f, 0.12f);
        backRT.pivot = new Vector2(0.5f, 0.5f);
        backRT.sizeDelta = new Vector2(600, 120);
        backRT.anchoredPosition = Vector2.zero;

        CreateMenuButton(backGO.transform, "BACK", () => SwitchToPanel("Main"));
    }

    private void CreateWorldSelectionPanel()
    {
        worldSelectionPanel = new GameObject("WorldSelectionPanel", typeof(RectTransform), typeof(Image));
        worldSelectionPanel.transform.SetParent(menuCanvasGO.transform, false);
        RectTransform rt = worldSelectionPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1500, 950);
        rt.anchoredPosition = Vector2.zero;

        Image img = worldSelectionPanel.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(worldSelectionPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(1200, 200);
        titleRT.anchoredPosition = new Vector2(0, -50);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SELECT WORLD";
        titleText.fontSize = 90;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // 1. Create ScrollRect GameObject
        GameObject scrollRectGO = new GameObject("WorldScrollRect", typeof(RectTransform), typeof(ScrollRect));
        scrollRectGO.transform.SetParent(worldSelectionPanel.transform, false);
        RectTransform scrollRectRT = scrollRectGO.GetComponent<RectTransform>();
        scrollRectRT.anchorMin = new Vector2(0.5f, 0.50f);
        scrollRectRT.anchorMax = new Vector2(0.5f, 0.50f);
        scrollRectRT.pivot = new Vector2(0.5f, 0.5f);
        scrollRectRT.sizeDelta = new Vector2(1260, 520);
        scrollRectRT.anchoredPosition = new Vector2(0, -20);

        ScrollRect scrollRect = scrollRectGO.GetComponent<ScrollRect>();
        scrollRect.horizontal = false; // vertical scrolling only
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 75f;

        // 2. Create Viewport GameObject (clips the child items)
        GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGO.transform.SetParent(scrollRectGO.transform, false);
        RectTransform viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = new Vector2(-60, 0); // Leave room on the right for scrollbar

        // 3. Create Content GameObject (holds the items and expands/contracts)
        GameObject listContainer = new GameObject("WorldListContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listContainer.transform.SetParent(viewportGO.transform, false);
        RectTransform listRT = listContainer.GetComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0f, 1f); // Anchor top
        listRT.anchorMax = new Vector2(1f, 1f);
        listRT.pivot = new Vector2(0.5f, 1f);   // Pivot top-middle
        listRT.sizeDelta = new Vector2(0, 0);   // Height driven by ContentSizeFitter

        VerticalLayoutGroup vlg = listContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 30;
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = listContainer.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 4. Create Vertical Scrollbar GameObject
        GameObject scrollbarGO = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(scrollRectGO.transform, false);
        RectTransform scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f); // Anchor to the right edge of ScrollRect
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(36, 0); // 36 units wide
        scrollbarRT.anchoredPosition = Vector2.zero;

        Image scrollbarImg = scrollbarGO.GetComponent<Image>();
        scrollbarImg.color = new Color(0.12f, 0.14f, 0.18f, 0.5f); // track color

        Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Sliding Area
        GameObject slideAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
        slideAreaGO.transform.SetParent(scrollbarGO.transform, false);
        RectTransform slideAreaRT = slideAreaGO.GetComponent<RectTransform>();
        slideAreaRT.anchorMin = Vector2.zero;
        slideAreaRT.anchorMax = Vector2.one;
        slideAreaRT.sizeDelta = Vector2.zero;

        // Handle
        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(slideAreaGO.transform, false);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = Vector2.zero;

        Image handleImg = handleGO.GetComponent<Image>();
        handleImg.color = new Color(0.3f, 0.35f, 0.45f, 0.9f); // Handle grabber color

        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImg;

        // Connect scrollRect
        scrollRect.viewport = viewportRT;
        scrollRect.content = listRT;
        scrollRect.verticalScrollbar = scrollbar;

        // Populate list
        List<int> slots = new List<int>();
        if (SaveLoadManager.Instance != null)
        {
            slots = SaveLoadManager.Instance.GetExistingSlots();
        }

        if (slots.Count > 0)
        {
            foreach (int slot in slots)
            {
                int currentSlot = slot;
                string mode = SaveLoadManager.Instance.GetSavedGameMode(currentSlot);
                string worldName = PlayerPrefs.GetString("WorldName_" + currentSlot, "World " + currentSlot);

                GameObject worldItem = new GameObject("WorldItem_" + currentSlot, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                worldItem.transform.SetParent(listContainer.transform, false);
                var itemRT = worldItem.GetComponent<RectTransform>();
                itemRT.sizeDelta = new Vector2(1140, 120);

                HorizontalLayoutGroup hlgItem = worldItem.GetComponent<HorizontalLayoutGroup>();
                hlgItem.spacing = 30;
                hlgItem.childControlWidth = false;
                hlgItem.childControlHeight = true;
                hlgItem.childForceExpandWidth = false;
                hlgItem.childForceExpandHeight = true;

                // Play Button (Left, width 870)
                GameObject playBtnGO = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
                playBtnGO.transform.SetParent(worldItem.transform, false);
                var playRT = playBtnGO.GetComponent<RectTransform>();
                playRT.sizeDelta = new Vector2(870, 120);

                var playImg = playBtnGO.GetComponent<Image>();
                playImg.color = new Color(0.18f, 0.20f, 0.25f, 0.9f);
                var playBtn = playBtnGO.GetComponent<Button>();
                playBtn.onClick.AddListener(() => {
                    SaveLoadManager.activeWorldSlot = currentSlot;
                    LoadGameScene();
                });

                GameObject playTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                playTxtGO.transform.SetParent(playBtnGO.transform, false);
                var playTxtRT = playTxtGO.GetComponent<RectTransform>();
                playTxtRT.anchorMin = Vector2.zero;
                playTxtRT.anchorMax = Vector2.one;
                playTxtRT.sizeDelta = Vector2.zero;
                var playTmp = playTxtGO.GetComponent<TextMeshProUGUI>();
                playTmp.text = $"🌍 {worldName} ({mode})";
                playTmp.fontSize = 42;
                playTmp.fontStyle = FontStyles.Bold;
                playTmp.alignment = TextAlignmentOptions.Center;
                playTmp.color = Color.white;

                // Delete Button (Right, width 240)
                GameObject delBtnGO = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
                delBtnGO.transform.SetParent(worldItem.transform, false);
                var delRT = delBtnGO.GetComponent<RectTransform>();
                delRT.sizeDelta = new Vector2(240, 120);

                var delImg = delBtnGO.GetComponent<Image>();
                delImg.color = new Color(0.75f, 0.15f, 0.15f, 0.9f);
                var delBtn = delBtnGO.GetComponent<Button>();
                delBtn.onClick.AddListener(() => {
                    ShowDeleteConfirmation(currentSlot, () => {
                        if (SaveLoadManager.Instance != null)
                        {
                            SaveLoadManager.Instance.DeleteSave(currentSlot);
                            PlayerPrefs.DeleteKey("WorldName_" + currentSlot);
                            PlayerPrefs.DeleteKey("GameMode_" + currentSlot);
                            PlayerPrefs.Save();
                            SwitchToPanel("WorldSelection");
                        }
                    });
                });

                GameObject delTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                delTxtGO.transform.SetParent(delBtnGO.transform, false);
                var delTxtRT = delTxtGO.GetComponent<RectTransform>();
                delTxtRT.anchorMin = Vector2.zero;
                delTxtRT.anchorMax = Vector2.one;
                delTxtRT.sizeDelta = Vector2.zero;
                var delTmp = delTxtGO.GetComponent<TextMeshProUGUI>();
                delTmp.text = "🗑️";
                delTmp.fontSize = 48;
                delTmp.alignment = TextAlignmentOptions.Center;
                delTmp.color = Color.white;
            }
        }
        else
        {
            GameObject emptyTxtGO = new GameObject("EmptyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyTxtGO.transform.SetParent(listContainer.transform, false);
            var emptyRT = emptyTxtGO.GetComponent<RectTransform>();
            emptyRT.sizeDelta = new Vector2(1140, 300);

            var tmp = emptyTxtGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "No worlds found.\nCreate a new world to start your adventure!";
            tmp.fontSize = 42;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.7f, 0.7f, 0.7f);
        }

        // Bottom Action Container for WorldSelectionPanel (Horizontal Layout)
        GameObject selectActionsGO = new GameObject("SelectActionsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        selectActionsGO.transform.SetParent(worldSelectionPanel.transform, false);
        RectTransform actionsRT = selectActionsGO.GetComponent<RectTransform>();
        actionsRT.anchorMin = new Vector2(0.5f, 0.12f);
        actionsRT.anchorMax = new Vector2(0.5f, 0.12f);
        actionsRT.pivot = new Vector2(0.5f, 0.5f);
        actionsRT.sizeDelta = new Vector2(1350, 120);
        actionsRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup selectHlg = selectActionsGO.GetComponent<HorizontalLayoutGroup>();
        selectHlg.spacing = 60;
        selectHlg.childAlignment = TextAnchor.MiddleCenter;
        selectHlg.childControlWidth = true;
        selectHlg.childControlHeight = true;
        selectHlg.childForceExpandWidth = true;
        selectHlg.childForceExpandHeight = true;

        // CREATE NEW WORLD button
        GameObject createBtnGO = new GameObject("CreateWorldButtonContainer", typeof(RectTransform));
        createBtnGO.transform.SetParent(selectActionsGO.transform, false);
        CreateMenuButton(createBtnGO.transform, "CREATE NEW WORLD", () => SwitchToPanel("CreateWorld"));

        // BACK Button
        GameObject backGO = new GameObject("BackButtonContainer", typeof(RectTransform));
        backGO.transform.SetParent(selectActionsGO.transform, false);
        CreateMenuButton(backGO.transform, "BACK", () => SwitchToPanel("Main"));
    }

    private void CreateWorldPanelCreation()
    {
        createWorldPanel = new GameObject("CreateWorldPanel", typeof(RectTransform), typeof(Image));
        createWorldPanel.transform.SetParent(menuCanvasGO.transform, false);
        RectTransform rt = createWorldPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1500, 950);
        rt.anchoredPosition = Vector2.zero;

        Image img = createWorldPanel.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(createWorldPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.90f);
        titleRT.anchorMax = new Vector2(0.5f, 0.90f);
        titleRT.pivot = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = new Vector2(1200, 100);
        titleRT.anchoredPosition = new Vector2(0, -30);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "CREATE NEW WORLD";
        titleText.fontSize = 80;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Mode Container (Horizontal)
        GameObject modeContainer = new GameObject("ModeContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        modeContainer.transform.SetParent(createWorldPanel.transform, false);
        RectTransform modeRT = modeContainer.GetComponent<RectTransform>();
        modeRT.anchorMin = new Vector2(0.5f, 0.77f);
        modeRT.anchorMax = new Vector2(0.5f, 0.77f);
        modeRT.pivot = new Vector2(0.5f, 0.5f);
        modeRT.sizeDelta = new Vector2(1200, 110);
        modeRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup hlg = modeContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 60;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // Survival Button
        GameObject survBtn = new GameObject("SurvivalButton", typeof(RectTransform), typeof(Image), typeof(Button));
        survBtn.transform.SetParent(modeContainer.transform, false);
        var survImg = survBtn.GetComponent<Image>();
        survImg.color = new Color(0.20f, 0.20f, 0.25f, 0.9f);
        var survB = survBtn.GetComponent<Button>();
        survB.onClick.AddListener(() => SelectMode("Survival"));

        GameObject survTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        survTxtGO.transform.SetParent(survBtn.transform, false);
        var survTxtRT = survTxtGO.GetComponent<RectTransform>();
        survTxtRT.anchorMin = Vector2.zero;
        survTxtRT.anchorMax = Vector2.one;
        survTxtRT.sizeDelta = Vector2.zero;
        survivalBtnText = survTxtGO.GetComponent<TextMeshProUGUI>();
        survivalBtnText.text = "SURVIVAL";
        survivalBtnText.fontSize = 48;
        survivalBtnText.alignment = TextAlignmentOptions.Center;
        survivalBtnText.color = Color.white;

        // Creative Button
        GameObject creatBtn = new GameObject("CreativeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        creatBtn.transform.SetParent(modeContainer.transform, false);
        var creatImg = creatBtn.GetComponent<Image>();
        creatImg.color = new Color(0.20f, 0.20f, 0.25f, 0.9f);
        var creatB = creatBtn.GetComponent<Button>();
        creatB.onClick.AddListener(() => SelectMode("Creative"));

        GameObject creatTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        creatTxtGO.transform.SetParent(creatBtn.transform, false);
        var creatTxtRT = creatTxtGO.GetComponent<RectTransform>();
        creatTxtRT.anchorMin = Vector2.zero;
        creatTxtRT.anchorMax = Vector2.one;
        creatTxtRT.sizeDelta = Vector2.zero;
        creativeBtnText = creatTxtGO.GetComponent<TextMeshProUGUI>();
        creativeBtnText.text = "CREATIVE";
        creativeBtnText.fontSize = 48;
        creativeBtnText.alignment = TextAlignmentOptions.Center;
        creativeBtnText.color = Color.white;

        // World Name Container (Horizontal)
        GameObject nameContainer = new GameObject("NameContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        nameContainer.transform.SetParent(createWorldPanel.transform, false);
        RectTransform nameRT = nameContainer.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.5f, 0.61f);
        nameRT.anchorMax = new Vector2(0.5f, 0.61f);
        nameRT.pivot = new Vector2(0.5f, 0.5f);
        nameRT.sizeDelta = new Vector2(1200, 110);
        nameRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup nameHlg = nameContainer.GetComponent<HorizontalLayoutGroup>();
        nameHlg.spacing = 45;
        nameHlg.childAlignment = TextAnchor.MiddleCenter;
        nameHlg.childControlWidth = false;
        nameHlg.childControlHeight = true;
        nameHlg.childForceExpandWidth = false;
        nameHlg.childForceExpandHeight = true;

        // Label
        GameObject nameLabelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameLabelGO.transform.SetParent(nameContainer.transform, false);
        var nameLabelRT = nameLabelGO.GetComponent<RectTransform>();
        nameLabelRT.sizeDelta = new Vector2(300, 110);
        var nameLabelTxt = nameLabelGO.GetComponent<TextMeshProUGUI>();
        nameLabelTxt.text = "World Name:";
        nameLabelTxt.fontSize = 36;
        nameLabelTxt.fontStyle = FontStyles.Bold;
        nameLabelTxt.alignment = TextAlignmentOptions.Left;
        nameLabelTxt.color = Color.white;

        // Input Field
        GameObject nameInputGO;
        worldNameInputField = CreateInputField(nameContainer.transform, "My Awesome World", out nameInputGO);
        var nameInputRT = nameInputGO.GetComponent<RectTransform>();
        nameInputRT.sizeDelta = new Vector2(855, 110);

        // Seed Container (Horizontal)
        GameObject seedContainer = new GameObject("SeedContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        seedContainer.transform.SetParent(createWorldPanel.transform, false);
        RectTransform seedRT = seedContainer.GetComponent<RectTransform>();
        seedRT.anchorMin = new Vector2(0.5f, 0.45f);
        seedRT.anchorMax = new Vector2(0.5f, 0.45f);
        seedRT.pivot = new Vector2(0.5f, 0.5f);
        seedRT.sizeDelta = new Vector2(1200, 110);
        seedRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup seedHlg = seedContainer.GetComponent<HorizontalLayoutGroup>();
        seedHlg.spacing = 45;
        seedHlg.childAlignment = TextAnchor.MiddleCenter;
        seedHlg.childControlWidth = false;
        seedHlg.childControlHeight = true;
        seedHlg.childForceExpandWidth = false;
        seedHlg.childForceExpandHeight = true;

        // Label
        GameObject seedLabelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        seedLabelGO.transform.SetParent(seedContainer.transform, false);
        var labelRT = seedLabelGO.GetComponent<RectTransform>();
        labelRT.sizeDelta = new Vector2(300, 110);
        var labelTxt = seedLabelGO.GetComponent<TextMeshProUGUI>();
        labelTxt.text = "World Seed:";
        labelTxt.fontSize = 36;
        labelTxt.fontStyle = FontStyles.Bold;
        labelTxt.alignment = TextAlignmentOptions.Left;
        labelTxt.color = Color.white;

        // Input Field
        GameObject seedInputGO;
        seedInputField = CreateInputField(seedContainer.transform, "Random Seed...", out seedInputGO);
        var inputRT = seedInputGO.GetComponent<RectTransform>();
        inputRT.sizeDelta = new Vector2(855, 110);

        // Description
        GameObject descGO = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGO.transform.SetParent(createWorldPanel.transform, false);
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0.5f, 0.27f);
        descRT.anchorMax = new Vector2(0.5f, 0.27f);
        descRT.pivot = new Vector2(0.5f, 0.5f);
        descRT.sizeDelta = new Vector2(1200, 180);
        descRT.anchoredPosition = Vector2.zero;

        modeDescriptionText = descGO.GetComponent<TextMeshProUGUI>();
        modeDescriptionText.fontSize = 26;
        modeDescriptionText.alignment = TextAlignmentOptions.Center;
        modeDescriptionText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        // Action Container
        GameObject actionContainer = new GameObject("ActionContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        actionContainer.transform.SetParent(createWorldPanel.transform, false);
        RectTransform actionRT = actionContainer.GetComponent<RectTransform>();
        actionRT.anchorMin = new Vector2(0.5f, 0.10f);
        actionRT.anchorMax = new Vector2(0.5f, 0.10f);
        actionRT.pivot = new Vector2(0.5f, 0.5f);
        actionRT.sizeDelta = new Vector2(1200, 110);
        actionRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup alg = actionContainer.GetComponent<HorizontalLayoutGroup>();
        alg.spacing = 60;
        alg.childAlignment = TextAnchor.MiddleCenter;
        alg.childControlWidth = true;
        alg.childControlHeight = true;
        alg.childForceExpandWidth = true;
        alg.childForceExpandHeight = true;

        CreateMenuButton(actionContainer.transform, "CREATE & START", () => StartNewWorld());
        CreateMenuButton(actionContainer.transform, "CANCEL", () => SwitchToPanel("WorldSelection"));

        SelectMode("Survival");
    }

    private void SelectMode(string mode)
    {
        chosenMode = mode;
        if (mode == "Survival")
        {
            survivalBtnText.text = "<b>▶ SURVIVAL ◀</b>";
            survivalBtnText.color = new Color(0.2f, 0.8f, 0.3f);
            creativeBtnText.text = "CREATIVE";
            creativeBtnText.color = Color.white;

            modeDescriptionText.text = "<b>Survival Mode</b>\n\nGather resources, craft tools, build voxel vehicles, and explore the open world.\n\n⚠️ <i>This will clear any existing saved progress.</i>";
        }
        else
        {
            creativeBtnText.text = "<b>▶ CREATIVE ◀</b>";
            creativeBtnText.color = new Color(0.1f, 0.7f, 1.0f);
            survivalBtnText.text = "SURVIVAL";
            survivalBtnText.color = Color.white;

            modeDescriptionText.text = "<b>Creative Mode</b>\n\nEnjoy infinite flight mode (double flight speed), no gravity, and free building tools.\n\n⚠️ <i>This will clear any existing saved progress.</i>";
        }
    }

    private void StartNewWorld()
    {
        selectedGameMode = chosenMode;
        
        if (SaveLoadManager.Instance != null)
        {
            var slots = SaveLoadManager.Instance.GetExistingSlots();
            int newSlot = 1;
            if (slots.Count > 0)
            {
                int max = 0;
                foreach (int s in slots)
                {
                    if (s > max) max = s;
                }
                newSlot = max + 1;
            }

            SaveLoadManager.activeWorldSlot = newSlot;
            PlayerPrefs.SetString("GameMode_" + newSlot, chosenMode);
            
            string worldName = $"World {newSlot}";
            if (worldNameInputField != null && !string.IsNullOrEmpty(worldNameInputField.text))
            {
                worldName = worldNameInputField.text;
            }
            PlayerPrefs.SetString("WorldName_" + newSlot, worldName);
            PlayerPrefs.Save();

            // Parse Seed Input
            int seed = 0;
            if (seedInputField != null && !string.IsNullOrEmpty(seedInputField.text))
            {
                if (!int.TryParse(seedInputField.text, out seed))
                {
                    seed = seedInputField.text.GetHashCode();
                }
            }
            else
            {
                seed = Random.Range(1, 1000000);
            }

            SaveLoadManager.activeWorldSeed = seed;
            SaveLoadManager.Instance.UpdateSeedOffsets();

            SaveLoadManager.Instance.PrepareNewWorld();
        }

        LoadGameScene();
    }

    private void LoadGameScene()
    {
        StartCoroutine(LoadGameSceneRoutine());
    }

    private System.Collections.IEnumerator LoadGameSceneRoutine()
    {
        if (menuCanvasGO != null)
        {
            GameObject overlay = new GameObject("LoadOverlay", typeof(RectTransform), typeof(CanvasGroup));
            overlay.transform.SetParent(menuCanvasGO.transform, false);
            var rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var bg = overlay.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 1.0f);

            GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(overlay.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0.5f, 0.5f);
            txtRT.anchorMax = new Vector2(0.5f, 0.5f);
            txtRT.pivot = new Vector2(0.5f, 0.5f);
            txtRT.sizeDelta = new Vector2(1500f, 300f);
            txtRT.anchoredPosition = Vector2.zero;

            var tmp = txtGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "🔄 LOADING WORLD...";
            tmp.fontSize = 96f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        yield return null;
        yield return null;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(1);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    private void ShowDeleteConfirmation(int slot, System.Action onConfirm)
    {
        if (confirmationPanel != null)
        {
            Destroy(confirmationPanel);
        }

        confirmationPanel = new GameObject("DeleteConfirmationPanel", typeof(RectTransform), typeof(Image));
        confirmationPanel.transform.SetParent(menuCanvasGO.transform, false);
        RectTransform rt = confirmationPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1200, 750);
        rt.anchoredPosition = Vector2.zero;

        Image img = confirmationPanel.GetComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.10f, 0.96f);

        // Add outline
        Outline outline = confirmationPanel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.2f);
        outline.effectDistance = new Vector2(3f, -3f);

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(confirmationPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(1080, 240);
        titleRT.anchoredPosition = new Vector2(0, -60);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = $"DELETE WORLD {slot}?";
        titleText.fontSize = 72;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.2f, 0.2f);

        // Subtitle
        GameObject subGO = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGO.transform.SetParent(confirmationPanel.transform, false);
        RectTransform subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 0.5f);
        subRT.anchorMax = new Vector2(0.5f, 0.5f);
        subRT.pivot = new Vector2(0.5f, 0.5f);
        subRT.sizeDelta = new Vector2(1080, 180);
        subRT.anchoredPosition = new Vector2(0, 30);

        TextMeshProUGUI subText = subGO.GetComponent<TextMeshProUGUI>();
        subText.text = "This action is permanent and cannot be undone.";
        subText.fontSize = 42;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(0.8f, 0.8f, 0.8f);

        // Buttons Container
        GameObject btnContainer = new GameObject("ButtonContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnContainer.transform.SetParent(confirmationPanel.transform, false);
        RectTransform containerRT = btnContainer.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.2f);
        containerRT.anchorMax = new Vector2(0.5f, 0.2f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(960, 150);
        containerRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup hlg = btnContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 60;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // Yes Button
        GameObject yesBtn = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonEffects));
        yesBtn.transform.SetParent(btnContainer.transform, false);
        yesBtn.GetComponent<Image>().color = new Color(0.75f, 0.15f, 0.15f, 0.9f);
        yesBtn.GetComponent<Button>().onClick.AddListener(() => {
            onConfirm?.Invoke();
            Destroy(confirmationPanel);
        });
        GameObject yesTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        yesTxt.transform.SetParent(yesBtn.transform, false);
        RectTransform yesTxtRT = yesTxt.GetComponent<RectTransform>();
        yesTxtRT.anchorMin = Vector2.zero; yesTxtRT.anchorMax = Vector2.one; yesTxtRT.sizeDelta = Vector2.zero;
        var yTmp = yesTxt.GetComponent<TextMeshProUGUI>();
        yTmp.text = "DELETE"; yTmp.fontSize = 48; yTmp.fontStyle = FontStyles.Bold; yTmp.alignment = TextAlignmentOptions.Center; yTmp.color = Color.white;

        // No Button
        GameObject noBtn = new GameObject("CancelBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonEffects));
        noBtn.transform.SetParent(btnContainer.transform, false);
        noBtn.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.25f, 0.9f);
        noBtn.GetComponent<Button>().onClick.AddListener(() => {
            Destroy(confirmationPanel);
        });
        GameObject noTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        noTxt.transform.SetParent(noBtn.transform, false);
        RectTransform noTxtRT = noTxt.GetComponent<RectTransform>();
        noTxtRT.anchorMin = Vector2.zero; noTxtRT.anchorMax = Vector2.one; noTxtRT.sizeDelta = Vector2.zero;
        var nTmp = noTxt.GetComponent<TextMeshProUGUI>();
        nTmp.text = "CANCEL"; nTmp.fontSize = 48; nTmp.fontStyle = FontStyles.Bold; nTmp.alignment = TextAlignmentOptions.Center; nTmp.color = Color.white;
    }

    private TMP_InputField CreateInputField(Transform parent, string placeholderText, out GameObject inputFieldGO)
    {
        inputFieldGO = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputFieldGO.transform.SetParent(parent, false);
        
        var img = inputFieldGO.GetComponent<Image>();
        img.color = new Color(0.18f, 0.20f, 0.25f, 0.9f);
        
        var outline = inputFieldGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
        outline.effectDistance = new Vector2(3f, -3f);

        var inputField = inputFieldGO.GetComponent<TMP_InputField>();

        GameObject textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(inputFieldGO.transform, false);
        var taRT = textArea.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero;
        taRT.anchorMax = Vector2.one;
        taRT.sizeDelta = new Vector2(-60, -30);

        GameObject placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderGO.transform.SetParent(textArea.transform, false);
        var placeholderRT = placeholderGO.GetComponent<RectTransform>();
        placeholderRT.anchorMin = Vector2.zero;
        placeholderRT.anchorMax = Vector2.one;
        placeholderRT.sizeDelta = Vector2.zero;
        
        var placeholderTmp = placeholderGO.GetComponent<TextMeshProUGUI>();
        placeholderTmp.text = placeholderText;
        placeholderTmp.fontSize = 36;
        placeholderTmp.fontStyle = FontStyles.Italic;
        placeholderTmp.alignment = TextAlignmentOptions.Left;
        placeholderTmp.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(textArea.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        
        var textTmp = textGO.GetComponent<TextMeshProUGUI>();
        textTmp.fontSize = 36;
        textTmp.alignment = TextAlignmentOptions.Left;
        textTmp.color = Color.white;

        inputField.textViewport = taRT;
        inputField.textComponent = textTmp;
        inputField.placeholder = placeholderTmp;

        return inputField;
    }

    private Slider CreateSlider(Transform parent, string labelText, float min, float max, float current, System.Action<float, TextMeshProUGUI> onValueChanged)
    {
        GameObject container = new GameObject(labelText + "_SliderContainer", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        RectTransform containerRT = container.GetComponent<RectTransform>();
        containerRT.sizeDelta = new Vector2(1200, 180);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(container.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.5f);
        labelRT.anchorMax = new Vector2(1f, 0.5f);
        labelRT.pivot = new Vector2(0f, 0.5f);
        labelRT.anchoredPosition = new Vector2(0, 45);
        labelRT.sizeDelta = new Vector2(0, 90);
        
        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.fontSize = 48;
        label.color = Color.white;
        label.text = labelText;

        GameObject sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(container.transform, false);
        RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(1f, 0f);
        sliderRT.pivot = new Vector2(0.5f, 0f);
        sliderRT.anchoredPosition = new Vector2(0, 15);
        sliderRT.sizeDelta = new Vector2(0, 60);

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
        handleRT.sizeDelta = new Vector2(45, 60);
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

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

/// <summary>
/// Handles premium button micro-animations.
/// </summary>
public class MenuButtonEffects : UnityEngine.EventSystems.EventTrigger
{
    private Vector3 originalScale;
    private Image buttonImage;
    private Color normalColor = new Color(0.18f, 0.20f, 0.25f, 0.9f);
    private Color hoverColor = new Color(0.24f, 0.28f, 0.35f, 1f);
    private Color activeColor = new Color(0.12f, 0.14f, 0.18f, 1f);

    void Start()
    {
        originalScale = transform.localScale;
        buttonImage = GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = normalColor;
            // Add a thin border look around the buttons
            Outline outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }

    public override void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale * 1.05f, hoverColor));
    }

    public override void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale, normalColor));
    }

    public override void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale * 0.95f, activeColor));
    }

    public override void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale * 1.05f, hoverColor));
    }

    private System.Collections.IEnumerator AnimateScale(Vector3 targetScale, Color targetColor)
    {
        float elapsed = 0f;
        float duration = 0.08f;
        Vector3 startScale = transform.localScale;
        Color startColor = buttonImage != null ? buttonImage.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            if (buttonImage != null) buttonImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        transform.localScale = targetScale;
        if (buttonImage != null) buttonImage.color = targetColor;
    }
}
