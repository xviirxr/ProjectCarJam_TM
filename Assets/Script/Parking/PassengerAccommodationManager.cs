using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the process of moving passengers from queue to vehicles
/// </summary>
public class PassengerAccommodationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCManager npcManager;
    [SerializeField] private ParkingSpaceManager parkingManager;
    [SerializeField] private SplineQueueController queueController;

    [Header("Settings")]
    [SerializeField] private float passengerProcessTime = 0.5f;
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool enforceColorMatching = true;

    // Track if we're currently processing a passenger
    private bool isProcessingPassenger = false;

    // Keep track of vehicles waiting for passengers
    private Queue<VehicleData> pendingVehicles = new Queue<VehicleData>();

    // Structure to store vehicle and its parking space
    private struct VehicleData
    {
        public VehicleController vehicle;
        public ParkSpaceController parkSpace;
        public int remainingCapacity;

        public VehicleData(VehicleController v, ParkSpaceController p)
        {
            vehicle = v;
            parkSpace = p;
            remainingCapacity = v.GetPassengerCapacity();
        }
    }

    private void Awake()
    {
        // Auto-find references if not assigned
        if (npcManager == null)
            npcManager = FindFirstObjectByType<NPCManager>();

        if (parkingManager == null)
            parkingManager = FindFirstObjectByType<ParkingSpaceManager>();

        if (queueController == null)
            queueController = FindFirstObjectByType<SplineQueueController>();
    }

    /// <summary>
    /// Assigns passengers from the queue to a vehicle
    /// </summary>
    public void AssignPassengersToVehicle(VehicleController vehicle, ParkSpaceController parkSpace)
    {
        if (vehicle == null || npcManager == null)
            return;

        // Add this vehicle to our pending queue
        pendingVehicles.Enqueue(new VehicleData(vehicle, parkSpace));
        DebugLog($"Vehicle added to pending queue. Total pending: {pendingVehicles.Count}");

        // If we're not already processing a passenger, start the process
        if (!isProcessingPassenger)
        {
            ProcessNextPassenger();
        }
    }

    /// <summary>
    /// Process the next passenger in the queue
    /// </summary>
    private void ProcessNextPassenger()
    {
        // If no vehicles are waiting or no passengers available, return
        if (pendingVehicles.Count == 0 || npcManager.GetNPCCount() == 0)
        {
            isProcessingPassenger = false;

            // If we have vehicles but no passengers, tell all vehicles to depart
            while (pendingVehicles.Count > 0)
            {
                VehicleData vd = pendingVehicles.Dequeue();
                vd.vehicle.Depart();
                DebugLog($"No passengers available, vehicle departing");
            }

            return;
        }

        isProcessingPassenger = true;

        // Get the next vehicle from the queue
        VehicleData vehicleData = pendingVehicles.Peek();

        // Get vehicle color if color matching is enabled
        ColorCodeManager.ColorCode vehicleColor = ColorCodeManager.ColorCode.Red; // Default
        bool hasVehicleColorComponent = false;

        if (enforceColorMatching)
        {
            VehicleColorController vehicleColorController = vehicleData.vehicle.GetComponent<VehicleColorController>();
            if (vehicleColorController != null)
            {
                vehicleColor = vehicleColorController.GetVehicleColor();
                hasVehicleColorComponent = true;
            }
            else
            {
                DebugLog("Vehicle has no VehicleColorController, disabling color matching for this vehicle");
            }
        }

        // Find first matching NPC based on color
        NPCController matchingNPC = null;
        int matchingIndex = -1;

        if (enforceColorMatching && hasVehicleColorComponent)
        {
            // Search all NPCs in queue for a color match
            for (int i = 0; i < npcManager.GetNPCCount(); i++)
            {
                NPCController npc = npcManager.GetNPCAtIndex(i);
                if (npc != null)
                {
                    NPCColorController npcColorController = npc.GetComponent<NPCColorController>();
                    if (npcColorController != null)
                    {
                        if (npcColorController.GetNPCColor() == vehicleColor)
                        {
                            matchingNPC = npc;
                            matchingIndex = i;
                            break;
                        }
                    }
                }
            }

            // If no matching NPC, check if we need to skip this vehicle
            if (matchingNPC == null)
            {
                DebugLog($"No matching NPC found for vehicle with color {vehicleColor}");

                // Skip this vehicle and try the next one
                pendingVehicles.Dequeue();

                // If there are more vehicles, retry with the next one
                if (pendingVehicles.Count > 0)
                {
                    StartCoroutine(ProcessNextPassengerAfterDelay(0.2f)); // Quick retry with next vehicle
                }
                else
                {
                    isProcessingPassenger = false;
                }

                return;
            }
        }
        else
        {
            // If not enforcing color matching, just get the first NPC
            matchingNPC = npcManager.GetNPCAtIndex(0);
            matchingIndex = 0;
        }

        if (matchingNPC != null)
        {
            DebugLog($"Processing passenger: {matchingNPC.name} for vehicle");

            // Create a list with just this one passenger for the vehicle
            List<NPCController> passengerList = new List<NPCController> { matchingNPC };

            // If this is the first passenger for this vehicle, assign the passenger list
            if (vehicleData.remainingCapacity == vehicleData.vehicle.GetPassengerCapacity())
            {
                vehicleData.vehicle.AssignPassengers(passengerList);
            }
            else
            {
                // Add this passenger to the existing list
                vehicleData.vehicle.AddPassenger(matchingNPC);
            }

            // Unregister NPC from queue
            npcManager.UnregisterNPC(matchingNPC);

            // Determine seat position and set custom destination for the NPC
            int seatIndex = vehicleData.vehicle.GetPassengerCapacity() - vehicleData.remainingCapacity;
            Vector3 seatPosition = vehicleData.vehicle.CalculateSeatPosition(seatIndex);
            matchingNPC.SetCustomDestination(seatPosition, vehicleData.vehicle.transform.rotation);

            // Start monitoring this NPC for boarding completion with proper seat index
            StartCoroutine(MonitorPassengerBoarding(matchingNPC, vehicleData.vehicle, seatIndex));

            // Update remaining capacity
            vehicleData = pendingVehicles.Dequeue();
            vehicleData.remainingCapacity--;

            // If this vehicle still has capacity, put it back in the queue
            if (vehicleData.remainingCapacity > 0)
            {
                pendingVehicles.Enqueue(vehicleData);
            }
            else
            {
                // Signal vehicle that loading is complete
                parkingManager.VehicleLoadingComplete(vehicleData.vehicle);
            }

            // Wait before processing next passenger
            StartCoroutine(ProcessNextPassengerAfterDelay());
        }
        else
        {
            // No more passengers in queue, tell remaining vehicles to depart
            DebugLog("No more passengers in queue, vehicles departing");
            while (pendingVehicles.Count > 0)
            {
                VehicleData vd = pendingVehicles.Dequeue();
                vd.vehicle.Depart();
            }

            isProcessingPassenger = false;
        }
    }

    /// <summary>
    /// Monitors an NPC until they have reached their destination (vehicle seat)
    /// Then destroys the NPC once they're seated
    /// </summary>
    private IEnumerator MonitorPassengerBoarding(NPCController passenger, VehicleController vehicle, int seatIndex)
    {
        if (passenger == null || vehicle == null)
            yield break;

        // Wait until the passenger has reached their destination
        while (!passenger.HasReachedDestination())
        {
            yield return new WaitForSeconds(0.2f);

            // Safety check in case passenger was destroyed for other reasons
            if (passenger == null)
                yield break;
        }

        // Passenger has reached their seat, notify vehicle to update UI
        if (vehicle != null)
        {
            DebugLog($"Passenger reached seat {seatIndex}, notifying vehicle");
            vehicle.NotifyPassengerSeated(seatIndex);
        }

        // Wait before destroying NPC (configurable delay)
        float boardingDelay = vehicle != null ? vehicle.GetPassengerBoardingDelay() : 0.5f;
        yield return new WaitForSeconds(boardingDelay);

        // Destroy the NPC once they're seated
        if (passenger != null)
        {
            DebugLog($"Passenger {passenger.name} has boarded, destroying NPC");
            Destroy(passenger.gameObject);
        }
    }

    /// <summary>
    /// Wait for a delay then process the next passenger
    /// </summary>
    private IEnumerator ProcessNextPassengerAfterDelay(float overrideDelay = -1)
    {
        float delay = overrideDelay > 0 ? overrideDelay : passengerProcessTime;
        yield return new WaitForSeconds(delay);
        ProcessNextPassenger();
    }

    /// <summary>
    /// Debug logging helper
    /// </summary>
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PassengerAccommodation] {message}");
    }
}