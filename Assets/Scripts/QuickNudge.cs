using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class QuickNudge : MonoBehaviour
{
    void Update()
    {
        bool isNudgePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            isNudgePressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.N))
        {
            isNudgePressed = true;
        }
#endif

        if (isNudgePressed)
        {
            GameObject vehicleObj = GameObject.FindGameObjectWithTag("Vehicle");
            if (vehicleObj != null)
            {
                Rigidbody rb = vehicleObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.WakeUp();
                    rb.linearVelocity = Vector3.zero; // Clear existing momentum
                    // Use a strong global X push with a vertical hop tuned to climb a 0.5 unit slab
                    Vector3 force = new Vector3(15f, 4.5f, 0f);
                    rb.AddForce(force, ForceMode.VelocityChange);
                    Debug.Log($"QuickNudge: Applied global velocity change {force} to {vehicleObj.name}!");
                }
                else
                {
                    Debug.LogWarning("QuickNudge: Vehicle found, but it has no Rigidbody component.");
                }
            }
            else
            {
                Debug.LogWarning("QuickNudge: No GameObject with tag 'Vehicle' was found in the scene.");
            }
        }
    }
}
