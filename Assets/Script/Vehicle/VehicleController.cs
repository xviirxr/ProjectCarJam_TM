using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class VehicleController : MonoBehaviour
{
    public enum VehicleState
    {
        Idle,
        MovingToParkingSpace,
        Parked,
        LoadingPassengers,
        PassengersLoaded,
        FollowingDeparturePath,
        Departing
    }

    [Header("Vehicle Configuration")]
    [SerializeField] private ParkingSpaceManager.VehicleSize vehicleSize = ParkingSpaceManager.VehicleSize.Small;

    // Remove these - will use the shared parameters instead
    // [SerializeField] private float moveSpeed = 10f;
    // [SerializeField] private float rotationSpeed = 5f;
    // [SerializeField] private float arrivalDistanceThreshold = 0.5f;
    // [SerializeField] private float arrivalAngleThreshold = 5f;

    [Header("Passenger Configuration")]
    [SerializeField] private Transform[] seatPositions;
    [SerializeField] private float passengerBoardingDelay = 0.5f;
    [SerializeField] private Transform passengerContainer;

    [Header("Departure Path")]
    // [SerializeField] private float pathPointArrivalThreshold = 1.0f; // This will come from parameters

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool autoRegisterWithManager = true;

    // Reference to the movement parameters
    private VehicleMovementParameters movementParams;

    private VehicleState currentState = VehicleState.Idle;
    private ParkSpaceController assignedParkingSpace;
    private bool isAssigned = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private List<NPCController> assignedPassengers = new List<NPCController>();
    private List<bool> seatOccupied;
    private int passengerCapacity = 4; // Default
    private Transform[] departurePathPoints;
    private int currentDepartPathIndex = 0;
    private float currentSpeed = 0f; // Added for smooth movement

    private ParkingSpaceManager parkingManager;

    private void Awake()
    {
        // Get the movement parameters
        movementParams = GetComponent<VehicleMovementParameters>();
        if (movementParams == null)
        {
            // If not found, add the component with default values
            movementParams = gameObject.AddComponent<VehicleMovementParameters>();
            Debug.LogWarning($"VehicleController on {name} had no VehicleMovementParameters - added with defaults");
        }

        // Set passenger capacity based on vehicle size
        switch (vehicleSize)
        {
            case ParkingSpaceManager.VehicleSize.Small:
                passengerCapacity = 4;
                break;
            case ParkingSpaceManager.VehicleSize.Medium:
                passengerCapacity = 6;
                break;
            case ParkingSpaceManager.VehicleSize.Large:
                passengerCapacity = 8;
                break;
        }

        // Initialize seat status array
        seatOccupied = new List<bool>(passengerCapacity);
        for (int i = 0; i < passengerCapacity; i++)
        {
            seatOccupied.Add(false);
        }

        // Make sure we have enough seat transforms
        if (seatPositions == null || seatPositions.Length < passengerCapacity)
        {
            Debug.LogWarning($"Vehicle {name} doesn't have enough seat positions defined for its capacity!");
        }
    }

    private void Start()
    {
        // Auto-register with parking manager
        if (autoRegisterWithManager)
        {
            parkingManager = FindFirstObjectByType<ParkingSpaceManager>();
            if (parkingManager != null)
            {
                parkingManager.RegisterVehicle(this);
            }
            else
            {
                Debug.LogError("No ParkingSpaceManager found in scene!");
            }
        }
    }

    private void Update()
    {
        switch (currentState)
        {
            case VehicleState.MovingToParkingSpace:
                MoveTowardsTarget();
                break;

            case VehicleState.FollowingDeparturePath:
                FollowDeparturePath();
                break;

            case VehicleState.Departing:
                // Move away from scene
                transform.Translate(Vector3.forward * movementParams.MoveSpeed * Time.deltaTime);
                break;
        }
    }

    public void AssignParkingSpace(ParkSpaceController parkingSpace)
    {
        if (parkingSpace != null)
        {
            assignedParkingSpace = parkingSpace;
            targetPosition = parkingSpace.GetParkingPosition().position;
            targetRotation = parkingSpace.GetParkingPosition().rotation;
            currentState = VehicleState.MovingToParkingSpace;
            isAssigned = true;

            if (showDebugInfo)
                Debug.Log($"Vehicle {name} assigned to parking space {parkingSpace.GetSpaceIndex()}");
        }
    }

    private void MoveTowardsTarget()
    {
        if (Vector3.Distance(transform.position, targetPosition) <= movementParams.ArrivalDistanceThreshold &&
            Quaternion.Angle(transform.rotation, targetRotation) <= movementParams.ArrivalAngleThreshold)
        {
            // Arrived at parking space
            currentState = VehicleState.Parked;
            transform.position = targetPosition;
            transform.rotation = targetRotation;

            if (assignedParkingSpace != null)
            {
                assignedParkingSpace.VehicleArrived(this);
            }

            if (showDebugInfo)
                Debug.Log($"Vehicle {name} arrived at parking space");
        }
        else
        {
            // Move towards target using unified movement parameters
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                movementParams.MoveSpeed * Time.deltaTime
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                movementParams.RotationSpeed * Time.deltaTime
            );
        }
    }

    private void FollowDeparturePath()
    {
        if (Vector3.Distance(transform.position, targetPosition) <= movementParams.PathPointArrivalThreshold)
        {
            // We've reached a waypoint

            // We're using departurePathPoints array when going through parking space points
            if (departurePathPoints != null && departurePathPoints.Length > 0)
            {
                // Arrived at current path point, go to next
                currentDepartPathIndex++;

                if (currentDepartPathIndex < departurePathPoints.Length)
                {
                    targetPosition = departurePathPoints[currentDepartPathIndex].position;
                    // Don't set targetRotation here, will be calculated in MoveTowardsPoint
                }
                else
                {
                    // If we've reached the end of the parking space path points
                    // Check if we should go to the manager's road exit point
                    if (parkingManager != null && parkingManager.GetRoadExitPoint() != null)
                    {
                        targetPosition = parkingManager.GetRoadExitPoint().position;
                        // Don't set targetRotation here, will be calculated in MoveTowardsPoint

                        // Clear departure path points to indicate we're now heading to the global exit
                        departurePathPoints = null;
                    }
                    else
                    {
                        StartFinalDeparture();
                    }
                }
            }
            // We're heading directly to the manager's road exit point
            else if (parkingManager != null && parkingManager.GetRoadExitPoint() != null)
            {
                if (Vector3.Distance(transform.position, parkingManager.GetRoadExitPoint().position) <= movementParams.PathPointArrivalThreshold)
                {
                    // We've reached the global exit point
                    StartFinalDeparture();
                }
            }
            else
            {
                // No more points to follow
                StartFinalDeparture();
            }
        }
        else
        {
            // Use the proven movement logic from VehicleTraversalController
            MoveTowardsPoint(targetPosition);
        }
    }

    // Movement method copied from VehicleTraversalController
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

    public void AssignPassengers(List<NPCController> passengers)
    {
        assignedPassengers = passengers ?? new List<NPCController>();
        currentState = VehicleState.LoadingPassengers;

        if (showDebugInfo)
            Debug.Log($"Vehicle {name} assigned {assignedPassengers.Count} passengers");
    }

    public void AddPassenger(NPCController passenger)
    {
        if (passenger != null && assignedPassengers.Count < passengerCapacity)
        {
            assignedPassengers.Add(passenger);
            currentState = VehicleState.LoadingPassengers;

            if (showDebugInfo)
                Debug.Log($"Vehicle {name} added passenger, total: {assignedPassengers.Count}");
        }
    }

    public Vector3 CalculateSeatPosition(int seatIndex)
    {
        if (seatPositions != null && seatIndex >= 0 && seatIndex < seatPositions.Length)
        {
            return seatPositions[seatIndex].position;
        }

        // Fallback if seat position not defined
        return transform.position + transform.right * (seatIndex * 0.5f - 1f);
    }

    public void NotifyPassengerSeated(int seatIndex)
    {
        if (seatIndex >= 0 && seatIndex < seatOccupied.Count)
        {
            seatOccupied[seatIndex] = true;

            if (showDebugInfo)
                Debug.Log($"Vehicle {name} passenger seated at position {seatIndex}");

            // Notify the UI Manager about the passenger being seated
            VehiclePassengerUIManager uiManager = GetComponent<VehiclePassengerUIManager>();
            if (uiManager != null)
            {
                uiManager.OnPassengerSeated(seatIndex);
            }
        }

        // Check if all assigned passengers are seated
        bool allSeated = true;
        for (int i = 0; i < assignedPassengers.Count; i++)
        {
            if (i < seatOccupied.Count && !seatOccupied[i])
            {
                allSeated = false;
                break;
            }
        }

        if (allSeated && assignedPassengers.Count > 0)
        {
            currentState = VehicleState.PassengersLoaded;

            if (showDebugInfo)
                Debug.Log($"Vehicle {name} all passengers seated");
        }
    }

    public void Depart()
    {
        // Reset speed
        currentSpeed = 0f;

        // Notify the UI Manager that we're departing
        VehiclePassengerUIManager uiManager = GetComponent<VehiclePassengerUIManager>();
        if (uiManager != null)
        {
            uiManager.OnVehicleDeparting();
        }

        if (assignedParkingSpace != null)
        {
            // Set up departure path
            departurePathPoints = assignedParkingSpace.GetDeparturePathPositions();
            currentDepartPathIndex = 0;

            if (departurePathPoints != null && departurePathPoints.Length > 0)
            {
                currentState = VehicleState.FollowingDeparturePath;
                targetPosition = departurePathPoints[0].position;
                // Don't set targetRotation here, will be calculated in MoveTowardsPoint
            }
            else
            {
                // If no departure path points, try to get a direct road exit point from manager
                if (parkingManager != null && parkingManager.GetRoadExitPoint() != null)
                {
                    currentState = VehicleState.FollowingDeparturePath;
                    targetPosition = parkingManager.GetRoadExitPoint().position;
                    // Don't set targetRotation here, will be calculated in MoveTowardsPoint
                }
                else
                {
                    StartFinalDeparture();
                }
            }

            assignedParkingSpace.VehicleDeparting(this);
        }
        else
        {
            // If there's no assigned parking space but we have a manager with an exit point
            if (parkingManager != null && parkingManager.GetRoadExitPoint() != null)
            {
                currentState = VehicleState.FollowingDeparturePath;
                targetPosition = parkingManager.GetRoadExitPoint().position;
                // Don't set targetRotation here, will be calculated in MoveTowardsPoint
            }
            else
            {
                StartFinalDeparture();
            }
        }

        if (showDebugInfo)
            Debug.Log($"Vehicle {name} departing");
    }

    private void StartFinalDeparture()
    {
        currentState = VehicleState.Departing;

        // Unregister from parking manager
        if (parkingManager != null)
        {
            parkingManager.UnregisterVehicle(this);
        }

        // Start destruction sequence
        StartCoroutine(DestroyAfterDelay(5f));

        if (showDebugInfo)
            Debug.Log($"Vehicle {name} starting final departure");
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (showDebugInfo)
            Debug.Log($"Vehicle {name} destroyed after departure");

        Destroy(gameObject);
    }

    public ParkingSpaceManager.VehicleSize GetVehicleSize()
    {
        return vehicleSize;
    }

    public VehicleState GetCurrentState()
    {
        return currentState;
    }

    public bool IsAssigned()
    {
        return isAssigned;
    }

    public void SetAssigned(bool assigned)
    {
        isAssigned = assigned;
    }

    public int GetPassengerCount()
    {
        return assignedPassengers.Count;
    }

    public int GetPassengerCapacity()
    {
        return passengerCapacity;
    }

    public int GetAvailableSeats()
    {
        return passengerCapacity - assignedPassengers.Count;
    }

    public float GetPassengerBoardingDelay()
    {
        return passengerBoardingDelay;
    }

    // Check if vehicle is properly parked and ready for boarding
    public bool IsReadyForBoarding()
    {
        return currentState == VehicleState.Parked ||
               currentState == VehicleState.LoadingPassengers ||
               currentState == VehicleState.PassengersLoaded;
    }

    // Public accessor to movement parameters for other scripts
    public VehicleMovementParameters GetMovementParameters()
    {
        return movementParams;
    }

#if UNITY_EDITOR
    [Button("Register With Manager")]
    private void EditorRegisterWithManager()
    {
        parkingManager = FindFirstObjectByType<ParkingSpaceManager>();
        if (parkingManager != null)
        {
            parkingManager.RegisterVehicle(this);
            Debug.Log($"Vehicle {name} registered with parking manager");
        }
    }

    [Button("Test Departure")]
    private void EditorTestDeparture()
    {
        Depart();
    }
#endif
}