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
    [SerializeField] private NPCManager npcManager;
    [SerializeField] private Transform roadExitPoint; // Added road exit point at manager level

    [Header("Color Matching")]
    [SerializeField] private bool enforceColorMatching = true;
    [SerializeField] private bool enableDebugLogs = true;

    private List<VehicleController> availableVehicles = new List<VehicleController>();

    private void Awake()
    {
        // Auto-find references if not assigned
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

    // Added method to get the road exit point
    public Transform GetRoadExitPoint()
    {
        return roadExitPoint;
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

            DebugLog($"Vehicle registered: {vehicle.name}, Size: {vehicle.GetVehicleSize()}{colorInfo}, Total vehicles: {availableVehicles.Count}");
        }
    }

    public void UnregisterVehicle(VehicleController vehicle)
    {
        if (vehicle != null)
        {
            availableVehicles.Remove(vehicle);
            DebugLog($"Vehicle unregistered: {vehicle.name}, Remaining vehicles: {availableVehicles.Count}");
        }
    }

    public bool HasVehicleAvailableForColor(ColorCodeManager.ColorCode npcColor)
    {
        if (!enforceColorMatching)
            return availableVehicles.Count > 0;

        foreach (ParkSpaceController space in parkingSpaces)
        {
            if (space == null || !space.IsOccupied())
                continue;

            VehicleController vehicle = space.GetAssignedVehicle();
            if (vehicle == null)
                continue;

            // Check if vehicle has available seats and matching color
            if (vehicle.GetAvailableSeats() > 0)
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

    public VehicleController GetVehicleForColor(ColorCodeManager.ColorCode npcColor)
    {
        if (!enforceColorMatching)
        {
            // Return the first vehicle with available seats
            foreach (ParkSpaceController space in parkingSpaces)
            {
                if (space == null || !space.IsOccupied())
                    continue;

                VehicleController vehicle = space.GetAssignedVehicle();
                if (vehicle != null && vehicle.GetAvailableSeats() > 0)
                {
                    return vehicle;
                }
            }
            return null;
        }

        // Find a vehicle with matching color and available seats
        foreach (ParkSpaceController space in parkingSpaces)
        {
            if (space == null || !space.IsOccupied())
                continue;

            VehicleController vehicle = space.GetAssignedVehicle();
            if (vehicle == null)
                continue;

            // Check if vehicle has available seats and matching color
            if (vehicle.GetAvailableSeats() > 0)
            {
                VehicleColorController colorController = vehicle.GetComponent<VehicleColorController>();
                if (colorController != null && colorController.GetVehicleColor() == npcColor)
                {
                    return vehicle;
                }
            }
        }

        return null;
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

            DebugLog($"Using existing vehicle: {availableVehicle.name}{colorInfo}");
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

    public void VehicleReadyForPassengers(VehicleController vehicle, ParkSpaceController parkSpace)
    {
        // Vehicle has arrived at parking space and is ready for passengers
        // With our new approach, NPCs will check for matching vehicles directly
        DebugLog($"Vehicle {vehicle.name} is ready for passengers at space {parkSpace.GetSpaceIndex()}");
    }

    public void VehicleLoadingComplete(VehicleController vehicle)
    {
        // Vehicle is now full of passengers
        DebugLog($"Vehicle {vehicle.name} loading complete");
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

        DebugLog($"Vehicle {vehicle.name} departing");
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

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ParkingManager] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        // Draw the road exit point
        if (roadExitPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(roadExitPoint.position, 0.4f);
        }
    }
}