using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class VehicleInteraction : MonoBehaviour
{
    private ParkingSpaceManager parkingManager;
    private VehicleController vehicleController;
    private VehicleTraversalController traversalController;

    private void Start()
    {
        vehicleController = GetComponent<VehicleController>();
        if (vehicleController == null)
        {
            Debug.LogError("No VehicleController found on this object!");
        }

        traversalController = GetComponent<VehicleTraversalController>();

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
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            HandleTouchInput(Input.GetTouch(0).position);
        }

#if UNITY_EDITOR
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
            if (hit.transform == transform)
            {
                if (vehicleController != null)
                {
                    VehicleController.VehicleState currentState = vehicleController.GetCurrentState();

                    if (currentState == VehicleController.VehicleState.LoadingPassengers ||
                        currentState == VehicleController.VehicleState.PassengersLoaded ||
                        currentState == VehicleController.VehicleState.FollowingDeparturePath ||
                        currentState == VehicleController.VehicleState.Departing)
                    {
                        Debug.Log("Vehicle is already occupied or departing. Cannot interact.");
                        return;
                    }

                    if (vehicleController.GetAvailableSeats() <= 0)
                    {
                        Debug.Log("Vehicle is fully occupied. Cannot interact.");
                        return;
                    }

                    if (traversalController != null && traversalController.IsFollowingTraversalPath())
                    {
                        Debug.Log("Vehicle is already following a traversal path. Cannot interact.");
                        return;
                    }
                }

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

        if (vehicleController.IsAssigned())
        {
            Debug.Log("Vehicle is already assigned to a parking space.");
            return;
        }

        ParkSpaceController availableSpace = FindAvailableSpace();

        if (availableSpace != null)
        {
            if (traversalController != null)
            {
                traversalController.StartTraversalToParkingSpace(availableSpace);
                availableSpace.AssignVehicle(vehicleController);
                Debug.Log($"Assigning tapped vehicle {gameObject.name} to parking space via traversal path.");
            }
            else
            {
                vehicleController.AssignParkingSpace(availableSpace);
                availableSpace.AssignVehicle(vehicleController);
                Debug.Log($"Assigning tapped vehicle {gameObject.name} to parking space directly.");
            }
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

        ParkSpaceController[] spaces = parkingManager.GetParkingSpaces();

        if (spaces == null || spaces.Length == 0)
            return null;

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

    [Button("Test Path Finding")]
    private void TestPathFinding()
    {
        if (traversalController == null)
        {
            Debug.LogError("Cannot test - No VehicleTraversalController attached to this object!");
            return;
        }

        ParkSpaceController availableSpace = FindAvailableSpace();
        if (availableSpace != null)
        {
            traversalController.StartTraversalToParkingSpace(availableSpace);
            Debug.Log("Test path finding started to parking space: " + availableSpace.name);
        }
        else
        {
            Debug.LogError("No available parking spaces for path test!");
        }
    }
#endif
}