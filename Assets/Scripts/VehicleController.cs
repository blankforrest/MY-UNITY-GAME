using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// STUB: Added to every spawned vehicle GameObject.
/// Replace this body with real driving input, engine forces, and steering logic.
///
/// The vehicle's Rigidbody is on this same GameObject — use GetComponent<Rigidbody>()
/// to apply forces.
/// </summary>
public class VehicleController : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField] private float lightVehicleDrag = 2f;
    [SerializeField] private float lightVehicleAngularDrag = 3f;
    [SerializeField] private float mediumVehicleDrag = 1.2f;
    [SerializeField] private float mediumVehicleAngularDrag = 2f;
    [SerializeField] private float heavyVehicleDrag = 0.6f;
    [SerializeField] private float heavyVehicleAngularDrag = 1.2f;

    [Header("Driving Settings")]
    public bool isBeingControlled = false;
    public float bruteStrengthForce  = 800f;
    public float bruteStrengthTorque = 800f;  // doubled for faster turning

    private Rigidbody _rb;
    public Vector3 CurrentVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;

    [HideInInspector] public bool isRestoredFromSave = false;
    [HideInInspector] public Vector3 savedLinearVelocity = Vector3.zero;
    [HideInInspector] public Vector3 savedAngularVelocity = Vector3.zero;

    private List<WheelBlock> registeredWheels = new List<WheelBlock>();
    public float totalWheelForce = 0f;
    public int groundedWheelCount = 0;
    private bool wasGrounded = false;
    private bool _isGroundedWithoutWheels = false;

    [Header("Step Climbing")]
    [Tooltip("Max step height the vehicle can climb (in world units). 1.05 = one full voxel block.")]
    public float maxStepHeight = 1.05f;
    [Tooltip("Upward velocity (m/s) injected when climbing a full block. Mass-independent.")]
    public float stepClimbSpeed = 10f;
    [Tooltip("How far ahead (m) to probe for a step wall. Larger = detects earlier = smoother.")]
    public float stepProbeDistance = 1.8f;

    // True while the vehicle is actively climbing a step this FixedUpdate
    private bool _isClimbing = false;
    // True if the vehicle is submerged/floating in water
    private bool _isInWater = false;
    // Combined local bounds of all block colliders for exit positioning
    private Bounds _localBounds;

    [Header("Dry Interior Settings")]
    [Tooltip("The local bounds inside the vehicle that should remain dry. Calculated automatically if not set.")]
    public Bounds dryBounds;
    [Tooltip("List of multiple dry interior box filters.")]
    public List<Bounds> dryFilters = new List<Bounds>();

    // Static list of all active vehicles for water clipping
    private static List<VehicleController> _activeVehicles = new List<VehicleController>();

    // Player CharacterController — cached so we can toggle IgnoreCollision without a scene search
    private CharacterController _playerCC;
    // Previous control state — used to detect the exact frame isBeingControlled changes
    private bool _wasControlled = false;
    // Cached layer mask — excludes the vehicle itself so wheels don't detect their own colliders
    private int _stepLayerMask = ~0;
    // How long (seconds) the vehicle has been nearly stationary — used to detect stuck-in-ravine state
    private float _stuckTimer = 0f;
    private const float StuckVelocityThreshold = 0.3f;
    private const float StuckTimeLimit = 1.5f;

    private Vector3 _playerSeatOffset;
    private float _lastVehicleYaw;

    private float _lastPushedTime = -99f;
    private float _lastControlledTime = -99f;

    public void NotifyPushed()
    {
        _lastPushedTime = Time.time;
    }

    // ── Buoyancy ──────────────────────────────────────────────────────────────
    [Header("Buoyancy")]
    [Tooltip("World-space Y of the water surface.")]
    public float waterLevel = 14f;
    [Tooltip("Upward force (N) applied per fully-submerged child block. Scales with submersion ratio.")]
    public float buoyancyForcePerBlock = 800f;
    [Tooltip("Linear drag coefficient applied while any block is in water (ForceMode.Acceleration).")]
    public float waterLinearDrag = 0.8f;
    [Tooltip("Angular drag coefficient applied while any block is in water.")]
    public float waterAngularDrag = 0.6f;

    // Cached child block world-space positions (updated each FixedUpdate)
    private Transform[] _hullBlocks;

    private Vector3 _localForward = Vector3.forward;
    private bool _hasPropeller = false;

    public Vector3 LocalForward => _localForward;
    public bool HasPropeller => _hasPropeller;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Always derive from world constants — the serialised default 14 is wrong for a 128-high world
        waterLevel = Mathf.Floor(VoxelData.ChunkHeight * 0.22f);
    }


    public void RegisterWheel(WheelBlock wheel)
    {
        if (!registeredWheels.Contains(wheel))
        {
            registeredWheels.Add(wheel);
            
            totalWheelForce = 0f;
            foreach (var w in registeredWheels) totalWheelForce += w.forceContribution;
            
            Debug.Log($"Wheel registered. Total force: {totalWheelForce}");
        }
    }

    private void Start()
    {
        Debug.Log($"[VC.Start] childCount={transform.childCount}, " +
                  $"rb={(_rb != null ? "found" : "NULL")}, " +
                  $"BoxColliders={GetComponentsInChildren<BoxCollider>().Length}");
        if (_rb == null) return;

        // 1. BOUNDS & CENTER OF MASS
        //    Use ALL collider types (box + sphere) so wheel SphereColliders are included.
        Vector3 centerSum = Vector3.zero;
        int childCount = transform.childCount;
        float minBoundY = float.MaxValue;

        Collider[] allColliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < childCount; i++)
            centerSum += transform.GetChild(i).localPosition;

        foreach (var col in allColliders)
        {
            Bounds b = col.bounds;
            float localBottom = transform.InverseTransformPoint(b.min).y;
            if (localBottom < minBoundY) minBoundY = localBottom;
        }

        if (minBoundY == float.MaxValue) minBoundY = -0.5f;

        if (childCount > 0)
        {
            Vector3 averageCenter = centerSum / childCount;
            averageCenter.y = minBoundY - 0.5f;
            _rb.centerOfMass = averageCenter;
        }

        // Cache hull block transforms for buoyancy calculations.
        // Every child collider (excluding SphereCollider wheels) is a buoyancy point.
        var hullList = new System.Collections.Generic.List<Transform>();
        foreach (var col in allColliders)
        {
            if (col is SphereCollider) continue;
            hullList.Add(col.transform);
        }
        _hullBlocks = hullList.ToArray();

        CacheVehicleSettings();

        // Heavy mech physics — Iron Giant feel
        _rb.mass             = Mathf.Max(50f, _rb.mass * 5f); // much heavier
        _rb.linearDamping    = 1.0f;   // sluggish, weighty
        _rb.angularDamping   = 3.5f;  // reduced slightly so turning input is more responsive
        _rb.sleepThreshold   = 0.0f;
        _rb.maxAngularVelocity = 1.0f; // nearly impossible to tip permanently

        // ── Friction materials ────────────────────────────────────────────────
        // Wheels (SphereColliders) get zero friction so they glide over terrain seams.
        // Body blocks (BoxColliders) keep normal friction so the player CharacterController
        // can register solid contact and NOT pass through the vehicle.
        PhysicsMaterial glideMaterial = new PhysicsMaterial("VehicleGlide")
        {
            dynamicFriction = 0.0f,
            staticFriction  = 0.0f,
            bounciness      = 0.0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine   = PhysicsMaterialCombine.Minimum
        };

        PhysicsMaterial bodyMaterial = new PhysicsMaterial("VehicleBody")
        {
            dynamicFriction = 0.5f,
            staticFriction  = 0.5f,
            bounciness      = 0.0f,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine   = PhysicsMaterialCombine.Minimum
        };

        // ALL child colliders (wheels + body blocks) get zero-friction glide material.
        // Body block BoxColliders would otherwise create horizontal contact forces when
        // hitting terrain step faces, blocking climbing even with _rb.position lift.
        // Player collision is handled exclusively by the root BoxCollider below.
        foreach (var col in allColliders)
            col.sharedMaterial = glideMaterial;

        // Initialize local bounds and dry interior filters
        InitializeDryFilters();


        // Cache the player CharacterController for targeted IgnoreCollision calls.
        if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
            _playerCC = VoxelWorld.Instance.playerTransform.GetComponent<CharacterController>();

        // While parked at spawn, player is outside — collision should be active.
        // (SetPlayerCollision(true) = normal collision, false = ignore)
        SetPlayerCollision(true);
        _wasControlled = false;

        // Build step-climb layer mask: exclude this vehicle's own layer
        int vehicleLayer = LayerMask.NameToLayer("Vehicle");
        _stepLayerMask = (vehicleLayer != -1) ? ~(1 << vehicleLayer) : ~0;

        // Ignore physics collision with all foliage MeshColliders in already-loaded chunks.
        // New chunks call IgnorePlayerCollision() when built, which now covers active vehicles too.
        Collider[] vehicleColliders = GetComponentsInChildren<Collider>();
        foreach (var chunk in Object.FindObjectsByType<Chunk>(FindObjectsSortMode.None))
            chunk.IgnoreFoliageCollisionWith(vehicleColliders);

        // Start delayed unfreeze to let chunk colliders finish baking
        StartCoroutine(UnfreezePhysicsDelayed());
    }

    private System.Collections.IEnumerator UnfreezePhysicsDelayed()
    {
        if (isRestoredFromSave)
        {
            // Give chunks ample time to generate and bake colliders
            yield return new WaitForSeconds(1.5f);
        }
        else
        {
            // Wait 4 fixed updates to ensure Unity has fully baked the new chunk MeshColliders
            // and any initial physics frame collisions have been resolved.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        if (_rb != null)
        {
            // Re-cache hull and forward settings after instantiation completes
            CacheVehicleSettings();

            // Run a quick check for non-wheeled grounding
            UpdateGroundedWithoutWheels();

            bool isResting = _isInWater || wasGrounded || _isGroundedWithoutWheels;

            if (isResting && !isRestoredFromSave)
            {
                _rb.isKinematic = true;
                _lastPushedTime = 0f;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            else
            {
                _rb.isKinematic = false;
                _lastPushedTime = isRestoredFromSave ? Time.time + 0.5f : 0f;
                if (isRestoredFromSave)
                {
                    _rb.linearVelocity = savedLinearVelocity;
                    _rb.angularVelocity = savedAngularVelocity;
                }
                else
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }
        }

        if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
        {
            PlayerController player = VoxelWorld.Instance.playerTransform.GetComponent<PlayerController>();
            player?.SetFrozen(false);
        }

        // Trigger the water mesh rebuild now that the vehicle is stable
        RefreshNearbyChunks(transform.position);
    }

    private void Update()
    {
        bool justBoarded = false;
        // Detect control-state transitions
        if (isBeingControlled != _wasControlled)
        {
            CharacterController playerCC = GetPlayerCC();
            if (isBeingControlled)
            {
                justBoarded = true;
                SetPlayerCollision(false);

                if (playerCC != null)
                {
                    // ── Camera-tilt fix ───────────────────────────────────────
                    // Do NOT parent the player to the vehicle. Parenting propagates
                    // the full vehicle rotation (pitch + roll) to the camera, making
                    // the view tilt whenever the boat rocks.
                    //
                    // Instead, record the player's offset from the vehicle expressed
                    // in yaw-only space. We resync the position every frame below,
                    // using only the vehicle's Y-axis rotation so the player follows
                    // turning but never inherits pitch or roll.
                    playerCC.enabled = false;

                    // 1. Calculate the seat offset in the boat's INITIAL rotation space BEFORE snapping
                    Quaternion initialYawOnly = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    _playerSeatOffset = Quaternion.Inverse(initialYawOnly) *
                                        (playerCC.transform.position - transform.position);

                    // 2. Snap the boat to face the camera's horizontal forward when boarding,
                    // so W immediately drives in the direction the player is looking.
                    if (registeredWheels.Count == 0 && _rb != null)
                    {
                        Camera cam = Camera.main;
                        if (cam != null)
                        {
                            Vector3 cf = cam.transform.forward; cf.y = 0f;
                            if (cf.sqrMagnitude > 0.001f)
                            {
                                cf.Normalize();
                                float yaw = Mathf.Atan2(cf.x, cf.z) * Mathf.Rad2Deg;
                                
                                // Offset the vehicle's transform yaw so its actual local forward matches the camera
                                Vector3 localFwd = GetLocalForwardDirection();
                                float offsetAngle = Mathf.Atan2(localFwd.x, localFwd.z) * Mathf.Rad2Deg;
                                
                                transform.rotation = Quaternion.Euler(0f, yaw - offsetAngle, 0f);
                                _rb.rotation = transform.rotation;
                                _rb.angularVelocity = Vector3.zero;
                                _rb.linearVelocity = Vector3.zero;
                            }
                        }
                    }

                    _lastVehicleYaw = transform.eulerAngles.y;
                }
            }
            else
            {
                if (playerCC != null)
                {
                    // Re-enable the CharacterController in-place — the player stays exactly
                    // where they were sitting. No teleport, no forced exit position.
                    playerCC.enabled = true;
                }
                StartCoroutine(RestorePlayerCollisionDelayed());
                _lastControlledTime = Time.time;
            }
            _wasControlled = isBeingControlled;
        }

        // ── Per-frame player position sync while riding ────────────────────────
        if (isBeingControlled)
        {
            CharacterController playerCC = GetPlayerCC();
            if (playerCC != null)
            {
                float currentVehicleYaw = transform.eulerAngles.y;
                if (_wasControlled && !justBoarded)
                {
                    float deltaYaw = Mathf.DeltaAngle(_lastVehicleYaw, currentVehicleYaw);
                    playerCC.transform.Rotate(Vector3.up * deltaYaw);
                }
                _lastVehicleYaw = currentVehicleYaw;

                Quaternion yawOnly = Quaternion.Euler(0f, currentVehicleYaw, 0f);
                playerCC.transform.position = transform.position + yawOnly * _playerSeatOffset;
            }
        }

        if (_isInWater)
        {
            Vector3 euler = transform.eulerAngles;
            transform.eulerAngles = new Vector3(0f, euler.y, 0f);
        }
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        // ── NaN / Infinite velocity guard ─────────────────────────────────────
        // If any velocity component goes NaN or Infinite (usually from a bad force
        // applied by a child block), reset it immediately to stop the cascade.
        Vector3 vel = _rb.linearVelocity;
        if (float.IsNaN(vel.x) || float.IsNaN(vel.y) || float.IsNaN(vel.z) ||
            float.IsInfinity(vel.x) || float.IsInfinity(vel.y) || float.IsInfinity(vel.z))
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            Debug.LogWarning("[VehicleController] NaN/Infinite velocity detected and reset on " + name);
        }

        // ── Buoyancy for all hull blocks ──────────────────────────────────────
        // Centralized buoyancy: every child block below waterLevel contributes an
        // upward force proportional to how deeply it is submerged. This makes any
        // voxel hull (including plank-only boats) float without needing a PropellerBlock.
        int submergedBlockCount = 0;
        if (_hullBlocks != null && _hullBlocks.Length > 0)
        {
            // Scale buoyancy force dynamically based on Rigidbody mass to guarantee any vehicle floats
            float forcePerBlock = Mathf.Max(buoyancyForcePerBlock, (_rb.mass * 15f) / _hullBlocks.Length);

            foreach (var block in _hullBlocks)
            {
                if (block == null) continue;
                float depth = waterLevel - block.position.y;
                if (depth <= 0f) continue;

                float ratio = Mathf.Clamp01(depth / 1.0f);
                if (!_rb.isKinematic)
                {
                    _rb.AddForceAtPosition(
                        Vector3.up * forcePerBlock * ratio,
                        block.position,
                        ForceMode.Force);
                }
                submergedBlockCount++;
            }
        }
        _isInWater = submergedBlockCount > 0;

        // --- GROUNDED CHECK ---
        int groundedNow = 0;
        foreach (var w in registeredWheels)
            if (w.isGrounded) groundedNow++;

        groundedWheelCount = groundedNow;
        wasGrounded = groundedWheelCount > 0;

        UpdateGroundedWithoutWheels();

        // --- KINEMATIC & STABILIZATION SYSTEM ---
        bool isPushed = (Time.time - _lastPushedTime) < 0.2f;
        bool isBeingDriven = isBeingControlled && IsInputActive();

        bool canMove = _hasPropeller && isBeingDriven;
        bool isResting = _isInWater || wasGrounded || _isGroundedWithoutWheels;

        // If it's resting on land or water, and we are not actively driving it or pushing it, freeze instantly!
        bool shouldBeDynamic = canMove || isPushed || !isResting;

        if (shouldBeDynamic)
        {
            _rb.isKinematic = false;
            
            // Adjust damping based on water vs land controlled state:
            if (isBeingControlled)
            {
                _rb.linearDamping = _isInWater ? 0.3f : 1.0f;
                _rb.angularDamping = _isInWater ? 0.8f : 3.5f;
            }
            else
            {
                // Normal unpiloted damping while moving/sliding/falling
                _rb.linearDamping = _isInWater ? 0.3f : 0.05f;
                _rb.angularDamping = _isInWater ? 0.8f : 0.05f;
            }
        }
        else
        {
            // Freeze completely instantly!
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // If kinematic, skip applying forces (downforce, extra gravity, gyros, step climbing, driving)
        if (_rb.isKinematic) return;

        // Water drag: damp motion when partially submerged so the boat doesn't oscillate
        if (_isInWater)
        {
            _rb.AddForce(-_rb.linearVelocity  * waterLinearDrag,  ForceMode.Acceleration);
            _rb.AddTorque(-_rb.angularVelocity * waterAngularDrag, ForceMode.Acceleration);

            // Keep the boat perfectly level (0 tilt on X and Z)
            Vector3 euler = transform.eulerAngles;
            transform.eulerAngles = new Vector3(0f, euler.y, 0f);

            Vector3 angVel = _rb.angularVelocity;
            _rb.angularVelocity = new Vector3(0f, angVel.y, 0f);
        }

        // --- DOWNFORCE: keeps wheels on ground at speed (skip in water) ---
        float speed = Mathf.Min(_rb.linearVelocity.magnitude, 100f);
        if (!_isInWater)
            _rb.AddForce(-transform.up * speed * speed * 2.5f * _rb.mass * Time.fixedDeltaTime,
                         ForceMode.Force);

        // --- EXTRA GRAVITY WHEN AIRBORNE (skip when floating — boat wheels never touch ground) ---
        // Only apply the heavy 3G stomping force when truly airborne over land, never in water or when flying/swimming (has propellers)
        bool isFlying = _hasPropeller;
        if (registeredWheels.Count > 0 && groundedNow == 0 && !_isInWater && !isFlying)
            _rb.AddForce(Vector3.down * _rb.mass * 29.4f, ForceMode.Force);

        // --- ANTI-FLIP GYROSCOPE ---
        float angle = Vector3.Angle(Vector3.up, transform.up);
        if (angle > 20f)
        {
            Vector3 cross = Vector3.Cross(transform.up, Vector3.up);
            _rb.AddTorque(cross * angle * _rb.mass * 3f);
        }

        // --- STEP CLIMBING ---
        TryClimbStep();

        // Early-exit when no player is controlling — keeps the boat stationary at rest
        if (!isBeingControlled) return;

        // ── WATERCRAFT DRIVING ────────────────────────────────────────────────
        if (_isInWater)
        {
            // If there is no propeller, the boat must not move
            if (!_hasPropeller) return;

            bool fwd = false, bwd = false, lft = false, rgt = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                fwd = UnityEngine.InputSystem.Keyboard.current.wKey.isPressed || UnityEngine.InputSystem.Keyboard.current.upArrowKey.isPressed;
                bwd = UnityEngine.InputSystem.Keyboard.current.sKey.isPressed || UnityEngine.InputSystem.Keyboard.current.downArrowKey.isPressed;
                lft = UnityEngine.InputSystem.Keyboard.current.aKey.isPressed || UnityEngine.InputSystem.Keyboard.current.leftArrowKey.isPressed;
                rgt = UnityEngine.InputSystem.Keyboard.current.dKey.isPressed || UnityEngine.InputSystem.Keyboard.current.rightArrowKey.isPressed;
            }
#else
            fwd = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bwd = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            lft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            rgt = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
#endif
            // Use the calculated vehicle local forward direction to support arbitrary build orientations
            Vector3 localFwd = GetLocalForwardDirection();
            Vector3 localRgt = Vector3.Cross(Vector3.up, localFwd);

            Vector3 thrustDir = transform.TransformDirection(localFwd);
            thrustDir.y = 0f;
            if (thrustDir.sqrMagnitude > 0.001f) thrustDir.Normalize();

            float fwdSpeed   = Vector3.Dot(_rb.linearVelocity, thrustDir);
            float boatThrust = bruteStrengthForce * 0.55f;

            if (fwd && fwdSpeed <  12f) _rb.AddForce( thrustDir * boatThrust, ForceMode.Force);
            if (bwd && fwdSpeed > -6f)  _rb.AddForce(-thrustDir * boatThrust, ForceMode.Force);

            // Yaw steering rotates the boat around the world Y axis
            float boatTorque = bruteStrengthTorque * 0.45f;
            if (lft) _rb.AddTorque(-Vector3.up * boatTorque, ForceMode.Force);
            if (rgt) _rb.AddTorque( Vector3.up * boatTorque, ForceMode.Force);

            // Lateral drag: cancel sideways slip relative to the boat's heading
            Vector3 lateralDir = transform.TransformDirection(localRgt);
            lateralDir.y = 0f;
            if (lateralDir.sqrMagnitude > 0.001f) lateralDir.Normalize();
            float lateralSpeed = Vector3.Dot(_rb.linearVelocity, lateralDir);
            _rb.AddForce(-lateralDir * lateralSpeed * _rb.mass * 6f, ForceMode.Force);

            // Clamp vertical velocity so the boat can't dive or rocket upward
            Vector3 v = _rb.linearVelocity;
            v.y = Mathf.Clamp(v.y, -2f, 2f);
            _rb.linearVelocity = v;

            return; // skip land-driving code below
        }


        bool forward = false, backward = false, left = false, right = false;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            forward = UnityEngine.InputSystem.Keyboard.current.wKey.isPressed || UnityEngine.InputSystem.Keyboard.current.upArrowKey.isPressed;
            backward = UnityEngine.InputSystem.Keyboard.current.sKey.isPressed || UnityEngine.InputSystem.Keyboard.current.downArrowKey.isPressed;
            left = UnityEngine.InputSystem.Keyboard.current.aKey.isPressed || UnityEngine.InputSystem.Keyboard.current.leftArrowKey.isPressed;
            right = UnityEngine.InputSystem.Keyboard.current.dKey.isPressed || UnityEngine.InputSystem.Keyboard.current.rightArrowKey.isPressed;
        }
#else
        forward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        backward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
#endif

        // Track stuck state: if nearly stationary for StuckTimeLimit seconds, allow driving
        // even with no grounded wheels (vehicle may be wedged in a ravine with suspended wheels)
        bool isVehicleStuck = false;
        if (registeredWheels.Count > 0 && groundedWheelCount == 0)
        {
            if (_rb.linearVelocity.magnitude < StuckVelocityThreshold)
                _stuckTimer += Time.fixedDeltaTime;
            else
                _stuckTimer = 0f;

            isVehicleStuck = _stuckTimer >= StuckTimeLimit;

            // Not stuck yet — block driving input (normal airborne behavior)
            if (!isVehicleStuck) return;
        }
        else
        {
            _stuckTimer = 0f; // reset whenever wheels are grounded
        }

        if (registeredWheels.Count > 0)
        {
            float forceMultiplier = totalWheelForce;

            float actualForce  = bruteStrengthForce  * forceMultiplier;
            float actualTorque = bruteStrengthTorque  * forceMultiplier;

            float currentForwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);

            // Cap top speed and apply forces
            if (forward && currentForwardSpeed < 18f)  
                _rb.AddForce(transform.forward * actualForce);
                
            if (backward && currentForwardSpeed > -10f) 
                _rb.AddForce(-transform.forward * actualForce);

            // Braking: apply opposing force instead of directly setting velocity
            if (!forward && !backward)
            {
                Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
                float brakeForce = -localVel.z * _rb.mass * 8f;
                _rb.AddForce(transform.forward * brakeForce);
            }

            // Steer
            if (left)     _rb.AddTorque(-Vector3.up * actualTorque);
            if (right)    _rb.AddTorque(Vector3.up * actualTorque);
        }
    }

    // ── Step Climbing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fires a fan of horizontal raycasts at 4 heights between the wheel base and
    /// maxStepHeight. If any hit a wall whose top surface is within maxStepHeight,
    /// injects upward + forward velocity (both VelocityChange, mass-independent)
    /// so the vehicle glides over the step without bumping.
    /// </summary>
    private void TryClimbStep()
    {
        if (_isInWater) { _isClimbing = false; return; }

        // Need at least a tiny horizontal movement — lower threshold so a vehicle
        // stalled against a block face (sqMag ~0) can still initiate a climb.
        Vector3 velFlat = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        if (velFlat.sqrMagnitude < 0.01f && !isBeingControlled) { _isClimbing = false; return; }

        // Determine climb direction from the vehicle's chassis heading, not raw velocity,
        // to avoid the probe ray deflecting sideways when the vehicle hits a wall at an angle.
        float localFwdSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
        // If the vehicle is being driven but has barely moved (stalled against the block),
        // default to the chassis forward direction so we can still probe the step.
        Vector3 climbPushDir;
        if (Mathf.Abs(localFwdSpeed) > 0.05f)
            climbPushDir = (localFwdSpeed >= 0f) ? transform.forward : -transform.forward;
        else
            climbPushDir = transform.forward; // default to forward when nearly stopped
        climbPushDir.y = 0f;
        if (climbPushDir.sqrMagnitude < 0.001f) { _isClimbing = false; return; }
        climbPushDir.Normalize();

        // Ground contact plane: use the lowest registered wheel position (most reliable
        // because wheel SphereColliders sit exactly on the terrain surface and their
        // Transform.position doesn't shift when the vehicle tilts or rotates).
        float lowestY = float.MaxValue;
        foreach (var wheel in registeredWheels)
        {
            if (wheel != null)
                lowestY = Mathf.Min(lowestY, wheel.transform.position.y - 0.5f);
        }
        // Fallback: scan child collider bounds when no wheels are registered
        if (lowestY == float.MaxValue)
        {
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                if (col.gameObject == gameObject) continue;
                lowestY = Mathf.Min(lowestY, col.bounds.min.y);
            }
        }
        if (lowestY == float.MaxValue) lowestY = transform.position.y;

        // ── Multi-height wall probes ──────────────────────────────────────────
        float noseOffset    = _localBounds.size.z * 0.5f;
        float probeDistance = noseOffset + stepProbeDistance;

        bool       hitWall    = false;
        RaycastHit wallHit    = default;
        float      lowestHitY = float.MaxValue;

        // Use 6 probe heights for better coverage — finer increments catch narrow steps
        const int probes = 6;
        for (int i = 0; i < probes; i++)
        {
            float dy  = Mathf.Lerp(0.05f, maxStepHeight * 0.9f, (float)i / (probes - 1));
            Vector3 org = new Vector3(transform.position.x, lowestY + dy, transform.position.z);

            RaycastHit[] hits = Physics.RaycastAll(org, climbPushDir, probeDistance,
                                                   _stepLayerMask, QueryTriggerInteraction.Ignore);

            RaycastHit closestValidHit = default;
            bool foundValid = false;
            foreach (var h in hits)
            {
                // Skip own colliders
                if (h.transform == transform || h.transform.IsChildOf(transform))
                    continue;
                // Skip foliage mesh colliders — not solid walls
                if (h.collider != null && h.collider.gameObject.name.Contains("Foliage"))
                    continue;
                // Skip water mesh colliders
                if (h.collider != null && h.collider.gameObject.name.Contains("Water"))
                    continue;

                if (!foundValid || h.distance < closestValidHit.distance)
                {
                    closestValidHit = h;
                    foundValid = true;
                }
            }

            if (!foundValid) continue;

            if (!hitWall || closestValidHit.point.y < lowestHitY)
            {
                lowestHitY = closestValidHit.point.y;
                wallHit    = closestValidHit;
                hitWall    = true;
            }
        }

        if (!hitWall) { _isClimbing = false; return; }

        // ── Step-top detection ────────────────────────────────────────────────
        float   topOriginY = lowestY + maxStepHeight + 0.3f;
        Vector3 topOrg     = new Vector3(
            wallHit.point.x + climbPushDir.x * 0.25f,
            topOriginY,
            wallHit.point.z + climbPushDir.z * 0.25f);

        RaycastHit[] topCandidates = Physics.RaycastAll(topOrg, Vector3.down,
                                     maxStepHeight + 0.5f, _stepLayerMask,
                                     QueryTriggerInteraction.Ignore);
        bool       foundTop = false;
        RaycastHit topHit   = default;
        foreach (var th in topCandidates)
        {
            if (th.transform == transform || th.transform.IsChildOf(transform)) continue;
            if (th.collider != null && th.collider.gameObject.name.Contains("Foliage")) continue;
            if (th.collider != null && th.collider.gameObject.name.Contains("Water")) continue;
            if (!foundTop || th.point.y > topHit.point.y) { topHit = th; foundTop = true; }
        }
        if (!foundTop) { _isClimbing = false; return; }

        float stepH = topHit.point.y - lowestY;
        if (stepH < 0.05f || stepH > maxStepHeight) { _isClimbing = false; return; }

        // ── Climbing confirmed ────────────────────────────────────────────────
        _isClimbing = true;

        // Lift: teleport up by up to stepClimbSpeed * dt per frame
        float liftThisFrame = Mathf.Min(stepH, stepClimbSpeed * Time.fixedDeltaTime);
        _rb.position += Vector3.up * liftThisFrame;

        // Forward nudge: keep a modest minimum forward speed so the vehicle
        // doesn't stall out on the step face. Reduced from 2.5 → 1.5 to avoid
        // overshooting narrow ledges.
        float currentFwd       = Vector3.Dot(_rb.linearVelocity, climbPushDir);
        float minFwdDuringClimb = 1.5f;
        if (currentFwd < minFwdDuringClimb)
            _rb.AddForce(climbPushDir * (minFwdDuringClimb - currentFwd), ForceMode.VelocityChange);
    }

    // ── Player collision helpers ───────────────────────────────────────────────

    /// <summary>
    /// Toggles collision between the player's CharacterController and all the
    /// individual block/wheel colliders on the vehicle.
    ///   enableCollision=true  → normal collision (player can walk on blocks/interior)
    ///   enableCollision=false → no collision     (prevents physics glitches while driving)
    /// </summary>
    private void SetPlayerCollision(bool enableCollision)
    {
        CharacterController playerCC = GetPlayerCC();
        if (playerCC != null)
        {
            Collider[] vehicleColliders = GetComponentsInChildren<Collider>();
            foreach (var col in vehicleColliders)
            {
                if (col == null || col.isTrigger) continue;
                Physics.IgnoreCollision(playerCC, col, !enableCollision);
            }
        }
    }

    /// <summary>
    /// Waits a few fixed frames for the exit animation / position update to move
    /// the player outside the vehicle bounds, then re-enables collision.
    /// </summary>
    private IEnumerator RestorePlayerCollisionDelayed()
    {
        // Wait 3 fixed-update frames — enough for the exit repositioning to fully settle
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        SetPlayerCollision(true);
    }

    /// <summary>
    /// Dynamically resolves and caches the player's CharacterController if not already set.
    /// </summary>
    private CharacterController GetPlayerCC()
    {
        if (_playerCC == null)
        {
            if (VoxelWorld.Instance != null && VoxelWorld.Instance.playerTransform != null)
            {
                _playerCC = VoxelWorld.Instance.playerTransform.GetComponent<CharacterController>();
            }
            else
            {
                _playerCC = Object.FindAnyObjectByType<CharacterController>();
            }
        }
        return _playerCC;
    }

    /// <summary>
    /// Finds a safe position outside the vehicle colliders for the player to exit to.
    /// Returns a clearly open spot and lets the CharacterController fall naturally to
    /// the ground. Does NOT snap to terrain surface (which was causing void-falls).
    /// </summary>
    private Vector3 GetSafeExitPosition()
    {
        // Side clearance: use half the vehicle bounds' longest horizontal dimension
        float halfExtent = Mathf.Max(_localBounds.size.x, _localBounds.size.z) * 0.5f;
        if (halfExtent < 0.5f) halfExtent = 0.5f;
        
        float sideStep = halfExtent + 1.8f; // clear of the hull with room for the player
        float upOffset = 1.2f;              // start above vehicle center, out of geometry

        // Try 4 cardinal directions, then roof
        Vector3[] directions = { -transform.right, transform.right, -transform.forward, transform.forward };

        foreach (var dir in directions)
        {
            Vector3 candidatePos = transform.position + dir * sideStep + Vector3.up * upOffset;

            // Check for solid geometry, but IGNORE this vehicle's own colliders.
            // Vehicle block colliders sit on Default layer, so _stepLayerMask alone
            // can't exclude them — we must filter by transform hierarchy.
            bool blocked = false;
            Collider[] overlaps = Physics.OverlapSphere(candidatePos, 0.45f,
                                      ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in overlaps)
            {
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (col != null && col.gameObject.name.Contains("Foliage")) continue;
                blocked = true;
                break;
            }
            if (blocked) continue;

            // Spot is clear. Elevate slightly and let the CC fall to ground naturally.
            return candidatePos + Vector3.up * 0.5f;
        }

        // Fallback: place on vehicle roof — player will slide or step off
        float roofY = transform.position.y + _localBounds.center.y + _localBounds.size.y * 0.5f + 0.8f;
        return new Vector3(transform.position.x, roofY, transform.position.z);
    }

    // ── Water Clipping & Dry Interior ──────────────────────────────────────────

    // Track the boat's last refreshed position and rotation to trigger updates dynamically
    private Vector3 _lastRefreshPos;
    private Quaternion _lastRefreshRot;

    private void OnEnable()
    {
        if (!_activeVehicles.Contains(this))
            _activeVehicles.Add(this);

        _lastRefreshPos = transform.position;
        _lastRefreshRot = transform.rotation;

        InitializeDryFilters();

        RefreshNearbyChunks(transform.position);
    }

    private void OnDisable()
    {
        _activeVehicles.Remove(this);
        // Restore water in chunks where it was suppressed
        RefreshNearbyChunks(transform.position);
    }

    // Called every frame — keeps water suppressed as the boat navigates
    private void LateUpdate()
    {
        // Detect if the boat has moved or rotated since the last water mesh refresh
        float posDelta = Vector3.Distance(transform.position, _lastRefreshPos);
        float rotDelta = Quaternion.Angle(transform.rotation, _lastRefreshRot);

        // If the boat has moved/rotated by a noticeable amount, refresh the surrounding chunks immediately
        if (posDelta > 0.05f || rotDelta > 0.5f)
        {
            RefreshNearbyChunksCombined(_lastRefreshPos, transform.position);

            _lastRefreshPos = transform.position;
            _lastRefreshRot = transform.rotation;
        }
    }

    private static Vector3Int WorldToVoxel(Vector3 pos)
        => new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));

    private static Vector3 VoxelToWorld(Vector3Int v)
        => new Vector3(v.x + 0.5f, v.y + 0.5f, v.z + 0.5f);


    /// <summary>
    /// Returns true if the given world-space position falls inside this vehicle's
    /// local bounds. Used by Chunk.cs to suppress water mesh inside the hull.
    /// </summary>
    public struct CachedVehicleData
    {
        public Vector3 position;
        public Quaternion inverseRotation;
        public List<Bounds> dryFilters;
    }

    public static List<CachedVehicleData> GetCachedVehicles()
    {
        var list = new List<CachedVehicleData>();
        foreach (var vc in _activeVehicles)
        {
            if (vc == null) continue;
            list.Add(new CachedVehicleData
            {
                position = vc.transform.position,
                inverseRotation = Quaternion.Inverse(vc.transform.rotation),
                dryFilters = vc.dryFilters != null ? new List<Bounds>(vc.dryFilters) : null
            });
        }
        return list;
    }

    public static bool IsWorldPosInsideVehicleCached(Vector3 worldPos, List<CachedVehicleData> cachedVehicles)
    {
        if (cachedVehicles == null) return false;
        for (int i = 0; i < cachedVehicles.Count; i++)
        {
            var cv = cachedVehicles[i];
            // Transform world point to vehicle local space
            Vector3 localPos = cv.inverseRotation * (worldPos - cv.position);
            // Check if position is inside any dry filter
            if (cv.dryFilters != null)
            {
                for (int j = 0; j < cv.dryFilters.Count; j++)
                {
                    if (cv.dryFilters[j].Contains(localPos))
                        return true;
                }
            }
        }
        return false;
    }

    public static bool IsWorldPosInsideVehicle(Vector3 worldPos)
    {
        foreach (var vc in _activeVehicles)
        {
            if (vc == null) continue;
            // Transform world point to vehicle local space
            Vector3 localPos = vc.transform.InverseTransformPoint(worldPos);
            // Check if position is inside any dry filter
            if (vc.dryFilters != null)
            {
                for (int i = 0; i < vc.dryFilters.Count; i++)
                {
                    bool inside = vc.dryFilters[i].Contains(localPos);
                    
                    // Log details to help diagnose the issue
                    // if (Vector3.Distance(worldPos, vc.transform.position) < 6f)
                    // {
                    //     Debug.Log($"[DryFilterCheck] worldPos={worldPos} localPos={localPos} filterBounds={vc.dryFilters[i]} inside={inside}");
                    // }

                    if (inside)
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>Triggers UpdateWaterMeshOnly on every loaded chunk that overlaps the vehicle footprint.</summary>
    private void RefreshNearbyChunks(Vector3 origin)
    {
        if (VoxelWorld.Instance == null) return;
        HashSet<Vector2Int> coords = new HashSet<Vector2Int>();
        AddChunkCoordsForPosition(origin, coords);
        foreach (var coord in coords)
        {
            Chunk c = VoxelWorld.Instance.GetChunkFromChunkPos(new Vector2(coord.x, coord.y));
            c?.UpdateWaterMeshOnly();
        }
    }

    /// <summary>Triggers UpdateWaterMeshOnly on the union of chunks covering both positions A and B, avoiding duplicate updates.</summary>
    private void RefreshNearbyChunksCombined(Vector3 posA, Vector3 posB)
    {
        if (VoxelWorld.Instance == null) return;
        HashSet<Vector2Int> coords = new HashSet<Vector2Int>();
        AddChunkCoordsForPosition(posA, coords);
        AddChunkCoordsForPosition(posB, coords);
        foreach (var coord in coords)
        {
            Chunk c = VoxelWorld.Instance.GetChunkFromChunkPos(new Vector2(coord.x, coord.y));
            c?.UpdateWaterMeshOnly();
        }
    }

    private void AddChunkCoordsForPosition(Vector3 origin, HashSet<Vector2Int> coords)
    {
        Vector3 worldCenter = origin + transform.rotation * _localBounds.center;
        Vector3 ext = _localBounds.extents;

        // Obtain AABB corners in world space
        Vector3[] localCorners = new Vector3[]
        {
            _localBounds.center + new Vector3(-ext.x, -ext.y, -ext.z),
            _localBounds.center + new Vector3(-ext.x, -ext.y,  ext.z),
            _localBounds.center + new Vector3(-ext.x,  ext.y, -ext.z),
            _localBounds.center + new Vector3(-ext.x,  ext.y,  ext.z),
            _localBounds.center + new Vector3( ext.x, -ext.y, -ext.z),
            _localBounds.center + new Vector3( ext.x, -ext.y,  ext.z),
            _localBounds.center + new Vector3( ext.x,  ext.y, -ext.z),
            _localBounds.center + new Vector3( ext.x,  ext.y,  ext.z)
        };

        Vector3 min = origin + transform.rotation * localCorners[0];
        Vector3 max = min;

        for (int i = 1; i < 8; i++)
        {
            Vector3 worldCorner = origin + transform.rotation * localCorners[i];
            min = Vector3.Min(min, worldCorner);
            max = Vector3.Max(max, worldCorner);
        }

        int minCX = Mathf.FloorToInt(min.x / VoxelData.ChunkWidth);
        int maxCX = Mathf.FloorToInt(max.x / VoxelData.ChunkWidth);
        int minCZ = Mathf.FloorToInt(min.z / VoxelData.ChunkWidth);
        int maxCZ = Mathf.FloorToInt(max.z / VoxelData.ChunkWidth);

        for (int cx = minCX; cx <= maxCX; cx++)
        {
            for (int cz = minCZ; cz <= maxCZ; cz++)
            {
                coords.Add(new Vector2Int(cx, cz));
            }
        }
    }

    public void InitializeDryFilters()
    {
        // Calculate the AABB of the vehicle blocks in the vehicle's local space.
        // We use child local positions (which are grid-aligned) to avoid rotation distortion.
        Bounds tb = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Collider>() != null)
            {
                Bounds lb = new Bounds(child.localPosition, Vector3.one);
                if (first) { tb = lb; first = false; }
                else tb.Encapsulate(lb);
            }
        }
        _localBounds = tb;

        // Calculate dry interior filters
        dryFilters = new List<Bounds>();

        // 1. Add primary overall shrunk bounding box filter to guarantee cabin dryness.
        // Shrink horizontally by 1.1 units (0.55 on each side) to stay inside the 1-block thick outer walls.
        if (_localBounds.size.magnitude > 0.1f)
        {
            Vector3 fc = _localBounds.center;
            Vector3 fs = _localBounds.size;
            fs.x = Mathf.Max(0.2f, fs.x - 1.1f);
            fs.z = Mathf.Max(0.2f, fs.z - 1.1f);
            float fbBottom = (fc.y - fs.y * 0.5f) - 1.5f;
            float fbTop    = (fc.y + fs.y * 0.5f) + 2.5f;
            fc.y = (fbBottom + fbTop) * 0.5f;
            fs.y = fbTop - fbBottom;
            dryFilters.Add(new Bounds(fc, fs));
            Debug.Log($"[DryFilters] Added primary overall filter. center={fc} size={fs}");
        }

        // 2. Generate flood-fill filters for multi-cabin details
        GenerateDryFilters();
    }

    private void GenerateDryFilters()
    {
        // 1. Gather all child blocks that have colliders
        var blocks = new List<Transform>();
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        int minZ = int.MaxValue, maxZ = int.MinValue;

        foreach (Transform child in transform)
        {
            // Skip wheel blocks (SphereCollider only) — include everything with a BoxCollider
            if (child.GetComponentInChildren<BoxCollider>() != null &&
                child.GetComponent<SphereCollider>() == null)
            {
                blocks.Add(child);
                Vector3Int lp = Vector3Int.RoundToInt(child.localPosition);
                if (lp.x < minX) minX = lp.x;
                if (lp.x > maxX) maxX = lp.x;
                if (lp.y < minY) minY = lp.y;
                if (lp.y > maxY) maxY = lp.y;
                if (lp.z < minZ) minZ = lp.z;
                if (lp.z > maxZ) maxZ = lp.z;
            }
        }

        if (blocks.Count == 0)
        {
            Debug.LogWarning("[DryFilters] No BoxCollider blocks found on vehicle — cannot generate filters!");
            return;
        }

        Debug.Log($"[DryFilters] Found {blocks.Count} hull blocks. Grid size: ({maxX-minX+1}, {maxY-minY+1}, {maxZ-minZ+1})");

        // 2. Create the grid representation
        int sizeX = maxX - minX + 1;
        int sizeY = maxY - minY + 1;
        int sizeZ = maxZ - minZ + 1;

        bool[,,] isSolid = new bool[sizeX, sizeY, sizeZ];
        foreach (var block in blocks)
        {
            Vector3Int lp = Vector3Int.RoundToInt(block.localPosition);
            isSolid[lp.x - minX, lp.y - minY, lp.z - minZ] = true;
        }

        // 3. Find interior empty voxels (bounded on bottom, left, right, front, back)
        bool[,,] isInterior = new bool[sizeX, sizeY, sizeZ];
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (isSolid[x, y, z]) continue;

                    // Check below
                    bool hasBelow = false;
                    for (int dy = y - 1; dy >= 0; dy--)
                    {
                        if (isSolid[x, dy, z]) { hasBelow = true; break; }
                    }

                    // Check left
                    bool hasLeft = false;
                    for (int dx = x - 1; dx >= 0; dx--)
                    {
                        if (isSolid[dx, y, z]) { hasLeft = true; break; }
                    }

                    // Check right
                    bool hasRight = false;
                    for (int dx = x + 1; dx < sizeX; dx++)
                    {
                        if (isSolid[dx, y, z]) { hasRight = true; break; }
                    }

                    // Check front (z-)
                    bool hasFront = false;
                    for (int dz = z - 1; dz >= 0; dz--)
                    {
                        if (isSolid[x, y, dz]) { hasFront = true; break; }
                    }

                    // Check back (z+)
                    bool hasBack = false;
                    for (int dz = z + 1; dz < sizeZ; dz++)
                    {
                        if (isSolid[x, y, dz]) { hasBack = true; break; }
                    }

                    if (hasBelow && hasLeft && hasRight && hasFront && hasBack)
                    {
                        isInterior[x, y, z] = true;
                    }
                }
            }
        }

        // 4. Merge interior voxels into rectangular Bounds (Greedy box generation)
        bool[,,] visited = new bool[sizeX, sizeY, sizeZ];
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (!isInterior[x, y, z] || visited[x, y, z]) continue;

                    // Start a new box at (x, y, z)
                    int startX = x, startY = y, startZ = z;
                    int endX = x, endY = y, endZ = z;

                    // Expand along X axis
                    while (endX + 1 < sizeX && isInterior[endX + 1, y, z] && !visited[endX + 1, y, z])
                    {
                        endX++;
                    }

                    // Expand along Z axis
                    bool canExpandZ = true;
                    while (canExpandZ && endZ + 1 < sizeZ)
                    {
                        // Check if the entire X range can expand to endZ + 1
                        for (int tx = startX; tx <= endX; tx++)
                        {
                            if (!isInterior[tx, y, endZ + 1] || visited[tx, y, endZ + 1])
                            {
                                canExpandZ = false;
                                break;
                            }
                        }
                        if (canExpandZ) endZ++;
                    }

                    // Expand along Y axis
                    bool canExpandY = true;
                    while (canExpandY && endY + 1 < sizeY)
                    {
                        // Check if the entire X-Z range can expand to endY + 1
                        for (int tx = startX; tx <= endX; tx++)
                        {
                            for (int tz = startZ; tz <= endZ; tz++)
                            {
                                if (!isInterior[tx, endY + 1, tz] || visited[tx, endY + 1, tz])
                                {
                                    canExpandY = false;
                                    break;
                                }
                            }
                            if (!canExpandY) break;
                        }
                        if (canExpandY) endY++;
                    }

                    // Mark all voxels inside the generated box as visited
                    for (int tx = startX; tx <= endX; tx++)
                        for (int ty = startY; ty <= endY; ty++)
                            for (int tz = startZ; tz <= endZ; tz++)
                                visited[tx, ty, tz] = true;

                    // Calculate local Bounds coordinates
                    Vector3 minLocal = new Vector3(startX + minX, startY + minY, startZ + minZ);
                    Vector3 maxLocal = new Vector3(endX + minX, endY + minY, endZ + minZ);

                    // Center of the interior voxel cluster (with 0.5 voxel-center offset)
                    Vector3 center = (minLocal + maxLocal) * 0.5f + new Vector3(0.5f, 0.5f, 0.5f);
                    Vector3 size = (maxLocal - minLocal) + Vector3.one;

                    // Expand horizontally slightly to overlap with the wall blocks
                    // This ensures rotated water voxels overlapping the cabin are culled
                    // without clipping water outside the outer hull.
                    size.x += 0.5f;
                    size.z += 0.5f;

                    // The interior air voxels are 1 row ABOVE the floor.
                    // Water sits AT the floor level (local Y = floor_y to floor_y+1).
                    // So we extend 1.5 units DOWNWARD and 2.5 upward from the interior center.
                    float rawCenterY = center.y;
                    float dryBottom = rawCenterY - size.y * 0.5f - 1.5f; // reach down to water level
                    float dryTop    = rawCenterY + size.y * 0.5f + 2.5f; // extend up for player head
                    center.y = (dryBottom + dryTop) * 0.5f;
                    size.y   = dryTop - dryBottom;

                    dryFilters.Add(new Bounds(center, size));
                }
            }
        }

        // Log results for debugging
        Debug.Log($"[DryFilters] Generated {dryFilters.Count} dry filter box(es).");
        for (int i = 0; i < dryFilters.Count; i++)
            Debug.Log($"  Filter[{i}]: center={dryFilters[i].center}, size={dryFilters[i].size}");
    }

    public void CacheVehicleSettings()
    {
        // Re-cache hull block transforms to ensure grandparent colliders are captured
        var hullList = new System.Collections.Generic.List<Transform>();
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        foreach (var col in allColliders)
        {
            if (col is SphereCollider) continue;
            hullList.Add(col.transform);
        }
        _hullBlocks = hullList.ToArray();

        PropellerBlock[] props = GetComponentsInChildren<PropellerBlock>();
        _hasPropeller = props != null && props.Length > 0;

        if (_hasPropeller)
        {
            Vector3 thrustDirSum = Vector3.zero;
            int count = 0;
            foreach (var p in props)
            {
                Vector3 localThrustDir = transform.InverseTransformDirection(-p.transform.forward);
                bool isLiftPropeller = Mathf.Abs(localThrustDir.y) > 0.8f;
                if (!isLiftPropeller)
                {
                    thrustDirSum += localThrustDir;
                    count++;
                }
            }

            if (count > 0)
            {
                Vector3 avgThrustDir = thrustDirSum / count;
                avgThrustDir.y = 0f;
                if (avgThrustDir.sqrMagnitude > 0.01f)
                {
                    avgThrustDir.Normalize();
                    if (Mathf.Abs(avgThrustDir.z) >= Mathf.Abs(avgThrustDir.x))
                    {
                        _localForward = avgThrustDir.z > 0f ? Vector3.forward : Vector3.back;
                    }
                    else
                    {
                        _localForward = avgThrustDir.x > 0f ? Vector3.right : Vector3.left;
                    }
                    Debug.Log($"[VehicleController] Cached local forward: {_localForward}");
                    return;
                }
            }
        }
        _localForward = Vector3.forward;
        Debug.Log($"[VehicleController] Cached default local forward: {_localForward}");
    }

    private void UpdateGroundedWithoutWheels()
    {
        _isGroundedWithoutWheels = false;
        if (registeredWheels.Count == 0 && _hullBlocks != null && _hullBlocks.Length > 0)
        {
            float lowestY = float.MaxValue;
            Transform lowestBlock = null;
            foreach (var block in _hullBlocks)
            {
                if (block != null && block.position.y < lowestY)
                {
                    lowestY = block.position.y;
                    lowestBlock = block;
                }
            }

            if (lowestBlock != null)
            {
                Vector3 origin = lowestBlock.position;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.6f, _stepLayerMask, QueryTriggerInteraction.Ignore))
                {
                    _isGroundedWithoutWheels = true;
                }
            }
        }
    }



    public Vector3 GetLocalForwardDirection()
    {
        return _localForward;
    }

    private bool IsInputActive()
    {
        bool fwd = false, bwd = false, lft = false, rgt = false;
        bool climb = false, descend = false, strafeLeft = false, strafeRight = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            fwd = UnityEngine.InputSystem.Keyboard.current.wKey.isPressed || UnityEngine.InputSystem.Keyboard.current.upArrowKey.isPressed;
            bwd = UnityEngine.InputSystem.Keyboard.current.sKey.isPressed || UnityEngine.InputSystem.Keyboard.current.downArrowKey.isPressed;
            lft = UnityEngine.InputSystem.Keyboard.current.aKey.isPressed || UnityEngine.InputSystem.Keyboard.current.leftArrowKey.isPressed;
            rgt = UnityEngine.InputSystem.Keyboard.current.dKey.isPressed || UnityEngine.InputSystem.Keyboard.current.rightArrowKey.isPressed;
            climb = UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed;
            descend = UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed;
            strafeLeft = UnityEngine.InputSystem.Keyboard.current.qKey.isPressed;
            strafeRight = UnityEngine.InputSystem.Keyboard.current.fKey.isPressed;
        }
#else
        fwd = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bwd = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        lft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        rgt = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        climb = Input.GetKey(KeyCode.Space);
        descend = Input.GetKey(KeyCode.LeftShift);
        strafeLeft = Input.GetKey(KeyCode.Q);
        strafeRight = Input.GetKey(KeyCode.F);
#endif
        return fwd || bwd || lft || rgt || climb || descend || strafeLeft || strafeRight;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw the dry zone filters as cyan wireframes in the scene view
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (dryFilters != null)
        {
            foreach (var filter in dryFilters)
            {
                Gizmos.DrawWireCube(filter.center, filter.size);
            }
        }
    }
}
