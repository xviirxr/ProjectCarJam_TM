using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class NPCController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string walkParameterName = "isWalking";

    private SplineContainer queueSpline;
    private NPCManager npcManager;
    private int queueIndex;
    private float currentPosition;
    private float targetPosition;
    private bool hasReachedDestination;
    private float walkSpeed;
    private float spacingBetweenNPCs;
    private float splineLength;

    private Animator animator;

    private void Awake()
    {
        if (useAnimator)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("Animator component not found on NPC but useAnimator is enabled.");
            }
        }
    }

    /// <summary>
    /// Initializes the NPC with necessary references and position in queue
    /// </summary>
    public void Initialize(SplineContainer spline, NPCManager manager, int initialIndex)
    {
        queueSpline = spline;
        npcManager = manager;
        queueIndex = initialIndex;
        currentPosition = 0;
        hasReachedDestination = false;

        SplineQueueController splineController = npcManager.GetSplineQueueController();
        if (splineController != null)
        {
            walkSpeed = splineController.GetWalkSpeed();
            spacingBetweenNPCs = splineController.GetSpacingBetweenNPCs();
            splineLength = splineController.GetSplineLength();
        }
        else
        {
            Debug.LogError("SplineQueueController reference not available!");
            walkSpeed = 1.0f;
            spacingBetweenNPCs = 1.2f;
            splineLength = 10f;
        }

        UpdateTargetPosition();
    }

    private void Update()
    {
        if (queueSpline == null || npcManager == null)
            return;

        if (!hasReachedDestination)
        {
            MoveAlongSpline();
        }

        PlaceOnSpline();
        UpdateTargetPosition();
        CheckForwardMovement();
    }

    private void MoveAlongSpline()
    {
        float step = (walkSpeed / splineLength) * Time.deltaTime;

        if (Mathf.Abs(currentPosition - targetPosition) < step)
        {
            currentPosition = targetPosition;
            hasReachedDestination = true;

            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, false);
            }

            if (queueIndex == 0)
            {
                OnReachedFront();
            }
        }
        else
        {
            currentPosition = Mathf.MoveTowards(
                currentPosition,
                targetPosition,
                step
            );
            hasReachedDestination = false;

            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, true);
            }
        }
    }

    private void PlaceOnSpline()
    {
        float t = currentPosition;
        float3 position = queueSpline.EvaluatePosition(t);
        float3 tangent = queueSpline.EvaluateTangent(t);
        float3 up = queueSpline.EvaluateUpVector(t);

        transform.position = position;

        if (math.lengthsq(tangent) > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(tangent, up);
        }
    }

    /// <summary>
    /// Updates the target position based on queue position
    /// </summary>
    public void UpdateTargetPosition()
    {
        if (queueIndex == 0)
        {
            targetPosition = 1.0f;
        }
        else
        {
            NPCController npcInFront = npcManager.GetNPCAtIndex(queueIndex - 1);

            if (npcInFront != null)
            {
                float spacing = spacingBetweenNPCs / splineLength;
                targetPosition = Mathf.Max(0, npcInFront.GetCurrentPosition() - spacing);

                if (npcInFront.HasReachedDestination() &&
                    Mathf.Abs(currentPosition - targetPosition) < 0.01f)
                {
                    hasReachedDestination = true;

                    if (useAnimator && animator != null)
                    {
                        animator.SetBool(walkParameterName, false);
                    }
                }
            }
            else
            {
                queueIndex = 0;
                targetPosition = 1.0f;
                hasReachedDestination = false;
            }
        }
    }

    /// <summary>
    /// Checks if we need to move forward in the queue
    /// </summary>
    private void CheckForwardMovement()
    {
        int currentRealIndex = npcManager.GetIndexOfNPC(this);

        if (currentRealIndex != -1 && currentRealIndex != queueIndex)
        {
            queueIndex = currentRealIndex;

            if (currentRealIndex < queueIndex)
            {
                hasReachedDestination = false;

                if (useAnimator && animator != null)
                {
                    animator.SetBool(walkParameterName, true);
                }
            }

            UpdateTargetPosition();
        }
    }

    /// <summary>
    /// Called when this NPC reaches the front of the queue
    /// </summary>
    protected virtual void OnReachedFront()
    {
    }

    /// <summary>
    /// Gets the current position of this NPC on the spline
    /// </summary>
    public float GetCurrentPosition()
    {
        return currentPosition;
    }

    /// <summary>
    /// Gets whether this NPC has reached its destination
    /// </summary>
    public bool HasReachedDestination()
    {
        return hasReachedDestination;
    }

    /// <summary>
    /// Gets the queue index of this NPC
    /// </summary>
    public int GetQueueIndex()
    {
        return queueIndex;
    }

    /// <summary>
    /// Sets the queue index for this NPC
    /// </summary>
    public void SetQueueIndex(int index)
    {
        if (queueIndex != index)
        {
            queueIndex = index;
            hasReachedDestination = false;
            UpdateTargetPosition();
        }
    }

#if UNITY_EDITOR
    [Button("Remove From Queue")]
#endif
    public void RemoveFromQueue()
    {
        if (npcManager != null)
        {
            npcManager.UnregisterNPC(this);
        }

        Destroy(gameObject);
    }
}