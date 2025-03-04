using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

/// <summary>
/// Provides touch interaction to summon the tapped vehicle to a parking space
/// </summary>
public class VehicleInteraction : MonoBehaviour
{
    private ParkingSpaceManager parkingManager;
    private VehicleController vehicleController;

    private void Start()
    {
        // Get the VehicleController from this object
        vehicleController = GetComponent<VehicleController>();
        if (vehicleController == null)
        {
            Debug.LogError("No VehicleController found on this object!");
        }

        // Auto-find parking manager if not assigned
        if (parkingManager == null)
        {
            parkingManager = FindFirstObjectByType<ParkingSpaceManager>();
            if (parkingManager == null)
            {
                Debug.LogError("No ParkingSpaceManager found in scene!");
            }
        }
    }

    private void Update()
    {
        // Handle touch input for mobile
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            HandleTouchInput(Input.GetTouch(0).position);
        }

#if UNITY_EDITOR
        // For testing in editor with mouse
        if (Input.GetMouseButtonDown(0))
        {
            HandleTouchInput(Input.mousePosition);
        }
#endif
    }

    /// <summary>
    /// Handles touch/click input and checks if this object was tapped
    /// </summary>
    private void HandleTouchInput(Vector2 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // Check if this object was tapped
            if (hit.transform == transform)
            {
                AssignVehicleToParking();
            }
        }
    }

    /// <summary>
    /// Assigns this specific vehicle to an available parking space
    /// </summary>
    public void AssignVehicleToParking()
    {
        if (vehicleController == null || parkingManager == null)
        {
            Debug.LogError("Cannot assign vehicle - missing required components!");
            return;
        }

        // Check if the vehicle is already assigned
        if (vehicleController.IsAssigned())
        {
            Debug.Log("Vehicle is already assigned to a parking space.");
            return;
        }

        // Find an available parking space
        ParkSpaceController availableSpace = FindAvailableSpace();

        if (availableSpace != null)
        {
            // Assign this specific vehicle to the parking space
            vehicleController.AssignParkingSpace(availableSpace);
            availableSpace.AssignVehicle(vehicleController);

            Debug.Log($"Assigning tapped vehicle {gameObject.name} to parking space.");
        }
        else
        {
            Debug.LogWarning("No available parking spaces for this vehicle!");
        }
    }

    /// <summary>
    /// Finds an available parking space
    /// </summary>
    private ParkSpaceController FindAvailableSpace()
    {
        if (parkingManager == null)
            return null;

        // Get all parking spaces
        ParkSpaceController[] spaces = parkingManager.GetParkingSpaces();

        if (spaces == null || spaces.Length == 0)
            return null;

        // Find first unoccupied space
        foreach (ParkSpaceController space in spaces)
        {
            if (space != null && !space.IsOccupied())
                return space;
        }

        return null;
    }

#if UNITY_EDITOR
    [Button("Test Assign Vehicle")]
    private void TestAssignVehicle()
    {
        if (vehicleController == null)
        {
            Debug.LogError("Cannot test - No VehicleController attached to this object!");
            return;
        }

        AssignVehicleToParking();
    }
#endif
}