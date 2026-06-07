using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HIERARCHY:
///   Player  ← CharacterController + this script
///   ├── Capsule
///   └── CameraHolder  ← drag this into Camera Holder field
///       └── Main Camera
///
/// No Input Actions asset needed. Uses the Input System directly.
/// Keyboard+Mouse: WASD, Mouse, Space, Left Shift
/// Controller:     Left Stick, Right Stick, South Button, Left Shoulder
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Ground Check")]
    public LayerMask groundMask;

    [Header("Look — drag CameraHolder here")]
    public Transform cameraHolder;
    public float mouseSensitivity = 0.15f;
    public float controllerSensitivity = 200f;   // degrees per second
    public float maxLookAngle = 85f;

    // ── private ────────────────────────────────────────────────────
    private CharacterController _cc;
    private float _xRotation;
    private Vector3 _vertVelocity;
    private bool _isGrounded;
    private bool _jumpConsumed;   // prevents holding space = infinite jump

    // ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        if (cameraHolder == null)
            Debug.LogError("[FPC] Assign CameraHolder in the Inspector!");
    }

    private void Start()
    {
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.identity;
            _xRotation = 0f;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        Look();
        Move();
    }

    // ── LOOK ────────────────────────────────────────────────────────
    private void Look()
    {
        if (cameraHolder == null) return;

        float dx, dy;

        // Controller right stick
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 stick = gp.rightStick.ReadValue();
            dx = stick.x * controllerSensitivity * Time.deltaTime;
            dy = stick.y * controllerSensitivity * Time.deltaTime;
        }
        else
        {
            // Mouse — Mouse.current.delta gives pixels moved this frame
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 delta = mouse.delta.ReadValue();
            dx = delta.x * mouseSensitivity;
            dy = delta.y * mouseSensitivity;
        }

        _xRotation -= dy;
        _xRotation = Mathf.Clamp(_xRotation, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * dx);
    }

    // ── MOVEMENT ────────────────────────────────────────────────────
    private void Move()
    {
        // ── Read move input ──────────────────────────────────────
        Vector2 moveInput = Vector2.zero;
        bool sprint = false;
        bool jumpDown = false;

        var gp = Gamepad.current;
        if (gp != null)
        {
            moveInput = gp.leftStick.ReadValue();
            sprint = gp.leftShoulder.isPressed;
            jumpDown = gp.buttonSouth.wasPressedThisFrame;
        }

        // Keyboard always checked (allows KB+mouse even when controller connected)
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) moveInput.y = 1f;
            if (kb.sKey.isPressed) moveInput.y = -1f;
            if (kb.aKey.isPressed) moveInput.x = -1f;
            if (kb.dKey.isPressed) moveInput.x = 1f;
            if (kb.leftShiftKey.isPressed) sprint = true;
            if (kb.spaceKey.wasPressedThisFrame) jumpDown = true;
        }

        // ── Ground check ─────────────────────────────────────────
        _isGrounded = _cc.isGrounded ||
                      Physics.CheckSphere(
                          transform.position + Vector3.up * 0.05f,
                          0.45f,
                          groundMask,
                          QueryTriggerInteraction.Ignore);

        if (_isGrounded && _vertVelocity.y < 0f)
            _vertVelocity.y = -4f;

        // ── Horizontal ───────────────────────────────────────────
        float speed = sprint ? sprintSpeed : walkSpeed;
        Vector3 move = transform.right * moveInput.x
                      + transform.forward * moveInput.y;
        _cc.Move(move * speed * Time.deltaTime);

        // ── Jump ─────────────────────────────────────────────────
        if (jumpDown && _isGrounded)
            _vertVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // ── Gravity ──────────────────────────────────────────────
        _vertVelocity.y += gravity * Time.deltaTime;
        _cc.Move(_vertVelocity * Time.deltaTime);
    }

    public void ApplyExternalVelocity(Vector3 delta) => _cc.Move(delta);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.45f);
    }
}