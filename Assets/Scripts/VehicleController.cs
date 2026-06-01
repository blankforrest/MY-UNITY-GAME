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
    public float bruteStrengthForce = 800f;
    public float bruteStrengthTorque = 400f;

    private Rigidbody _rb;
    public Vector3 CurrentVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;

    private List<WheelBlock> registeredWheels = new List<WheelBlock>();
    public float totalWheelForce = 0f;
    public int groundedWheelCount = 0;
    private bool wasGrounded = false;

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

        // Setup Physics
        _rb.mass = Mathf.Max(10f, _rb.mass);
        _rb.linearDamping = 0.5f;
        _rb.angularDamping = 2f;
        _rb.sleepThreshold = 0.0f;
        _rb.maxAngularVelocity = 3f;

        // Zero friction on all colliders so blocks never snag on terrain seams
        PhysicsMaterial glideMaterial = new PhysicsMaterial("GlideMaterial")
        {
            dynamicFriction = 0.0f,
            staticFriction  = 0.0f,
            bounciness      = 0.0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine   = PhysicsMaterialCombine.Minimum
        };

        foreach (var col in allColliders)
            col.material = glideMaterial;
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        // --- DOWNFORCE: keeps wheels on ground at high speed ---
        float speed = _rb.linearVelocity.magnitude;
        _rb.AddForce(-transform.up * speed * speed * 0.8f * _rb.mass * Time.fixedDeltaTime,
                     ForceMode.Force);

        // --- ANTI-FLIP GYROSCOPE ---
        // Only fires at significant tilt — avoids fighting normal terrain bumps
        float angle = Vector3.Angle(Vector3.up, transform.up);
        if (angle > 20f)
        {
            Vector3 cross = Vector3.Cross(transform.up, Vector3.up);
            _rb.AddTorque(cross * angle * _rb.mass * 2f);
        }

        // 1. UPDATE GROUNDED WHEELS
        groundedWheelCount = 0;
        foreach (var w in registeredWheels)
        {
            if (w.isGrounded) groundedWheelCount++;
        }

        if (groundedWheelCount > 0 && !wasGrounded)
        {
            wasGrounded = true;
        }
        else if (groundedWheelCount == 0 && wasGrounded)
        {
            wasGrounded = false;
        }

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

        float actualForce = bruteStrengthForce * forceMultiplier;
        float actualTorque = bruteStrengthTorque * forceMultiplier * 0.5f;

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
