using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehiclePassengerUIManager : MonoBehaviour
{
    [SerializeField] private GameObject passengerWorldCanvas;
    [SerializeField] private PassengerSeatUIController[] seatControllers;

    private VehicleController vehicleController;
    private bool[] seatOccupied;

    private void Start()
    {
        vehicleController = GetComponent<VehicleController>();
        if (vehicleController == null)
        {
            Debug.LogError("No VehicleController found on this object!");
            enabled = false;
            return;
        }

        // Auto-find seat controllers if not set
        if (seatControllers == null || seatControllers.Length == 0)
        {
            FindSeatControllers();
        }

        // Initialize seat state array
        seatOccupied = new bool[vehicleController.GetPassengerCapacity()];
        for (int i = 0; i < seatOccupied.Length; i++)
        {
            seatOccupied[i] = false;
        }

        // Initialize UI in hidden state
        HideUI();
    }

    private void FindSeatControllers()
    {
        if (passengerWorldCanvas == null)
        {
            // Try to find canvas in children
            passengerWorldCanvas = transform.Find("VehiclePassengerWorldCanvas")?.gameObject;

            if (passengerWorldCanvas == null)
            {
                Debug.LogError("Passenger World Canvas not assigned and not found in children!");
                return;
            }
        }

        seatControllers = passengerWorldCanvas.GetComponentsInChildren<PassengerSeatUIController>(true);

        if (seatControllers.Length == 0)
        {
            Debug.LogWarning("No PassengerSeatUIControllers found in canvas children!");
        }
        else if (seatControllers.Length != vehicleController.GetPassengerCapacity())
        {
            Debug.LogWarning($"Found {seatControllers.Length} seat controllers, but vehicle has {vehicleController.GetPassengerCapacity()} capacity!");

            // Create a new array of the correct size
            PassengerSeatUIController[] resizedControllers = new PassengerSeatUIController[vehicleController.GetPassengerCapacity()];

            // Copy the available controllers
            for (int i = 0; i < Mathf.Min(seatControllers.Length, resizedControllers.Length); i++)
            {
                resizedControllers[i] = seatControllers[i];
            }

            seatControllers = resizedControllers;
        }
    }

    private void Update()
    {
        if (vehicleController == null) return;

        VehicleController.VehicleState state = vehicleController.GetCurrentState();

        // Show UI when parked or loading passengers (but not when departing)
        bool shouldShowCanvas = (state == VehicleController.VehicleState.Parked ||
                               state == VehicleController.VehicleState.LoadingPassengers ||
                               state == VehicleController.VehicleState.PassengersLoaded);

        // Hide UI when departing or following departure path
        bool shouldHideCanvas = (state == VehicleController.VehicleState.Departing ||
                               state == VehicleController.VehicleState.FollowingDeparturePath ||
                               state == VehicleController.VehicleState.Idle);

        if (shouldShowCanvas)
        {
            ShowUI();
        }
        else if (shouldHideCanvas)
        {
            HideUI();
        }
    }

    public void ShowUI()
    {
        if (passengerWorldCanvas != null && !passengerWorldCanvas.activeSelf)
        {
            passengerWorldCanvas.SetActive(true);
        }
    }

    public void HideUI()
    {
        if (passengerWorldCanvas != null && passengerWorldCanvas.activeSelf)
        {
            passengerWorldCanvas.SetActive(false);
        }
    }

    public void ResetAllSeats()
    {
        // Reset state tracking
        if (seatOccupied != null)
        {
            for (int i = 0; i < seatOccupied.Length; i++)
            {
                seatOccupied[i] = false;
            }
        }

        // Reset UI controllers
        if (seatControllers != null)
        {
            foreach (PassengerSeatUIController seat in seatControllers)
            {
                if (seat != null)
                {
                    seat.SetOccupied(false);
                }
            }
        }

        // Make sure UI is hidden when resetting
        HideUI();
    }

    // Used to set specific seats as occupied
    public void SetSeatOccupied(int seatIndex, bool occupied)
    {
        // Validate index
        if (seatIndex < 0 || seatOccupied == null || seatIndex >= seatOccupied.Length)
        {
            Debug.LogWarning($"Invalid seat index: {seatIndex}, capacity: {(seatOccupied != null ? seatOccupied.Length : 0)}");
            return;
        }

        // Only update if state has changed (prevents flickering)
        if (seatOccupied[seatIndex] != occupied)
        {
            // Update internal state
            seatOccupied[seatIndex] = occupied;

            // Update UI controller
            if (seatControllers != null && seatIndex < seatControllers.Length && seatControllers[seatIndex] != null)
            {
                seatControllers[seatIndex].SetOccupied(occupied);
            }
        }
    }

    // Called when vehicle departs
    public void OnVehicleDeparting()
    {
        ResetAllSeats();
        HideUI();
    }
}