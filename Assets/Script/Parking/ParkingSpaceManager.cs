using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

/// <summary>
/// Manages the parking spaces for vehicles picking up passengers
/// </summary>
public class ParkingSpaceManager : MonoBehaviour
{
    public enum VehicleSize
    {
        Small,  // 4 passengers
        Medium, // 6 passengers
        Large   // 8 passengers
    }

    [Header("Parking Space Setup")]
    [SerializeField] private ParkSpaceController[] parkingSpaces;

    [Header("References")]
    [SerializeField] private PassengerAccommodationManager passengerManager;
    [SerializeField] private NPCManager npcManager;

    [Header("Vehicle Servicing")]
    [SerializeField] private bool serviceOneVehicleAtATime = true;

    private List<VehicleController> availableVehicles = new List<VehicleController>();
    private bool isServicingVehicle = false;
    private Queue<VehicleController> vehicleServiceQueue = new Queue<VehicleController>();

    private void Awake()
    {
        // Auto-find references if not assigned
        if (passengerManager == null)
            passengerManager = FindFirstObjectByType<PassengerAccommodationManager>();

        if (npcManager == null)
            npcManager = FindFirstObjectByType<NPCManager>();

        // Validate parking spaces array
        if (parkingSpaces == null || parkingSpaces.Length == 0)
        {
            parkingSpaces = GetComponentsInChildren<ParkSpaceController>();
            if (parkingSpaces.Length == 0)
                Debug.LogError("No parking spaces found for ParkingSpaceManager!");
        }

        // Initialize each parking space
        for (int i = 0; i < parkingSpaces.Length; i++)
        {
            if (parkingSpaces[i] != null)
                parkingSpaces[i].Initialize(this, i);
        }
    }

    /// <summary>
    /// Registers a vehicle with the manager
    /// </summary>
    public void RegisterVehicle(VehicleController vehicle)
    {
        if (vehicle != null && !availableVehicles.Contains(vehicle))
        {
            availableVehicles.Add(vehicle);
            Debug.Log($"Vehicle registered: {vehicle.name}, Size: {vehicle.GetVehicleSize()}, Total vehicles: {availableVehicles.Count}");
        }
    }

    /// <summary>
    /// Unregisters a vehicle from the manager
    /// </summary>
    public void UnregisterVehicle(VehicleController vehicle)
    {
        if (vehicle != null)
        {
            availableVehicles.Remove(vehicle);
            Debug.Log($"Vehicle unregistered: {vehicle.name}, Remaining vehicles: {availableVehicles.Count}");
        }
    }

#if UNITY_EDITOR
    [Button("Summon Vehicle")]
    public void SummonVehicleButton(VehicleSize vehicleSize)
    {
        SummonVehicle(vehicleSize);
    }

    [ShowInInspector, ReadOnly]
    public List<VehicleController> RegisteredVehicles => availableVehicles;

    [ShowInInspector, ReadOnly]
    public int AvailableVehicleCount => availableVehicles.Count;

    [ShowInInspector, ReadOnly]
    public int AvailableParkingSpaces
    {
        get
        {
            int count = 0;
            foreach (var space in parkingSpaces)
            {
                if (space != null && !space.IsOccupied())
                    count++;
            }
            return count;
        }
    }

    [ShowInInspector, ReadOnly]
    public int VehiclesInServiceQueue => vehicleServiceQueue.Count;
#endif

    /// <summary>
    /// Summons a vehicle of the specified size
    /// </summary>
    public void SummonVehicle(VehicleSize vehicleSize)
    {
        // Find an available parking space
        ParkSpaceController availableSpace = FindAvailableParkingSpace();
        if (availableSpace == null)
        {
            Debug.LogWarning("No available parking spaces!");
            return;
        }

        // Try to find an available vehicle of the requested size
        VehicleController availableVehicle = FindAvailableVehicle(vehicleSize);

        if (availableVehicle != null)
        {
            // Use an existing vehicle
            Debug.Log($"Using existing vehicle: {availableVehicle.name}");
            availableVehicle.AssignParkingSpace(availableSpace);
            availableSpace.AssignVehicle(availableVehicle);
        }
        else
        {
            Debug.LogWarning($"No available vehicle of size {vehicleSize} found!");
        }
    }

