using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a single parking space for vehicles
/// </summary>
public class ParkSpaceController : MonoBehaviour
{
    [Header("Parking Space Config")]
    [SerializeField] private Transform parkingPosition;
    [SerializeField] private Transform[] passengerEntryPoints;

    [Header("Debug Visualization")]
    [SerializeField] private Color availableColor = Color.green;
    [SerializeField] private Color occupiedColor = Color.red;
    [SerializeField] private Color reservedColor = Color.yellow;
    [SerializeField] private bool showDebugVisuals = true;

    private ParkingSpaceManager parkingManager;
    private VehicleController assignedVehicle;
    private int spaceIndex;
    private bool isReserved;

    private Renderer visualRenderer;

    private void Awake()
    {
        // Find visual indicator if any
        visualRenderer = GetComponentInChildren<Renderer>();

        // Default parking position to transform if none assigned
        if (parkingPosition == null)
            parkingPosition = transform;
    }

    /// <summary>
    /// Initializes the parking space with necessary references
    /// </summary>
    public void Initialize(ParkingSpaceManager manager, int index)
    {
        parkingManager = manager;
        spaceIndex = index;
        assignedVehicle = null;
        isReserved = false;
        UpdateVisuals();
    }

    /// <summary>
    /// Assigns a vehicle to this parking space
    /// </summary>
    public void AssignVehicle(VehicleController vehicle)
    {
        assignedVehicle = vehicle;
        isReserved = true;
        UpdateVisuals();
    }

    /// <summary>
    /// Called when a vehicle arrives at this parking space
    /// </summary>
    public void VehicleArrived(VehicleController vehicle)
    {
        if (assignedVehicle == vehicle)
        {
            // Vehicle has reached its parking position
            if (parkingManager != null)
            {
                parkingManager.VehicleReadyForPassengers(vehicle, this);
            }
            UpdateVisuals();
        }
    }

    /// <summary>
    /// Called when vehicle is departing
    /// </summary>
    public void VehicleDeparting(VehicleController vehicle)
    {
        if (assignedVehicle == vehicle)
        {
            if (parkingManager != null)
            {
                parkingManager.VehicleDeparting(vehicle, this);
            }
        }
    }

    /// <summary>
    /// Clears the assigned vehicle
    /// </summary>
    public void ClearVehicle()
    {
        assignedVehicle = null;
        isReserved = false;
        UpdateVisuals();
    }

    /// <summary>
    /// Returns true if this space is occupied or reserved
    /// </summary>
    public bool IsOccupied()
    {
        return assignedVehicle != null || isReserved;
    }

    /// <summary>
    /// Gets the parking position transform
    /// </summary>
    public Transform GetParkingPosition()
    {
        return parkingPosition;
    }

    /// <summary>
    /// Gets the passenger entry points
    /// </summary>
    public Transform[] GetPassengerEntryPoints()
    {
        return passengerEntryPoints;
    }

    /// <summary>
    /// Gets the index of this parking space
    /// </summary>
    public int GetSpaceIndex()
    {
        return spaceIndex;
    }

    /// <summary>
    /// Gets the assigned vehicle (if any)
    /// </summary>
    public VehicleController GetAssignedVehicle()
    {
        return assignedVehicle;
    }

    /// <summary>
    /// Updates the visual indicators of the parking space
    /// </summary>
    private void UpdateVisuals()
    {
        if (!showDebugVisuals || visualRenderer == null)
            return;

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        visualRenderer.GetPropertyBlock(props);

        if (assignedVehicle != null)
            props.SetColor("_Color", occupiedColor);
        else if (isReserved)
            props.SetColor("_Color", reservedColor);
        else
            props.SetColor("_Color", availableColor);

        visualRenderer.SetPropertyBlock(props);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugVisuals)
            return;

        // Draw parking position
        if (parkingPosition != null)
        {
            Gizmos.color = IsOccupied() ? occupiedColor : availableColor;
            Gizmos.DrawCube(parkingPosition.position, new Vector3(0.5f, 0.2f, 1f));
            Gizmos.DrawLine(transform.position, parkingPosition.position);
        }

        // Draw passenger entry points
        if (passengerEntryPoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (Transform point in passengerEntryPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawSphere(point.position, 0.2f);
                    if (parkingPosition != null)
                        Gizmos.DrawLine(parkingPosition.position, point.position);
                }
            }
        }
    }
}