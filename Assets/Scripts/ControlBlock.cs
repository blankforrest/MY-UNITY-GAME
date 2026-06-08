using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ControlBlock : MonoBehaviour
{
    private VehicleController vc;
    private bool isLookedAt = false;

    // Layer mask: everything except the Vehicle layer (avoids vehicle self-hits being missed)
    private int _interactMask = ~0;

    private void Start()
    {
        vc = GetComponentInParent<VehicleController>();

        // Exclude the player's own layer from the raycast so the player capsule
        // doesn't swallow the ray when standing right next to the block.
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer != -1) _interactMask &= ~(1 << playerLayer);
    }

    private void Update()
    {
        isLookedAt = false;

        // Auto-create VehicleHUD if it doesn't exist in the scene
        if (VehicleHUD.Instance == null)
            new GameObject("VehicleHUD_Singleton").AddComponent<VehicleHUD>();

        if (VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen)
            return;

        // If the HUD was closed THIS frame (player just pressed E to exit), skip.
        // Prevents ControlBlock from re-opening the HUD on the same frame it was closed,
        // regardless of which script runs first in Unity's Update loop.
        if (VehicleHUD.Instance != null && VehicleHUD.Instance.JustClosedFrame == Time.frameCount)
            return;

        Camera cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();
        if (cam == null) return;

        bool canInteract = false;

        // ── Method 1: Raycast (normal look-at detection) ──────────────────────
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 5f, _interactMask, QueryTriggerInteraction.Ignore))
        {
            // Accept if the hit collider is on THIS control block or anywhere on the
            // same vehicle. The player could be looking at a hull block next to us.
            bool hitThisBlock   = hit.collider.GetComponentInParent<ControlBlock>()    == this;
            bool hitThisVehicle = vc != null &&
                                  hit.collider.GetComponentInParent<VehicleController>() == vc;

            if (hitThisBlock || hitThisVehicle)
                canInteract = true;
        }

        // ── Method 2: Proximity fallback ──────────────────────────────────────
        // If the player is within 2.5 units of this block, E works even when the
        // raycast is blocked by terrain or another geometry piece in front.
        if (!canInteract && cam != null)
        {
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist <= 2.5f)
                canInteract = true;
        }

        if (canInteract)
        {
            isLookedAt = true;

            bool interactPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                interactPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.E))
                interactPressed = true;
#endif
            if (interactPressed && vc != null)
                VehicleHUD.Instance.OpenHUD(vc);
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
            GUI.Label(new Rect(Screen.width / 2f - 100f, Screen.height / 2f + 30f, 200f, 40f),
                      "Press E to control", style);
        }
    }
}
