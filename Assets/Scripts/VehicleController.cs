using UnityEngine;
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

    private List<WheelBlock> registeredWheels = new List<WheelBlock>();
    public float totalWheelForce = 0f;
    public int groundedWheelCount = 0;
    private bool wasGrounded = false;

    [Header("Step Climbing")]
    [Tooltip("Max step height the vehicle can climb (in world units). 1.05 = one full voxel block.")]
    public float maxStepHeight = 1.05f;
    [Tooltip("Upward velocity (m/s) injected when climbing a full block. Mass-independent.")]
    public float stepClimbSpeed = 10f;
    [Tooltip("How far ahead (m) to probe for a step wall. Larger = detects earlier = smoother.")]
    public float stepProbeDistance = 1.8f;

    // True while the vehicle is actively climbing a step this FixedUpdate
    private bool _isClimbing = false;
    // Cached layer mask — excludes the vehicle itself so wheels don't detect their own colliders
    private int _stepLayerMask = ~0;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
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
            // bounds are in world space; convert bottom to local Y
            float localBottom = transform.InverseTransformPoint(b.min).y;
            if (localBottom < minBoundY) minBoundY = localBottom;
        }

        // Safety: if no colliders found keep CoM at a reasonable low point
        if (minBoundY == float.MaxValue) minBoundY = -0.5f;

        if (childCount > 0)
        {
            Vector3 averageCenter = centerSum / childCount;
            averageCenter.y = minBoundY - 0.5f; // low CoM for stability
            _rb.centerOfMass = averageCenter;
        }

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

        foreach (var col in allColliders)
        {
            if (col is SphereCollider)
                col.sharedMaterial = glideMaterial;  // wheels — slide on terrain
            else
                col.sharedMaterial = bodyMaterial;   // body blocks — player can stand on these
        }

        // ── Root body collider (player collision) ─────────────────────────────
        // The CharacterController is unreliable against compound child colliders on a
        // dynamic Rigidbody, so we add a root-level BoxCollider that the CC always hits.
        //
        // CLIMBING SAFETY: the collider's bottom is raised to localMinY + maxStepHeight + buffer
        // so it sits entirely in the vehicle BODY zone and never contacts a terrain step face.
        // The step-climb raycasts probe below this level independently.
        {
            Bounds tb    = new Bounds(Vector3.zero, Vector3.zero);
            bool   first = true;
            foreach (var col in allColliders)
            {
                Bounds lb = new Bounds(
                    transform.InverseTransformPoint(col.bounds.center),
                    col.bounds.size);
                if (first) { tb = lb; first = false; }
                else tb.Encapsulate(lb);
            }

            if (!first)
            {
                // Bottom of root collider = above every climbable step
                float safeBottom = tb.min.y + maxStepHeight + 0.2f;
                float safeTop    = tb.max.y;
                float safeHeight = safeTop - safeBottom;

                if (safeHeight > 0.1f)   // only add when there is real body above the wheels
                {
                    BoxCollider rootBox  = gameObject.AddComponent<BoxCollider>();
                    rootBox.center       = new Vector3(tb.center.x,
                                                       (safeBottom + safeTop) * 0.5f,
                                                       tb.center.z);
                    rootBox.size         = new Vector3(tb.size.x, safeHeight, tb.size.z);
                    rootBox.sharedMaterial = bodyMaterial;  // friction so CC grips the surface
                }
            }
        }

        // Build step-climb layer mask: exclude this vehicle's own layer
        int vehicleLayer = LayerMask.NameToLayer("Vehicle");
        _stepLayerMask = (vehicleLayer != -1) ? ~(1 << vehicleLayer) : ~0;
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        // --- DOWNFORCE: keeps wheels on ground at speed ---
        // Suppressed while climbing so gravity doesn't fight the upward lift.
        if (!_isClimbing)
        {
            float speed = _rb.linearVelocity.magnitude;
            _rb.AddForce(-transform.up * speed * speed * 2.5f * _rb.mass * Time.fixedDeltaTime,
                         ForceMode.Force);
        }

        // --- EXTRA GRAVITY WHEN AIRBORNE: heavy stomp feel ---
        // Count grounded wheels before the loop below
        int groundedNow = 0;
        foreach (var w in registeredWheels)
            if (w.isGrounded) groundedNow++;

        if (registeredWheels.Count > 0 && groundedNow == 0)
        {
            // 3G extra downward force — mech falls fast like something very heavy
            _rb.AddForce(Vector3.down * _rb.mass * 29.4f, ForceMode.Force);
        }

        // --- ANTI-FLIP GYROSCOPE ---
        float angle = Vector3.Angle(Vector3.up, transform.up);
        if (angle > 20f)
        {
            Vector3 cross = Vector3.Cross(transform.up, Vector3.up);
            _rb.AddTorque(cross * angle * _rb.mass * 3f); // stronger for heavy mech
        }

        // Sync grounded wheel count (already counted above)
        groundedWheelCount = groundedNow;
        wasGrounded = groundedWheelCount > 0;

        // --- STEP CLIMBING ---
        TryClimbStep();

        if (!isBeingControlled) return;

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

        // Only drive if we have wheels touching the ground
        if (registeredWheels.Count > 0 && groundedWheelCount == 0) return;

        float forceMultiplier = registeredWheels.Count > 0 ? totalWheelForce : 1f;

        float actualForce  = bruteStrengthForce  * forceMultiplier;
        float actualTorque = bruteStrengthTorque  * forceMultiplier; // no 0.5 penalty

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

    // ── Step Climbing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fires a fan of horizontal raycasts at 4 heights between the wheel base and
    /// maxStepHeight. If any hit a wall whose top surface is within maxStepHeight,
    /// injects upward + forward velocity (both VelocityChange, mass-independent)
    /// so the vehicle glides over the step without bumping.
    /// </summary>
    private void TryClimbStep()
    {
        // Need some horizontal movement
        Vector3 velFlat = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        if (velFlat.sqrMagnitude < 0.04f) { _isClimbing = false; return; }
        Vector3 moveDir = velFlat.normalized;

        // Lowest world-Y of all colliders = wheel contact plane
        float lowestY = float.MaxValue;
        foreach (var col in GetComponentsInChildren<Collider>())
            lowestY = Mathf.Min(lowestY, col.bounds.min.y);
        if (lowestY == float.MaxValue) lowestY = transform.position.y;

        // ── Multi-height wall probes ──────────────────────────────────────────
        bool       hitWall    = false;
        RaycastHit wallHit    = default;
        float      lowestHitY = float.MaxValue;

        const int probes = 4;
        for (int i = 0; i < probes; i++)
        {
            float dy  = Mathf.Lerp(0.06f, maxStepHeight * 0.85f, (float)i / (probes - 1));
            Vector3 org = new Vector3(transform.position.x, lowestY + dy, transform.position.z);

            if (Physics.Raycast(org, moveDir, out RaycastHit h, stepProbeDistance, _stepLayerMask))
            {
                if (!hitWall || h.point.y < lowestHitY)
                {
                    lowestHitY = h.point.y;
                    wallHit    = h;
                    hitWall    = true;
                }
            }
        }

        if (!hitWall) { _isClimbing = false; return; }

        // ── Step-top detection ────────────────────────────────────────────────
        float   topOriginY = lowestY + maxStepHeight + 0.2f;
        Vector3 topOrg     = new Vector3(
            wallHit.point.x + moveDir.x * 0.2f,
            topOriginY,
            wallHit.point.z + moveDir.z * 0.2f);

        if (!Physics.Raycast(topOrg, Vector3.down, out RaycastHit topHit,
                             maxStepHeight + 0.4f, _stepLayerMask))
        { _isClimbing = false; return; }

        float stepH = topHit.point.y - lowestY;
        if (stepH < 0.05f || stepH > maxStepHeight) { _isClimbing = false; return; }

        // ── Climbing confirmed — inject velocity ──────────────────────────────
        _isClimbing = true;

        float ratio = Mathf.Clamp01(stepH / maxStepHeight);

        // 1. Upward: lift vehicle over the step
        float targetUpVel = stepClimbSpeed * ratio;
        if (_rb.linearVelocity.y < targetUpVel)
            _rb.AddForce(Vector3.up * (targetUpVel - _rb.linearVelocity.y), ForceMode.VelocityChange);

        // 2. Forward: prevent horizontal momentum from dying on the block face
        //    Ensure the vehicle keeps at least a gentle crawl speed while climbing.
        float currentFwd = Vector3.Dot(_rb.linearVelocity, moveDir);
        float minFwdDuringClimb = 2.5f;   // m/s — just enough to carry it over
        if (currentFwd < minFwdDuringClimb)
            _rb.AddForce(moveDir * (minFwdDuringClimb - currentFwd), ForceMode.VelocityChange);
    }
}
