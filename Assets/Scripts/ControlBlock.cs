using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ControlBlock : MonoBehaviour
{
    private VehicleController vc;
    private bool isLookedAt = false;

    private void Start()
    {
        vc = GetComponentInParent<VehicleController>();
    }

    private void Update()
    {
        isLookedAt = false;

        // Auto-create VehicleHUD if it doesn't exist in the scene
        if (VehicleHUD.Instance == null)
        {
            new GameObject("VehicleHUD_Singleton").AddComponent<VehicleHUD>();
        }

        if (VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen)
            return;

        // Fallback for camera if it's not tagged "MainCamera"
        Camera cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();

        if (cam != null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            // Increased reach to 5f to match normal player interaction distance, and ignore triggers
            if (Physics.Raycast(ray, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Use GetComponentInParent in case the collider is on a child object of the block prefab
                if (hit.collider != null)
                {
                    if (vc == null) vc = GetComponentInParent<VehicleController>();

                    bool isTarget = hit.collider.GetComponentInParent<ControlBlock>() == this ||
                                    (vc != null && hit.collider.GetComponentInParent<VehicleController>() == vc) ||
                                    hit.collider.GetComponentInChildren<ControlBlock>() == this;

                    if (isTarget)
                    {
                        isLookedAt = true;

                        bool interactPressed = false;
                        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                        {
                            interactPressed = true;
                        }

                        if (interactPressed && vc != null)
                        {
                            VehicleHUD.Instance.OpenHUD(vc);
                        }
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        if (isLookedAt)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 20;
            style.normal.textColor = Color.yellow;
            style.alignment = TextAnchor.MiddleCenter;

            GUI.Label(new Rect(Screen.width / 2f - 100f, Screen.height / 2f + 30f, 200f, 40f), "Press E to control", style);
        }
    }
}
