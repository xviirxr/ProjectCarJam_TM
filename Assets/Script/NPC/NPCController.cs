using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class NPCController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string walkParameterName = "isWalking";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private SplineContainer queueSpline;
    private NPCManager npcManager;
    private int queueIndex;
    private float currentPosition;
    private float targetPosition;
    private bool hasReachedDestination;
    private float walkSpeed;
    private float spacingBetweenNPCs;
    private float splineLength;

    private bool isCustomDestination = false;
    private Vector3 customDestinationPosition;
    private Quaternion customDestinationRotation;

    private VehicleController targetVehicle = null;
    private int targetSeatIndex = -1;
    private bool isBoardingVehicle = false;
    private float checkVehicleTimer = 0f;
    private float checkVehicleInterval = 1f;

    private Animator animator;
    private float lastQueueCheckTime = 0f;
    private float queueCheckInterval = 0.2f;

    private void Awake()
    {
        if (useAnimator)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("Animator component not found on NPC but useAnimator is enabled.");
            }
        }
    }

    public void Initialize(SplineContainer spline, NPCManager manager, int initialIndex)
    {
        queueSpline = spline;
        npcManager = manager;
        queueIndex = initialIndex;
        currentPosition = 0;
        hasReachedDestination = false;
        isCustomDestination = false;
        isBoardingVehicle = false;
        targetVehicle = null;
        targetSeatIndex = -1;
        lastQueueCheckTime = Time.time;
        checkVehicleTimer = 0f;

        SplineQueueController splineController = npcManager.GetSplineQueueController();
        if (splineController != null)
        {
            walkSpeed = splineController.GetWalkSpeed();
            spacingBetweenNPCs = splineController.GetSpacingBetweenNPCs();
            splineLength = splineController.GetSplineLength();
        }
        else
        {
            Debug.LogError("SplineQueueController reference not available!");
            walkSpeed = 1.0f;
            spacingBetweenNPCs = 1.2f;
            splineLength = 10f;
        }

        UpdateTargetPosition();
    }

    private void Update()
    {
        // Periodically check for vehicle when at front of queue
        if (queueIndex == 0 && !isBoardingVehicle && !isCustomDestination && hasReachedDestination)
        {
            checkVehicleTimer += Time.deltaTime;
            if (checkVehicleTimer >= checkVehicleInterval)
            {
                TryFindVehicleToBoard();
                checkVehicleTimer = 0f;
            }
        }

        // If we are using a custom destination (going to vehicle), handle that movement
        if (isCustomDestination)
        {
            MoveToCustomDestination();
            return;
        }

        // Normal queue behavior
        if (queueSpline == null || npcManager == null)
            return;

        if (!hasReachedDestination)
        {
            MoveAlongSpline();
        }

        PlaceOnSpline();

        // Periodically check for queue changes to avoid excessive updates
        if (Time.time >= lastQueueCheckTime + queueCheckInterval)
        {
            lastQueueCheckTime = Time.time;
            UpdateTargetPosition();
            CheckForwardMovement();
        }
    }

    private void TryFindVehicleToBoard()
    {
        // Get the NPC's color
        NPCColorController npcColorController = GetComponent<NPCColorController>();
        if (npcColorController == null)
        {
            DebugLog("No NPCColorController found on this NPC!");
            return;
        }

        ColorCodeManager.ColorCode myColor = npcColorController.GetNPCColor();
        DebugLog($"Looking for vehicle with color: {myColor}");

        // Find parking manager
        ParkingSpaceManager parkingManager = FindFirstObjectByType<ParkingSpaceManager>();
        if (parkingManager == null)
        {
            DebugLog("No ParkingSpaceManager found in scene!");
            return;
        }

        // Check if there's a vehicle available with my color
        if (!parkingManager.HasVehicleAvailableForColor(myColor))
        {
            DebugLog("No matching vehicle available");
            return;
        }

        // Find a parked vehicle with matching color that has available seats
        foreach (var parkSpace in parkingManager.GetParkingSpaces())
        {
            if (parkSpace == null || !parkSpace.IsOccupied())
                continue;

            VehicleController vehicle = parkSpace.GetAssignedVehicle();
            if (vehicle == null)
                continue;

            // Check if vehicle is properly parked
            if (!vehicle.IsReadyForBoarding())
            {
                DebugLog($"Vehicle found but not ready for boarding (state: {vehicle.GetCurrentState()})");
                continue;
            }

            // Check if vehicle has a color controller
            VehicleColorController vehicleColorController = vehicle.GetComponent<VehicleColorController>();
            if (vehicleColorController == null)
                continue;

            // Check if colors match and vehicle has available seats
            if (vehicleColorController.GetVehicleColor() == myColor && vehicle.GetAvailableSeats() > 0)
            {
                DebugLog($"Found matching vehicle with {vehicle.GetAvailableSeats()} available seats");

                // Board this vehicle
                StartBoardingVehicle(vehicle);
                break;
            }
        }
    }

    private void StartBoardingVehicle(VehicleController vehicle)
    {
        targetVehicle = vehicle;
        isBoardingVehicle = true;

        // Get seat index (based on how many passengers are already in the vehicle)
        targetSeatIndex = vehicle.GetPassengerCapacity() - vehicle.GetAvailableSeats();

        // Calculate seat position
        Vector3 seatPosition = vehicle.CalculateSeatPosition(targetSeatIndex);

        // Set custom destination to that seat
        SetCustomDestination(seatPosition, vehicle.transform.rotation);

        // Register as a passenger with the vehicle
        if (vehicle.GetPassengerCount() == 0)
        {
            // First passenger
            List<NPCController> passengerList = new List<NPCController> { this };
            vehicle.AssignPassengers(passengerList);
        }
        else
        {
            // Additional passenger
            vehicle.AddPassenger(this);
        }

        // Unregister from queue
        npcManager.UnregisterNPC(this);

        DebugLog($"Starting to board vehicle at seat {targetSeatIndex}");
    }

    public void SetCustomDestination(Vector3 position, Quaternion rotation)
    {
        isCustomDestination = true;
        customDestinationPosition = position;
        customDestinationRotation = rotation;
        hasReachedDestination = false;

        if (useAnimator && animator != null)
        {
            animator.SetBool(walkParameterName, true);
        }
    }

    private void MoveToCustomDestination()
    {
        float distance = Vector3.Distance(transform.position, customDestinationPosition);

        if (distance <= 0.1f)
        {
            // Reached destination
            transform.position = customDestinationPosition;
            transform.rotation = customDestinationRotation;

            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, false);
            }

            hasReachedDestination = true;

            // If boarding vehicle and reached destination, notify vehicle
            if (isBoardingVehicle && targetVehicle != null && targetSeatIndex >= 0)
            {
                DebugLog($"Reached seat {targetSeatIndex} in vehicle");

                // Notify vehicle that passenger is seated
                targetVehicle.NotifyPassengerSeated(targetSeatIndex);

                // Start self-destruct sequence after delay
                StartCoroutine(DestroyAfterBoarding());
            }

            return;
        }

        // Still moving to destination
        float speed = walkSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, customDestinationPosition, speed);
        transform.rotation = Quaternion.Slerp(transform.rotation, customDestinationRotation, 5f * Time.deltaTime);

        if (useAnimator && animator != null)
        {
            animator.SetBool(walkParameterName, true);
        }
    }

    private IEnumerator DestroyAfterBoarding()
    {
        if (targetVehicle == null)
            yield break;

        float boardingDelay = targetVehicle.GetPassengerBoardingDelay();
        DebugLog($"Will be destroyed after {boardingDelay} seconds");

        yield return new WaitForSeconds(boardingDelay);

        // Check if vehicle is now full and should depart
        if (targetVehicle.GetAvailableSeats() <= 0)
        {
            DebugLog("Vehicle is now full, signaling departure");
            targetVehicle.Depart();
        }

        // Destroy self after boarding is complete
        DebugLog("Destroying self after boarding");
        Destroy(gameObject);
    }

    private void MoveAlongSpline()
    {
        float step = (walkSpeed / splineLength) * Time.deltaTime;

        if (Mathf.Abs(currentPosition - targetPosition) < step)
        {
            currentPosition = targetPosition;

            // Only mark as reached destination if we're at the final target
            // (important for gap filling behavior)
            if (queueIndex == 0 || Mathf.Approximately(targetPosition, GetFinalTargetPosition()))
            {
                hasReachedDestination = true;

                if (useAnimator && animator != null)
                {
                    animator.SetBool(walkParameterName, false);
                }

                if (queueIndex == 0)
                {
                    OnReachedFront();
                }
            }
        }
        else
        {
            currentPosition = Mathf.MoveTowards(currentPosition, targetPosition, step);
            hasReachedDestination = false;

            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, true);
            }
        }
    }

    private void PlaceOnSpline()
    {
        float t = currentPosition;
        float3 position = queueSpline.EvaluatePosition(t);
        float3 tangent = queueSpline.EvaluateTangent(t);
        float3 up = queueSpline.EvaluateUpVector(t);

        transform.position = position;

        if (math.lengthsq(tangent) > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(tangent, up);
        }
    }

    private float GetFinalTargetPosition()
    {
        if (queueIndex == 0)
        {
            return 1.0f;
        }
        else
        {
            // Calculate where this NPC should be based on its position in line
            float spacing = spacingBetweenNPCs / splineLength;
            return Mathf.Clamp01(1.0f - (queueIndex * spacing));
        }
    }

    public void UpdateTargetPosition()
    {
        if (isCustomDestination)
            return;

        if (queueIndex == 0)
        {
            targetPosition = 1.0f;
        }
        else
        {
            NPCController npcInFront = npcManager.GetNPCAtIndex(queueIndex - 1);

            if (npcInFront != null)
            {
                float spacing = spacingBetweenNPCs / splineLength;

                // Target position is directly behind the NPC in front
                float targetBehindFront = Mathf.Max(0, npcInFront.GetCurrentPosition() - spacing);

                // Check if we should force a gap fill
                // If the NPC in front is far ahead, set our target to follow them
                float currentFrontPos = npcInFront.GetCurrentPosition();
                float minExpectedPosition = 1.0f - ((queueIndex - 1) * spacing);

                if (currentFrontPos > minExpectedPosition + (spacing * 0.5f) &&
                    npcInFront.HasReachedDestination())
                {
                    // Gap detected - move to fill it
                    targetPosition = targetBehindFront;
                    hasReachedDestination = false;
                }
                else
                {
                    targetPosition = targetBehindFront;
                }

                // Update animation state based on whether we need to move
                if (npcInFront.HasReachedDestination() &&
                    Mathf.Abs(currentPosition - targetPosition) < 0.01f)
                {
                    hasReachedDestination = true;

                    if (useAnimator && animator != null)
                    {
                        animator.SetBool(walkParameterName, false);
                    }
                }
            }
            else
            {
                // No NPC in front! We should now be at the front
                queueIndex = 0;
                targetPosition = 1.0f;
                hasReachedDestination = false;
            }
        }
    }

    private void CheckForwardMovement()
    {
        if (isCustomDestination)
            return;

        int currentRealIndex = npcManager.GetIndexOfNPC(this);

        if (currentRealIndex != -1 && currentRealIndex != queueIndex)
        {
            int oldIndex = queueIndex;
            queueIndex = currentRealIndex;

            // Force movement if our position in line has changed
            if (currentRealIndex < oldIndex)
            {
                hasReachedDestination = false;

                if (useAnimator && animator != null)
                {
                    animator.SetBool(walkParameterName, true);
                }
            }

            UpdateTargetPosition();
        }
    }

    protected virtual void OnReachedFront()
    {
        DebugLog("Reached front of queue");

        // Immediately check for a vehicle to board
        TryFindVehicleToBoard();
    }

    public float GetCurrentPosition()
    {
        return currentPosition;
    }

    public bool HasReachedDestination()
    {
        return hasReachedDestination;
    }

    public int GetQueueIndex()
    {
        return queueIndex;
    }

    public void SetQueueIndex(int index)
    {
        if (queueIndex != index)
        {
            queueIndex = index;
            hasReachedDestination = false;
            UpdateTargetPosition();
        }
    }

    public bool IsUsingCustomDestination()
    {
        return isCustomDestination;
    }

    public bool IsBoardingVehicle()
    {
        return isBoardingVehicle;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPC {name}] {message}");
        }
    }

#if UNITY_EDITOR
    [Button("Remove From Queue")]
#endif
    public void RemoveFromQueue()
    {
        if (npcManager != null)
        {
            npcManager.UnregisterNPC(this);
        }

        Destroy(gameObject);
    }
}