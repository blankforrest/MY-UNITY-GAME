using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class MovingPlatformRider : MonoBehaviour
{
    [Header("Platform Settings")]
    [SerializeField] private float jumpIgnoreDuration = 0.3f;
    [SerializeField] private float redetectionRadius = 1f;

    private CharacterController controller;
    private Rigidbody currentPlatform;

    // Exact Delta Tracking
    private Vector3 lastPlatformPos;
    private Quaternion lastPlatformRot;

    // Jump state
    private float jumpTimer = 0f;
    private Vector3 jumpLaunchVelocity;

    // Log throttling
    private float nextLogTime = 0f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Disable platform riding while driving a vehicle to prevent double-movement/sliding
        if (VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen)
        {
            currentPlatform = null;
            return;
        }

        if (jumpTimer > 0f)
        {
            jumpTimer -= Time.deltaTime;
        }

        // Detect jump
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpTimer = jumpIgnoreDuration;
            
            if (currentPlatform != null)
            {
                jumpLaunchVelocity = currentPlatform.linearVelocity;
            }
            else
            {
                jumpLaunchVelocity = Vector3.zero;
            }
        }
    }

    private void FixedUpdate()
    {
        // Disable platform riding while driving a vehicle to prevent double-movement/sliding
        if (VehicleHUD.Instance != null && VehicleHUD.Instance.IsOpen)
        {
            currentPlatform = null;
            return;
        }

        // 1. PLATFORM DETECTION
        Vector3 feetPos = transform.position + controller.center - new Vector3(0, controller.height / 2f, 0);
        Vector3 castOrigin = feetPos + Vector3.up * 0.3f;
        float castRadius = 0.5f;
        
        float castDistance = (currentPlatform != null) ? 2.5f : 0.6f;

        bool foundVehicle = false;
        Rigidbody newPlatform = null;

        if (Physics.SphereCast(castOrigin, castRadius, Vector3.down, out RaycastHit hit, castDistance))
        {
            Rigidbody hitRb = hit.collider.attachedRigidbody;
            bool isVehicle = false;
            
            if (hit.collider.CompareTag("Vehicle")) 
                isVehicle = true;
            else if (hitRb != null && hitRb.gameObject.CompareTag("Vehicle")) 
                isVehicle = true;
            else if (LayerMask.LayerToName(hit.collider.gameObject.layer) == "Vehicle") 
                isVehicle = true;

            if (isVehicle && hitRb != null)
            {
                foundVehicle = true;
                newPlatform = hitRb;
            }
        }

        if (foundVehicle)
        {
            SetPlatform(newPlatform);
        }
        else
        {
            bool fallbackDetected = false;

            if (currentPlatform != null)
            {
                int vehicleLayer = LayerMask.NameToLayer("Vehicle");
                int mask = (vehicleLayer != -1) ? (1 << vehicleLayer) : ~0;

                Collider[] overlaps = Physics.OverlapSphere(feetPos, redetectionRadius, mask);
                foreach (Collider col in overlaps)
                {
                    Rigidbody rb = col.attachedRigidbody;
                    if (col.CompareTag("Vehicle") || 
                        (rb != null && rb.gameObject.CompareTag("Vehicle")) || 
                        LayerMask.LayerToName(col.gameObject.layer) == "Vehicle")
                    {
                        fallbackDetected = true;
                        break;
                    }
                }
            }

            if (!fallbackDetected)
            {
                SetPlatform(null);
            }
        }

        // 2. PLATFORM FOLLOW (Exact Delta Tracking)
        if (currentPlatform != null)
        {
            if (jumpTimer > 0f)
            {
                // Apply jump launch velocity (horizontal only) when jumping off
                Vector3 horizontalVelocity = new Vector3(jumpLaunchVelocity.x, 0f, jumpLaunchVelocity.z);
                transform.position += horizontalVelocity * Time.fixedDeltaTime;
                
                // Keep history updated so we don't snap when landing back on it
                lastPlatformPos = currentPlatform.transform.position;
                lastPlatformRot = currentPlatform.transform.rotation;
            }
            else
            {
                // Calculate exact translation delta
                Vector3 platformDeltaPos = currentPlatform.transform.position - lastPlatformPos;
                
                // Calculate exact rotation delta
                Quaternion deltaRot = currentPlatform.transform.rotation * Quaternion.Inverse(lastPlatformRot);
                
                // Calculate how the rotation orbits the player around the platform's center
                Vector3 playerOffsetFromPlatform = transform.position - lastPlatformPos;
                Vector3 rotatedOffset = deltaRot * playerOffsetFromPlatform;
                Vector3 rotationOrbitDelta = rotatedOffset - playerOffsetFromPlatform;
                
                // Temporarily disable character controller to prevent collision fighting while shifting
                bool wasEnabled = controller.enabled;
                controller.enabled = false;

                // Apply EXACT movement (translation + orbital rotation)
                transform.position += platformDeltaPos + rotationOrbitDelta;

                // Extract only the Y-axis rotation to apply to the player's facing direction
                float deltaY = deltaRot.eulerAngles.y;
                if (Mathf.Abs(deltaY) > 0.001f)
                {
                    transform.Rotate(0f, deltaY, 0f);
                }

                controller.enabled = wasEnabled;

                // Update history
                lastPlatformPos = currentPlatform.transform.position;
                lastPlatformRot = currentPlatform.transform.rotation;

                // Logging when fast
                if (currentPlatform.linearVelocity.magnitude > 3f)
                {
                    if (Time.time > nextLogTime)
                    {
                        Debug.Log($"Riding vehicle [velocity: {currentPlatform.linearVelocity.magnitude:F2}]");
                        nextLogTime = Time.time + 0.5f;
                    }
                }
            }
        }
    }

    private void SetPlatform(Rigidbody newPlatform)
    {
        if (currentPlatform != newPlatform)
        {
            if (newPlatform != null)
            {
                Debug.Log($"Riding vehicle: {newPlatform.gameObject.name}");
                currentPlatform = newPlatform;
                lastPlatformPos = currentPlatform.transform.position;
                lastPlatformRot = currentPlatform.transform.rotation;
            }
            else
            {
                if (jumpTimer <= 0f)
                {
                    Debug.Log("Left vehicle");
                }
                currentPlatform = null;
            }
        }
    }
}
