using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages physics-based helicopter flight controls: vertical lift, directional strafe/forward,
/// yaw steering, and strict rotation clamping to ensure stable hover behavior.
/// </summary>
public class HelicopterController : MonoBehaviour
{
    private Rigidbody rb;
    private VehicleController vehicleController;

    [Header("Flight Settings")]
    public float liftPower = 12f;
    public float fallPower = 8f;
    public float movePower = 8f;
    public float strafePower = 8f;
    public float turnPower = 12f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        vehicleController = GetComponent<VehicleController>();
    }

    private void FixedUpdate()
    {
        if (rb == null || vehicleController == null || !vehicleController.isBeingControlled)
        {
            return;
        }

        // 1. Keep the helicopter perfectly level (0 tilt on X and Z)
        Vector3 euler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(0f, euler.y, 0f);
        
        // Zero out angular velocity on X and Z to prevent jitter/tilt
        Vector3 angVel = rb.angularVelocity;
        rb.angularVelocity = new Vector3(0f, angVel.y, 0f);

        // 2. Read Inputs
        bool forward = false;
        bool backward = false;
        bool left = false;
        bool right = false;
        bool climb = false;
        bool descend = false;
        bool strafeLeft = false;
        bool strafeRight = false;

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
        else
        {
            forward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            backward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            climb = Input.GetKey(KeyCode.Space);
            descend = Input.GetKey(KeyCode.LeftShift);
            strafeLeft = Input.GetKey(KeyCode.Q);
            strafeRight = Input.GetKey(KeyCode.F);
        }

        // 3. Apply vertical lift (thrust)
        float gravityAcc = Physics.gravity.magnitude;
        float hoverForce = rb.mass * gravityAcc;
        float finalLiftForce = hoverForce;

        float powerMultiplier = HasLargePropeller() ? 1.8f : 1.0f;

        if (climb)
        {
            finalLiftForce = hoverForce + rb.mass * liftPower * powerMultiplier;
        }
        else if (descend)
        {
            finalLiftForce = Mathf.Max(0f, hoverForce - rb.mass * fallPower * powerMultiplier);
        }

        // Apply lift force along world UP (since Z/X rotation is clamped, this is the same as local up)
        rb.AddForce(Vector3.up * finalLiftForce, ForceMode.Force);

        // Lock vertical velocity when hovering to prevent drift during horizontal movement
        if (!climb && !descend)
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
        }

        // 4. Apply forward/backward movement
        float targetFwd = 0f;
        if (forward) targetFwd += 1f;
        if (backward) targetFwd -= 1f;
        if (Mathf.Abs(targetFwd) > 0.01f)
        {
            rb.AddForce(transform.forward * targetFwd * rb.mass * movePower * powerMultiplier, ForceMode.Force);
        }

        // 5. Apply strafe movement
        float targetStrafe = 0f;
        if (strafeLeft) targetStrafe -= 1f;
        if (strafeRight) targetStrafe += 1f;
        if (Mathf.Abs(targetStrafe) > 0.01f)
        {
            rb.AddForce(transform.right * targetStrafe * rb.mass * strafePower * powerMultiplier, ForceMode.Force);
        }

        // 6. Apply yaw (steering) torque
        float targetYaw = 0f;
        if (left) targetYaw -= 1f;
        if (right) targetYaw += 1f;
        if (Mathf.Abs(targetYaw) > 0.01f)
        {
            rb.AddTorque(Vector3.up * targetYaw * rb.mass * turnPower * powerMultiplier, ForceMode.Force);
        }
    }

    private bool HasLargePropeller()
    {
        foreach (Transform child in transform)
        {
            if (child.name.Contains("_26")) return true;
        }
        return false;
    }

    private void LateUpdate()
    {
        if (rb == null || vehicleController == null || !vehicleController.isBeingControlled)
        {
            return;
        }

        // Additional reinforcement in LateUpdate to guarantee zero tilt
        Vector3 euler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(0f, euler.y, 0f);
    }
}
