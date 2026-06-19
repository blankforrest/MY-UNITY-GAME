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

    private string chosenMode = "Survival";
    private TextMeshProUGUI modeDescriptionText;
    private TextMeshProUGUI survivalBtnText;
    private TextMeshProUGUI creativeBtnText;

    void Start()
    {
        CreateMainMenuUI();
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
        mainRT.sizeDelta = new Vector2(500, 600);
        mainRT.anchoredPosition = Vector2.zero;

        // 4. Title Text
        GameObject titleGO = new GameObject("GameTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(mainPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(600, 150);
        titleRT.anchoredPosition = new Vector2(0, 0);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "MY UNITY GAME";
        titleText.fontSize = 56;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Add uniform outline to title for readability
        titleText.fontMaterial.EnableKeyword("OUTLINE_ON");
        titleText.fontMaterial.SetColor("_OutlineColor", Color.black);
        titleText.fontMaterial.SetFloat("_OutlineWidth", 0.25f);
        titleText.UpdateMeshPadding();

        // 5. Button Container (Vertical layout for buttons)
        GameObject btnContainer = new GameObject("ButtonContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        btnContainer.transform.SetParent(mainPanel.transform, false);
        RectTransform containerRT = btnContainer.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.35f);
        containerRT.anchorMax = new Vector2(0.5f, 0.35f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(300, 310);
        containerRT.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = btnContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
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
        tmp.fontSize = 20;
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
        setRT.sizeDelta = new Vector2(500, 600);
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
        titleRT.sizeDelta = new Vector2(400, 80);
        titleRT.anchoredPosition = new Vector2(0, -20);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SETTINGS";
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Info Text placeholder
        GameObject infoGO = new GameObject("SettingsInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
        infoGO.transform.SetParent(settingsPanel.transform, false);
        RectTransform infoRT = infoGO.GetComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.5f, 0.5f);
        infoRT.anchorMax = new Vector2(0.5f, 0.5f);
        infoRT.pivot = new Vector2(0.5f, 0.5f);
        infoRT.sizeDelta = new Vector2(450, 350);
        infoRT.anchoredPosition = new Vector2(0, 10);

        TextMeshProUGUI infoText = infoGO.GetComponent<TextMeshProUGUI>();
        infoText.text = "<b>SETTINGS</b>\n\nSettings controls (graphics, audio, and gameplay options) will be configurable here.";
        infoText.fontSize = 18;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        // Back Button
        GameObject backGO = new GameObject("BackButtonContainer", typeof(RectTransform));
        backGO.transform.SetParent(settingsPanel.transform, false);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0.15f);
        backRT.anchorMax = new Vector2(0.5f, 0.15f);
        backRT.pivot = new Vector2(0.5f, 0.5f);
        backRT.sizeDelta = new Vector2(200, 50);
        backRT.anchoredPosition = Vector2.zero;

        CreateMenuButton(backGO.transform, "BACK", () => SwitchToPanel("Main"));
    }

    private void SwitchToPanel(string panelName)
    {
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
        helpRT.sizeDelta = new Vector2(500, 600);
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
        titleRT.sizeDelta = new Vector2(400, 80);
        titleRT.anchoredPosition = new Vector2(0, -20);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "HELP / CONTROLS";
        titleText.fontSize = 36;
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
        infoRT.sizeDelta = new Vector2(450, 350);
        infoRT.anchoredPosition = new Vector2(0, 10);

        TextMeshProUGUI infoText = infoGO.GetComponent<TextMeshProUGUI>();
        infoText.text = "<b>CONTROLS & SHORTCUTS</b>\n\n" +
                        "<b>Movement:</b> WASD | <b>Look around:</b> Mouse\n" +
                        "<b>Sneak Mode:</b> Left Control (Toggle)\n" +
                        "<b>Inventory / Crafting:</b> I | <b>Enter Vehicle:</b> E\n" +
                        "<b>Return to Boat (Rescue):</b> R\n\n" +
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
        backRT.anchorMin = new Vector2(0.5f, 0.15f);
        backRT.anchorMax = new Vector2(0.5f, 0.15f);
        backRT.pivot = new Vector2(0.5f, 0.5f);
        backRT.sizeDelta = new Vector2(200, 50);
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
        rt.sizeDelta = new Vector2(500, 600);
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
        titleRT.sizeDelta = new Vector2(400, 80);
        titleRT.anchoredPosition = new Vector2(0, -20);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SELECT WORLD";
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // World List Container
        GameObject listContainer = new GameObject("WorldListContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listContainer.transform.SetParent(worldSelectionPanel.transform, false);
        RectTransform listRT = listContainer.GetComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0.5f, 0.6f);
        listRT.anchorMax = new Vector2(0.5f, 0.6f);
        listRT.pivot = new Vector2(0.5f, 0.5f);
        listRT.sizeDelta = new Vector2(400, 200);
        listRT.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = listContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

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

                GameObject worldItem = new GameObject("WorldItem_" + currentSlot, typeof(RectTransform), typeof(Image), typeof(Button));
                worldItem.transform.SetParent(listContainer.transform, false);
                var itemRT = worldItem.GetComponent<RectTransform>();
                itemRT.sizeDelta = new Vector2(380, 50);

                var itemImg = worldItem.GetComponent<Image>();
                itemImg.color = new Color(0.18f, 0.20f, 0.25f, 0.9f);
                
                var btn = worldItem.GetComponent<Button>();
                btn.onClick.AddListener(() => {
                    SaveLoadManager.activeWorldSlot = currentSlot;
                    LoadGameScene();
                });

                GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGO.transform.SetParent(worldItem.transform, false);
                var txtRT = txtGO.GetComponent<RectTransform>();
                txtRT.anchorMin = Vector2.zero;
                txtRT.anchorMax = Vector2.one;
                txtRT.sizeDelta = Vector2.zero;

                var tmp = txtGO.GetComponent<TextMeshProUGUI>();
                tmp.text = $"🌍 World {currentSlot} ({mode})";
                tmp.fontSize = 16;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
        }
        else
        {
            GameObject emptyTxtGO = new GameObject("EmptyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyTxtGO.transform.SetParent(listContainer.transform, false);
            var emptyRT = emptyTxtGO.GetComponent<RectTransform>();
            emptyRT.sizeDelta = new Vector2(380, 100);

            var tmp = emptyTxtGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "No worlds found.\nCreate a new world to start your adventure!";
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.7f, 0.7f, 0.7f);
        }

        // CREATE NEW WORLD button in the middle lower part of the screen
        GameObject createBtnGO = new GameObject("CreateWorldButtonContainer", typeof(RectTransform));
        createBtnGO.transform.SetParent(worldSelectionPanel.transform, false);
        RectTransform createRT = createBtnGO.GetComponent<RectTransform>();
        createRT.anchorMin = new Vector2(0.5f, 0.28f);
        createRT.anchorMax = new Vector2(0.5f, 0.28f);
        createRT.pivot = new Vector2(0.5f, 0.5f);
        createRT.sizeDelta = new Vector2(300, 50);
        createRT.anchoredPosition = Vector2.zero;

        CreateMenuButton(createBtnGO.transform, "CREATE NEW WORLD", () => SwitchToPanel("CreateWorld"));

        // BACK Button at the bottom
        GameObject backGO = new GameObject("BackButtonContainer", typeof(RectTransform));
        backGO.transform.SetParent(worldSelectionPanel.transform, false);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0.14f);
        backRT.anchorMax = new Vector2(0.5f, 0.14f);
        backRT.pivot = new Vector2(0.5f, 0.5f);
        backRT.sizeDelta = new Vector2(200, 50);
        backRT.anchoredPosition = Vector2.zero;

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
        rt.sizeDelta = new Vector2(500, 600);
        rt.anchoredPosition = Vector2.zero;

        Image img = createWorldPanel.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(createWorldPanel.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(400, 80);
        titleRT.anchoredPosition = new Vector2(0, -20);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "CREATE NEW WORLD";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Mode Container (Horizontal)
        GameObject modeContainer = new GameObject("ModeContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        modeContainer.transform.SetParent(createWorldPanel.transform, false);
        RectTransform modeRT = modeContainer.GetComponent<RectTransform>();
        modeRT.anchorMin = new Vector2(0.5f, 0.65f);
        modeRT.anchorMax = new Vector2(0.5f, 0.65f);
        modeRT.pivot = new Vector2(0.5f, 0.5f);
        modeRT.sizeDelta = new Vector2(400, 60);
        modeRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup hlg = modeContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
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
        survivalBtnText.fontSize = 16;
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
        creativeBtnText.fontSize = 16;
        creativeBtnText.alignment = TextAlignmentOptions.Center;
        creativeBtnText.color = Color.white;

        // Description
        GameObject descGO = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGO.transform.SetParent(createWorldPanel.transform, false);
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0.5f, 0.4f);
        descRT.anchorMax = new Vector2(0.5f, 0.4f);
        descRT.pivot = new Vector2(0.5f, 0.5f);
        descRT.sizeDelta = new Vector2(400, 150);
        descRT.anchoredPosition = Vector2.zero;

        modeDescriptionText = descGO.GetComponent<TextMeshProUGUI>();
        modeDescriptionText.fontSize = 16;
        modeDescriptionText.alignment = TextAlignmentOptions.Center;
        modeDescriptionText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        // Action Container
        GameObject actionContainer = new GameObject("ActionContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        actionContainer.transform.SetParent(createWorldPanel.transform, false);
        RectTransform actionRT = actionContainer.GetComponent<RectTransform>();
        actionRT.anchorMin = new Vector2(0.5f, 0.15f);
        actionRT.anchorMax = new Vector2(0.5f, 0.15f);
        actionRT.pivot = new Vector2(0.5f, 0.5f);
        actionRT.sizeDelta = new Vector2(400, 50);
        actionRT.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup alg = actionContainer.GetComponent<HorizontalLayoutGroup>();
        alg.spacing = 20;
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
            txtRT.sizeDelta = new Vector2(500f, 100f);
            txtRT.anchoredPosition = Vector2.zero;

            var tmp = txtGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "🔄 LOADING WORLD...";
            tmp.fontSize = 32f;
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
