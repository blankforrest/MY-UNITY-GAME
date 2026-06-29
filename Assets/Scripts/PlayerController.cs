using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float sneakSpeed = 2f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f; 
    public float mouseSensitivity = 0.1f; 
    public float lookSmoothTime = 0.02f; // Smoothing to fix jittery camera
    public bool isCreativeMode = false;
    private bool isFlying = false;
    private float lastJumpPressTime = 0f;

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
    public bool IsDead => isDead;
    private Vector3 spawnPoint;
    private Vector3 originalCameraLocalPosition;
    private bool isSneaking = false;

    // ── Suffocation (stuck-in-block) state ────────────────────────────────────
    private bool   isStuck           = false;
    private Vector3 stuckPosition;
    private float  suffocationTimer  = 0f;
    private const float SuffocationDeathTime = 5f;  // seconds until death
    private const float SuffocationTickTime  = 1f;  // damage flash interval
    private float  suffocationTickTimer = 0f;
    private UnityEngine.UI.Image suffocationOverlay;

    [Header("Survival Mechanics")]
    public float maxHealth = 20f;
    public float currentHealth = 20f;
    public float maxHunger = 20f;
    public float currentHunger = 20f;
    private float hungerDecayAccumulator = 0f;
    private float healthRegenTimer = 0f;
    private float starvationDamageTimer = 0f;
    private float fallStartY;

    // Air/Drowning
    public float maxAir = 10f;
    public float currentAir = 10f;
    private float drowningTimer = 0f;

    // Eating
    private float eatingTimer = 0f;
    private const float EATING_DURATION = 1.2f;
    private bool isEating = false;
    private string eatingItemName = "";

    private GameObject eatingProgressPanel;
    private UnityEngine.UI.Image eatingProgressBar;
    private TMPro.TextMeshProUGUI eatingProgressText;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        controller.radius = 0.3f; // slim for 1-block-wide gaps
        controller.stepOffset = 0.51f; // allow climbing 0.5-unit stair steps

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
        cam.nearClipPlane = 0.01f; // Prevents block faces close to the player view from clipping and becoming invisible
        cameraTransform = cam.transform;
        originalCameraLocalPosition = cameraTransform.localPosition;

        // Disable shadow casting on player's visual components (no bean shadow)
        Renderer[] playerRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in playerRenderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (PlayerPrefs.HasKey("MouseSensitivity"))
        {
            mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity");
        }
        if (cam != null && PlayerPrefs.HasKey("FOV"))
        {
            cam.fieldOfView = PlayerPrefs.GetFloat("FOV");
        }

        // Initial cursor state
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Set initial game mode from Main Menu or PlayerPrefs choice
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.HasSaveFile())
        {
            // Loaded game: restore game mode from PlayerPrefs or load data
            string savedMode = PlayerPrefs.GetString("GameMode_" + SaveLoadManager.activeWorldSlot, "Survival");
            isCreativeMode = (savedMode == "Creative");
            spawnPoint = transform.position;
        }
        else
        {
            // New game: set based on Main Menu selectedGameMode
            isCreativeMode = (MainMenu.selectedGameMode == "Creative");

            // Find beach spawn
            Vector3 beachSpawn = FindNearestBeachSpawn();
            if (controller != null) controller.enabled = false;
            transform.position = beachSpawn;
            spawnPoint = beachSpawn;
            if (controller != null) controller.enabled = true;
            Debug.Log($"[PlayerController] New world spawned player at beach: {beachSpawn}");
        }

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

            // Setup Eating Progress Panel
            eatingProgressPanel = new GameObject("EatingProgressPanel", typeof(RectTransform));
            eatingProgressPanel.transform.SetParent(canvas.transform, false);
            RectTransform epRT = eatingProgressPanel.GetComponent<RectTransform>();
            epRT.anchorMin = new Vector2(0.5f, 0f);
            epRT.anchorMax = new Vector2(0.5f, 0f);
            epRT.pivot = new Vector2(0.5f, 0f);
            epRT.anchoredPosition = new Vector2(0f, 110f); // above hotbar and stats
            epRT.sizeDelta = new Vector2(120f, 25f);

            GameObject bgGO = new GameObject("Bg", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bgGO.transform.SetParent(eatingProgressPanel.transform, false);
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            GameObject barGO = new GameObject("Bar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            barGO.transform.SetParent(eatingProgressPanel.transform, false);
            RectTransform barRT = barGO.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0f, 0f);
            barRT.anchorMax = new Vector2(0f, 1f);
            barRT.pivot = new Vector2(0f, 0.5f);
            barRT.anchoredPosition = new Vector2(2f, 0f);
            barRT.sizeDelta = new Vector2(0f, -4f);
            eatingProgressBar = barGO.GetComponent<UnityEngine.UI.Image>();
            eatingProgressBar.color = new Color(0.2f, 0.8f, 0.2f, 1f);

            GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textGO.transform.SetParent(eatingProgressPanel.transform, false);
            RectTransform tRT = textGO.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 1f);
            tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 0f);
            tRT.anchoredPosition = new Vector2(0f, 2f);
            tRT.sizeDelta = new Vector2(0f, 15f);
            eatingProgressText = textGO.GetComponent<TMPro.TextMeshProUGUI>();
            eatingProgressText.fontSize = 10f;
            eatingProgressText.alignment = TMPro.TextAlignmentOptions.Center;
            eatingProgressText.color = Color.white;
            eatingProgressText.text = "Eating Apple...";
            eatingProgressPanel.SetActive(false);

            // Dynamically add SurvivalHUD if not present
            if (canvas.GetComponentInChildren<SurvivalHUD>() == null)
            {
                GameObject hudGO = new GameObject("SurvivalHUDContainer");
                hudGO.transform.SetParent(canvas.transform, false);
                hudGO.AddComponent<SurvivalHUD>();
            }

            // Dynamically add FPSDisplay if not present
            if (canvas.GetComponentInChildren<FPSDisplay>() == null)
            {
                GameObject fpsGO = new GameObject("FPSDisplayContainer");
                fpsGO.transform.SetParent(canvas.transform, false);
                fpsGO.AddComponent<FPSDisplay>();
            }
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

        bool isUIOpen = InventoryUI.IsInventoryOpen || ConfirmationWindow.IsOpen || DevToolsUI.IsCursorUnlocked || isDead || PauseMenu.IsPaused;

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
                if (isCreativeMode && isFlying)
                {
                    // Remain suspended/no gravity when inventory or pause menu is opened while flying
                    velocity.y = 0f;
                }
                else
                {
                    if (isGrounded && velocity.y < 0) velocity.y = -2f;
                    velocity.y += gravity * Time.deltaTime;
                }
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
            if (isGrounded && velocity.y < 0 && !(isCreativeMode && isFlying))
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

            // Clear underwater overlay when entering driving mode
            if (underwaterOverlay != null && underwaterOverlay.gameObject.activeSelf)
            {
                underwaterOverlay.gameObject.SetActive(false);
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

        // Toggle Sneak Mode
        if (isDriving)
        {
            isSneaking = false;
        }
        else if (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame && isGrounded)
        {
            isSneaking = !isSneaking;
        }

        // Smoothly lerp camera position for sneaking visual indicator
        Vector3 targetCamPos = isSneaking ? (originalCameraLocalPosition + new Vector3(0f, -0.2f, 0f)) : originalCameraLocalPosition;
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetCamPos, Time.deltaTime * 10f);
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
        if (move.magnitude > 1f) move.Normalize();

        float speed = walkSpeed;
        if (isRunning) speed = runSpeed;
        else if (isSneaking) speed = sneakSpeed;

        if (isCreativeMode)
        {
            speed *= 2f;
        }

        // Prevent falling off edges when sneaking
        if (isSneaking && isGrounded)
        {
            float dt = Time.deltaTime;
            Vector3 moveX = new Vector3(move.x, 0f, 0f);
            Vector3 moveZ = new Vector3(0f, 0f, move.z);

            // Test X movement separately
            if (moveX.magnitude > 0.0001f)
            {
                Vector3 nextPosX = transform.position + moveX * speed * dt;
                if (!HasGroundUnder(nextPosX))
                {
                    move.x = 0f;
                }
            }

            // Test Z movement separately
            if (moveZ.magnitude > 0.0001f)
            {
                Vector3 nextPosZ = transform.position + moveZ * speed * dt;
                if (!HasGroundUnder(nextPosZ))
                {
                    move.z = 0f;
                }
            }

            // Re-normalize if any component was modified
            if (move.magnitude > 1f) move.Normalize();
        }

        if (controller.enabled)
        {
            controller.Move(move * speed * Time.deltaTime);
        }

        // Creative flight toggle via double-jump
        if (isCreativeMode && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            float timeSinceLastJump = Time.time - lastJumpPressTime;
            if (timeSinceLastJump < 0.25f)
            {
                isFlying = !isFlying;
                if (isFlying)
                {
                    velocity.y = 2f; // lift off slightly
                }
                else
                {
                    velocity.y = 0f;
                }
            }
            lastJumpPressTime = Time.time;
        }

        // Jump / Swim / Flight
        if (Keyboard.current != null)
        {
            if (isCreativeMode && isFlying)
            {
                velocity.y = 0f;
                float verticalSpeed = (isRunning ? runSpeed : walkSpeed) * 2f; // flight speed
                if (Keyboard.current.spaceKey.isPressed)
                {
                    velocity.y = verticalSpeed;
                }
                else if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.leftCtrlKey.isPressed)
                {
                    velocity.y = -verticalSpeed;
                }
            }
            else if (inWater && !isGrounded)
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
                            Vector3 checkPosLow = transform.position + checkDir * 0.8f + new Vector3(0f, 0.2f, 0f);
                            Vector3 checkPosMid = transform.position + checkDir * 0.8f + new Vector3(0f, 0.9f, 0f);
                            Vector3 checkPosHigh = transform.position + checkDir * 0.8f + new Vector3(0f, 1.6f, 0f);
                            Vector3 checkPosExtra = transform.position + checkDir * 0.8f + new Vector3(0f, 2.0f, 0f);

                            byte blockLow = VoxelWorld.Instance.GetBlock(checkPosLow);
                            byte blockMid = VoxelWorld.Instance.GetBlock(checkPosMid);
                            byte blockHigh = VoxelWorld.Instance.GetBlock(checkPosHigh);
                            byte blockExtra = VoxelWorld.Instance.GetBlock(checkPosExtra);

                            if ((blockLow != 0 && blockLow != 7) || 
                                (blockMid != 0 && blockMid != 7) || 
                                (blockHigh != 0 && blockHigh != 7) ||
                                (blockExtra != 0 && blockExtra != 7))
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
                if (!isCreativeMode)
                {
                    currentHunger = Mathf.Max(0f, currentHunger - 0.05f); // jump hunger depletion
                }
            }
        }

        // Gravity / Buoyancy
        if (isCreativeMode && isFlying)
        {
            // No gravity when flying in creative mode
        }
        else if (inWater)
        {
            // Slower sinking and drag in water
            velocity.y += (gravity * 0.25f) * Time.deltaTime;
            velocity.y = Mathf.Max(velocity.y, -2.5f); // Terminal velocity in water
        }
        else
        {
            // Normal gravity
            velocity.y += gravity * Time.deltaTime;
        }
        
        if (controller.enabled)
        {
            controller.Move(velocity * Time.deltaTime);
        }

        // ── Suffocation tick (player is pinned inside a block) ────────────────
        if (isStuck && !isCreativeMode)
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

            // Deal damage on each tick (every 1 second)
            if (suffocationTimer >= 1.0f)
            {
                suffocationTimer = 0f;
                if (suffocationOverlay != null)
                    StartCoroutine(FlashSuffocation());
                TakeDamage(2.0f); // 1 heart of damage
            }
            return;
        }

        // ── Survival Mechanics (Health, Hunger, Drowning, Eating, Fall Damage) ──
        if (isCreativeMode)
        {
            currentHealth = maxHealth;
            currentHunger = maxHunger;
            currentAir = maxAir;
            isEating = false;
            if (eatingProgressPanel != null) eatingProgressPanel.SetActive(false);
            fallStartY = transform.position.y;
        }
        else if (!isDead)
        {
            // 1. Hunger decay based on movement
            float moveMagnitude = move.magnitude;
            float currentDecayRate = 0.002f; // base rate
            if (inWater)
            {
                if (moveMagnitude > 0.1f) currentDecayRate = 0.02f; // swimming
            }
            else if (moveMagnitude > 0.1f)
            {
                if (isRunning) currentDecayRate = 0.035f; // sprinting
                else if (isSneaking) currentDecayRate = 0.005f; // sneaking
                else currentDecayRate = 0.01f; // walking
            }
            currentHunger = Mathf.Max(0f, currentHunger - currentDecayRate * Time.deltaTime);

            // 2. Health regeneration & Starvation
            if (currentHunger >= 18f && currentHealth < maxHealth)
            {
                healthRegenTimer += Time.deltaTime;
                if (healthRegenTimer >= 4.0f)
                {
                    currentHealth = Mathf.Min(maxHealth, currentHealth + 1.0f);
                    healthRegenTimer = 0f;
                }
            }
            else
            {
                healthRegenTimer = 0f;
            }

            if (currentHunger <= 0f)
            {
                starvationDamageTimer += Time.deltaTime;
                if (starvationDamageTimer >= 2.0f)
                {
                    TakeDamage(1.0f); // 0.5 heart of damage
                    starvationDamageTimer = 0f;
                }
            }
            else
            {
                starvationDamageTimer = 0f;
            }

            // 3. Fall damage
            if (isCreativeMode && isFlying || inWater)
            {
                fallStartY = transform.position.y;
            }
            else
            {
                if (isGrounded)
                {
                    float fallDistance = fallStartY - transform.position.y;
                    if (fallDistance > 3.0f)
                    {
                        float damage = Mathf.Floor(fallDistance - 3.0f);
                        if (damage > 0)
                        {
                            TakeDamage(damage);
                        }
                    }
                    fallStartY = transform.position.y;
                }
                else
                {
                    if (transform.position.y > fallStartY)
                    {
                        fallStartY = transform.position.y;
                    }
                }
            }

            // 4. Drowning
            if (headInWater)
            {
                currentAir = Mathf.Max(0f, currentAir - Time.deltaTime);
                if (currentAir <= 0f)
                {
                    drowningTimer += Time.deltaTime;
                    if (drowningTimer >= 1.0f)
                    {
                        TakeDamage(2.0f); // 1 heart of damage
                        drowningTimer = 0f;
                    }
                }
            }
            else
            {
                currentAir = maxAir;
                drowningTimer = 0f;
            }

            // 5. Eating Mechanic
            HandleEating();
        }

        // Void rescue safety check (only when not already stuck)
        if (transform.position.y < -5f)
        {
            RescuePlayerFromVoid();
        }
    }

    public void TakeDamage(float amount)
    {
        if (isCreativeMode || isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        
        // General damage feedback - red flash
        if (suffocationOverlay != null && !isStuck)
        {
            StartCoroutine(FlashSuffocation());
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void HandleEating()
    {
        if (isDead)
        {
            isEating = false;
            if (eatingProgressPanel != null) eatingProgressPanel.SetActive(false);
            return;
        }

        InventorySlot selectedSlot = Hotbar.Instance != null ? Hotbar.Instance.GetSelectedSlot() : null;
        Item heldItem = selectedSlot?.item;
        bool isEdible = heldItem != null && heldItem.itemName == "Apple";

        bool canEat = isEdible && (currentHunger < maxHunger || currentHealth < maxHealth);
        bool isRightClickHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (isRightClickHeld && canEat)
        {
            if (!isEating || eatingItemName != heldItem.itemName)
            {
                isEating = true;
                eatingTimer = 0f;
                eatingItemName = heldItem.itemName;
            }
            else
            {
                eatingTimer += Time.deltaTime;
                if (eatingProgressPanel != null)
                {
                    eatingProgressPanel.SetActive(true);
                    float pct = eatingTimer / EATING_DURATION;
                    eatingProgressBar.rectTransform.sizeDelta = new Vector2(116f * pct, -4f);
                    eatingProgressText.text = $"Eating {eatingItemName}...";
                }

                if (eatingTimer >= EATING_DURATION)
                {
                    currentHunger = Mathf.Min(maxHunger, currentHunger + 4.0f);
                    currentHealth = Mathf.Min(maxHealth, currentHealth + 2.0f);

                    if (selectedSlot != null && !isCreativeMode)
                    {
                        selectedSlot.amount--;
                        if (selectedSlot.amount <= 0)
                        {
                            Hotbar.Instance.SetSlot(Hotbar.Instance.SelectedIndex, null, 0);
                        }
                        else
                        {
                            Hotbar.Instance.SetSlot(Hotbar.Instance.SelectedIndex, selectedSlot.item, selectedSlot.amount);
                        }
                    }

                    isEating = false;
                    eatingTimer = 0f;
                    if (eatingProgressPanel != null) eatingProgressPanel.SetActive(false);

                    if (suffocationOverlay != null)
                    {
                        StartCoroutine(FlashEatingGreen());
                    }
                }
            }
        }
        else
        {
            if (isEating)
            {
                isEating = false;
                eatingTimer = 0f;
                if (eatingProgressPanel != null) eatingProgressPanel.SetActive(false);
            }
        }
    }

    private System.Collections.IEnumerator FlashEatingGreen()
    {
        if (suffocationOverlay == null) yield break;
        Color oldColor = suffocationOverlay.color;
        suffocationOverlay.color = new Color(0.1f, 0.6f, 0.1f, 0.4f);
        suffocationOverlay.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.15f);
        suffocationOverlay.gameObject.SetActive(false);
        suffocationOverlay.color = oldColor;
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

        // Reset survival states
        currentHealth = maxHealth;
        currentHunger = maxHunger;
        currentAir = maxAir;
        drowningTimer = 0f;
        healthRegenTimer = 0f;
        starvationDamageTimer = 0f;
        isEating = false;
        if (eatingProgressPanel != null) eatingProgressPanel.SetActive(false);

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
        if (rb == null) return;
        if (hit.moveDirection.y < -0.3f) return;

        VehicleController vc = rb.GetComponent<VehicleController>();
        if (vc != null)
        {
            vc.NotifyPushed();
        }

        // Ignore push force if the rigidbody is kinematic (it will unfreeze in the next fixed update, allowing pushing on subsequent contacts)
        if (rb.isKinematic) return;

        // Push direction = horizontal movement direction of the player
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);

        // Push proportional to player speed — feel free to tune the multiplier
        float pushForce = 3f;
        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }

    /// <summary>
    /// Checks if there is a solid block or physics collider below the given position.
    /// Used to prevent sneaking off edges.
    /// </summary>
    private bool HasGroundUnder(Vector3 pos)
    {
        // Calculate the exact bottom (feet) of the CharacterController at the target position
        Vector3 feetPos = pos;
        if (controller != null)
        {
            feetPos = pos + controller.center + Vector3.down * (controller.height * 0.5f);
        }

        // Using a tiny check radius (0.05f) ensures the player stops exactly at the ledge,
        // keeping the controller firmly supported so they never slip off and lose grounding.
        float radius = 0.05f;
        float yOffset = -0.3f; // Safely check the block below the feet
        Vector3[] checkPoints = new Vector3[] {
            feetPos + new Vector3(0f, yOffset, 0f),
            feetPos + new Vector3(radius, yOffset, 0f),
            feetPos + new Vector3(-radius, yOffset, 0f),
            feetPos + new Vector3(0f, yOffset, radius),
            feetPos + new Vector3(0f, yOffset, -radius),
            feetPos + new Vector3(radius * 0.7f, yOffset, radius * 0.7f),
            feetPos + new Vector3(-radius * 0.7f, yOffset, radius * 0.7f),
            feetPos + new Vector3(radius * 0.7f, yOffset, -radius * 0.7f),
            feetPos + new Vector3(-radius * 0.7f, yOffset, -radius * 0.7f)
        };

        if (VoxelWorld.Instance != null)
        {
            foreach (var pt in checkPoints)
            {
                byte block = VoxelWorld.Instance.GetBlock(pt);
                if (block != 0 && block != 7) 
                {
                    return true;
                }
            }
        }

        // Physics check fallback (e.g. for vehicle colliders or custom meshes)
        // Start raycast slightly above the feet and point down, ignoring the player's own collider
        RaycastHit hit;
        Vector3 rayStart = feetPos + new Vector3(0f, 0.05f, 0f);
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 0.3f))
        {
            if (hit.collider != controller && !hit.collider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 FindNearestBeachSpawn()
    {
        if (SaveLoadManager.Instance == null) return transform.position;

        float seedOffsetX = SaveLoadManager.worldSeedOffsetX;
        float seedOffsetZ = SaveLoadManager.worldSeedOffsetZ;
        int chunkHeight = VoxelData.ChunkHeight;
        float seaLevel = Mathf.Floor(chunkHeight * 0.22f); // 28

        // Helper function for 2D noise (Perlin)
        float Noise2D(float x, float z)
        {
            float val = Unity.Mathematics.noise.cnoise(new Unity.Mathematics.float2(x, z)) * 0.5f + 0.5f;
            return Mathf.Clamp01(val);
        }

        // Spiral search outwards
        // We'll search in steps of 8 blocks up to a radius of 1200 blocks to guarantee finding a beach
        for (int r = 0; r < 1200; r += 8)
        {
            // Check points on a square ring of radius r
            for (int angleDeg = 0; angleDeg < 360; angleDeg += 30)
            {
                float rad = angleDeg * Mathf.Deg2Rad;
                float globalX = Mathf.Round(Mathf.Cos(rad) * r);
                float globalZ = Mathf.Round(Mathf.Sin(rad) * r);

                float ox = globalX + seedOffsetX;
                float oz = globalZ + seedOffsetZ;

                // Base rolling noise
                float baseNoise = Noise2D(ox * 0.02f, oz * 0.02f);
                baseNoise = Mathf.Pow(baseNoise, 2.5f);
                
                float detailNoise = Noise2D(ox * 0.08f, oz * 0.08f) * 0.15f;
                float finalNoise = baseNoise + detailNoise;
                float exactHeight = finalNoise * chunkHeight * 0.25f + (chunkHeight / 5f);
                
                float continentNoise = Noise2D(ox * 0.00015f + 1234.5f, oz * 0.00015f + 5678.9f);
                float startScale = Mathf.Clamp01(Mathf.Max(Mathf.Abs(globalX), Mathf.Abs(globalZ)) / 300f);
                continentNoise = Mathf.Lerp(0.35f, continentNoise, startScale);

                float heightOffset = 0f;
                if (continentNoise < 0.45f)
                {
                    float dryT = (0.45f - continentNoise) / 0.45f;
                    heightOffset = dryT * 18f;
                }
                else if (continentNoise > 0.55f)
                {
                    float oceanT = (continentNoise - 0.55f) / 0.45f;
                    heightOffset = -oceanT * 22f;
                }

                // Check biomes
                float biomeNoise = Noise2D(ox * 0.003f + 4000f, oz * 0.003f + 8000f);
                int biome = 2;
                if (continentNoise > 0.55f) biome = 0;
                else if (biomeNoise < 0.25f) biome = 1;
                else if (biomeNoise < 0.50f) biome = 2;
                else biome = 3;

                float landHeight = 0f;
                if (biomeNoise < 0.20f)
                {
                    float duneN = (Noise2D(ox * 0.01f, oz * 0.01f) + Noise2D(ox * 0.03f, oz * 0.03f) * 0.25f) / 1.25f;
                    landHeight = (Mathf.Pow(duneN, 2f) * chunkHeight * 0.26f) + (chunkHeight * 0.22f) + heightOffset;
                }
                else if (biomeNoise < 0.30f)
                {
                    float t = (biomeNoise - 0.20f) / 0.10f;
                    t = t * t * (3f - 2f * t);
                    float duneN = (Noise2D(ox * 0.01f, oz * 0.01f) + Noise2D(ox * 0.03f, oz * 0.03f) * 0.25f) / 1.25f;
                    float desertH = (Mathf.Pow(duneN, 2f) * chunkHeight * 0.26f) + (chunkHeight * 0.22f) + heightOffset;
                    float plainsH = (Noise2D(ox * 0.01f, oz * 0.01f) * chunkHeight * 0.03f) + (chunkHeight * 0.22f) + heightOffset;
                    landHeight = Mathf.Lerp(desertH, plainsH, t);
                }
                else if (biomeNoise < 0.45f) landHeight = (Noise2D(ox * 0.01f, oz * 0.01f) * chunkHeight * 0.03f) + (chunkHeight * 0.22f) + heightOffset;
                else if (biomeNoise < 0.55f)
                {
                    float t = (biomeNoise - 0.45f) / 0.10f;
                    t = t * t * (3f - 2f * t);
                    float plainsH = (Noise2D(ox * 0.01f, oz * 0.01f) * chunkHeight * 0.03f) + (chunkHeight * 0.22f) + heightOffset;
                    float forestH = finalNoise * chunkHeight * 0.20f + (chunkHeight * 0.21f) + heightOffset;
                    landHeight = Mathf.Lerp(plainsH, forestH, t);
                }
                else landHeight = finalNoise * chunkHeight * 0.20f + (chunkHeight * 0.21f) + heightOffset;

                float oceanFloorNoise = Noise2D(ox * 0.015f, oz * 0.015f);
                float oceanDetail = Noise2D(ox * 0.06f, oz * 0.06f) * 2.5f;
                float oceanHeight = 12f + (oceanFloorNoise * 7f) + oceanDetail;

                float oceanWeight = Mathf.Clamp01((continentNoise - 0.45f) / 0.12f);
                oceanWeight = oceanWeight * oceanWeight * (3f - 2f * oceanWeight);
                exactHeight = Mathf.Lerp(landHeight, oceanHeight, oceanWeight);
                exactHeight = Mathf.Max(2f, exactHeight);

                float warpX = Noise2D(ox * 0.015f + 10f, oz * 0.015f + 20f) * 60f;
                float warpZ = Noise2D(ox * 0.015f + 30f, oz * 0.015f + 40f) * 60f;
                float riverNoise = Noise2D((ox + warpX) * 0.002f + 400f, (oz + warpZ) * 0.002f + 800f);
                float riverCenterDist = Mathf.Abs(riverNoise - 0.5f);
                float widthNoise = Noise2D(ox * 0.01f + 150f, oz * 0.01f + 250f);
                float riverWidth = 0.01f + widthNoise * 0.008f;
                float riverStrength = 0f;
                if (continentNoise < 0.45f) riverStrength = Mathf.Clamp01((continentNoise - 0.35f) / 0.10f);
                else if (continentNoise > 0.55f) riverStrength = Mathf.Clamp01((0.65f - continentNoise) / 0.10f);
                bool isRiver = (riverStrength > 0f) && (riverCenterDist < riverWidth);

                if (isRiver)
                {
                    float riverFactor = Mathf.Clamp01(riverCenterDist / riverWidth);
                    riverFactor = riverFactor * riverFactor * (3f - 2f * riverFactor);
                    float riverbedHeight = seaLevel - (3.5f * riverStrength);
                    exactHeight = Mathf.Min(exactHeight, Mathf.Lerp(riverbedHeight, exactHeight, riverFactor));
                }
                else
                {
                    float minHeight = Mathf.Lerp(seaLevel + 1.5f, 2f, oceanWeight);
                    exactHeight = Mathf.Max(minHeight, exactHeight);
                }

                // Lower beach height check
                float beachNoiseVal = Noise2D(ox * 0.1f, oz * 0.1f);
                if (exactHeight <= seaLevel + 1.6f && exactHeight >= seaLevel && beachNoiseVal > 0.0f)
                {
                    exactHeight -= 1.0f;
                }

                int floorY = Mathf.FloorToInt(exactHeight);

                // Is it beach sand?
                if (!isRiver && biome != 1 && floorY <= seaLevel + 1 && floorY >= seaLevel)
                {
                    return new Vector3(globalX + 0.5f, floorY + 2.5f, globalZ + 0.5f);
                }
            }
        }

        // Fallback
        return new Vector3(0.5f, 35f, 0.5f);
    }
}
