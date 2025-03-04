using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class SplineQueueController : MonoBehaviour
{
    [Header("Spline Settings")]
    [SerializeField] private SplineContainer queueSpline;
    [SerializeField] private float walkSpeed = 1.0f;

    [Header("Queue Settings")]
    [SerializeField] private float spacingBetweenNPCs = 1.2f;
    [SerializeField] private int maxQueueSize = 10;
    [SerializeField] private Transform npcPrefab;
    [SerializeField] private float spawnInterval = 3.0f;
    [SerializeField] private bool autoSpawn = true;

    [Header("Pool Settings")]
    [SerializeField] private Transform NpcHolder;

    private float splineLength;
    private float nextSpawnTime;

    private NPCManager npcManager;

    private void Start()
    {
        if (queueSpline == null)
        {
            Debug.LogError("Queue Spline not assigned to SplineQueueController!");
            return;
        }

        splineLength = queueSpline.CalculateLength();
        nextSpawnTime = Time.time + spawnInterval;

        npcManager = FindFirstObjectByType<NPCManager>();
        if (npcManager == null)
        {
            Debug.LogError("NPCManager not found in the scene!");
        }
    }

    private void Update()
    {
        if (autoSpawn && Time.time >= nextSpawnTime && !IsQueueFull())
        {
            SpawnNPC();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    /// <summary>
    /// Spawns a new NPC at the start of the spline
    /// </summary>
    public void SpawnNPC()
    {
        float3 spawnPosition = queueSpline.EvaluatePosition(0);
        float3 spawnTangent = queueSpline.EvaluateTangent(0);
        float3 spawnUp = queueSpline.EvaluateUpVector(0);

        Quaternion spawnRotation = Quaternion.identity;
        if (math.lengthsq(spawnTangent) > 0.001f)
        {
            spawnRotation = Quaternion.LookRotation(spawnTangent, spawnUp);
        }

        Transform newNPC = Instantiate(npcPrefab, spawnPosition, spawnRotation);

        NPCController npcController = newNPC.GetComponent<NPCController>();
        if (npcController == null)
        {
            Debug.LogError("NPCController component not found on NPC prefab!");
            Destroy(newNPC.gameObject);
            return;
        }

        npcController.Initialize(queueSpline, npcManager, GetQueueCount());
        npcManager.RegisterNPC(npcController);
        newNPC.transform.SetParent(NpcHolder.transform);
    }

    /// <summary>
    /// Gets the spline used for the queue
    /// </summary>
    public SplineContainer GetQueueSpline()
    {
        return queueSpline;
    }

    /// <summary>
    /// Gets the walk speed for NPCs
    /// </summary>
    public float GetWalkSpeed()
    {
        return walkSpeed;
    }

    /// <summary>
    /// Gets the spacing between NPCs in the queue
    /// </summary>
    public float GetSpacingBetweenNPCs()
    {
        return spacingBetweenNPCs;
    }

    /// <summary>
    /// Gets the length of the spline
    /// </summary>
    public float GetSplineLength()
    {
        return splineLength;
    }

    /// <summary>
    /// Gets the current number of NPCs in the queue
    /// </summary>
    public int GetQueueCount()
    {
        return npcManager != null ? npcManager.GetNPCCount() : 0;
    }

    /// <summary>
    /// Gets whether the queue is at maximum capacity
    /// </summary>
    public bool IsQueueFull()
    {
        return GetQueueCount() >= maxQueueSize;
    }

    /// <summary>
    /// Sets whether NPCs should automatically spawn at intervals
    /// </summary>
    public void SetAutoSpawn(bool enabled)
    {
        autoSpawn = enabled;
        if (enabled)
        {
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    /// <summary>
    /// Clears all NPCs from the queue
    /// </summary>
    public void ClearQueue()
    {
        if (npcManager != null)
        {
            npcManager.ClearAllNPCs();
        }
    }
}