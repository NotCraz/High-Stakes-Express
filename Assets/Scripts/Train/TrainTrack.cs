using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A simple waypoint-based track.
/// Place this on an empty GameObject and add child GameObjects named "Waypoint_0",
/// "Waypoint_1", etc. — or just drag Transforms into the waypoints list.
///
/// The train will smoothly follow these points using Catmull-Rom interpolation.
/// </summary>
public class TrainTrack : MonoBehaviour
{
    [Header("Waypoints")]
    [Tooltip("Assign track waypoints in order. The train will visit each one.")]
    public List<Transform> waypoints = new List<Transform>();

    [Tooltip("How many samples per segment to use when baking the distance table. " +
             "Higher = smoother but more memory.")]
    [Range(10, 200)]
    public int samplesPerSegment = 50;

    public bool closedLoop = false;

    // Baked data
    private List<float> _distances = new List<float>();
    private List<Vector3> _positions = new List<Vector3>();
    private List<Vector3> _tangents = new List<Vector3>();
    private float _totalLength;

    // ----------------------------------------------------------------
    private void Awake()
    {
        // Auto-discover child waypoints if none assigned
        if (waypoints.Count == 0)
        {
            foreach (Transform child in transform)
                waypoints.Add(child);
        }

        BakeDistanceTable();
    }

    private void OnValidate()
    {
        if (waypoints.Count >= 2)
            BakeDistanceTable();
    }

    // ----------------------------------------------------------------
    // Bake a distance → position/tangent lookup table using Catmull-Rom spline
    // ----------------------------------------------------------------
    private void BakeDistanceTable()
    {
        _distances.Clear();
        _positions.Clear();
        _tangents.Clear();

        if (waypoints == null || waypoints.Count < 2) return;

        float cumDist = 0f;
        _distances.Add(0f);

        int segCount = closedLoop ? waypoints.Count : waypoints.Count - 1;

        for (int seg = 0; seg < segCount; seg++)
        {
            for (int s = 0; s < samplesPerSegment; s++)
            {
                float t0 = (float)s / samplesPerSegment;
                float t1 = (float)(s + 1) / samplesPerSegment;

                Vector3 p0 = CatmullRom(seg, t0);
                Vector3 p1 = CatmullRom(seg, t1);
                Vector3 tan = (p1 - p0).normalized;

                cumDist += Vector3.Distance(p0, p1);
                _positions.Add(p0);
                _tangents.Add(tan);
                _distances.Add(cumDist);
            }
        }

        // Add final point
        _positions.Add(CatmullRom(segCount - 1, 1f));
        _tangents.Add(_tangents.Count > 0 ? _tangents[_tangents.Count - 1] : Vector3.forward);

        _totalLength = cumDist;
    }

    // ----------------------------------------------------------------
    // Sample the spline by world-space distance travelled
    // ----------------------------------------------------------------
    public Vector3 GetPositionAtDistance(float distance)
    {
        if (_positions.Count == 0) return transform.position;

        distance = closedLoop
            ? Mathf.Repeat(distance, _totalLength)
            : Mathf.Clamp(distance, 0f, _totalLength);

        int idx = BinarySearchDistance(distance);
        return _positions[Mathf.Clamp(idx, 0, _positions.Count - 1)];
    }

    public Quaternion GetRotationAtDistance(float distance)
    {
        if (_tangents.Count == 0) return Quaternion.identity;

        distance = closedLoop
            ? Mathf.Repeat(distance, _totalLength)
            : Mathf.Clamp(distance, 0f, _totalLength);

        int idx = BinarySearchDistance(distance);
        Vector3 tan = _tangents[Mathf.Clamp(idx, 0, _tangents.Count - 1)];

        if (tan == Vector3.zero) return Quaternion.identity;
        return Quaternion.LookRotation(tan, Vector3.up);
    }

    public float TotalLength => _totalLength;

    // ----------------------------------------------------------------
    // Catmull-Rom spline helper
    // ----------------------------------------------------------------
    private Vector3 CatmullRom(int segment, float t)
    {
        int count = waypoints.Count;

        Vector3 p0 = GetWaypointPos(segment - 1);
        Vector3 p1 = GetWaypointPos(segment);
        Vector3 p2 = GetWaypointPos(segment + 1);
        Vector3 p3 = GetWaypointPos(segment + 2);

        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private Vector3 GetWaypointPos(int i)
    {
        if (waypoints.Count == 0) return Vector3.zero;
        if (closedLoop) i = ((i % waypoints.Count) + waypoints.Count) % waypoints.Count;
        else i = Mathf.Clamp(i, 0, waypoints.Count - 1);
        return waypoints[i] != null ? waypoints[i].position : Vector3.zero;
    }

    private int BinarySearchDistance(float target)
    {
        int lo = 0, hi = _distances.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_distances[mid] < target) lo = mid + 1;
            else hi = mid;
        }
        return Mathf.Clamp(lo - 1, 0, _positions.Count - 1);
    }

    // ----------------------------------------------------------------
    // Gizmos — draws the track in the Scene view
    // ----------------------------------------------------------------
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        Gizmos.color = Color.yellow;
        int segCount = closedLoop ? waypoints.Count : waypoints.Count - 1;

        for (int seg = 0; seg < segCount; seg++)
        {
            Vector3 prev = CatmullRom(seg, 0f);
            for (int s = 1; s <= 20; s++)
            {
                float t = s / 20f;
                Vector3 next = CatmullRom(seg, t);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        // Draw waypoint spheres
        Gizmos.color = Color.cyan;
        foreach (var wp in waypoints)
            if (wp != null) Gizmos.DrawWireSphere(wp.position, 0.5f);
    }
}