using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

/// <summary>
/// Centralized vehicle movement configuration to ensure consistency across all controllers
/// </summary>
[CreateAssetMenu(fileName = "VehicleMovementConfig", menuName = "Vehicle/Movement Config", order = 1)]
public class VehicleMovementConfig : ScriptableObject
{
    [Header("Movement Parameters")]
    [Tooltip("Base movement speed in units per second")]
    [SerializeField] private float moveSpeed = 10f;

    [Tooltip("Rotation speed in degrees per second")]
    [SerializeField] private float rotationSpeed = 5f;

    [Tooltip("How quickly the vehicle accelerates")]
    [SerializeField] private float acceleration = 2f;

    [Tooltip("How quickly the vehicle slows down")]
    [SerializeField] private float deceleration = 4f;

    [Tooltip("How much the vehicle slows down when turning")]
    [SerializeField] private float steeringFactor = 2f;

    [Header("Arrival Parameters")]
    [Tooltip("Distance threshold to consider arrival at target")]
    [SerializeField] private float arrivalDistanceThreshold = 0.5f;

    [Tooltip("Angle threshold in degrees to consider arrival at target")]
    [SerializeField] private float arrivalAngleThreshold = 5f;

    [Tooltip("Distance threshold for arrival at waypoints")]
    [SerializeField] private float pathPointArrivalThreshold = 1.0f;

    // Public accessors
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float SteeringFactor => steeringFactor;
    public float ArrivalDistanceThreshold => arrivalDistanceThreshold;
    public float ArrivalAngleThreshold => arrivalAngleThreshold;
    public float PathPointArrivalThreshold => pathPointArrivalThreshold;

#if UNITY_EDITOR
    [Button("Apply To All Vehicles In Scene")]
    private void ApplyToAllVehicles()
    {
        VehicleController[] vehicles = UnityEngine.Object.FindObjectsOfType<VehicleController>();
        int count = 0;

        foreach (VehicleController vehicle in vehicles)
        {
            // Get the movement parameters component if it exists
            VehicleMovementParameters parameters = vehicle.GetComponent<VehicleMovementParameters>();

            // If not found, add it
            if (parameters == null)
            {
                parameters = vehicle.gameObject.AddComponent<VehicleMovementParameters>();
            }

            // Set the config
            parameters.SetConfig(this);
            count++;
        }

        Debug.Log($"Applied movement config to {count} vehicles in scene");
    }
#endif
}