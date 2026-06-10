using UnityEngine;
using UnityEngine.InputSystem;

public class PropellerBlock : MonoBehaviour
{
    [Header("Water Settings")]
    [Tooltip("The waterline Y height in world coordinates (matching world seaLevel).")]
    public float waterLevel = 14f;

    [Header("Propeller Propulsion")]
    [Tooltip("Forward/backward thrust force applied when driving while submerged.")]
    public float thrustForce = 9000f;
    
    [Tooltip("Rotational torque applied to steer the boat.")]
    public float steeringTorque = 6000f;
    
    public float maxForwardSpeed = 16f;
    public float maxReverseSpeed = 8f;

    [Header("Visuals")]
    public Transform propellerMesh;
    public float maxRotationSpeed = 900f; // Degrees per second

    private Rigidbody rb;
    private VehicleController vehicleController;
    private float currentRotationSpeed = 0f;

    private float logTimer = 0f;

    private void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
        vehicleController = GetComponentInParent<VehicleController>();

        // Auto-locate propeller visual child if not manually assigned
        if (propellerMesh == null && transform.childCount > 0)
        {
            propellerMesh = transform.GetChild(0);
        }
    }

    private void FixedUpdate()
    {
        // Try resolving references dynamically if they were not ready at Start
        if (rb == null) rb = GetComponentInParent<Rigidbody>();
        if (vehicleController == null) vehicleController = GetComponentInParent<VehicleController>();

        if (rb == null) return;

        // Skip this frame if the Rigidbody is already in a corrupt state to avoid amplifying NaN
        Vector3 v = rb.linearVelocity;
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
            return;

        float currentWaterLevel = (vehicleController != null) ? vehicleController.waterLevel : waterLevel;
        if (currentWaterLevel <= 0f) currentWaterLevel = 14f;

        float blockY = transform.position.y;
        float submersionDepth = currentWaterLevel - blockY;

        // logTimer += Time.fixedDeltaTime;
        // if (logTimer >= 1.0f)
        // {
        //     logTimer = 0f;
        //     Debug.Log($"[PropellerBlock] rb={(rb != null ? "Found" : "Null")}, " +
        //               $"vc={(vehicleController != null ? "Found" : "Null")}, " +
        //               $"isControlled={(vehicleController != null ? vehicleController.isBeingControlled.ToString() : "False")}, " +
        //               $"depth={submersionDepth:F2}, " +
        //               $"kbd={(Keyboard.current != null ? "Found" : "Null")}");
        // }

        // ── PROPULSION & STEERING ──
        // Fire propulsion when the vehicle's hull is in water.
        // VehicleController already handles buoyancy and water drag centrally,
        // so we only apply propulsion forces here.
        bool vehicleInWater = vehicleController != null
                              ? (currentWaterLevel - vehicleController.transform.position.y) > -1.5f
                              : submersionDepth > 0f;

        if (vehicleInWater)
        {
            bool isControlled = vehicleController != null && vehicleController.isBeingControlled;
            float targetSpinSpeed = 0f;

            if (isControlled)
            {
                bool forward = false;
                bool backward = false;
                bool left = false;
                bool right = false;

#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    forward = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
                    backward = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
                    left = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
                    right = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
                }
#else
                forward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                backward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
                right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
#endif

                // Project velocity onto vehicle forward (parent forward) to check speed limits
                Vector3 vehicleUp = transform.parent != null ? transform.parent.up : Vector3.up;
                // Speed limit: measure along the direction the propeller pushes the boat (-transform.forward)
                float currentFwdSpeed = Vector3.Dot(rb.linearVelocity, -transform.forward);

                // Thrust is always full when the vehicle hull is in water
                // (the propeller block itself may be above the waterline)
                float thrustRatio = 1.0f;

                // The spawner rotates the propeller to face AWAY from the hull.
                // Pushing in -transform.forward (opposite to facing) always drives the boat
                // toward the hull, i.e. "forward", regardless of world orientation.
                if (forward && currentFwdSpeed < maxForwardSpeed)
                {
                    rb.AddForce(-transform.forward * thrustForce * thrustRatio, ForceMode.Force);
                    targetSpinSpeed = maxRotationSpeed;
                }
                else if (backward && currentFwdSpeed > -maxReverseSpeed)
                {
                    rb.AddForce(transform.forward * thrustForce * thrustRatio, ForceMode.Force);
                    targetSpinSpeed = -maxRotationSpeed;
                }

                // Steering torque: rotate around the vehicle's up axis
                if (left)
                {
                    rb.AddTorque(-vehicleUp * steeringTorque * thrustRatio, ForceMode.Force);
                    targetSpinSpeed = maxRotationSpeed * 0.6f;
                }
                else if (right)
                {
                    rb.AddTorque(vehicleUp * steeringTorque * thrustRatio, ForceMode.Force);
                    targetSpinSpeed = -maxRotationSpeed * 0.6f;
                }
            }

            // Interpolate propeller rotation speed
            currentRotationSpeed = Mathf.MoveTowards(currentRotationSpeed, targetSpinSpeed, maxRotationSpeed * 2.5f * Time.fixedDeltaTime);
        }
        else
        {
            // Propeller is out of water: spin down gradually
            currentRotationSpeed = Mathf.MoveTowards(currentRotationSpeed, 0f, maxRotationSpeed * 0.8f * Time.fixedDeltaTime);
        }

        // Apply rotation to the propeller visual mesh
        if (propellerMesh != null && Mathf.Abs(currentRotationSpeed) > 0.01f)
        {
            propellerMesh.Rotate(0f, 0f, currentRotationSpeed * Time.fixedDeltaTime, Space.Self);
        }
    }
}
