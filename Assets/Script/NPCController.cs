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

    // Queue position data
    private SplineContainer queueSpline;
    private NPCManager npcManager;
    private int queueIndex;
    private float currentPosition; // Position on spline from 0 to 1
    private float targetPosition; // Target position on spline
    private bool hasReachedDestination;
    private float walkSpeed;
    private float spacingBetweenNPCs;
    private float splineLength;

    // Custom destination for vehicle boarding
    private bool isCustomDestination = false;
    private Vector3 customDestinationPosition;
    private Quaternion customDestinationRotation;

    // Components
    private Animator animator;

    private void Awake()
    {
        // Get the animator component if needed
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
        isCustomDestination = false;

        // Get configuration from the spline controller
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

        // Calculate initial target position
        UpdateTargetPosition();
    }

    private void Update()
    {
        // Handle custom destination movement if assigned
        if (isCustomDestination)
        {
            MoveToCustomDestination();
            return;
        }

        if (queueSpline == null || npcManager == null)
            return;

        // Move along the spline if not at destination
        if (!hasReachedDestination)
        {
            MoveAlongSpline();
        }

        // Place NPC on the spline
        PlaceOnSpline();

        // Check if we need to recalculate our target position
        // (in case NPCs ahead of us have moved or been removed)
        UpdateTargetPosition();

        // Check if we need to move forward in the queue
        CheckForwardMovement();
    }

    /// <summary>
    /// Sets a custom destination for the NPC (used for vehicle boarding)
    /// </summary>
    public void SetCustomDestination(Vector3 position, Quaternion rotation)
    {
        isCustomDestination = true;
        customDestinationPosition = position;
        customDestinationRotation = rotation;
        hasReachedDestination = false;

        // Start the walking animation
        if (useAnimator && animator != null)
        {
            animator.SetBool(walkParameterName, true);
        }
    }

    /// <summary>
    /// Handles movement to a custom destination (like a vehicle seat)
    /// </summary>
    private void MoveToCustomDestination()
    {
        // Calculate distance to destination
        float distance = Vector3.Distance(transform.position, customDestinationPosition);

        // Check if we've arrived
        if (distance <= 0.1f)
        {
            // We've reached the destination
            transform.position = customDestinationPosition;
            transform.rotation = customDestinationRotation;

            // Update animation state
            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, false);
            }

            hasReachedDestination = true;
            return;
        }

        // Move toward destination
        float speed = walkSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, customDestinationPosition, speed);

        // Rotate toward the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, customDestinationRotation, 5f * Time.deltaTime);

        // Ensure the walking animation is playing
        if (useAnimator && animator != null)
        {
            animator.SetBool(walkParameterName, true);
        }
    }

    private void MoveAlongSpline()
    {
        // Calculate movement this frame
        float step = (walkSpeed / splineLength) * Time.deltaTime;

        // Check if we're close to target
        if (Mathf.Abs(currentPosition - targetPosition) < step)
        {
            currentPosition = targetPosition;
            hasReachedDestination = true;

            // Update animation state
            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, false);
            }

            // If this is the first NPC, it has reached the end of the line
            if (queueIndex == 0)
            {
                OnReachedFront();
            }
        }
        else
        {
            // Move toward target
            currentPosition = Mathf.MoveTowards(
                currentPosition,
                targetPosition,
                step
            );
            hasReachedDestination = false;

            // Update animation state
            if (useAnimator && animator != null)
            {
                animator.SetBool(walkParameterName, true);
            }
        }
    }

    private void PlaceOnSpline()
    {
        // Get position and orientation from spline
        float t = currentPosition;
        float3 position = queueSpline.EvaluatePosition(t);
        float3 tangent = queueSpline.EvaluateTangent(t);
        float3 up = queueSpline.EvaluateUpVector(t);

        // Set position
        transform.position = position;

        // Set rotation to face along spline
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
        // If in custom destination mode, don't update target position
        if (isCustomDestination)
            return;

        // Front of line (index 0) goes to end of spline
        if (queueIndex == 0)
        {
            targetPosition = 1.0f; // End of spline
        }
        else
        {
            // Get the NPC in front of us
            NPCController npcInFront = npcManager.GetNPCAtIndex(queueIndex - 1);

            if (npcInFront != null)
            {
                // Calculate spacing in normalized spline units
                float spacing = spacingBetweenNPCs / splineLength;

                // Target position is behind the NPC in front
                targetPosition = Mathf.Max(0, npcInFront.GetCurrentPosition() - spacing);

                // If NPC in front has reached its destination, and we're close to our target,
                // we should also stop
                if (npcInFront.HasReachedDestination() &&
                    Mathf.Abs(currentPosition - targetPosition) < 0.01f)
                {
                    hasReachedDestination = true;

                    // Stop the walking animation
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

    /// <summary>
    /// Checks if we need to move forward in the queue
    /// </summary>
    private void CheckForwardMovement()
    {
        // If in custom destination mode, don't check forward movement
        if (isCustomDestination)
            return;

        // Get our current actual index from the manager
        int currentRealIndex = npcManager.GetIndexOfNPC(this);

        // If our real index is different from what we think, update it
        if (currentRealIndex != -1 && currentRealIndex != queueIndex)
        {
            queueIndex = currentRealIndex;

            // Only set hasReachedDestination to false if we need to move forward
            // (i.e., our index decreased, meaning we're closer to the front)
            if (currentRealIndex < queueIndex)
            {
                hasReachedDestination = false;

                // Start the walking animation again
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
        // This can be overridden in derived classes
        // Default behavior does nothing
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
            hasReachedDestination = false; // Force recalculation
            UpdateTargetPosition();
        }
    }

    /// <summary>
    /// Returns whether this NPC is currently using a custom destination
    /// </summary>
    public bool IsUsingCustomDestination()
    {
        return isCustomDestination;
    }

    /// <summary>
    /// Clears the custom destination and returns to queue-based movement
    /// </summary>
    public void ClearCustomDestination()
    {
        isCustomDestination = false;
        hasReachedDestination = false;

        // Only reset to queue if we have a valid queue
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

        // Destroy this NPC
        Destroy(gameObject);
    }
}