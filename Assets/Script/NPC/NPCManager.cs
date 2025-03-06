using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    [SerializeField] private SplineQueueController splineQueueController;
    [SerializeField] private float updateQueueInterval = 0.5f;
    [SerializeField] private bool enableDebugLogs = false;

    private List<NPCController> npcsInQueue = new List<NPCController>();
    private float updateQueuePositionsTimer = 0f;

    private void Awake()
    {
        if (splineQueueController == null)
        {
            splineQueueController = FindFirstObjectByType<SplineQueueController>();
            if (splineQueueController == null)
            {
                Debug.LogError("SplineQueueController not found in scene!");
            }
        }
    }

    private void Update()
    {
        // Periodically update queue positions to ensure all NPCs have correct targets
        updateQueuePositionsTimer += Time.deltaTime;
        if (updateQueuePositionsTimer >= updateQueueInterval)
        {
            UpdateAllNPCPositions();
            updateQueuePositionsTimer = 0f;
        }
    }

    public void RegisterNPC(NPCController npc)
    {
        if (!npcsInQueue.Contains(npc))
        {
            npcsInQueue.Add(npc);
            npc.SetQueueIndex(npcsInQueue.Count - 1);
            DebugLog($"NPC {npc.name} registered at position {npcsInQueue.Count - 1}");

            // Force update of all NPCs when a new one is added
            UpdateAllNPCPositions();
        }
    }

    public void UnregisterNPC(NPCController npc)
    {
        int index = npcsInQueue.IndexOf(npc);
        if (index != -1)
        {
            DebugLog($"NPC {npc.name} unregistered from position {index}");
            npcsInQueue.RemoveAt(index);

            // Update indices for all NPCs behind the removed one
            for (int i = index; i < npcsInQueue.Count; i++)
            {
                npcsInQueue[i].SetQueueIndex(i);
                DebugLog($"NPC {npcsInQueue[i].name} moved up to position {i}");
            }

            // Force update of all NPCs when one is removed
            UpdateAllNPCPositions();
        }
    }

    private void UpdateAllNPCPositions()
    {
        // Update target positions for all NPCs to ensure they move properly
        for (int i = 0; i < npcsInQueue.Count; i++)
        {
            if (npcsInQueue[i] != null)
            {
                npcsInQueue[i].UpdateTargetPosition();
            }
        }
    }

    public NPCController GetNPCAtIndex(int index)
    {
        if (index >= 0 && index < npcsInQueue.Count)
        {
            return npcsInQueue[index];
        }
        return null;
    }

    public int GetNPCCount()
    {
        return npcsInQueue.Count;
    }

    public int GetIndexOfNPC(NPCController npc)
    {
        return npcsInQueue.IndexOf(npc);
    }

    public SplineQueueController GetSplineQueueController()
    {
        return splineQueueController;
    }

    public void ClearAllNPCs()
    {
        for (int i = npcsInQueue.Count - 1; i >= 0; i--)
        {
            Destroy(npcsInQueue[i].gameObject);
        }
        npcsInQueue.Clear();
        DebugLog("All NPCs cleared from queue");
    }

    public bool IsNPCAtFront(NPCController npc)
    {
        return npcsInQueue.Count > 0 && npcsInQueue[0] == npc;
    }

    public NPCController GetFirstNPCWithColor(ColorCodeManager.ColorCode colorCode)
    {
        foreach (var npc in npcsInQueue)
        {
            NPCColorController colorController = npc.GetComponent<NPCColorController>();
            if (colorController != null && colorController.GetNPCColor() == colorCode)
            {
                return npc;
            }
        }
        return null;
    }

    public List<NPCController> GetAllNPCsWithColor(ColorCodeManager.ColorCode colorCode)
    {
        List<NPCController> matchingNPCs = new List<NPCController>();

        foreach (var npc in npcsInQueue)
        {
            NPCColorController colorController = npc.GetComponent<NPCColorController>();
            if (colorController != null && colorController.GetNPCColor() == colorCode)
            {
                matchingNPCs.Add(npc);
            }
        }

        return matchingNPCs;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCManager] {message}");
        }
    }
}