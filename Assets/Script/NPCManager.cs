using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    [SerializeField] private SplineQueueController splineQueueController;

    private List<NPCController> npcsInQueue = new List<NPCController>();

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

    /// <summary>
    /// Registers a new NPC in the queue
    /// </summary>
    public void RegisterNPC(NPCController npc)
    {
        if (!npcsInQueue.Contains(npc))
        {
            npcsInQueue.Add(npc);
            npc.SetQueueIndex(npcsInQueue.Count - 1);
        }
    }

    /// <summary>
    /// Unregisters an NPC from the queue
    /// </summary>
    public void UnregisterNPC(NPCController npc)
    {
        int index = npcsInQueue.IndexOf(npc);
        if (index != -1)
        {
            npcsInQueue.RemoveAt(index);

            for (int i = index; i < npcsInQueue.Count; i++)
            {
                npcsInQueue[i].SetQueueIndex(i);
            }
        }
    }

    /// <summary>
    /// Gets the NPC at the specified index
    /// </summary>
    public NPCController GetNPCAtIndex(int index)
    {
        if (index >= 0 && index < npcsInQueue.Count)
        {
            return npcsInQueue[index];
        }
        return null;
    }

    /// <summary>
    /// Gets the number of NPCs in the queue
    /// </summary>
    public int GetNPCCount()
    {
        return npcsInQueue.Count;
    }

    /// <summary>
    /// Gets the index of an NPC in the queue
    /// </summary>
    public int GetIndexOfNPC(NPCController npc)
    {
        return npcsInQueue.IndexOf(npc);
    }

    /// <summary>
    /// Gets the reference to the SplineQueueController
    /// </summary>
    public SplineQueueController GetSplineQueueController()
    {
        return splineQueueController;
    }

    /// <summary>
    /// Clears all NPCs from the queue
    /// </summary>
    public void ClearAllNPCs()
    {
        for (int i = npcsInQueue.Count - 1; i >= 0; i--)
        {
            Destroy(npcsInQueue[i].gameObject);
        }
        npcsInQueue.Clear();
    }

    /// <summary>
    /// Services and removes the NPC at the front of the queue
    /// </summary>
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