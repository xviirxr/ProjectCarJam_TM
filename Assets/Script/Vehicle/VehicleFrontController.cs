using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

/// <summary>
/// Controls the front movement check for vehicles
/// </summary>
public class VehicleFrontController : MonoBehaviour
{
    [Header("Ray Cast Settings")]
    [SerializeField] private Transform raycastOrigin;
    [SerializeField] private float raycastDistance = 2f;
    [SerializeField] private LayerMask vehicleLayerMask;
    [SerializeField] private GameObject forwardArrowIndicator; // Arrow indicator

    [Header("Movement Settings")]
    [SerializeField] private float moveForwardTime = 2f;
    [SerializeField] private float blockedMoveDuration = 0.5f;

    // These will be taken from VehicleController
    private float moveSpeed = 5f;
    private float rotationSpeed = 3f;
    private float acceleration = 2f;
    private float deceleration = 4f;
    private float steeringFactor = 2f;

    // References
    private VehicleController vehicleController;
    private VehicleInteraction vehicleInteraction;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isMoving = false;

    // Movement coroutines
    private Coroutine currentMovementCoroutine;

    private void Awake()
    {
        // Get references
        vehicleController = GetComponent<VehicleController>();
        vehicleInteraction = GetComponent<VehicleInteraction>();

        // Create raycast origin if not set
        if (raycastOrigin == null)
        {
            GameObject originObj = new GameObject("RaycastOrigin");
            originObj.transform.SetParent(transform);
            originObj.transform.localPosition = new Vector3(0, 0.5f, 0); // Slightly above the car
            raycastOrigin = originObj.transform;

            Debug.Log("Created raycast origin at: " + raycastOrigin.position);
        }

        // Get movement parameters from VehicleController
        if (vehicleController != null)
        {
            System.Reflection.FieldInfo[] fields = vehicleController.GetType().GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                switch (field.Name)
                {
                    case "moveSpeed":
                        moveSpeed = (float)field.GetValue(vehicleController);
                        break;
                    case "rotationSpeed":
                        rotationSpeed = (float)field.GetValue(vehicleController);
                        break;
                    case "acceleration":
                        acceleration = (float)field.GetValue(vehicleController);
                        break;
                    case "deceleration":
                        deceleration = (float)field.GetValue(vehicleController);
                        break;
                    case "steeringFactor":
                        steeringFactor = (float)field.GetValue(vehicleController);
                        break;
                }
            }

            Debug.Log($"Got movement parameters from VehicleController: speed={moveSpeed}, " +
                      $"rotation={rotationSpeed}, accel={acceleration}, decel={deceleration}");
        }
    }

    private void Start()
    {
        // Get movement parameters from VehicleController if not already done in Awake
        if (moveSpeed == 5f && vehicleController != null)
        {
            System.Reflection.FieldInfo[] fields = vehicleController.GetType().GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                switch (field.Name)
                {
                    case "moveSpeed":
                        moveSpeed = (float)field.GetValue(vehicleController);
                        break;
                    case "rotationSpeed":
                        rotationSpeed = (float)field.GetValue(vehicleController);
                        break;
                    case "acceleration":
                        acceleration = (float)field.GetValue(vehicleController);
                        break;
                    case "deceleration":
                        deceleration = (float)field.GetValue(vehicleController);
                        break;
                    case "steeringFactor":
                        steeringFactor = (float)field.GetValue(vehicleController);
                        break;
                }
            }
        }

        // Make sure the forward arrow indicator is active at start
        if (forwardArrowIndicator != null)
        {
            forwardArrowIndicator.SetActive(true);
        }
        else
        {
            Debug.LogWarning("ForwardArrowIndicator not assigned in VehicleFrontController!");
        }
    }

    /// <summary>
    /// Checks if there's a vehicle in front and handles movement accordingly
    /// </summary>
    public void CheckFrontAndMove()
    {
        if (isMoving)
        {
            Debug.Log("Already in a movement animation, ignoring request");
            return;
        }

        // Store original position and rotation for returning if blocked
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // Perform the raycast
        bool isBlocked = CheckIfBlocked();

        if (isBlocked)
        {
            // Animate moving toward obstacle then back
            if (currentMovementCoroutine != null)
            {
                StopCoroutine(currentMovementCoroutine);
            }
            currentMovementCoroutine = StartCoroutine(AnimateBlockedMovement());
        }
        else
        {
            // Move forward for 2 seconds then continue with normal behavior
            if (currentMovementCoroutine != null)
            {
                StopCoroutine(currentMovementCoroutine);
            }
            currentMovementCoroutine = StartCoroutine(MoveForwardThenContinue());

            // Hide the arrow indicator when we start moving forward
            if (forwardArrowIndicator != null)
            {
                forwardArrowIndicator.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Checks if there's a vehicle in front
    /// </summary>
    private bool CheckIfBlocked()
    {
        if (raycastOrigin == null)
        {
            Debug.LogError("Raycast origin is not set!");
            return false;
        }

        Debug.DrawRay(raycastOrigin.position, raycastOrigin.forward * raycastDistance, Color.red, 2f);

        RaycastHit hit;
        if (Physics.Raycast(raycastOrigin.position, raycastOrigin.forward, out hit, raycastDistance, vehicleLayerMask))
        {
            // Check if the hit object is a vehicle
            if (hit.transform.GetComponent<VehicleController>() != null)
            {
                Debug.Log("Vehicle blocked by: " + hit.transform.name + " at distance: " + hit.distance);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Animates a blocked movement (forward then back)
    /// Using acceleration/deceleration for smoother motion
    /// </summary>
    private IEnumerator AnimateBlockedMovement()
    {
        isMoving = true;

        // Store original position and rotation
        Vector3 startPos = originalPosition;
        Quaternion startRot = originalRotation;

        // Calculate a point to move toward (40% of the raycast distance)
        Vector3 targetPosition = transform.position + transform.forward * (raycastDistance * 0.4f);

        // Variables for smooth movement
        float currentSpeed = 0f;
        float moveProgress = 0f;
        bool movingForward = true;

        // Movement with acceleration/deceleration
        while (moveProgress < 1.0f)
        {
            float targetSpeed = movingForward ? moveSpeed * 0.5f : moveSpeed * 0.6f;
            float distanceToTarget;

            if (movingForward)
            {
                distanceToTarget = Vector3.Distance(transform.position, targetPosition);
                // Start slowing down when we're close to target
                if (distanceToTarget < 1.0f)
                {
                    targetSpeed *= distanceToTarget;
                }
            }
            else
            {
                distanceToTarget = Vector3.Distance(transform.position, startPos);
                // Start slowing down when we're close to original position
                if (distanceToTarget < 1.0f)
                {
                    targetSpeed *= distanceToTarget;
                }
            }

            // Apply acceleration or deceleration
            if (currentSpeed < targetSpeed)
                currentSpeed += acceleration * Time.deltaTime;
            else
                currentSpeed -= deceleration * Time.deltaTime;

            currentSpeed = Mathf.Clamp(currentSpeed, 0, moveSpeed);

            // Move in the correct direction
            if (movingForward)
            {
                transform.position += transform.forward * currentSpeed * Time.deltaTime;

                // Check if we've reached or passed the target
                if (Vector3.Dot(targetPosition - transform.position, transform.forward) <= 0)
                {
                    movingForward = false;
                    currentSpeed *= 0.5f; // Reduce speed for direction change
                }
            }
            else
            {
                transform.position -= transform.forward * currentSpeed * Time.deltaTime;

                // Check if we've reached or passed the original position
                float distanceFromStart = Vector3.Distance(transform.position, startPos);
                moveProgress = 1.0f - (distanceFromStart / Vector3.Distance(targetPosition, startPos));

                // If we're back at original position
                if (distanceFromStart < 0.1f || moveProgress >= 1.0f)
                {
                    moveProgress = 1.0f;
                }
            }

            yield return null;
        }

        // Ensure we're back at the exact original position
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        isMoving = false;
        currentMovementCoroutine = null;
    }

    /// <summary>
    /// Moves forward for 2 seconds then continues with normal behavior
    /// Using the same movement logic as VehicleController for consistency
    /// </summary>
    private IEnumerator MoveForwardThenContinue()
    {
        isMoving = true;

        // Calculate a point to move forward
        Vector3 targetPosition = transform.position + transform.forward * (moveSpeed * moveForwardTime);
        Quaternion targetRotation = transform.rotation; // Keep current rotation

        // Store starting position for possible collision reset
        Vector3 startPosition = transform.position;
        float currentSpeed = 0f;
        float timeElapsed = 0f;

        while (timeElapsed < moveForwardTime)
        {
            // Calculate direction to target
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            // Calculate desired rotation (maintain current direction)
            Quaternion desiredRotation = Quaternion.LookRotation(directionToTarget);

            // Smoothly rotate towards the target (minimal rotation as we're going straight)
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);

            // Determine speed adjustments
            float angleToTarget = Quaternion.Angle(transform.rotation, desiredRotation);
            float speedFactor = Mathf.Clamp01(1.0f - angleToTarget / 90.0f);

            // Adjust speed based on distance to target and angle
            float targetSpeed = moveSpeed;

            // Slow down when approaching target
            if (distanceToTarget < 5.0f)
            {
                targetSpeed *= Mathf.Min(distanceToTarget / 5.0f, speedFactor * steeringFactor);
            }

            // Apply acceleration
            if (currentSpeed < targetSpeed)
                currentSpeed += acceleration * Time.deltaTime;
            else
                currentSpeed -= deceleration * Time.deltaTime;

            currentSpeed = Mathf.Clamp(currentSpeed, 0, moveSpeed);

            // Move the vehicle
            transform.position += transform.forward * currentSpeed * Time.deltaTime;

            // Check for any unexpected collision (optional)
            if (CheckIfBlocked() && Vector3.Distance(transform.position, startPosition) > 0.5f)
            {
                Debug.Log("Detected obstacle during forward movement, stopping early");
                break;
            }

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Smoothly stop the vehicle
        float stopTime = 0.5f;
        float stopElapsed = 0f;

        while (stopElapsed < stopTime && currentSpeed > 0.1f)
        {
            currentSpeed -= deceleration * Time.deltaTime;
            if (currentSpeed < 0) currentSpeed = 0;

            transform.position += transform.forward * currentSpeed * Time.deltaTime;
            stopElapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure we've stopped
        currentSpeed = 0f;

        isMoving = false;
        currentMovementCoroutine = null;

        // Continue with normal behavior if we have a vehicle interaction component
        if (vehicleInteraction != null)
        {
            vehicleInteraction.AssignVehicleToParking();
        }
    }

    /// <summary>
    /// Checks if the vehicle is currently in a movement animation
    /// </summary>
    public bool IsMoving()
    {
        return isMoving;
    }

    private void OnDestroy()
    {
        if (currentMovementCoroutine != null)
        {
            StopCoroutine(currentMovementCoroutine);
        }
    }

    private void OnDrawGizmos()
    {
        if (raycastOrigin != null)
        {
            // Draw raycast line
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(raycastOrigin.position, raycastOrigin.position + raycastOrigin.forward * raycastDistance);

            // Draw a sphere at the end of the raycast distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(raycastOrigin.position + raycastOrigin.forward * raycastDistance, 0.2f);
        }
    }

#if UNITY_EDITOR
    [Button("Test Check Front")]
    private void TestCheckFront()
    {
        bool isBlocked = CheckIfBlocked();
        Debug.Log("Is Blocked: " + isBlocked);
    }

    [Button("Test Blocked Animation")]
    private void TestBlockedAnimation()
    {
        if (currentMovementCoroutine != null)
        {
            StopCoroutine(currentMovementCoroutine);
        }
        currentMovementCoroutine = StartCoroutine(AnimateBlockedMovement());
    }

    [Button("Test Forward Movement")]
    private void TestForwardMovement()
    {
        if (currentMovementCoroutine != null)
        {
            StopCoroutine(currentMovementCoroutine);
        }
        currentMovementCoroutine = StartCoroutine(MoveForwardThenContinue());
    }
#endif
}
