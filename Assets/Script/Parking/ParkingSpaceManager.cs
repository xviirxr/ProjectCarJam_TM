using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

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

    [Header("Color Matching")]
    [SerializeField] private bool enforceColorMatching = true;

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

    public void RegisterVehicle(VehicleController vehicle)
    {
        if (vehicle != null && !availableVehicles.Contains(vehicle))
        {
            availableVehicles.Add(vehicle);

            // Get the vehicle's color for logging
            string colorInfo = "";
            if (enforceColorMatching)
            {
                VehicleColorController colorController = vehicle.GetComponent<VehicleColorController>();
                if (colorController != null)
                {
                    colorInfo = $", Color: {colorController.GetVehicleColor()}";
                }
            }

            Debug.Log($"Vehicle registered: {vehicle.name}, Size: {vehicle.GetVehicleSize()}{colorInfo}, Total vehicles: {availableVehicles.Count}");
        }
    }

    public void UnregisterVehicle(VehicleController vehicle)
    {
        if (vehicle != null)
        {
            availableVehicles.Remove(vehicle);
            Debug.Log($"Vehicle unregistered: {vehicle.name}, Remaining vehicles: {availableVehicles.Count}");
        }
    }

    public bool HasVehicleAvailableForColor(ColorCodeManager.ColorCode npcColor)
    {
        if (!enforceColorMatching)
            return availableVehicles.Count > 0;

        foreach (VehicleController vehicle in availableVehicles)
        {
            if (vehicle != null && !vehicle.IsAssigned())
            {
                VehicleColorController colorController = vehicle.GetComponent<VehicleColorController>();
                if (colorController != null && colorController.GetVehicleColor() == npcColor)
                {
                    return true;
                }
            }
        }

        return false;
    }

#if UNITY_EDITOR
    [Button("Summon Vehicle")]
    public void SummonVehicleButton(VehicleSize vehicleSize)
    {
        SummonVehicle(vehicleSize);
    }

    [Button("Summon Red Vehicle")]
    public void SummonRedVehicle(VehicleSize vehicleSize)
    {
        SummonVehicleWithColor(vehicleSize, ColorCodeManager.ColorCode.Red);
    }

    [Button("Summon Blue Vehicle")]
    public void SummonBlueVehicle(VehicleSize vehicleSize)
    {
        SummonVehicleWithColor(vehicleSize, ColorCodeManager.ColorCode.Blue);
    }

    [Button("Summon Yellow Vehicle")]
    public void SummonYellowVehicle(VehicleSize vehicleSize)
    {
        SummonVehicleWithColor(vehicleSize, ColorCodeManager.ColorCode.Yellow);
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

    public void SummonVehicle(VehicleSize vehicleSize)
    {
        // No longer assigning colors directly
        SummonVehicleImplementation(vehicleSize, null);
    }

    public void SummonVehicleWithColor(VehicleSize vehicleSize, ColorCodeManager.ColorCode color)
    {
        SummonVehicleImplementation(vehicleSize, color);
    }

    private void SummonVehicleImplementation(VehicleSize vehicleSize, ColorCodeManager.ColorCode? color)
    {
        // Find an available parking space
        ParkSpaceController availableSpace = FindAvailableParkingSpace();
        if (availableSpace == null)
        {
            Debug.LogWarning("No available parking spaces!");
            return;
        }

        // Try to find an available vehicle of the requested size
        VehicleController availableVehicle = null;

        if (color.HasValue)
        {
            // Find vehicle with matching color
            availableVehicle = FindAvailableVehicleWithColor(vehicleSize, color.Value);
        }
        else
        {
            // Find any vehicle of the right size
            availableVehicle = FindAvailableVehicle(vehicleSize);
        }

        if (availableVehicle != null)
        {
            // Use an existing vehicle
            string colorInfo = "";
            if (color.HasValue)
            {
                colorInfo = $" with color {color.Value}";

                // Use the vehicle's existing color controller to set the color
                // This will now handle unregistering with ColorCodeManager
                VehicleColorController colorController = availableVehicle.GetComponent<VehicleColorController>();
                if (colorController != null)
                {
                    colorController.SetVehicleColor(color.Value);
                }
            }

            Debug.Log($"Using existing vehicle: {availableVehicle.name}{colorInfo}");
            availableVehicle.AssignParkingSpace(availableSpace);
            availableSpace.AssignVehicle(availableVehicle);
        }
        else
        {
            string colorInfo = color.HasValue ? $" with color {color.Value}" : "";
            Debug.LogWarning($"No available vehicle of size {vehicleSize}{colorInfo} found!");
        }
    }

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

    private VehicleController FindAvailableVehicleWithColor(VehicleSize size, ColorCodeManager.ColorCode color)
    {
        foreach (VehicleController vehicle in availableVehicles)
        {
            if (vehicle != null && vehicle.GetVehicleSize() == size && !vehicle.IsAssigned())
            {
                VehicleColorController colorController = vehicle.GetComponent<VehicleColorController>();
                if (colorController != null && colorController.GetVehicleColor() == color)
                {
                    return vehicle;
                }
            }
        }
        return null;
    }

    private ParkSpaceController FindAvailableParkingSpace()
    {
        foreach (ParkSpaceController space in parkingSpaces)
        {
            if (space != null && !space.IsOccupied())
                return space;
        }
        return null;
    }

    public NPCManager GetNPCManager()
    {
        return npcManager;
    }

    public PassengerAccommodationManager GetPassengerManager()
    {
        return passengerManager;
    }

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

    public void VehicleLoadingComplete(VehicleController vehicle)
    {
        // If using service queue, this indicates the vehicle is done with its passenger assignment
        // We don't start servicing the next vehicle yet because we need to wait for this one to depart
        // That's handled in VehicleDeparting
    }

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

    public ParkSpaceController[] GetParkingSpaces()
    {
        return parkingSpaces;
    }

    public ParkSpaceController GetParkingSpace(int index)
    {
        if (index >= 0 && index < parkingSpaces.Length)
            return parkingSpaces[index];
        return null;
    }
}