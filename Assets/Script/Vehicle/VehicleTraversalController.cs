using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class VehicleTraversalController : MonoBehaviour
{
    [Header("Traversal Settings")]
    [SerializeField] private bool useTraversalPath = true;

    // We'll use the centralized arrival distance instead
    // [SerializeField] private float arrivalDistance = 1.0f;

    // Set to 0 to always use traversal path, never direct
    [SerializeField] private float directParkingDistance = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private Color pathColor = new Color(1f, 0.5f, 0f, 0.5f);
    [SerializeField] private bool logPathInfo = true;

    private VehicleController vehicleController;
    private VehicleMovementParameters movementParams;
    private List<Transform> currentPath = new List<Transform>();
    private int currentPathIndex = 0;
    private bool isFollowingPath = false;
    private ParkSpaceController targetParkingSpace = null;

    // Movement variables
    private float currentSpeed = 0f;

    private enum TraversalState
    {
        Idle,
        MovingToClosestCorner,
        MovingToConnector,
        MovingToParkingEntryExit,
        MovingToParkingPosition,
        Complete
    }

    private TraversalState currentState = TraversalState.Idle;
    private Coroutine activeCoroutine = null;

    private void Awake()
    {
        vehicleController = GetComponent<VehicleController>();
        if (vehicleController == null)
        {
            Debug.LogError("VehicleTraversalController requires a VehicleController component!");
            enabled = false;
        }

        // Get the movement parameters
        movementParams = GetComponent<VehicleMovementParameters>();
        if (movementParams == null && vehicleController != null)
        {
            // Try to get from vehicle controller
            movementParams = vehicleController.GetMovementParameters();
        }

        if (movementParams == null)
        {
            // If still not found, add the component with default values
            movementParams = gameObject.AddComponent<VehicleMovementParameters>();
            Debug.LogWarning($"VehicleTraversalController on {name} had no VehicleMovementParameters - added with defaults");
        }
    }

    private void OnDisable()
    {
        // Make sure to stop all coroutines when disabled
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }

    /// <summary>
    /// Start traversal to a parking space
    /// </summary>
    public void StartTraversalToParkingSpace(ParkSpaceController parkingSpace)
    {
        Debug.Log("[VehicleTraversal] StartTraversalToParkingSpace called");

        if (!useTraversalPath || parkingSpace == null)
        {
            Debug.Log("[VehicleTraversal] useTraversalPath is false or parkingSpace is null, using direct path");
            if (vehicleController != null)
            {
                GoToParkingEntryPoint(parkingSpace);
            }
            return;
        }

        // Stop any existing traversal
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        // Clear existing data
        currentPath.Clear();
        currentPathIndex = 0;
        currentSpeed = 0f;
        targetParkingSpace = parkingSpace;

        // Find the closest corner - we'll always go to the closest corner first
        Transform closestCorner = VehicleTraversalManager.Instance.GetClosestCornerPoint(transform.position);
        if (closestCorner == null)
        {
            Debug.LogError("[VehicleTraversal] Could not find a valid corner point!");
            if (vehicleController != null)
            {
                GoToParkingEntryPoint(parkingSpace);
            }
            return;
        }

        int cornerIndex = VehicleTraversalManager.Instance.GetCornerIndex(closestCorner);
        Debug.Log($"[VehicleTraversal] Closest corner: {closestCorner.name}, Index: {cornerIndex}");

        // Always go to closest corner first, then potentially through a connector
        currentPath.Add(closestCorner);

        // If the closest corner is not a connector, we need to go through a connector
        if (!VehicleTraversalManager.Instance.IsConnectorPoint(closestCorner))
        {
            // Get the connector path from this corner
            List<Transform> pathToConnector = VehicleTraversalManager.Instance.GetPathToConnector(closestCorner);
            if (pathToConnector.Count > 0)
            {
                Debug.Log($"[VehicleTraversal] Adding connector path with {pathToConnector.Count} points");
                currentPath.AddRange(pathToConnector);
            }
            else
            {
                Debug.LogError("[VehicleTraversal] Got empty path to connector!");
            }
        }

        // Remove any duplicates in the path
        for (int i = currentPath.Count - 1; i > 0; i--)
        {
            if (currentPath[i] == currentPath[i - 1])
            {
                currentPath.RemoveAt(i);
            }
        }

        // Log the final path
        string pathDesc = "[VehicleTraversal] Final path: ";
        foreach (var point in currentPath)
        {
            int idx = VehicleTraversalManager.Instance.GetCornerIndex(point);
            pathDesc += $"Corner {idx} ({point.name}) -> ";
        }
        pathDesc += "Parking Entry";
        Debug.Log(pathDesc);

        // Start the traversal
        currentState = TraversalState.MovingToClosestCorner;
        isFollowingPath = true;
        activeCoroutine = StartCoroutine(FollowPathCoroutine());
    }

    /// <summary>
    /// Transitions to parking entry/exit point
    /// </summary>
    private void GoToParkingEntryPoint(ParkSpaceController parkingSpace)
    {
        if (vehicleController == null || parkingSpace == null)
        {
            Debug.LogError("[VehicleTraversal] Missing components for parking transition!");
            return;
        }

        // Store the parking space for later
        targetParkingSpace = parkingSpace;

        // Get the entry/exit point
        Transform entryExitPoint = parkingSpace.GetParkingEntryExitPoint();

        if (entryExitPoint != null)
        {
            Debug.Log("[VehicleTraversal] Moving to parking entry/exit point");
            currentState = TraversalState.MovingToParkingEntryExit;

            // Create a new path for the entry point
            currentPath.Clear();
            currentPath.Add(entryExitPoint);
            currentPathIndex = 0;

            // Start following the new path
            isFollowingPath = true;
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
            }
            activeCoroutine = StartCoroutine(FollowParkingEntryCoroutine());
        }
        else
        {
            Debug.Log("[VehicleTraversal] No entry/exit point found, going directly to parking position");
            GoToParkingPosition(parkingSpace);
        }
    }

    /// <summary>
    /// Coroutine to follow the path to the parking entry/exit point
    /// </summary>
    private IEnumerator FollowParkingEntryCoroutine()
    {
        yield return null;

        while (isFollowingPath && targetParkingSpace != null)
        {
            // Safety check for empty path
            if (currentPath.Count == 0)
            {
                Debug.LogError("[VehicleTraversal] Path to entry is empty!");
                GoToParkingPosition(targetParkingSpace);
                yield break;
            }

            Transform targetPoint = currentPath[0]; // Only one point - the entry/exit point
            if (targetPoint == null)
            {
                Debug.LogWarning("[VehicleTraversal] Entry/exit point is null");
                GoToParkingPosition(targetParkingSpace);
                yield break;
            }

            // Calculate distance to target
            float distanceToTarget = Vector3.Distance(transform.position, targetPoint.position);

            // Use the centralized arrival threshold
            // Check if we've reached the current waypoint
            if (distanceToTarget <= movementParams.PathPointArrivalThreshold)
            {
                Debug.Log("[VehicleTraversal] Reached parking entry/exit point");

                // Snap to exact position for cleaner movement
                transform.position = targetPoint.position;
                transform.rotation = targetPoint.rotation;

                // Now move to the final parking position
                yield return new WaitForSeconds(0.1f); // Small delay for stability
                GoToParkingPosition(targetParkingSpace);
                yield break;
            }

            // Move toward the entry point using the centralized parameters
            MoveTowardsPoint(targetPoint.position);
            yield return null;
        }
    }

    /// <summary>
    /// Transitions to parking position
    /// </summary>
    private void GoToParkingPosition(ParkSpaceController parkingSpace)
    {
        // We've reached the entry/exit point, now go to parking position
        isFollowingPath = false;
        currentState = TraversalState.MovingToParkingPosition;

        if (vehicleController != null && parkingSpace != null)
        {
            Debug.Log("[VehicleTraversal] Moving to final parking position");
            vehicleController.AssignParkingSpace(parkingSpace);
        }
        else
        {
            Debug.LogError("[VehicleTraversal] Missing components for parking position transition!");
        }
    }

    /// <summary>
    /// Coroutine to make the vehicle follow the path
    /// </summary>
    private IEnumerator FollowPathCoroutine()
    {
        yield return null;

        while (isFollowingPath)
        {
            // Safety check for empty path
            if (currentPath.Count == 0)
            {
                Debug.LogError("[VehicleTraversal] Path is empty during traversal!");
                GoToParkingEntryPoint(targetParkingSpace);
                yield break;
            }

            // Check if we've reached the end of the path
            if (currentPathIndex >= currentPath.Count)
            {
                Debug.Log("[VehicleTraversal] Reached end of path at index " + currentPathIndex);
                // We've reached the connector point
                GoToParkingEntryPoint(targetParkingSpace);
                yield break;
            }

            // Get current waypoint
            Transform targetPoint = currentPath[currentPathIndex];
            if (targetPoint == null)
            {
                Debug.LogWarning("[VehicleTraversal] Target point is null at index " + currentPathIndex);
                currentPathIndex++;
                continue;
            }

            // Calculate distance to target
            float distanceToTarget = Vector3.Distance(transform.position, targetPoint.position);

            // Use centralized path point arrival threshold
            // Check if we've reached the current waypoint
            if (distanceToTarget <= movementParams.PathPointArrivalThreshold)
            {
                Debug.Log($"[VehicleTraversal] Reached waypoint {currentPathIndex}: {targetPoint.name} (distance: {distanceToTarget})");

                // For cleaner movement at non-final waypoints
                transform.position = targetPoint.position;

                // Move to next waypoint
                currentPathIndex++;

                // If we've reached the last waypoint, prepare to go to parking
                if (currentPathIndex >= currentPath.Count)
                {
                    Debug.Log("[VehicleTraversal] That was the final waypoint");
                    yield return new WaitForSeconds(0.1f); // Small delay for stability
                    GoToParkingEntryPoint(targetParkingSpace);
                    yield break;
                }

                continue;
            }

            // Move toward the current target
            MoveTowardsPoint(targetPoint.position);

            yield return null;
        }
    }

    /// <summary>
    /// Moves the vehicle towards a target point with improved movement
    /// </summary>
    private void MoveTowardsPoint(Vector3 targetPosition)
    {
        // Calculate direction and distance
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // Calculate rotation
        Quaternion desiredRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            movementParams.RotationSpeed * Time.deltaTime
        );

        // Calculate angle to target for speed adjustment
        float angleToTarget = Quaternion.Angle(transform.rotation, desiredRotation);
        float speedFactor = Mathf.Clamp01(1.0f - angleToTarget / 90.0f);

        // Adjust target speed based on distance and angle
        float targetSpeed = movementParams.MoveSpeed;

        // Slow down when approaching target or turning sharply
        if (distanceToTarget < 5.0f || angleToTarget > 30.0f)
        {
            targetSpeed *= Mathf.Min(distanceToTarget / 5.0f, speedFactor * movementParams.SteeringFactor);
        }

        // Apply acceleration or deceleration
        if (currentSpeed < targetSpeed)
            currentSpeed += movementParams.Acceleration * Time.deltaTime;
        else
            currentSpeed -= movementParams.Deceleration * Time.deltaTime;

        currentSpeed = Mathf.Clamp(currentSpeed, 0, movementParams.MoveSpeed);

        // Move the vehicle
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    /// <summary>
    /// Gets the current traversal state as a string
    /// </summary>
    public string GetCurrentStateString()
    {
        return currentState.ToString();
    }

    /// <summary>
    /// Is the vehicle currently following a traversal path?
    /// </summary>
    public bool IsFollowingTraversalPath()
    {
        return isFollowingPath;
    }

    /// <summary>
    /// Gets the current path waypoint index
    /// </summary>
    public int GetCurrentPathIndex()
    {
        return currentPathIndex;
    }

    /// <summary>
    /// Gets the total number of waypoints in the current path
    /// </summary>
    public int GetTotalPathPoints()
    {
        return currentPath.Count;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || !isFollowingPath) return;

        Gizmos.color = pathColor;

        if (currentPath.Count > 0 && currentPathIndex < currentPath.Count && currentPath[currentPathIndex] != null)
        {
            Gizmos.DrawLine(transform.position, currentPath[currentPathIndex].position);
        }

        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            if (currentPath[i] != null && currentPath[i + 1] != null)
            {
                Gizmos.DrawLine(currentPath[i].position, currentPath[i + 1].position);
                Gizmos.DrawSphere(currentPath[i].position, 0.3f);
            }
        }

        if (currentPath.Count > 0 && currentPath[currentPath.Count - 1] != null)
        {
            Gizmos.DrawSphere(currentPath[currentPath.Count - 1].position, 0.3f);
        }
    }

