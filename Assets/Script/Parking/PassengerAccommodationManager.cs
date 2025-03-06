using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PassengerAccommodationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCManager npcManager;
    [SerializeField] private ParkingSpaceManager parkingManager;
    [SerializeField] private SplineQueueController queueController;

    [Header("Settings")]
    [SerializeField] private float passengerProcessTime = 0.5f;
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

    private void Update()
    {
        // Debug info every frame to see what's happening
        Debug.Log($"[DEBUG] Processing: {isProcessingPassenger}, Vehicles: {pendingVehicles.Count}, NPCs: {npcManager.GetNPCCount()}");

        // If we're not processing and have vehicles + NPCs, force try again every frame
        if (!isProcessingPassenger && pendingVehicles.Count > 0 && npcManager.GetNPCCount() > 0)
        {
            Debug.Log("[DEBUG] Forcing process next passenger");
            ProcessNextPassenger();
        }
    }

    /// <summary>
    /// Assigns passengers from the queue to a vehicle
    /// </summary>
    public void AssignPassengersToVehicle(VehicleController vehicle, ParkSpaceController parkSpace)
    {
        Debug.Log($"[DEBUG] Vehicle {vehicle.name} ready for passengers at space {parkSpace.name}");

        if (vehicle == null || npcManager == null)
            return;

        // Add this vehicle to our pending queue
        pendingVehicles.Enqueue(new VehicleData(vehicle, parkSpace));
        Debug.Log($"[DEBUG] Vehicle added to pending queue. Total pending: {pendingVehicles.Count}");

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
        Debug.Log("[DEBUG] Starting ProcessNextPassenger()");

        // If no vehicles are waiting or no passengers available, return
        if (pendingVehicles.Count == 0)
        {
            Debug.Log("[DEBUG] No vehicles waiting for passengers");
            isProcessingPassenger = false;
            return;
        }

        if (npcManager.GetNPCCount() == 0)
        {
            Debug.Log("[DEBUG] No NPCs available in queue");
            isProcessingPassenger = false;

            // If we have vehicles but no passengers, tell all vehicles to depart
            while (pendingVehicles.Count > 0)
            {
                VehicleData vd = pendingVehicles.Dequeue();
                vd.vehicle.Depart();
                Debug.Log($"[DEBUG] No passengers available, vehicle {vd.vehicle.name} departing");
            }

            return;
        }

        isProcessingPassenger = true;

        // Get the next vehicle from the queue
        VehicleData vehicleData = pendingVehicles.Peek();
        Debug.Log($"[DEBUG] Processing vehicle: {vehicleData.vehicle.name}, RemainingCapacity: {vehicleData.remainingCapacity}");

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
                Debug.Log($"[DEBUG] Vehicle color: {vehicleColor}");
            }
            else
            {
                Debug.Log("[DEBUG] Vehicle has no VehicleColorController, disabling color matching for this vehicle");
            }
        }

        // Find first matching NPC based on color
        NPCController matchingNPC = null;
        int matchingIndex = -1;

        if (enforceColorMatching && hasVehicleColorComponent)
        {
            // Search all NPCs in queue for a color match
            Debug.Log($"[DEBUG] Searching for NPC with color {vehicleColor} among {npcManager.GetNPCCount()} NPCs");
            for (int i = 0; i < npcManager.GetNPCCount(); i++)
            {
                NPCController npc = npcManager.GetNPCAtIndex(i);
                if (npc != null)
                {
                    NPCColorController npcColorController = npc.GetComponent<NPCColorController>();
                    if (npcColorController != null)
                    {
                        Debug.Log($"[DEBUG] NPC #{i} color: {npcColorController.GetNPCColor()}");
                        if (npcColorController.GetNPCColor() == vehicleColor)
                        {
                            matchingNPC = npc;
                            matchingIndex = i;
                            Debug.Log($"[DEBUG] Found matching NPC at index {i}!");
                            break;
                        }
                    }
                    else
                    {
                        Debug.Log($"[DEBUG] NPC #{i} has no NPCColorController!");
                    }
                }
                else
                {
                    Debug.Log($"[DEBUG] NPC at index {i} is null!");
                }
            }

            // If no matching NPC, check if we need to skip this vehicle
            if (matchingNPC == null)
            {
                Debug.Log($"[DEBUG] No matching NPC found for vehicle with color {vehicleColor}");

                // Skip this vehicle and try the next one
                pendingVehicles.Dequeue();

                // If there are more vehicles, retry with the next one
                if (pendingVehicles.Count > 0)
                {
                    Debug.Log("[DEBUG] Trying next vehicle");
                    isProcessingPassenger = false;
                    StartCoroutine(ProcessNextPassengerAfterDelay(0.2f)); // Quick retry with next vehicle
                }
                else
                {
                    Debug.Log("[DEBUG] No more vehicles to try");
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
            Debug.Log($"[DEBUG] Not enforcing color matching, taking first NPC: {(matchingNPC != null ? matchingNPC.name : "null")}");
        }

        if (matchingNPC != null)
        {
            Debug.Log($"[DEBUG] Processing passenger: {matchingNPC.name} for vehicle {vehicleData.vehicle.name}");

            // Create a list with just this one passenger for the vehicle
            List<NPCController> passengerList = new List<NPCController> { matchingNPC };

            // If this is the first passenger for this vehicle, assign the passenger list
            if (vehicleData.remainingCapacity == vehicleData.vehicle.GetPassengerCapacity())
            {
                Debug.Log("[DEBUG] Assigning first passenger to vehicle");
                vehicleData.vehicle.AssignPassengers(passengerList);
            }
            else
            {
                // Add this passenger to the existing list
                Debug.Log("[DEBUG] Adding additional passenger to vehicle");
                vehicleData.vehicle.AddPassenger(matchingNPC);
            }

            // Unregister NPC from queue
            Debug.Log("[DEBUG] Unregistering NPC from queue");
            npcManager.UnregisterNPC(matchingNPC);

            // Determine seat position and set custom destination for the NPC
            int seatIndex = vehicleData.vehicle.GetPassengerCapacity() - vehicleData.remainingCapacity;
            Vector3 seatPosition = vehicleData.vehicle.CalculateSeatPosition(seatIndex);
            Debug.Log($"[DEBUG] Setting NPC destination to seat {seatIndex}: {seatPosition}");
            matchingNPC.SetCustomDestination(seatPosition, vehicleData.vehicle.transform.rotation);

            // Start monitoring this NPC for boarding completion with proper seat index
            StartCoroutine(MonitorPassengerBoarding(matchingNPC, vehicleData.vehicle, seatIndex));

            // Update remaining capacity
            vehicleData = pendingVehicles.Dequeue();
            vehicleData.remainingCapacity--;
            Debug.Log($"[DEBUG] Updated remaining capacity: {vehicleData.remainingCapacity}");

            // If this vehicle still has capacity, put it back in the queue
            if (vehicleData.remainingCapacity > 0)
            {
                Debug.Log("[DEBUG] Vehicle still has capacity, adding back to queue");
                pendingVehicles.Enqueue(vehicleData);
            }
            else
            {
                // Signal vehicle that loading is complete
                Debug.Log("[DEBUG] Vehicle at full capacity, finishing loading");
                parkingManager.VehicleLoadingComplete(vehicleData.vehicle);
            }

            // Wait before processing next passenger
            Debug.Log("[DEBUG] Starting delay before processing next passenger");
            StartCoroutine(ProcessNextPassengerAfterDelay());
        }
        else
        {
            // No more passengers in queue, tell remaining vehicles to depart
            Debug.Log("[DEBUG] No matching passengers, vehicles departing");
            while (pendingVehicles.Count > 0)
            {
                VehicleData vd = pendingVehicles.Dequeue();
                vd.vehicle.Depart();
                Debug.Log($"[DEBUG] Vehicle {vd.vehicle.name} departing");
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
        {
            Debug.Log("[DEBUG] MonitorPassengerBoarding: NPC or vehicle is null!");
            yield break;
        }

        Debug.Log($"[DEBUG] Monitoring passenger {passenger.name} boarding to seat {seatIndex}");

        // Wait until the passenger has reached their destination
        while (!passenger.HasReachedDestination())
        {
            Debug.Log($"[DEBUG] Passenger {passenger.name} still moving to seat {seatIndex}");
            yield return new WaitForSeconds(0.2f);

            // Safety check in case passenger was destroyed for other reasons
            if (passenger == null)
            {
                Debug.Log("[DEBUG] Passenger was destroyed while moving to seat");
                yield break;
            }
        }

        // Passenger has reached their seat, notify vehicle to update UI
        if (vehicle != null)
        {
            Debug.Log($"[DEBUG] Passenger reached seat {seatIndex}, notifying vehicle");
            vehicle.NotifyPassengerSeated(seatIndex);
        }
        else
        {
            Debug.Log("[DEBUG] Vehicle is null when passenger reached seat!");
        }

        // Wait before destroying NPC (configurable delay)
        float boardingDelay = vehicle != null ? vehicle.GetPassengerBoardingDelay() : 0.5f;
        Debug.Log($"[DEBUG] Waiting {boardingDelay}s before destroying passenger");
        yield return new WaitForSeconds(boardingDelay);

        // Destroy the NPC once they're seated
        if (passenger != null)
        {
            Debug.Log($"[DEBUG] Passenger {passenger.name} has boarded, destroying NPC");
            Destroy(passenger.gameObject);
        }
        else
        {
            Debug.Log("[DEBUG] Passenger already destroyed before boarding completion");
        }
    }

    /// <summary>
    /// Wait for a delay then process the next passenger
    /// </summary>
    private IEnumerator ProcessNextPassengerAfterDelay(float overrideDelay = -1)
    {
        float delay = overrideDelay > 0 ? overrideDelay : passengerProcessTime;
        Debug.Log($"[DEBUG] Waiting {delay}s before processing next passenger");
        yield return new WaitForSeconds(delay);
        Debug.Log("[DEBUG] Delay complete, processing next passenger");
        isProcessingPassenger = false;
        ProcessNextPassenger();
    }
}