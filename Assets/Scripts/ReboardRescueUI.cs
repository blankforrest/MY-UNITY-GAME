using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Spawns a clean UI button on the middle-left side of the screen and monitors for 
/// keypress (R) to teleport the player back to the control block of the nearest boat.
/// </summary>
public class ReboardRescueUI : MonoBehaviour
{
    private static ReboardRescueUI Instance { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("ReboardRescueUI");
        Instance = go.AddComponent<ReboardRescueUI>();
        DontDestroyOnLoad(go);
    }

    private GameObject _buttonGO;
    private TextMeshProUGUI _buttonText;
    private Image _buttonImage;

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        // 1. Determine if we should show the reboard button
        VehicleController nearestVC = FindNearestVehicle();
        bool isDriving = VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen;

        // Check if player is dead
        bool isDead = false;
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) isDead = pc.IsDead;
        }

        // Evaluate distance and bounds
        bool isInside = false;
        bool isTooClose = false;
        bool isTooFar = false;

        if (nearestVC != null && player != null)
        {
            ControlBlock cb = nearestVC.GetComponentInChildren<ControlBlock>();
            Vector3 targetPos = cb != null ? cb.transform.position : nearestVC.transform.position;
            float dist = Vector3.Distance(player.transform.position, targetPos);

            // Stand inside dry cabin filter
            isInside = VehicleController.IsWorldPosInsideVehicle(player.transform.position);
            
            // Too close (< 6 units) or already inside/on the boat
            isTooClose = dist < 6.0f || isInside;
            
            // Too far from the boat (> 40 units)
            isTooFar = dist > 40.0f;
        }

        // Only show the reboard indicator if there is a vehicle, player is alive, not driving, not too close, and not too far
        bool shouldShow = nearestVC != null && !isDriving && !isDead && !isTooClose && !isTooFar;

        if (_buttonGO != null)
        {
            _buttonGO.SetActive(shouldShow);
        }

        if (!shouldShow) return;

        // 2. Handle Shortcut Key (R)
        bool isUIOpen = InventoryUI.IsInventoryOpen || ConfirmationWindow.IsOpen || DevToolsUI.IsCursorUnlocked;
        if (!isUIOpen)
        {
            bool rPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                rPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.R))
                rPressed = true;
#endif
            if (rPressed)
            {
                ReboardNearest(nearestVC);
            }
        }
    }

    private VehicleController FindNearestVehicle()
    {
        VehicleController[] vehicles = FindObjectsByType<VehicleController>(FindObjectsSortMode.None);
        if (vehicles == null || vehicles.Length == 0) return null;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return null;

        VehicleController nearest = null;
        float minDist = float.MaxValue;
        foreach (var vc in vehicles)
        {
            float dist = Vector3.Distance(player.transform.position, vc.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = vc;
            }
        }
        return nearest;
    }

    private void ReboardNearest(VehicleController vc)
    {
        if (vc == null) return;

        ControlBlock cb = vc.GetComponentInChildren<ControlBlock>();
        Vector3 targetPos;
        if (cb != null)
        {
            // Teleport 1 unit above the control block seat so they stand on it
            targetPos = cb.transform.position + Vector3.up * 1.0f;
        }
        else
        {
            // Fallback to vehicle center
            targetPos = vc.transform.position + Vector3.up * 1.5f;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        // Teleport the player
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

        // Align player yaw to boat direction
        player.transform.rotation = Quaternion.Euler(0f, vc.transform.eulerAngles.y, 0f);

        // Reset camera vertical angle
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.transform.localRotation = Quaternion.identity;
        }

        Debug.Log($"[Reboard] Teleported player to control block on {vc.name} without entering control mode.");
    }

    private void BuildUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ReboardRescueUI] Canvas not found in scene.");
            return;
        }

        // Create the button at middle-left of screen
        _buttonGO = new GameObject("ReboardRescueButton", typeof(RectTransform));
        _buttonGO.transform.SetParent(canvas.transform, false);

        var rt = _buttonGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f); // middle-left
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(50f, 50f); // square 50x50
        rt.anchoredPosition = new Vector2(20f, 0f); // 20 pixels from left

        // Gray color palette
        _buttonImage = _buttonGO.AddComponent<Image>();
        _buttonImage.color = new Color(0.25f, 0.25f, 0.25f, 0.85f); // gray background

        // Border outline
        var outline = _buttonGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Add Button component for click interaction
        var btn = _buttonGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(0.25f, 0.25f, 0.25f, 0.85f);
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 0.90f);
        colors.pressedColor     = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        btn.colors = colors;
        btn.onClick.AddListener(() => {
            VehicleController nearest = FindNearestVehicle();
            if (nearest != null)
            {
                ReboardNearest(nearest);
            }
        });

        // Add Boat Icon using TextMeshPro
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(_buttonGO.transform, false);

        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;

        _buttonText = labelGO.AddComponent<TextMeshProUGUI>();
        _buttonText.text = "🚢"; // boat icon
        _buttonText.fontSize = 24f;
        _buttonText.color = Color.white; // white icon
        _buttonText.alignment = TextAlignmentOptions.Center;

        // Add a small "[R]" keybind hint below the button
        var hintGO = new GameObject("KeyHint", typeof(RectTransform));
        hintGO.transform.SetParent(_buttonGO.transform, false);
        var hintRT = hintGO.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0.5f, 0f);
        hintRT.anchorMax = new Vector2(0.5f, 0f);
        hintRT.pivot     = new Vector2(0.5f, 1f);
        hintRT.sizeDelta = new Vector2(50f, 15f);
        hintRT.anchoredPosition = new Vector2(0f, -5f); // 5 pixels below button

        var hintText = hintGO.AddComponent<TextMeshProUGUI>();
        hintText.text = "[R]";
        hintText.fontSize = 11f;
        hintText.color = Color.white;
        hintText.alignment = TextAlignmentOptions.Center;

        // Hide initially until a vehicle is detected
        _buttonGO.SetActive(false);
    }
}
