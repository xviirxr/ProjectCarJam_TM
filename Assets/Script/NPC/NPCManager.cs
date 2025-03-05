using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    [SerializeField] private SplineQueueController splineQueueController;

    private List<NPCController> npcsInQueue = new List<NPCController>();
    private float updateQueuePositionsTimer = 0f;
    private float updateQueueInterval = 0.5f;

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

            // Force update of all NPCs when a new one is added
            UpdateAllNPCPositions();
        }
    }

    public void UnregisterNPC(NPCController npc)
    {
        int index = npcsInQueue.IndexOf(npc);
        if (index != -1)
        {
            npcsInQueue.RemoveAt(index);

            // Update indices for all NPCs behind the removed one
            for (int i = index; i < npcsInQueue.Count; i++)
            {
                npcsInQueue[i].SetQueueIndex(i);
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
    }

    public void ServiceFrontNPC(float serviceTime = 0)
    {
        if (npcsInQueue.Count > 0)
        {
            NPCController frontNPC = npcsInQueue[0];

            if (serviceTime > 0)
            {
                StartCoroutine(ServiceNPCRoutine(frontNPC, serviceTime));
            }
            else
            {
                frontNPC.RemoveFromQueue();
            }
        }
    }

    private IEnumerator ServiceNPCRoutine(NPCController npc, float serviceTime)
    {
        yield return new WaitForSeconds(serviceTime);

        if (npc != null)
        {
            npc.RemoveFromQueue();
        }
    }
}