    /// <summary>
    /// Finds an available vehicle of the specified size
    /// </summary>
    private VehicleController FindAvailableVehicle(VehicleSize size)
    {
        foreach (VehicleController vehicle in availableVehicles)
        {
            if (vehicle != null && vehicle.GetVehicleSize() == size && !vehicle.IsAssigned())
            {
                return vehicle;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds an available parking space
    /// </summary>
    private ParkSpaceController FindAvailableParkingSpace()
    {
        foreach (ParkSpaceController space in parkingSpaces)
        {
            if (space != null && !space.IsOccupied())
                return space;
        }
        return null;
    }

    /// <summary>
    /// Gets the NPCManager reference
    /// </summary>
    public NPCManager GetNPCManager()
    {
        return npcManager;
    }

    /// <summary>
    /// Gets the PassengerAccommodationManager reference
    /// </summary>
    public PassengerAccommodationManager GetPassengerManager()
    {
        return passengerManager;
    }

    /// <summary>
    /// Vehicle calls this when it's ready to load passengers
    /// </summary>
    public void VehicleReadyForPassengers(VehicleController vehicle, ParkSpaceController parkSpace)
    {
        if (serviceOneVehicleAtATime)
        {
            // Add to service queue
            vehicleServiceQueue.Enqueue(vehicle);

            // If not currently servicing, start with this vehicle
            if (!isServicingVehicle)
            {
                ServiceNextVehicle();
            }
        }
        else
        {
            // Original behavior - service immediately
            if (passengerManager != null)
            {
                passengerManager.AssignPassengersToVehicle(vehicle, parkSpace);
            }
        }
    }

    /// <summary>
    /// Services the next vehicle in the queue
    /// </summary>
    private void ServiceNextVehicle()
    {
        if (vehicleServiceQueue.Count == 0)
        {
            isServicingVehicle = false;
            return;
        }

        isServicingVehicle = true;
        VehicleController nextVehicle = vehicleServiceQueue.Dequeue();

        if (nextVehicle != null && passengerManager != null)
        {
            ParkSpaceController parkSpace = null;

            // Find which parking space this vehicle is assigned to
            foreach (ParkSpaceController space in parkingSpaces)
            {
                if (space != null && space.GetAssignedVehicle() == nextVehicle)
                {
                    parkSpace = space;
                    break;
                }
            }

            if (parkSpace != null)
            {
                passengerManager.AssignPassengersToVehicle(nextVehicle, parkSpace);
            }
        }
    }

    /// <summary>
    /// Called when a vehicle completes passenger assignment (but may still be loading passengers)
    /// </summary>
    public void VehicleLoadingComplete(VehicleController vehicle)
    {
        // If using service queue, this indicates the vehicle is done with its passenger assignment
        // We don't start servicing the next vehicle yet because we need to wait for this one to depart
        // That's handled in VehicleDeparting
    }

    /// <summary>
    /// Vehicle calls this when it's ready to depart
    /// </summary>
    public void VehicleDeparting(VehicleController vehicle, ParkSpaceController parkSpace)
    {
        // Free up the parking space
        if (parkSpace != null)
            parkSpace.ClearVehicle();

        // Set vehicle as available again
        if (vehicle != null)
        {
            vehicle.SetAssigned(false);
        }

        // If using service queue, service the next vehicle
        if (serviceOneVehicleAtATime && isServicingVehicle)
        {
            // Wait a short moment before servicing next vehicle
            StartCoroutine(ServiceNextAfterDelay(0.5f));
        }
    }

    private IEnumerator ServiceNextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ServiceNextVehicle();
    }

    /// <summary>
    /// Gets all parking spaces
    /// </summary>
    public ParkSpaceController[] GetParkingSpaces()
    {
        return parkingSpaces;
    }

    /// <summary>
    /// Gets a specific parking space by index
    /// </summary>
    public ParkSpaceController GetParkingSpace(int index)
    {
        if (index >= 0 && index < parkingSpaces.Length)
            return parkingSpaces[index];
        return null;
    }
}