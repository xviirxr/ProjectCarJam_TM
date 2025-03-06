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
    private float splineLengthUpdateTimer = 0f;
    private const float SPLINE_LENGTH_UPDATE_INTERVAL = 5f;

    private void Start()
    {
        if (queueSpline == null)
        {
            Debug.LogError("Queue Spline not assigned to SplineQueueController!");
            return;
        }

        // Calculate initial spline length
        CalculateSplineLength();
        nextSpawnTime = Time.time + spawnInterval;

        npcManager = FindFirstObjectByType<NPCManager>();
        if (npcManager == null)
        {
            Debug.LogError("NPCManager not found in the scene!");
        }
    }

    private void Update()
    {
        // Periodically recalculate spline length in case it changes
        splineLengthUpdateTimer += Time.deltaTime;
        if (splineLengthUpdateTimer >= SPLINE_LENGTH_UPDATE_INTERVAL)
        {
            CalculateSplineLength();
            splineLengthUpdateTimer = 0f;
        }

        if (autoSpawn && Time.time >= nextSpawnTime && !IsQueueFull())
        {
            SpawnNPC();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private void CalculateSplineLength()
    {
        if (queueSpline != null)
        {
            // Ensure the spline length is calculated correctly
            splineLength = queueSpline.CalculateLength();

            // Make sure it's not zero or extremely small
            if (splineLength < 0.1f)
            {
                Debug.LogWarning("Spline length is very small (" + splineLength + "), setting to minimum value.");
                splineLength = 1f;
            }
        }
    }

    public void SpawnNPC()
    {
        // Evaluate the start position of the spline
        float3 spawnPosition = queueSpline.EvaluatePosition(0);
        float3 spawnTangent = queueSpline.EvaluateTangent(0);
        float3 spawnUp = queueSpline.EvaluateUpVector(0);

        Quaternion spawnRotation = Quaternion.identity;
        if (math.lengthsq(spawnTangent) > 0.001f)
        {
            spawnRotation = Quaternion.LookRotation(spawnTangent, spawnUp);
        }

        // Create a new NPC
        Transform newNPC = Instantiate(npcPrefab, spawnPosition, spawnRotation);

        NPCController npcController = newNPC.GetComponent<NPCController>();
        if (npcController == null)
        {
            Debug.LogError("NPCController component not found on NPC prefab!");
            Destroy(newNPC.gameObject);
            return;
        }

        // We no longer need to assign colors here - NPCColorController will get its color
        // from ColorCodeManager in its Start() method

        // Initialize the NPC with current queue position
        npcController.Initialize(queueSpline, npcManager, GetQueueCount());
        npcManager.RegisterNPC(npcController);

        // Ensure the NPC is parented correctly for organization
        if (NpcHolder != null)
        {
            newNPC.transform.SetParent(NpcHolder);
        }
    }

    public SplineContainer GetQueueSpline()
    {
        return queueSpline;
    }

    public float GetWalkSpeed()
    {
        return walkSpeed;
    }

    public float GetSpacingBetweenNPCs()
    {
        return spacingBetweenNPCs;
    }

    public float GetSplineLength()
    {
        return splineLength;
    }

    public int GetQueueCount()
    {
        return npcManager != null ? npcManager.GetNPCCount() : 0;
    }

    public bool IsQueueFull()
    {
        return GetQueueCount() >= maxQueueSize;
    }

    public void SetAutoSpawn(bool enabled)
    {
        autoSpawn = enabled;
        if (enabled)
        {
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    public void ClearQueue()
    {
        if (npcManager != null)
        {
            npcManager.ClearAllNPCs();
        }
    }
}