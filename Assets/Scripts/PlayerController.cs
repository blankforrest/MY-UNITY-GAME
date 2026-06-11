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
    private GameObject deathScreenGO;
    private bool isDead = false;
    private Vector3 spawnPoint;

    // ── Suffocation (stuck-in-block) state ────────────────────────────────────
    private bool   isStuck           = false;
    private Vector3 stuckPosition;
    private float  suffocationTimer  = 0f;
    private const float SuffocationDeathTime = 5f;  // seconds until death
    private const float SuffocationTickTime  = 1f;  // damage flash interval
    private float  suffocationTickTimer = 0f;
    private UnityEngine.UI.Image suffocationOverlay;

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

        spawnPoint = transform.position;

        // Create procedural UIs
        Canvas canvas = FindAnyObjectByType<Canvas>();
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

            // Suffocation overlay — red flash when stuck inside a block
            GameObject sufGO = new GameObject("SuffocationOverlay");
            sufGO.transform.SetParent(canvas.transform, false);
            suffocationOverlay = sufGO.AddComponent<UnityEngine.UI.Image>();
            suffocationOverlay.color = new Color(0.6f, 0.0f, 0.0f, 0.55f); // opaque red flash
            UnityEngine.RectTransform sufRect = sufGO.GetComponent<UnityEngine.RectTransform>();
            sufRect.anchorMin = Vector2.zero;
            sufRect.anchorMax = Vector2.one;
            sufRect.sizeDelta = Vector2.zero;
            sufGO.SetActive(false);

            CreateDeathScreen(canvas);
        }
    }

    private bool isFrozen = false;

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        if (controller != null)
        {
            controller.enabled = !frozen;
        }
        velocity = Vector3.zero;
    }

    void Update()
    {
        if (isFrozen)
        {
            velocity = Vector3.zero;
            return;
        }
        if (cameraTransform == null) return;

        bool isUIOpen = InventoryUI.IsInventoryOpen || ConfirmationWindow.IsOpen || DevToolsUI.IsCursorUnlocked || isDead;

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
            if (controller.enabled)
            {
                isGrounded = controller.isGrounded;
                if (isGrounded && velocity.y < 0) velocity.y = -2f;
                velocity.y += gravity * Time.deltaTime;
                controller.Move(velocity * Time.deltaTime);
            }
            
            // Reset smoothing to prevent camera snapping when closing UI
            currentMouseDelta = Vector2.zero;
            currentMouseVelocity = Vector2.zero;
            return;
        }

        // Ground Check
        if (controller.enabled)
        {
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Slight downward force to keep grounded
            }
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

        bool isDriving = VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen;
        if (isDriving)
        {
            // ── E to exit vehicle ─────────────────────────────────────────────
            // Handled here in PlayerController so player input is guaranteed to be active.
            bool exitPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                exitPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.E))
                exitPressed = true;
