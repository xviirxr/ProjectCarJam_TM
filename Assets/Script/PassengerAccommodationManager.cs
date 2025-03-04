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

        int capacity = vehicle.GetPassengerCapacity();
        DebugLog($"Assigning up to {capacity} passengers to vehicle");

        // Get NPCs from the front of the queue
        List<NPCController> selectedPassengers = new List<NPCController>();

        for (int i = 0; i < capacity; i++)
        {
            NPCController npc = npcManager.GetNPCAtIndex(i);
            if (npc != null)
            {
                selectedPassengers.Add(npc);
                DebugLog($"Selected passenger {i}: {npc.name}");
            }
        }

        if (selectedPassengers.Count == 0)
        {
            DebugLog("No passengers available in queue");
            // No passengers available, vehicle should leave
            vehicle.Depart();
            return;
        }

        // Assign passengers to the vehicle
        vehicle.AssignPassengers(selectedPassengers);

        // Process passengers one by one with a slight delay
        StartCoroutine(ProcessPassengersSequentially(selectedPassengers));
    }

    /// <summary>
    /// Processes passengers one by one with a slight delay
    /// </summary>
    private IEnumerator ProcessPassengersSequentially(List<NPCController> passengers)
    {
        for (int i = 0; i < passengers.Count; i++)
        {
            NPCController passenger = passengers[i];

            // Unregister from the queue (but don't destroy the NPC)
            if (npcManager != null)
            {
                npcManager.UnregisterNPC(passenger);
            }

            // NPCController will handle movement to the vehicle seat

            // Wait before processing next passenger
            yield return new WaitForSeconds(passengerProcessTime);
        }
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