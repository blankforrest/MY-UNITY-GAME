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
        Vector3 centerSum = Vector3.zero;
        int childCount = transform.childCount;
        float minBoundY = float.MaxValue;
        float maxBoundY = float.MinValue;

        BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>();

        for (int i = 0; i < childCount; i++)
        {
            centerSum += transform.GetChild(i).localPosition;
        }

        foreach (var col in colliders)
        {
            Vector3 c = col.transform.localPosition + col.center;
            Vector3 s = col.size;
            Vector3 scl = col.transform.localScale;

            float top = c.y + (s.y * scl.y) / 2f;
            float bottom = c.y - (s.y * scl.y) / 2f;
            
            if (top > maxBoundY) maxBoundY = top;
            if (bottom < minBoundY) minBoundY = bottom;
        }

        if (childCount > 0)
        {
            Vector3 averageCenter = centerSum / childCount;

            // Make the center of mass EXTREMELY low (1.5 units below the absolute bottom of the vehicle)
            // This acts like a Weeble-Wobble toy, making it virtually impossible to permanently flip over.
            averageCenter.y = minBoundY - 1.5f;
            _rb.centerOfMass = averageCenter;
        }

        // Setup Physics
        _rb.mass = Mathf.Max(10f, _rb.mass);
        _rb.linearDamping = 0.5f; 
        _rb.angularDamping = 2f;
        _rb.sleepThreshold = 0.0f;
        _rb.maxAngularVelocity = 3f; // Limit insane spinning!

        // Zero friction so blocks never catch or jam on terrain seams
        PhysicsMaterial glideMaterial = new PhysicsMaterial("GlideMaterial")
        {
            dynamicFriction = 0.0f,
            staticFriction = 0.0f,
            bounciness = 0.0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        foreach (var col in colliders)
        {
            col.material = glideMaterial;
        }
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        // --- ANTI-FLIP GYROSCOPE ---
        // Automatically rights the vehicle if it tilts too much
        float angle = Vector3.Angle(Vector3.up, transform.up);
        if (angle > 5f)
        {
            Vector3 cross = Vector3.Cross(transform.up, Vector3.up);
            _rb.AddTorque(cross * angle * _rb.mass * 3f);
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

        // Braking logic: if not pressing W or S, smoothly decay forward momentum
        if (!forward && !backward)
        {
            Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
            
            // Smoothly lerp forward/backward velocity to 0 (reduces by ~20% every fixed frame)
            localVel.z = Mathf.Lerp(localVel.z, 0f, 10f * Time.fixedDeltaTime);
            
            // If it's extremely slow, snap to 0 to stop completely without micro-drifting
            if (Mathf.Abs(localVel.z) < 0.1f) 
            {
                localVel.z = 0f;
            }

            _rb.linearVelocity = transform.TransformDirection(localVel);
        }

        // Steer
        if (left)     _rb.AddTorque(-Vector3.up * actualTorque);
        if (right)    _rb.AddTorque(Vector3.up * actualTorque);
    }
}
