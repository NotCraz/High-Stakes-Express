using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the LOCOMOTIVE (first car) only.
/// Child cars are linked via the LinkedCars list and follow using a spline-like
/// delayed-position approach so the train bends around curves naturally.
/// </summary>
public class TrainController : MonoBehaviour
{
    // ----------------------------------------------------------------
    // Inspector
    // ----------------------------------------------------------------
    [Header("Train Cars")]
    [Tooltip("Drag the car GameObjects here IN ORDER: index 0 = first car behind loco.")]
    public List<Transform> linkedCars = new List<Transform>();

    [Tooltip("Distance (metres) each car keeps behind the one in front.")]
    public float carSpacing = 10f;

    [Header("Locomotive Movement")]
    public float maxSpeed = 20f;   // Units per second at full throttle
    public float acceleration = 4f;
    public float deceleration = 6f;    // Braking force

    [Header("Track Following")]
    [Tooltip("Assign a Bezier/Spline path object here (optional). " +
             "If left empty the train drives straight along its local Z axis.")]
    public TrainTrack track;            // See TrainTrack.cs

    [Tooltip("If true the train steers toward the track tangent automatically.")]
    public bool followTrack = true;
    public float steeringSpeed = 90f;   // Degrees per second max turn rate

    [Header("Start Lever")]
    [Tooltip("The lever Animator. Set a Bool parameter 'Pulled' to trigger animation.")]
    public Animator leverAnimator;
    [Tooltip("Name of the Bool parameter on the lever animator.")]
    public string leverBoolParam = "Pulled";

    [Header("Passengers")]
    [Tooltip("Players standing on the train receive its delta-position each frame so they " +
             "don't slide.  Assign FirstPersonController components here.")]
    public List<FirstPersonController> passengers = new List<FirstPersonController>();

    // ----------------------------------------------------------------
    // Private State
    // ----------------------------------------------------------------
    private float _currentSpeed;
    private bool _engineRunning;
    private float _trackDistance; // How far along the track the loco has travelled

    // Each car remembers a history of world positions the loco passed through.
    // Car[i] picks the position from history that is (i+1)*spacing steps old.
    private Queue<Vector3> _positionHistory = new Queue<Vector3>();
    private Queue<Quaternion> _rotationHistory = new Queue<Quaternion>();

    // How many history samples we need per car spacing unit
    private const float SAMPLE_RATE = 10f; // Samples per Unity unit (higher = smoother curves)

    // ----------------------------------------------------------------
    // Unity
    // ----------------------------------------------------------------
    private void Start()
    {
        // Warm-up history with the loco's starting pose
        int warmup = Mathf.CeilToInt(linkedCars.Count * carSpacing * SAMPLE_RATE) + 10;
        for (int i = 0; i < warmup; i++)
        {
            _positionHistory.Enqueue(transform.position);
            _rotationHistory.Enqueue(transform.rotation);
        }
    }

    private void Update()
    {
        if (!_engineRunning)
        {
            // Decelerate to a stop
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * Time.deltaTime);
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed, acceleration * Time.deltaTime);
        }

        if (_currentSpeed > 0.01f)
        {
            MoveLoco();
            UpdateLinkedCars();
            PushPassengers();
        }
    }

    // ----------------------------------------------------------------
    // Locomotive movement
    // ----------------------------------------------------------------
    private void MoveLoco()
    {
        Vector3 prevPos = transform.position;

        if (followTrack && track != null)
        {
            _trackDistance += _currentSpeed * Time.deltaTime;

            Vector3 targetPos = track.GetPositionAtDistance(_trackDistance);
            Quaternion targetRot = track.GetRotationAtDistance(_trackDistance);

            // Smooth steering toward track tangent
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, steeringSpeed * Time.deltaTime);

            transform.position = targetPos;
        }
        else
        {
            // No track assigned — just drive straight forward
            transform.position += transform.forward * _currentSpeed * Time.deltaTime;
        }

        // Record position/rotation history at SAMPLE_RATE samples per unit
        float distMoved = Vector3.Distance(transform.position, prevPos);
        int samples = Mathf.Max(1, Mathf.RoundToInt(distMoved * SAMPLE_RATE));
        for (int s = 0; s < samples; s++)
        {
            _positionHistory.Enqueue(transform.position);
            _rotationHistory.Enqueue(transform.rotation);
        }

        // Trim history so it doesn't grow forever
        int maxHistory = Mathf.CeilToInt(linkedCars.Count * carSpacing * SAMPLE_RATE) + 50;
        while (_positionHistory.Count > maxHistory)
        {
            _positionHistory.Dequeue();
            _rotationHistory.Dequeue();
        }
    }

    // ----------------------------------------------------------------
    // Linked cars follow the locomotive's position history
    // ----------------------------------------------------------------
    private void UpdateLinkedCars()
    {
        Vector3[] posArr = new Vector3[_positionHistory.Count];
        Quaternion[] rotArr = new Quaternion[_rotationHistory.Count];
        _positionHistory.CopyTo(posArr, 0);
        _rotationHistory.CopyTo(rotArr, 0);

        int totalSamples = posArr.Length;

        for (int i = 0; i < linkedCars.Count; i++)
        {
            if (linkedCars[i] == null) continue;

            // How many samples back should this car be?
            int sampleOffset = Mathf.RoundToInt((i + 1) * carSpacing * SAMPLE_RATE);
            int idx = Mathf.Max(0, totalSamples - 1 - sampleOffset);

            linkedCars[i].position = posArr[idx];
            linkedCars[i].rotation = rotArr[idx];
        }
    }

    // ----------------------------------------------------------------
    // Push passengers so they move with the train
    // ----------------------------------------------------------------
    private void PushPassengers()
    {
        // We cache prev position before MoveLoco; this is called after, so delta is live.
        // Simple version: move passengers by the loco's velocity * dt.
        Vector3 delta = transform.forward * _currentSpeed * Time.deltaTime;
        foreach (var p in passengers)
        {
            if (p != null)
                p.ApplyExternalVelocity(delta);
        }
    }

    // ----------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------

    /// <summary>Call this when the player interacts with the start lever.</summary>
    public void PullStartLever()
    {
        if (_engineRunning) return; // Already running

        _engineRunning = true;

        if (leverAnimator != null)
            leverAnimator.SetBool(leverBoolParam, true);

        Debug.Log("[TrainController] Engine started!");
    }

    /// <summary>Stops the engine (e.g., at a checkpoint).</summary>
    public void StopEngine()
    {
        _engineRunning = false;

        if (leverAnimator != null)
            leverAnimator.SetBool(leverBoolParam, false);

        Debug.Log("[TrainController] Engine stopped.");
    }

    public float CurrentSpeed => _currentSpeed;
    public bool EngineRunning => _engineRunning;
}