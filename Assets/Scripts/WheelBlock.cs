using UnityEngine;

public enum WheelSize { Small, Large }

public class WheelBlock : MonoBehaviour
{
    [Header("Wheel Settings")]
    public WheelSize wheelSize = WheelSize.Small;
    public float forceContribution = 1f;
    public float suspensionDistance = 0.5f;
    public float suspensionStrength = 3000f;
    public float suspensionDamping = 200f;
    public float frictionCoefficient = 0.8f;
    public float rotationScale = 30f;
    
    public Transform wheelMesh;
    
    [SerializeField] private bool _isGrounded;
    public bool isGrounded => _isGrounded;

    private Rigidbody rb;
    private VehicleController vehicleController;
    private int vehicleLayerMask;

    private void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
        vehicleController = GetComponentInParent<VehicleController>();
        
        if (vehicleController != null)
        {
            vehicleController.RegisterWheel(this);
        }

        int vehicleLayer = LayerMask.NameToLayer("Vehicle");
        if (vehicleLayer == -1) 
            vehicleLayerMask = ~0;
        else
            vehicleLayerMask = ~(1 << vehicleLayer); // Hit everything EXCEPT vehicle

        if (wheelMesh == null && transform.childCount > 0)
        {
            wheelMesh = transform.GetChild(0);
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        bool hitGround = false;
        float totalCompression = 0f;
        int hitCount = 0;
        
        // The block itself is 1 unit tall. So the distance from center to bottom is 0.5f.
        // We must add 0.5f to the suspension distance so the raycast actually extends BELOW the physical wheel block!
        float blockHalfHeight = (wheelSize == WheelSize.Small) ? 0.55f : 1.1f;
        float raycastLength = blockHalfHeight + suspensionDistance;
        float sphereRadius = 0.3f; // Proactive detection radius!
        
        if (wheelSize == WheelSize.Small)
        {
            if (Physics.SphereCast(transform.position, sphereRadius, -transform.up, out RaycastHit hit, raycastLength, vehicleLayerMask))
            {
                hitGround = true;
                totalCompression += (raycastLength - hit.distance);
                hitCount++;
            }
        }
        else
        {
            // Large wheel: cast from 4 corners of the 2x2 base. 
            // Corners are at roughly +-0.6 units from center to account for the 0.3f radius.
            Vector3[] corners = new Vector3[]
            {
                transform.TransformPoint(new Vector3(-0.6f, 0, -0.6f)),
                transform.TransformPoint(new Vector3(0.6f, 0, -0.6f)),
                transform.TransformPoint(new Vector3(-0.6f, 0, 0.6f)),
                transform.TransformPoint(new Vector3(0.6f, 0, 0.6f))
            };

            foreach (Vector3 corner in corners)
            {
                if (Physics.SphereCast(corner, sphereRadius, -transform.up, out RaycastHit hit, raycastLength, vehicleLayerMask))
                {
                    hitGround = true;
                    totalCompression += (raycastLength - hit.distance);
                    hitCount++;
                }
            }
        }

        _isGrounded = hitGround;

        if (hitGround && hitCount > 0)
        {
            float averageCompression = totalCompression / hitCount;

            // Clamp compression so step-edge spikes can't launch the vehicle
            averageCompression = Mathf.Min(averageCompression, suspensionDistance * 0.75f);

            Vector3 worldVelocity  = rb.GetPointVelocity(transform.position);
            float verticalVelocity = Vector3.Dot(worldVelocity, transform.up);

            float totalForce = (vehicleController != null) ? vehicleController.totalWheelForce : forceContribution;
            totalForce = Mathf.Max(0.001f, totalForce);

            float sensibleStrength = (suspensionStrength / 3000f) * 20f * rb.mass / totalForce;
            float sensibleDamping  = (suspensionDamping  / 200f)  * 12f  * rb.mass / totalForce;

            // Extra damping when moving upward (hitting a bump) — absorbs the spike
            float speedBoostDamping = Mathf.Max(0f, verticalVelocity) * 2.5f;

            Vector3 suspensionForce = transform.up * (averageCompression * sensibleStrength)
                                    - transform.up * ((verticalVelocity * sensibleDamping) + speedBoostDamping * rb.mass / totalForce);

            // Guard against NaN before applying — a NaN force permanently corrupts the Rigidbody
            if (!float.IsNaN(suspensionForce.x) && !float.IsNaN(suspensionForce.y) && !float.IsNaN(suspensionForce.z)
                && !float.IsInfinity(suspensionForce.x) && !float.IsInfinity(suspensionForce.y) && !float.IsInfinity(suspensionForce.z))
                rb.AddForceAtPosition(suspensionForce, transform.position);

            // Lateral friction
            float slipVelocity = Vector3.Dot(worldVelocity, transform.right);
            Vector3 frictionForce = -transform.right * slipVelocity * frictionCoefficient * rb.mass;
            if (!float.IsNaN(frictionForce.x) && !float.IsNaN(frictionForce.y) && !float.IsNaN(frictionForce.z)
                && !float.IsInfinity(frictionForce.x) && !float.IsInfinity(frictionForce.y) && !float.IsInfinity(frictionForce.z))
                rb.AddForceAtPosition(frictionForce, transform.position);

            if (wheelMesh != null)
            {
                float forwardSpeed = Vector3.Dot(worldVelocity, transform.parent != null ? transform.parent.forward : transform.forward);
                // Guard against NaN speed before rotating — NaN rotation permanently breaks the transform
                if (!float.IsNaN(forwardSpeed) && !float.IsInfinity(forwardSpeed))
                    wheelMesh.Rotate(forwardSpeed * rotationScale * Time.fixedDeltaTime, 0, 0, Space.Self);
            }
        }
    }
}
