using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f; 
    public float mouseSensitivity = 0.1f; 
    public float lookSmoothTime = 0.02f; // Smoothing to fix jittery camera

    private CharacterController controller;
    private Transform cameraTransform;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;

    // Smoothing state
    private Vector2 currentMouseDelta;
    private Vector2 currentMouseVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        controller.radius = 0.3f; // slim for 1-block-wide gaps
        // height/center left at Inspector values — CharacterController self-positions correctly

        Camera cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            Debug.LogError("PlayerController needs a Camera as a child object.");
            return;
        }
        cameraTransform = cam.transform;
        // Do NOT override localPosition — Inspector value is the correct eye height

        // Initial cursor state
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (cameraTransform == null) return;

        bool isUIOpen = InventoryUI.IsInventoryOpen || ConfirmationWindow.IsOpen;

        // Handle cursor lock state based on UI
        if (isUIOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Block all input when inventory OR confirmation window is open
        if (isUIOpen)
        {
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0) velocity.y = -2f;
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            
            // Reset smoothing to prevent camera snapping when closing UI
            currentMouseDelta = Vector2.zero;
            currentMouseVelocity = Vector2.zero;
            return;
        }

        // Ground Check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Slight downward force to keep grounded
        }

        // Look
        if (Mouse.current != null)
        {
            Vector2 targetMouseDelta = Mouse.current.delta.ReadValue();
            
            // Smooth the mouse delta to prevent micro-jitters
            currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseVelocity, lookSmoothTime);

            float mouseX = currentMouseDelta.x * mouseSensitivity;
            float mouseY = currentMouseDelta.y * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        // Movement
        float x = 0;
        float z = 0;
        bool isRunning = false;
        
        bool isDriving = VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen;

        if (Keyboard.current != null && !isDriving)
        {
            if (Keyboard.current.wKey.isPressed) z += 1;
            if (Keyboard.current.sKey.isPressed) z -= 1;
            if (Keyboard.current.dKey.isPressed) x += 1;
            if (Keyboard.current.aKey.isPressed) x -= 1;
            
            if (Keyboard.current.leftShiftKey.isPressed) isRunning = true;
        }

        Vector3 move = transform.right * x + transform.forward * z;
        // Normalize vector so diagonal movement isn't faster
        if (move.magnitude > 1f) move.Normalize();

        float speed = isRunning ? runSpeed : walkSpeed;
        
        controller.Move(move * speed * Time.deltaTime);

        // Jump
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // ── Push Rigidbodies ──────────────────────────────────────────────────────

    /// <summary>
    /// Called by CharacterController whenever it hits a collider.
    /// Applies a push force to any Rigidbody the player walks into (e.g. vehicles).
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.collider.attachedRigidbody;

        // Ignore: no rigidbody, kinematic, or something we're standing ON (pushes down)
        if (rb == null || rb.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;

        // Push direction = horizontal movement direction of the player
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);

        // Force proportional to player speed — feel free to tune the multiplier
        float pushForce = 3f;
        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }
}
