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
    private GameObject menuCanvasGO;
    private GameObject mainPanel;
    private GameObject settingsPanel;

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

        // 3. Main Menu Panel (Holds title and main buttons)
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
        containerRT.sizeDelta = new Vector2(300, 240);
        containerRT.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = btnContainer.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        // Create Play, Settings, and Quit buttons
        CreateMenuButton(btnContainer.transform, "PLAY GAME", () => LoadGameScene());
        CreateMenuButton(btnContainer.transform, "SETTINGS", () => ShowSettings(true));
        CreateMenuButton(btnContainer.transform, "QUIT GAME", () => QuitGame());

        // 6. Create Settings Panel
        CreateSettingsPanel();
        ShowSettings(false);
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
        infoText.text = "<b>CONTROLS & SHORTCUTS</b>\n\n" +
                        "<b>Movement:</b> WASD\n" +
                        "<b>Look around:</b> Mouse\n" +
                        "<b>Sneak Mode:</b> Left Control (Toggle)\n" +
                        "<b>Inventory / Crafting:</b> I\n" +
                        "<b>Enter Vehicle Control:</b> E\n" +
                        "<b>Return to Boat (Rescue):</b> R";
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

        CreateMenuButton(backGO.transform, "BACK", () => ShowSettings(false));
    }

    private void ShowSettings(bool show)
    {
        if (mainPanel) mainPanel.SetActive(!show);
        if (settingsPanel) settingsPanel.SetActive(show);
    }

    private void LoadGameScene()
    {
        // Loads the gameplay scene (usually the next build index)
        SceneManager.LoadScene(1);
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
