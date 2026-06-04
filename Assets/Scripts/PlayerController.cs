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

    private UnityEngine.UI.Image underwaterOverlay;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        controller.radius = 0.3f; // slim for 1-block-wide gaps

        // Ignore collision with all existing chunk foliage colliders
        foreach (var chunk in FindObjectsByType<Chunk>())
        {
            chunk.IgnorePlayerCollision();
        }
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

        // Create procedural underwater overlay UI
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            GameObject overlayGO = new GameObject("UnderwaterOverlay");
            overlayGO.transform.SetParent(canvas.transform, false);
            
            underwaterOverlay = overlayGO.AddComponent<UnityEngine.UI.Image>();
            underwaterOverlay.color = new Color(0.05f, 0.25f, 0.55f, 0.75f); // Substantial uniform blue screen tint
            
            UnityEngine.RectTransform rect = overlayGO.GetComponent<UnityEngine.RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            
            overlayGO.SetActive(false);
        }
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

        // Check if player is in water (differentiating feet, waist, and head positions)
        bool feetInWater = false;
        bool waistInWater = false;
        bool headInWater = false;
        if (VoxelWorld.Instance != null)
        {
            feetInWater = VoxelWorld.Instance.GetBlock(transform.position + new Vector3(0f, 0.1f, 0f)) == 7;
            waistInWater = VoxelWorld.Instance.GetBlock(transform.position + new Vector3(0f, 0.9f, 0f)) == 7;
            
            // Check if the camera position itself is in/under water, or slightly above the water surface when swimming
            Vector3 camPos = cameraTransform.position;
            byte camBlock = VoxelWorld.Instance.GetBlock(camPos);
            if (camBlock == 7)
            {
                headInWater = true;
            }
            else
            {
                byte blockBelowCam = VoxelWorld.Instance.GetBlock(camPos - new Vector3(0f, 0.5f, 0f));
                if (blockBelowCam == 7)
                {
                    float waterSurfaceY = Mathf.Floor(camPos.y - 0.5f) + 0.85f;
                    if (camPos.y < waterSurfaceY + 0.15f) // 15cm buffer above water surface
                    {
                        headInWater = true;
                    }
                }
            }
        }
        bool inWater = feetInWater || waistInWater || headInWater;

        // Toggle underwater overlay screen-space effect
        if (underwaterOverlay != null)
        {
            underwaterOverlay.gameObject.SetActive(headInWater && !isUIOpen);
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
        if (inWater) speed *= 0.5f;
        
        controller.Move(move * speed * Time.deltaTime);

        // Jump / Swim
        if (Keyboard.current != null)
        {
            if (inWater)
            {
                if (Keyboard.current.spaceKey.isPressed)
                {
                    // If player is at the surface (feet/waist in water, but head out)
                    if (!headInWater)
                    {
                        // Check if there is a wall in front to climb out onto
                        bool hasWallInFront = false;
                        if (VoxelWorld.Instance != null)
                        {
                            Vector3 checkDir = move.magnitude > 0.1f ? move.normalized : transform.forward;
                            Vector3 checkPosLow = transform.position + checkDir * 0.7f + new Vector3(0f, 0.2f, 0f);
                            Vector3 checkPosHigh = transform.position + checkDir * 0.7f + new Vector3(0f, 1.0f, 0f);

                            byte blockLow = VoxelWorld.Instance.GetBlock(checkPosLow);
                            byte blockHigh = VoxelWorld.Instance.GetBlock(checkPosHigh);

                            if ((blockLow != 0 && blockLow != 7) || (blockHigh != 0 && blockHigh != 7))
                            {
                                hasWallInFront = true;
                            }
                        }

                        if (hasWallInFront)
                        {
                            if (velocity.y < 3.5f)
                            {
                                velocity.y = 5.5f; // Leap out of water
                            }
                        }
                        else
                        {
                            // Gently float at the surface instead of launching into the air
                            velocity.y = 0.8f;
                        }
                    }
                    else
                    {
                        velocity.y = 2.5f; // Swim up
                    }
                }
            }
            else if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        // Gravity / Buoyancy
        if (inWater)
        {
            // Slower sinking and drag in water
            velocity.y += (gravity * 0.25f) * Time.deltaTime;
            velocity.y = Mathf.Max(velocity.y, -2.5f); // Terminal velocity in water
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        
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