#if UNITY_EDITOR
    [ShowInInspector, ReadOnly]
    public string CurrentStateDisplay => currentState.ToString();

    [ShowInInspector, ReadOnly]
    public string CurrentPathProgressDisplay => currentPathIndex + " / " + currentPath.Count;

    [Button("Test Path Visualization")]
    private void TestPathVisualization()
    {
        if (VehicleTraversalManager.Instance == null)
        {
            Debug.LogError("No VehicleTraversalManager in scene!");
            return;
        }

        Transform closestCorner = VehicleTraversalManager.Instance.GetClosestCornerPoint(transform.position);
        if (closestCorner == null)
        {
            Debug.LogError("Could not find closest corner!");
            return;
        }

        currentPath.Clear();
        currentPath.Add(closestCorner);

        List<Transform> pathToConnector = VehicleTraversalManager.Instance.GetPathToConnector(closestCorner);
        currentPath.AddRange(pathToConnector);

        currentPathIndex = 0;
        isFollowingPath = true;

        Debug.Log("Test path created with " + currentPath.Count + " points.");
    }

    [Button("Direct to Parking")]
    private void GoDirectToParking()
    {
        if (vehicleController == null || targetParkingSpace == null)
        {
            Debug.LogError("Missing components for direct parking");
            return;
        }

        isFollowingPath = false;
        currentState = TraversalState.MovingToParkingPosition;
        vehicleController.AssignParkingSpace(targetParkingSpace);
    }
#endif
}