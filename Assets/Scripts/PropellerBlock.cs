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
    private HelicopterController helicopterController;
    private float currentRotationSpeed = 0f;

    private float logTimer = 0f;

    private void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
        vehicleController = GetComponentInParent<VehicleController>();
        helicopterController = GetComponentInParent<HelicopterController>();

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
        if (helicopterController == null) helicopterController = GetComponentInParent<HelicopterController>();

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
        bool isControlled = vehicleController != null && vehicleController.isBeingControlled;
        float targetSpinSpeed = 0f;

        if (isControlled)
        {
            bool forward = false;
            bool backward = false;
            bool left = false;
            bool right = false;
            bool climb = false;
            bool descend = false;
            bool strafeLeft = false;
            bool strafeRight = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                forward = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
                backward = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
                left = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
                right = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
                climb = Keyboard.current.spaceKey.isPressed;
                descend = Keyboard.current.leftShiftKey.isPressed;
                strafeLeft = Keyboard.current.qKey.isPressed;
                strafeRight = Keyboard.current.fKey.isPressed;
            }
#else
            forward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            backward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            climb = Input.GetKey(KeyCode.Space);
            descend = Input.GetKey(KeyCode.LeftShift);
            strafeLeft = Input.GetKey(KeyCode.Q);
            strafeRight = Input.GetKey(KeyCode.F);
#endif

            // Identify orientation relative to vehicle's parent axes
            Vector3 vehicleUp = transform.parent != null ? transform.parent.up : Vector3.up;
            Vector3 vehicleForward = transform.parent != null ? transform.parent.forward : Vector3.forward;
            Vector3 vehicleRight = transform.parent != null ? transform.parent.right : Vector3.right;

            // Project propeller's local thrust direction (-transform.forward) in parent space
            Vector3 localThrustDir = transform.parent != null 
                ? transform.parent.InverseTransformDirection(-transform.forward) 
                : -transform.forward;

            bool isLiftPropeller = Mathf.Abs(localThrustDir.y) > 0.8f;
            bool isLateralPropeller = Mathf.Abs(localThrustDir.x) > 0.8f;
            bool isLongitudinalPropeller = Mathf.Abs(localThrustDir.z) > 0.8f;

            if (isLiftPropeller)
            {
                // Find total lift propellers to share weight
                int liftPropCount = 0;
                PropellerBlock[] allProps = transform.parent.GetComponentsInChildren<PropellerBlock>();
                foreach (var p in allProps)
                {
                    Vector3 pLocalDir = transform.parent.InverseTransformDirection(-p.transform.forward);
                    if (Mathf.Abs(pLocalDir.y) > 0.8f) liftPropCount++;
                }
                if (liftPropCount == 0) liftPropCount = 1;

                float gravityAcc = Physics.gravity.magnitude;
                float hoverForce = (rb.mass * gravityAcc) / liftPropCount;

                float finalLiftForce = 0f;

                if (climb)
                {
                    finalLiftForce = hoverForce + (rb.mass * 8.0f) / liftPropCount;
                    targetSpinSpeed = maxRotationSpeed;
                }
                else if (descend)
                {
                    finalLiftForce = Mathf.Max(0f, hoverForce - (rb.mass * 5.0f) / liftPropCount);
                    targetSpinSpeed = maxRotationSpeed * 0.2f;
                }
                else
                {
                    finalLiftForce = hoverForce;
                    targetSpinSpeed = maxRotationSpeed * 0.6f;
                }

                // Apply lift force along vehicle's local up
                if (helicopterController == null)
                {
                    rb.AddForceAtPosition(vehicleUp * finalLiftForce, transform.position, ForceMode.Force);
                }
            }
            else if (isLongitudinalPropeller)
            {
                float targetForceZ = 0f;
                if (forward)
                {
                    targetForceZ = 1.0f;
                    targetSpinSpeed = maxRotationSpeed;
                }
                else if (backward)
                {
                    targetForceZ = -1.0f;
                    targetSpinSpeed = -maxRotationSpeed;
                }

                // If steering in water, let the boat propeller spin a bit for visuals
                if (Mathf.Abs(targetForceZ) < 0.01f && (left || right))
                {
                    targetSpinSpeed = maxRotationSpeed * 0.4f * (left ? 1f : -1f);
                }

                if (Mathf.Abs(targetForceZ) > 0.01f)
                {
                    float thrustMultiplier = (transform.name.Contains("_26")) ? 2.5f : 1.0f;
                    float force = targetForceZ * rb.mass * 6.0f * thrustMultiplier;
                    if (helicopterController == null)
                    {
                        rb.AddForceAtPosition(vehicleForward * force, transform.position, ForceMode.Force);
                    }
                }
            }
            else if (isLateralPropeller)
            {
                float targetForceX = 0f;

                // 1. Strafe input (Q/F)
                if (strafeLeft)  targetForceX -= 1.0f;
                if (strafeRight) targetForceX += 1.0f;

                // 2. Yaw/Steering assistance
                Vector3 localPos = transform.localPosition;
                float zSign = 0f;
                if (localPos.z > 0.2f) zSign = 1f;
                else if (localPos.z < -0.2f) zSign = -1f;

                if (left)  targetForceX -= zSign * 1.0f;
                if (right) targetForceX += zSign * 1.0f;

                targetForceX = Mathf.Clamp(targetForceX, -1f, 1f);

                if (Mathf.Abs(targetForceX) > 0.01f)
                {
                    float thrustMultiplier = (transform.name.Contains("_26")) ? 2.5f : 1.0f;
                    float force = targetForceX * rb.mass * 6.0f * thrustMultiplier;
                    if (helicopterController == null)
                    {
                        rb.AddForceAtPosition(vehicleRight * force, transform.position, ForceMode.Force);
                    }
                    targetSpinSpeed = targetForceX * maxRotationSpeed;
                }
            }

            // Apply yaw torque for steering
            if (helicopterController == null)
            {
                float actualSteeringTorque = Mathf.Max(steeringTorque, rb.mass * 12f);
                if (left)
                {
                    rb.AddTorque(-vehicleUp * actualSteeringTorque, ForceMode.Force);
                }
                else if (right)
                {
                    rb.AddTorque(vehicleUp * actualSteeringTorque, ForceMode.Force);
                }
            }
        }

        // Interpolate propeller rotation speed
        currentRotationSpeed = Mathf.MoveTowards(currentRotationSpeed, targetSpinSpeed, maxRotationSpeed * 2.5f * Time.fixedDeltaTime);

        // Apply rotation to the propeller visual mesh
        if (propellerMesh != null && Mathf.Abs(currentRotationSpeed) > 0.01f)
        {
            propellerMesh.Rotate(0f, 0f, currentRotationSpeed * Time.fixedDeltaTime, Space.Self);
        }
    }
}
