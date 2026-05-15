using UnityEngine;

/// <summary>
/// STUB: Added to every spawned vehicle GameObject.
/// Replace this body with real driving input, engine forces, and steering logic.
///
/// The vehicle's Rigidbody is on this same GameObject — use GetComponent<Rigidbody>()
/// to apply forces.
/// </summary>
public class VehicleController : MonoBehaviour
{
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        Debug.Log($"[VehicleController] Vehicle ready. Mass: {_rb?.mass:F1} kg");
    }

    // TODO: Add driving controls here.
    // Example fields to add later:
    //   public float engineForce    = 800f;
    //   public float steeringAngle  = 30f;
    //   public float brakeForce     = 1200f;
    //
    // private void FixedUpdate()
    // {
    //     float throttle = Keyboard.current.wKey.isPressed ? 1f : 0f;
    //     _rb.AddForce(transform.forward * throttle * engineForce);
    // }
}
