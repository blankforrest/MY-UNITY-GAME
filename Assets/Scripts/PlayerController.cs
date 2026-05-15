using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f; 
    public float mouseSensitivity = 0.1f; // Lowered for new input system delta

    private CharacterController controller;
    private Transform cameraTransform;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        controller.radius = 0.3f; // slim for 1-block-wide gaps
        // height/center left at Inspector values — CharacterController self-positions correctly

        Camera cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            Debug.LogError("PlayerController needs a Camera as a child object.");
            return;
        }
        cameraTransform = cam.transform;
        // Do NOT override localPosition — Inspector value is the correct eye height


        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (cameraTransform == null) return;

        if (InventoryUI.IsInventoryOpen)
        {
            // Apply gravity so we don't float, but stop other input
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0) velocity.y = -2f;
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // Ground Check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Slight downward force to keep grounded
        }

        // Look
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        // Movement
        float x = 0;
        float z = 0;
        bool isRunning = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) z += 1;
            if (Keyboard.current.sKey.isPressed) z -= 1;
            if (Keyboard.current.dKey.isPressed) x += 1;
            if (Keyboard.current.aKey.isPressed) x -= 1;
            
            if (Keyboard.current.leftShiftKey.isPressed) isRunning = true;
        }

        Vector3 move = transform.right * x + transform.forward * z;
        // Normalize vector so diagonal movement isn't faster
        if (move.magnitude > 1f) move.Normalize();

        float speed = isRunning ? runSpeed : walkSpeed;
        
        controller.Move(move * speed * Time.deltaTime);

        // Jump
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