#endif
            if (exitPressed)
            {
                VehicleHUD.Instance.CloseHUD();
            }

            velocity = Vector3.zero;
            return;
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
        
        if (Keyboard.current != null)
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
        if (controller.enabled)
        {
            controller.Move(move * speed * Time.deltaTime);
        }

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
                        // Check if there is a wall or a vehicle in front to climb out onto
                        bool hasWallInFront = false;
                        bool hasVehicleInFront = false;
                        Vector3 checkDir = move.magnitude > 0.1f ? move.normalized : transform.forward;

                        if (VoxelWorld.Instance != null)
                        {
                            Vector3 checkPosLow = transform.position + checkDir * 0.7f + new Vector3(0f, 0.2f, 0f);
                            Vector3 checkPosMid = transform.position + checkDir * 0.7f + new Vector3(0f, 0.7f, 0f);
                            Vector3 checkPosHigh = transform.position + checkDir * 0.7f + new Vector3(0f, 1.2f, 0f);

                            byte blockLow = VoxelWorld.Instance.GetBlock(checkPosLow);
                            byte blockMid = VoxelWorld.Instance.GetBlock(checkPosMid);
                            byte blockHigh = VoxelWorld.Instance.GetBlock(checkPosHigh);

                            if ((blockLow != 0 && blockLow != 7) || 
                                (blockMid != 0 && blockMid != 7) || 
                                (blockHigh != 0 && blockHigh != 7))
                            {
                                hasWallInFront = true;
                            }
                        }

                        // Check for a vehicle (boat) directly in front of the player
                        int vehicleLayer = LayerMask.NameToLayer("Vehicle");
                        if (vehicleLayer != -1)
                        {
                            int vehicleMask = 1 << vehicleLayer;
                            // Check if a sphere in front of the player overlaps any vehicle collider
                            Vector3 overlapCenter = transform.position + checkDir * 0.5f + new Vector3(0f, 0.6f, 0f);
                            Collider[] cols = Physics.OverlapSphere(overlapCenter, 0.5f, vehicleMask);
                            if (cols.Length > 0)
                            {
                                hasVehicleInFront = true;
                            }
                        }

                        if (hasWallInFront || hasVehicleInFront)
                        {
                            if (velocity.y < 3.5f)
                            {
                                velocity.y = 8.5f; // Leap out of water (increased to clear 1.0 unit blocks from floating state)
                            }
                        }
                        else
                        {
                            // Gently float at the surface instead of launching into the air
                            // Only set if we aren't already mid-leap (to preserve upward momentum)
                            if (velocity.y <= 0.8f)
                            {
                                velocity.y = 0.8f;
                            }
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
        
        if (controller.enabled)
        {
            controller.Move(velocity * Time.deltaTime);
        }

        // ── Suffocation tick (player is pinned inside a block) ────────────────
        if (isStuck)
        {
            // Hard-pin position — keep controller disabled so physics can't push player out
            controller.enabled = false;
            transform.position = stuckPosition;
            velocity = Vector3.zero;

            if (VoxelWorld.Instance != null)
            {
                // Check if the block the player is inside has been broken
                byte blockHere = VoxelWorld.Instance.GetBlock(stuckPosition);
                if (blockHere == 0 || blockHere == 7) // air or water → escaped!
                {
                    isStuck = false;
                    suffocationTimer = 0f;
                    suffocationTickTimer = 0f;
                    controller.enabled = true;
                    velocity = new Vector3(0f, 3f, 0f); // pop upward so they don't re-sink
                    if (suffocationOverlay != null) suffocationOverlay.gameObject.SetActive(false);
                    return;
                }
            }

            // Accumulate total time stuck
            suffocationTimer += Time.deltaTime;
            suffocationTickTimer += Time.deltaTime;

            // Flash red overlay on each tick
            if (suffocationTickTimer >= SuffocationTickTime)
            {
                suffocationTickTimer = 0f;
                if (suffocationOverlay != null)
                    StartCoroutine(FlashSuffocation());
            }

            // Die after SuffocationDeathTime seconds
            if (suffocationTimer >= SuffocationDeathTime)
            {
                isStuck = false;
                suffocationTimer = 0f;
                controller.enabled = true;
                if (suffocationOverlay != null) suffocationOverlay.gameObject.SetActive(false);
                Die();
            }
            return;
        }

        // Void rescue safety check (only when not already stuck)
        if (transform.position.y < -5f)
        {
            RescuePlayerFromVoid();
        }
    }

    private System.Collections.IEnumerator FlashSuffocation()
    {
        if (suffocationOverlay == null) yield break;
        suffocationOverlay.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.15f);
        suffocationOverlay.gameObject.SetActive(false);
    }

    private void RescuePlayerFromVoid()
    {
        if (isDead || isStuck) return; // already handled — don't chain-teleport

        if (VoxelWorld.Instance == null) { Die(); return; }

        Vector3 currentPos = transform.position;
        int px = Mathf.FloorToInt(currentPos.x);
        int pz = Mathf.FloorToInt(currentPos.z);

        // Scan downward from the top of the world to find a solid block to embed in
        int targetY = -1;
        for (int y = VoxelData.ChunkHeight - 1; y >= 0; y--)
        {
            byte blockType = VoxelWorld.Instance.GetBlock(new Vector3(px + 0.5f, y + 0.5f, pz + 0.5f));
            if (blockType != 0 && blockType != 7) // solid block found
            {
                targetY = y;
                break;
            }
        }

        if (targetY != -1)
        {
            // Pin the player inside the topmost solid block.
            // Controller stays DISABLED until the player breaks the block.
            // This prevents the CharacterController from pushing them out and
            // causing an infinite fall → rescue → fall loop.
            stuckPosition = new Vector3(px + 0.5f, targetY + 0.5f, pz + 0.5f);
            controller.enabled = false;
            transform.position = stuckPosition;
            velocity = Vector3.zero;
            isStuck = true;
            suffocationTimer = 0f;
            suffocationTickTimer = 0f;
            Debug.Log($"[VoidRescue] Player pinned inside block at {stuckPosition}. Break the block to escape!");
        }
        else
        {
            // True void — no solid block anywhere, just die
            Debug.Log("[VoidRescue] Player fell to void and died (no solid block found).");
            Die();
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // Reset velocity
        velocity = Vector3.zero;

        // Show death screen UI
        if (deathScreenGO != null)
        {
            deathScreenGO.SetActive(true);
        }

        // Unlock cursor for death screen interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Respawn()
    {
        isDead = false;

        // Clear any stuck/suffocation state so movement works after respawn
        isStuck = false;
        suffocationTimer = 0f;
        suffocationTickTimer = 0f;
        if (suffocationOverlay != null) suffocationOverlay.gameObject.SetActive(false);

        if (deathScreenGO != null)
        {
            deathScreenGO.SetActive(false);
        }

        // Teleport player to the spawn point
        controller.enabled = false;
        transform.position = spawnPoint;
        velocity = Vector3.zero;
        controller.enabled = true;

        // Re-lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void CreateDeathScreen(Canvas canvas)
    {
        deathScreenGO = new GameObject("DeathScreen");
        deathScreenGO.transform.SetParent(canvas.transform, false);

        // Dark red overlay
        var img = deathScreenGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.45f, 0.05f, 0.05f, 0.65f);

        var rect = deathScreenGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        // "You Died!" Text
        GameObject textGO = new GameObject("DeathText");
        textGO.transform.SetParent(deathScreenGO.transform, false);
        var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = "You Died!";
        text.fontSize = 48f;
        text.fontStyle = TMPro.FontStyles.Bold;
        text.color = Color.red;
        text.alignment = TMPro.TextAlignmentOptions.Center;

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.6f);
        textRT.anchorMax = new Vector2(0.5f, 0.6f);
        textRT.pivot = new Vector2(0.5f, 0.5f);
        textRT.sizeDelta = new Vector2(400f, 100f);

        // Respawn Button
        GameObject buttonGO = new GameObject("RespawnButton");
        buttonGO.transform.SetParent(deathScreenGO.transform, false);
        
        var btnImg = buttonGO.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var btn = buttonGO.AddComponent<UnityEngine.UI.Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(Respawn);

        var btnRT = buttonGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.45f);
        btnRT.anchorMax = new Vector2(0.5f, 0.45f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.sizeDelta = new Vector2(180f, 40f);

        // Button Label Text
        GameObject btnTextGO = new GameObject("Label");
        btnTextGO.transform.SetParent(buttonGO.transform, false);
        var btnText = btnTextGO.AddComponent<TMPro.TextMeshProUGUI>();
        btnText.text = "Respawn";
        btnText.fontSize = 18f;
        btnText.alignment = TMPro.TextAlignmentOptions.Center;
        btnText.color = Color.white;

        var btnTextRT = btnTextGO.GetComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.sizeDelta = Vector2.zero;

        deathScreenGO.SetActive(false);
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
