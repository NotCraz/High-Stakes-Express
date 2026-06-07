using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Remove this line if you're not using TextMeshPro

/// <summary>
/// Place this on the start lever object.
/// When a player is within range and presses the interact key (E / South button),
/// it calls TrainController.PullStartLever().
/// </summary>
public class LeverInteractable : MonoBehaviour
{
    [Header("References")]
    public TrainController train;

    [Header("Settings")]
    [Tooltip("How close the player must be to interact (metres).")]
    public float interactRange = 2.5f;

    [Header("UI Prompt (optional)")]
    [Tooltip("A world-space TextMeshPro showing '[E] Start Engine'")]
    public GameObject promptObject; // Can be a canvas or a floating text

    // ----------------------------------------------------------------
    private Transform _playerTransform;
    private bool _playerInRange;

    // Input (new system)
    private InputAction _interactAction;

    // ----------------------------------------------------------------
    private void Awake()
    {
        // Try to find an interact action in the default input asset
        var pi = FindAnyObjectByType<PlayerInput>();
        if (pi != null)
        {
            _interactAction = pi.actions.FindAction("Interact");
        }

        if (promptObject != null)
            promptObject.SetActive(false);
    }

    private void Start()
    {
        // Cache the player transform — works for single player testing
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) _playerTransform = player.transform;
    }

    private void Update()
    {
        if (_playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        _playerInRange = dist <= interactRange;

        if (promptObject != null)
            promptObject.SetActive(_playerInRange && !train.EngineRunning);

        if (_playerInRange && !train.EngineRunning)
        {
            bool interactPressed = false;

            // New Input System
            if (_interactAction != null)
                interactPressed = _interactAction.WasPressedThisFrame();

            // Legacy fallback
            if (!interactPressed)
                interactPressed = Input.GetKeyDown(KeyCode.E) ||
                                  Input.GetButtonDown("Submit");

            if (interactPressed)
                Interact();
        }
    }

    private void Interact()
    {
        train.PullStartLever();

        // Animate the lever mesh (optional simple rotation tween)
        StartCoroutine(AnimateLever());
    }

    private System.Collections.IEnumerator AnimateLever()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(-60f, 0f, 0f); // Pull forward

        while (elapsed < duration)
        {
            transform.localRotation = Quaternion.Slerp(startRot, endRot, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localRotation = endRot;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}