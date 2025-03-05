using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

/// <summary>
/// Controls a vehicle that picks up passengers
/// </summary>
public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Settings")]
    [SerializeField] private ParkingSpaceManager.VehicleSize vehicleSize;
    [SerializeField] private int passengerCapacity = 4;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float arrivalDistance = 0.5f;
    [SerializeField] private float departDelay = 3f;
    [SerializeField] private float passengerBoardingDelay = 0.5f;

    [Header("Physics")]
    [SerializeField] private float acceleration = 2f;
    [SerializeField] private float deceleration = 4f;
    [SerializeField] private float steeringFactor = 2f;

    private ParkingSpaceManager parkingManager;
    private ParkSpaceController assignedParkSpace;
    private List<NPCController> assignedPassengers = new List<NPCController>();

    private bool isDriving = false;
    private bool isParked = false;
    private bool isReadyToDepart = false;
    private bool isAssigned = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float currentSpeed = 0f;

    // Departure path variables
    private Transform[] departurePathPoints;
    private int currentPathIndex = 0;
    private bool isFollowingDeparturePath = false;

    public enum VehicleState
    {
        Idle,
        DrivingToParking,
        Parked,
        LoadingPassengers,
        PassengersLoaded,
        Departing,
        FollowingDeparturePath
    }

    private VehicleState currentState = VehicleState.Idle;

#if UNITY_EDITOR
    [ShowInInspector, ReadOnly]
    public int EmptySeats => passengerCapacity - assignedPassengers.Count;

    [ShowInInspector, ReadOnly]
    public int OccupiedSeats => assignedPassengers.Count;

    [ShowInInspector, ReadOnly]
    public string StateInfo => currentState.ToString();

    [ShowInInspector, ReadOnly]
    public bool IsCurrentlyAssigned => isAssigned;

    [ShowInInspector, ReadOnly]
    public int CurrentPathPointIndex => currentPathIndex;
