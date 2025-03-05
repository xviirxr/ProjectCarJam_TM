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

    private bool isCustomDestination = false;
    private Vector3 customDestinationPosition;
    private Quaternion customDestinationRotation;

    private Animator animator;
    private float lastQueueCheckTime = 0f;
    private float queueCheckInterval = 0.2f;

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

    public void Initialize(SplineContainer spline, NPCManager manager, int initialIndex)
    {
        queueSpline = spline;
        npcManager = manager;
        queueIndex = initialIndex;
        currentPosition = 0;
        hasReachedDestination = false;
        isCustomDestination = false;
        lastQueueCheckTime = Time.time;

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
        if (isCustomDestination)
        {
            MoveToCustomDestination();
            return;
        }

        if (queueSpline == null || npcManager == null)
            return;

        if (!hasReachedDestination)
        {
            MoveAlongSpline();
        }

        PlaceOnSpline();

        // Periodically check for queue changes to avoid excessive updates
        if (Time.time >= lastQueueCheckTime + queueCheckInterval)
        {
            lastQueueCheckTime = Time.time;
            UpdateTargetPosition();
            CheckForwardMovement();
        }
    }

    public void SetCustomDestination(Vector3 position, Quaternion rotation)
    {
        isCustomDestination = true;
        customDestinationPosition = position;
        customDestinationRotation = rotation;
        hasReachedDestination = false;

        if (useAnimator && animator != null)
        {
            animator.SetBool(walkParameterName, true);
        }
    }

    private void MoveToCustomDestination()
    {
        float distance = Vector3.Distance(transform.position, customDestinationPosition);

        if (distance <= 0.1f)
        {
            transform.position = customDestinationPosition;
            transform.rotation = customDestinationRotation;

            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, false);
            }

            hasReachedDestination = true;
            return;
        }

        float speed = walkSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, customDestinationPosition, speed);
        transform.rotation = Quaternion.Slerp(transform.rotation, customDestinationRotation, 5f * Time.deltaTime);

        if (useAnimator && animator != null)
        {
            animator.SetBool(walkParameterName, true);
        }
    }

    private void MoveAlongSpline()
    {
        float step = (walkSpeed / splineLength) * Time.deltaTime;

        if (Mathf.Abs(currentPosition - targetPosition) < step)
        {
            currentPosition = targetPosition;

            // Only mark as reached destination if we're at the final target
            // (important for gap filling behavior)
            if (queueIndex == 0 || Mathf.Approximately(targetPosition, GetFinalTargetPosition()))
            {
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
        }
        else
        {
            currentPosition = Mathf.MoveTowards(currentPosition, targetPosition, step);
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

    private float GetFinalTargetPosition()
    {
        if (queueIndex == 0)
        {
            return 1.0f;
        }
        else
        {
            // Calculate where this NPC should be based on its position in line
            float spacing = spacingBetweenNPCs / splineLength;
            return Mathf.Clamp01(1.0f - (queueIndex * spacing));
        }
    }

    public void UpdateTargetPosition()
    {
        if (isCustomDestination)
            return;

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

                // Target position is directly behind the NPC in front
                float targetBehindFront = Mathf.Max(0, npcInFront.GetCurrentPosition() - spacing);

                // Check if we should force a gap fill
                // If the NPC in front is far ahead, set our target to follow them
                float currentFrontPos = npcInFront.GetCurrentPosition();
                float minExpectedPosition = 1.0f - ((queueIndex - 1) * spacing);

                if (currentFrontPos > minExpectedPosition + (spacing * 0.5f) &&
                    npcInFront.HasReachedDestination())
                {
                    // Gap detected - move to fill it
                    targetPosition = targetBehindFront;
                    hasReachedDestination = false;
                }
                else
                {
                    targetPosition = targetBehindFront;
                }

                // Update animation state based on whether we need to move
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
                // No NPC in front! We should now be at the front
                queueIndex = 0;
                targetPosition = 1.0f;
                hasReachedDestination = false;
            }
        }
    }

    private void CheckForwardMovement()
    {
        if (isCustomDestination)
            return;

        int currentRealIndex = npcManager.GetIndexOfNPC(this);

        if (currentRealIndex != -1 && currentRealIndex != queueIndex)
        {
            int oldIndex = queueIndex;
            queueIndex = currentRealIndex;

            // Force movement if our position in line has changed
            if (currentRealIndex < oldIndex)
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

    protected virtual void OnReachedFront()
    {
        // Can be overridden in derived classes
    }

    public float GetCurrentPosition()
    {
        return currentPosition;
    }

    public bool HasReachedDestination()
    {
        return hasReachedDestination;
    }

    public int GetQueueIndex()
    {
        return queueIndex;
    }

    public void SetQueueIndex(int index)
    {
        if (queueIndex != index)
        {
            queueIndex = index;
            hasReachedDestination = false;
            UpdateTargetPosition();
        }
    }

    public bool IsUsingCustomDestination()
    {
        return isCustomDestination;
    }

    public void ClearCustomDestination()
    {
        isCustomDestination = false;
        hasReachedDestination = false;

        if (queueSpline != null && npcManager != null)
        {
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