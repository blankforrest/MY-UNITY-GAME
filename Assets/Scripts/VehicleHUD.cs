using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class VehicleHUD : MonoBehaviour
{
    public static VehicleHUD Instance;

    public bool IsOpen { get; private set; }
    /// <summary>The frame number on which CloseHUD() was last called. Used by
    /// ControlBlock to prevent same-frame re-open after the player presses E to exit.</summary>
    public int JustClosedFrame { get; private set; } = -1;
    private VehicleController currentVC;
    private PlayerController playerController;
    private int justOpenedFrame = -1;

    private GameObject hudContainer;
    private Text powerText;
    private Image wImage, aImage, sImage, dImage;

    private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    private Color activeColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        CreateCanvasHUD();
        CloseHUD();
    }

    private void CreateCanvasHUD()
    {
        // 1. Create Canvas
        GameObject canvasGO = new GameObject("VehicleHUD_Canvas");
        canvasGO.transform.SetParent(transform);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Above inventory
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 2. Container Panel
        hudContainer = new GameObject("HUD_Container");
        hudContainer.transform.SetParent(canvasGO.transform, false);
        RectTransform containerRect = hudContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.1f);
        containerRect.anchorMax = new Vector2(0.5f, 0.1f);
        containerRect.anchoredPosition = new Vector2(0, 100);
        containerRect.sizeDelta = new Vector2(200, 200);

        // 3. Create directional buttons
        wImage = CreateButton(hudContainer.transform, "W", new Vector2(0, 60));
        sImage = CreateButton(hudContainer.transform, "S", new Vector2(0, -60));
        aImage = CreateButton(hudContainer.transform, "A", new Vector2(-60, 0));
        dImage = CreateButton(hudContainer.transform, "D", new Vector2(60, 0));

        // 4. Power Text (Center)
        GameObject textGO = new GameObject("PowerText");
        textGO.transform.SetParent(hudContainer.transform, false);
        powerText = textGO.AddComponent<Text>();
        powerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        powerText.alignment = TextAnchor.MiddleCenter;
        powerText.color = Color.white;
        powerText.text = "0.0";
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(60, 60);

        // 5. Close Button (X)
        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(canvasGO.transform, false);
        Image closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        RectTransform closeRect = closeGO.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-50, -50);
        closeRect.sizeDelta = new Vector2(40, 40);

        Text closeText = new GameObject("XText").AddComponent<Text>();
        closeText.transform.SetParent(closeGO.transform, false);
        closeText.font = powerText.font;
        closeText.text = "X";
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = Color.white;
        closeText.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 40);

        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseHUD);
    }

    private Image CreateButton(Transform parent, string label, Vector2 position)
    {
        GameObject btnGO = new GameObject($"Btn_{label}");
        btnGO.transform.SetParent(parent, false);
        Image img = btnGO.AddComponent<Image>();
        img.color = normalColor;
        RectTransform rect = btnGO.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(50, 50);

        Text txt = new GameObject("Text").AddComponent<Text>();
        txt.transform.SetParent(btnGO.transform, false);
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 50);

        return img;
    }

    public void OpenHUD(VehicleController vc)
    {
        if (vc == null) return;

        currentVC = vc;
        currentVC.isBeingControlled = true;
        IsOpen = true;
        justOpenedFrame = Time.frameCount; // don't close on the same frame we opened
        hudContainer.transform.parent.gameObject.SetActive(true);

        Debug.Log("Vehicle control started");
    }

    public void CloseHUD()
    {
        JustClosedFrame = Time.frameCount; // stamp so ControlBlock skips this frame
        if (currentVC != null)
        {
            currentVC.isBeingControlled = false;
            currentVC = null;
        }

        IsOpen = false;
        if (hudContainer != null)
            hudContainer.transform.parent.gameObject.SetActive(false);

        Debug.Log("Vehicle control ended");
    }

    private void Update()
    {
        if (!IsOpen || currentVC == null) return;

        // E key exit is now handled by VehicleController.Update() to avoid
        // race conditions with ControlBlock running in the same frame.
        // VehicleHUD only manages HUD visuals here.

        // Read input for visual feedback
        bool w = false, a = false, s = false, d = false;

        if (Keyboard.current != null)
        {
            w = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            s = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            a = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            d = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
        }

        // Update Button Colors
        if (wImage != null) wImage.color = w ? activeColor : normalColor;
        if (sImage != null) sImage.color = s ? activeColor : normalColor;
        if (aImage != null) aImage.color = a ? activeColor : normalColor;
        if (dImage != null) dImage.color = d ? activeColor : normalColor;

        // Update Power Text (Velocity Magnitude)
        if (powerText != null)
        {
            float speed = currentVC.CurrentVelocity.magnitude;
            powerText.text = speed.ToString("F1");
        }
    }
}
