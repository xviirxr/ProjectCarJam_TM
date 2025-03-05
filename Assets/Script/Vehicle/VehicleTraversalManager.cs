using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class VehicleTraversalManager : MonoBehaviour
{
    [Header("Traversal Setup")]
    [SerializeField] private Transform[] squareCornerPoints; // 0:TopLeft, 1:TopRight, 2:BottomLeft, 3:BottomRight
    [SerializeField] private bool logTraversalPaths = false;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private Color cornerPointColor = Color.blue;
    [SerializeField] private Color connectorColor = Color.green;
    [SerializeField] private Color pathLineColor = Color.yellow;

    // Singleton instance
    private static VehicleTraversalManager _instance;
    public static VehicleTraversalManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<VehicleTraversalManager>();
                if (_instance == null)
                {
                    Debug.LogError("No VehicleTraversalManager found in scene!");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        Debug.Log("[VehicleTraversalManager] Awaking...");

        if (squareCornerPoints == null || squareCornerPoints.Length != 4)
        {
            Debug.LogError("[VehicleTraversalManager] Requires exactly 4 corner points!");
        }
        else
        {
            Debug.Log($"[VehicleTraversalManager] Found {squareCornerPoints.Length} corner points");
            for (int i = 0; i < squareCornerPoints.Length; i++)
            {
                if (squareCornerPoints[i] != null)
                    Debug.Log($"[VehicleTraversalManager] Corner {i}: {squareCornerPoints[i].name}");
                else
                    Debug.LogError($"[VehicleTraversalManager] Corner {i} is null!");
            }
        }

        if (_instance == null)
        {
            _instance = this;
            Debug.Log("[VehicleTraversalManager] Instance set to this");
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[VehicleTraversalManager] Multiple instances found, destroying this one");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Gets the closest corner point to the specified position
    /// </summary>
    public Transform GetClosestCornerPoint(Vector3 position)
    {
        Transform closestPoint = null;
        float closestDistance = float.MaxValue;

        foreach (Transform point in squareCornerPoints)
        {
            if (point == null) continue;

            float distance = Vector3.Distance(position, point.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    /// <summary>
    /// Gets the path to the nearest connector point (first or second corner)
    /// based on the closest corner to the vehicle
    /// </summary>
    public List<Transform> GetPathToConnector(Transform startingCorner)
    {
        List<Transform> path = new List<Transform>();

        // Find the index of the starting corner
        int cornerIndex = System.Array.IndexOf(squareCornerPoints, startingCorner);
        if (cornerIndex < 0)
        {
            Debug.LogError("Starting corner is not one of the corner points!");
            return path;
        }

        // Based on the specified logic: corners 0 and 1 are connectors
        // vehicles from corners 2 and 3 need to move to 0 and 1 respectively
        switch (cornerIndex)
        {
            case 0: // TopLeft - already a connector
                path.Add(squareCornerPoints[0]); // Just add self as the path
                break;

            case 1: // TopRight - already a connector
                path.Add(squareCornerPoints[1]); // Just add self as the path
                break;

            case 2: // BottomLeft - go to TopLeft
                path.Add(squareCornerPoints[0]); // Path to TopLeft
                break;

            case 3: // BottomRight - go to TopRight
                path.Add(squareCornerPoints[1]); // Path to TopRight
                break;
        }

        if (logTraversalPaths)
        {
            string pathDesc = $"Path from Corner {cornerIndex} to connector: ";
            foreach (Transform point in path)
            {
                int idx = System.Array.IndexOf(squareCornerPoints, point);
                pathDesc += $"Corner {idx} -> ";
            }
            pathDesc += "Parking";
            Debug.Log(pathDesc);
        }

        return path;
    }

    /// <summary>
    /// Determines if the given corner is a connector point (corners 0 and 1)
    /// </summary>
    public bool IsConnectorPoint(Transform corner)
    {
        int cornerIndex = System.Array.IndexOf(squareCornerPoints, corner);
        bool isConnector = cornerIndex == 0 || cornerIndex == 1;
        Debug.Log($"[VehicleTraversalManager] IsConnectorPoint for {corner.name}: index {cornerIndex}, isConnector: {isConnector}");
        return isConnector;
    }

    /// <summary>
    /// Gets an array of all corner points
    /// </summary>
    public Transform[] GetAllCornerPoints()
    {
        return squareCornerPoints;
    }

    /// <summary>
    /// Gets the index of a corner point
    /// </summary>
    public int GetCornerIndex(Transform corner)
    {
        return System.Array.IndexOf(squareCornerPoints, corner);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugVisuals) return;

        if (squareCornerPoints != null)
        {
            // Draw corner points with labels
            for (int i = 0; i < squareCornerPoints.Length; i++)
            {
                if (squareCornerPoints[i] == null) continue;

                // Use special color for connector points (0 and 1)
                if (i == 0 || i == 1)
                {
                    Gizmos.color = connectorColor;
                    Gizmos.DrawSphere(squareCornerPoints[i].position, 0.6f);
                }
                else
                {
                    Gizmos.color = cornerPointColor;
                    Gizmos.DrawSphere(squareCornerPoints[i].position, 0.5f);
                }

                // Label the corners in the scene view
#if UNITY_EDITOR
                UnityEditor.Handles.Label(squareCornerPoints[i].position + Vector3.up, "Corner " + i);
#endif
            }

            // Draw the square outline
            Gizmos.color = pathLineColor;
            for (int i = 0; i < squareCornerPoints.Length; i++)
            {
                if (squareCornerPoints[i] != null && squareCornerPoints[(i + 1) % squareCornerPoints.Length] != null)
                {
                    Gizmos.DrawLine(
                        squareCornerPoints[i].position,
                        squareCornerPoints[(i + 1) % squareCornerPoints.Length].position
                    );
                }
            }

            // Draw special path from bottom corners to top corners
            if (squareCornerPoints[2] != null && squareCornerPoints[0] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(squareCornerPoints[2].position, squareCornerPoints[0].position);
            }

            if (squareCornerPoints[3] != null && squareCornerPoints[1] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(squareCornerPoints[3].position, squareCornerPoints[1].position);
            }
        }
    }

#if UNITY_EDITOR
    [Button("Validate Points")]
    private void ValidatePoints()
    {
        if (squareCornerPoints == null || squareCornerPoints.Length != 4)
        {
            Debug.LogError("Must have exactly 4 corner points!");
        }
        else
        {
            Debug.Log("Corner points validated: " + squareCornerPoints.Length);

            // Output the order of corners for clarity
            for (int i = 0; i < squareCornerPoints.Length; i++)
            {
                if (squareCornerPoints[i] != null)
                {
                    Debug.Log($"Corner {i}: {squareCornerPoints[i].name}");
                }
                else
                {
                    Debug.LogError($"Corner {i} is null!");
                }
            }
        }
    }

    [Button("Test Path From Each Corner")]
    private void TestPathsFromAllCorners()
    {
        for (int i = 0; i < squareCornerPoints.Length; i++)
        {
            if (squareCornerPoints[i] == null) continue;

            List<Transform> path = GetPathToConnector(squareCornerPoints[i]);

            string pathStr = $"Path from Corner {i}: ";
            foreach (Transform point in path)
            {
                int idx = System.Array.IndexOf(squareCornerPoints, point);
                if (idx >= 0)
                {
                    pathStr += $"Corner {idx} -> ";
                }
                else
                {
                    pathStr += "Unknown Point -> ";
                }
            }
            pathStr += "Parking";

            Debug.Log(pathStr);
        }
    }
#endif
}