#endif

    private void Start()
    {
        // Auto-register with the parking manager
        if (parkingManager == null)
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

    /// <summary>
    /// Initializes the vehicle with necessary references
    /// </summary>
    public void Initialize(ParkingSpaceManager manager, int capacity, ParkSpaceController parkSpace)
    {
        parkingManager = manager;
        assignedParkSpace = parkSpace;
        isAssigned = true;

        // Override inspector capacity if provided
        if (capacity > 0)
            passengerCapacity = capacity;

        // Start driving to parking space
        AssignParkingSpace(parkSpace);
    }

    /// <summary>
    /// Assigns a parking space for this vehicle to drive to
    /// </summary>
    public void AssignParkingSpace(ParkSpaceController parkSpace)
    {
        assignedParkSpace = parkSpace;
        isAssigned = true;

        if (assignedParkSpace != null)
        {
            Transform parkingPos = assignedParkSpace.GetParkingPosition();
            if (parkingPos != null)
            {
                targetPosition = parkingPos.position;
                targetRotation = parkingPos.rotation;
                isDriving = true;
                currentState = VehicleState.DrivingToParking;
            }
        }

        // Initialize passenger list
        assignedPassengers = new List<NPCController>(passengerCapacity);
    }

    private void Update()
    {
        switch (currentState)
        {
            case VehicleState.DrivingToParking:
                DriveToTargetPosition();
                break;

            case VehicleState.LoadingPassengers:
                // Managed by passenger system
                CheckAllPassengersLoaded();
                break;

            case VehicleState.FollowingDeparturePath:
                FollowDeparturePath();
                break;

            case VehicleState.Departing:
                DriveToTargetPosition();
                break;
        }
    }

    /// <summary>
    /// Drives the vehicle to the target position using simple physics
    /// </summary>
    private void DriveToTargetPosition()
    {
        if (!isDriving)
            return;

        // Calculate distance to target
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // Check if we've arrived
        if (distanceToTarget <= arrivalDistance)
        {
            ArriveAtDestination();
            return;
        }

        // Calculate direction to target
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;

        // Calculate desired rotation
        Quaternion desiredRotation = Quaternion.LookRotation(directionToTarget);

        // Smoothly rotate towards the target
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);

        // Determine if we need to slow down based on angle to target
        float angleToTarget = Quaternion.Angle(transform.rotation, desiredRotation);
        float speedFactor = Mathf.Clamp01(1.0f - angleToTarget / 90.0f);

        // Adjust speed based on distance to target and angle
        float targetSpeed = moveSpeed;

        // Slow down when approaching target or turning sharply
        if (distanceToTarget < 5.0f || angleToTarget > 30.0f)
        {
            targetSpeed *= Mathf.Min(distanceToTarget / 5.0f, speedFactor * steeringFactor);
        }

        // Apply acceleration or deceleration
        if (currentSpeed < targetSpeed)
            currentSpeed += acceleration * Time.deltaTime;
        else
            currentSpeed -= deceleration * Time.deltaTime;

        currentSpeed = Mathf.Clamp(currentSpeed, 0, moveSpeed);

        // Move the vehicle
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    /// <summary>
    /// Follows the departure path points
    /// </summary>
    private void FollowDeparturePath()
    {
        if (!isFollowingDeparturePath || departurePathPoints == null || departurePathPoints.Length == 0)
        {
            // If we have no path, go straight to departing
            currentState = VehicleState.Departing;
            SetFinalDepartureTarget();
            return;
        }

        // Make sure we have a valid current index
        if (currentPathIndex >= departurePathPoints.Length)
        {
            // We've reached the end of the path
            currentState = VehicleState.Departing;
            SetFinalDepartureTarget();
            return;
        }

        // Make sure the current path point is valid
        if (departurePathPoints[currentPathIndex] == null)
        {
            // Skip invalid points
            currentPathIndex++;
            if (currentPathIndex >= departurePathPoints.Length)
            {
                currentState = VehicleState.Departing;
                SetFinalDepartureTarget();
                return;
            }
        }

        // Set target to the current path point
        targetPosition = departurePathPoints[currentPathIndex].position;
        targetRotation = departurePathPoints[currentPathIndex].rotation;
        isDriving = true; // Make sure driving is enabled

        // Calculate distance to current point
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // Check if we've reached the current path point
        if (distanceToTarget <= arrivalDistance)
        {
            // Move to next path point
            currentPathIndex++;

            // Set next target immediately instead of waiting for next frame
            if (currentPathIndex < departurePathPoints.Length)
            {
                targetPosition = departurePathPoints[currentPathIndex].position;
                targetRotation = departurePathPoints[currentPathIndex].rotation;
            }
            else
            {
                // We've reached the end of the path
                currentState = VehicleState.Departing;
                SetFinalDepartureTarget();
            }
        }

        // Continue driving (reuse the same driving logic)
        DriveToTargetPosition();
    }

    /// <summary>
    /// Set the final departure target (after following the path)
    /// </summary>
    private void SetFinalDepartureTarget()
    {
        // Set target to a point off-screen in the direction we're facing
        targetPosition = transform.position + transform.forward * 50f;
        isDriving = true;
        currentSpeed = moveSpeed * 0.5f; // Start with some initial speed
    }

    /// <summary>
    /// Called when vehicle arrives at destination
    /// </summary>
    private void ArriveAtDestination()
    {
        // For path following, we don't want to stop driving
        if (currentState != VehicleState.FollowingDeparturePath)
        {
            isDriving = false;
            currentSpeed = 0f;
        }

        // Snap to exact position and rotation
        transform.position = targetPosition;
        transform.rotation = targetRotation;

        // Update state based on what we were doing
        if (currentState == VehicleState.DrivingToParking)
        {
            currentState = VehicleState.Parked;
            isParked = true;
            isDriving = false; // Stop driving when parked

            // Show passenger UI when we arrive at parking
            VehiclePassengerUIManager passengerUIManager = GetComponent<VehiclePassengerUIManager>();
            if (passengerUIManager != null)
            {
                passengerUIManager.ShowUI();
            }

            // Notify parking space that we've arrived
            if (assignedParkSpace != null)
            {
                assignedParkSpace.VehicleArrived(this);
            }
        }
        else if (currentState == VehicleState.FollowingDeparturePath)
        {
            // Move to the next point in the path
            currentPathIndex++;
            isDriving = true; // Keep driving

            if (currentPathIndex >= departurePathPoints.Length)
            {
                // End of path reached
                currentState = VehicleState.Departing;
                SetFinalDepartureTarget();
            }
            else
            {
                // Continue to next point
                targetPosition = departurePathPoints[currentPathIndex].position;
                targetRotation = departurePathPoints[currentPathIndex].rotation;
            }
        }
        else if (currentState == VehicleState.Departing)
        {
            // We've left the scene, return to pool or destroy
            currentState = VehicleState.Idle;
            isDriving = false; // Stop driving

            if (parkingManager != null)
            {
                // Just mark as unassigned to be reused
                isAssigned = false;
            }

            // Reset path variables
            departurePathPoints = null;
            currentPathIndex = 0;
            isFollowingDeparturePath = false;

            // Clear passenger list
            assignedPassengers.Clear();

            // Make sure UI is hidden
            VehiclePassengerUIManager passengerUIManager = GetComponent<VehiclePassengerUIManager>();
            if (passengerUIManager != null)
            {
                passengerUIManager.ResetAllSeats();
                passengerUIManager.HideUI();
            }
        }
    }

    /// <summary>
    /// Assigns passengers to this vehicle
    /// </summary>
    public void AssignPassengers(List<NPCController> passengers)
    {
        currentState = VehicleState.LoadingPassengers;
        assignedPassengers.Clear();

        int seatsToFill = Mathf.Min(passengers.Count, passengerCapacity);

        for (int i = 0; i < seatsToFill; i++)
        {
            if (passengers[i] != null)
            {
                assignedPassengers.Add(passengers[i]);

                // Calculate seat position based on vehicle dimensions and capacity
                Vector3 seatPosition = CalculateSeatPosition(i);

                // Update the passenger destination
                passengers[i].SetCustomDestination(seatPosition, transform.rotation);
            }
        }
    }

    /// <summary>
    /// Adds a single passenger to this vehicle
    /// </summary>
    public void AddPassenger(NPCController passenger)
    {
        if (passenger != null && !assignedPassengers.Contains(passenger))
        {
            assignedPassengers.Add(passenger);
        }
    }

    /// <summary>
    /// Calculates a seat position based on index
    /// </summary>
    public Vector3 CalculateSeatPosition(int seatIndex)
    {
        // Calculate a grid layout for seats based on vehicle size
        float rowSpacing = 1.0f;
        float columnSpacing = 0.8f;
        int seatsPerRow = 2;

        if (vehicleSize == ParkingSpaceManager.VehicleSize.Medium)
        {
            seatsPerRow = 2;
            rowSpacing = 1.2f;
        }
        else if (vehicleSize == ParkingSpaceManager.VehicleSize.Large)
        {
            seatsPerRow = 2;
            rowSpacing = 1.5f;
        }

        // Calculate row and column
        int row = seatIndex / seatsPerRow;
        int column = seatIndex % seatsPerRow;

        // Calculate local position
        float xOffset = (column - (seatsPerRow - 1) / 2.0f) * columnSpacing;
        float zOffset = -row * rowSpacing + 0.5f; // +0.5f to move seats behind driver

        // Get world position
        Vector3 localPosition = new Vector3(xOffset, 0, zOffset);
        return transform.TransformPoint(localPosition);
    }

    /// <summary>
    /// Checks if all passengers have boarded
    /// </summary>
    private void CheckAllPassengersLoaded()
    {
        if (assignedPassengers.Count == 0)
            return;

        // Remove null references (destroyed passengers)
        for (int i = assignedPassengers.Count - 1; i >= 0; i--)
        {
            if (assignedPassengers[i] == null)
            {
                assignedPassengers.RemoveAt(i);
            }
        }

        // If all passengers have been destroyed (meaning they've boarded)
        // and we still have passenger entries in our list, we're just waiting for cleanup
        if (assignedPassengers.Count == 0)
        {
            currentState = VehicleState.PassengersLoaded;
            isReadyToDepart = true;
            StartCoroutine(DepartAfterDelay());
            return;
        }

        // Check if all passengers have reached their seats
        bool allSeated = true;
        foreach (NPCController passenger in assignedPassengers)
        {
            if (passenger != null && !passenger.HasReachedDestination())
            {
                allSeated = false;
                break;
            }
        }

        // If all passengers are seated, prepare to depart
        if (allSeated && !isReadyToDepart)
        {
            currentState = VehicleState.PassengersLoaded;
            isReadyToDepart = true;
            StartCoroutine(DepartAfterDelay());
        }
    }

    /// <summary>
    /// Departs after a short delay
    /// </summary>
    private IEnumerator DepartAfterDelay()
    {
        yield return new WaitForSeconds(departDelay);
        Depart();
    }

    /// <summary>
    /// Starts the departure sequence
    /// </summary>
    public void Depart()
    {
        if (assignedParkSpace != null && parkingManager != null)
        {
            // Get the departure path from the parking space
            departurePathPoints = assignedParkSpace.GetDeparturePathPositions();

            // Notify manager that we're departing
            parkingManager.VehicleDeparting(this, assignedParkSpace);
        }

        // Update vehicle state
        isParked = false;
        isDriving = true;
        isReadyToDepart = false;

        // Clear the passenger list
        assignedPassengers.Clear();

        // Reset and hide passenger UI
        VehiclePassengerUIManager passengerUIManager = GetComponent<VehiclePassengerUIManager>();
        if (passengerUIManager != null)
        {
            passengerUIManager.ResetAllSeats();
            passengerUIManager.OnVehicleDeparting(); // Explicit call to hide UI
        }

        // If we have a departure path, follow it
        if (departurePathPoints != null && departurePathPoints.Length > 0)
        {
            Debug.Log("Starting departure path with " + departurePathPoints.Length + " points");
            currentState = VehicleState.FollowingDeparturePath;
            isFollowingDeparturePath = true;
            currentPathIndex = 0;

            // Set initial target to first point (parking entry/exit point)
            targetPosition = departurePathPoints[0].position;
            targetRotation = departurePathPoints[0].rotation;
        }
        else
        {
            // No path, just depart in current direction
            Debug.LogWarning("No departure path found, departing directly");
            currentState = VehicleState.Departing;
            SetFinalDepartureTarget();
        }
    }

    /// <summary>
    /// Gets the current state of the vehicle
    /// </summary>
    public VehicleState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// Gets the maximum passenger capacity
    /// </summary>
    public int GetPassengerCapacity()
    {
        return passengerCapacity;
    }

    /// <summary>
    /// Gets the number of available seats
    /// </summary>
    public int GetAvailableSeats()
    {
        return passengerCapacity - assignedPassengers.Count;
    }

    /// <summary>
    /// Gets the vehicle size
    /// </summary>
    public ParkingSpaceManager.VehicleSize GetVehicleSize()
    {
        return vehicleSize;
    }

    /// <summary>
    /// Returns if the vehicle is currently assigned to a parking space
    /// </summary>
    public bool IsAssigned()
    {
        return isAssigned;
    }

    /// <summary>
    /// Sets whether this vehicle is assigned to a parking space
    /// </summary>
    public void SetAssigned(bool assigned)
    {
        isAssigned = assigned;
        if (!assigned)
        {
            assignedParkSpace = null;
            currentState = VehicleState.Idle;
        }
    }

    /// <summary>
    /// Force the vehicle to depart immediately
    /// </summary>
    public void ForceDeparture()
    {
        StopAllCoroutines();
        Depart();
    }

    /// <summary>
    /// Returns the boarding delay for this vehicle type
    /// </summary>
    public float GetPassengerBoardingDelay()
    {
        return passengerBoardingDelay;
    }

    /// <summary>
    /// Called by PassengerAccommodationManager when a passenger reaches their seat
    /// </summary>
    public void NotifyPassengerSeated(int seatIndex)
    {
        // Update UI directly
        VehiclePassengerUIManager passengerUIManager = GetComponent<VehiclePassengerUIManager>();
        if (passengerUIManager != null)
        {
            passengerUIManager.SetSeatOccupied(seatIndex, true);
        }
    }

    private void OnDestroy()
    {
        // Unregister from parking manager when destroyed
        if (parkingManager != null)
        {
            parkingManager.UnregisterVehicle(this);
        }
    }

#if UNITY_EDITOR
    [Button("Force Departure")]
    private void ForceVehicleDeparture()
    {
        ForceDeparture();
    }
#endif
}