using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

/// <summary>
/// Component to store and provide movement parameters for a vehicle
/// This is attached to each vehicle GameObject and accessed by all controllers
/// </summary>
public class VehicleMovementParameters : MonoBehaviour
{
    [SerializeField] private VehicleMovementConfig config;

    // Current runtime parameters (can be modified at runtime)
    private float currentMoveSpeed;
    private float currentRotationSpeed;
    private float currentAcceleration;
    private float currentDeceleration;
    private float currentSteeringFactor;
    private float currentArrivalDistanceThreshold;
    private float currentArrivalAngleThreshold;
    private float currentPathPointArrivalThreshold;

    private void Awake()
    {
        if (config != null)
        {
            InitializeFromConfig();
        }
        else
        {
            // Use default values if no config is set
            currentMoveSpeed = 10f;
            currentRotationSpeed = 5f;
            currentAcceleration = 2f;
            currentDeceleration = 4f;
            currentSteeringFactor = 2f;
            currentArrivalDistanceThreshold = 0.5f;
            currentArrivalAngleThreshold = 5f;
            currentPathPointArrivalThreshold = 1.0f;

            Debug.LogWarning($"No movement config assigned to {gameObject.name}. Using default values.");
        }
    }

    private void InitializeFromConfig()
    {
        if (config == null) return;

        currentMoveSpeed = config.MoveSpeed;
        currentRotationSpeed = config.RotationSpeed;
        currentAcceleration = config.Acceleration;
        currentDeceleration = config.Deceleration;
        currentSteeringFactor = config.SteeringFactor;
        currentArrivalDistanceThreshold = config.ArrivalDistanceThreshold;
        currentArrivalAngleThreshold = config.ArrivalAngleThreshold;
        currentPathPointArrivalThreshold = config.PathPointArrivalThreshold;
    }

    public void SetConfig(VehicleMovementConfig newConfig)
    {
        config = newConfig;
        InitializeFromConfig();
    }

    // Getters
    public float MoveSpeed => currentMoveSpeed;
    public float RotationSpeed => currentRotationSpeed;
    public float Acceleration => currentAcceleration;
    public float Deceleration => currentDeceleration;
    public float SteeringFactor => currentSteeringFactor;
    public float ArrivalDistanceThreshold => currentArrivalDistanceThreshold;
    public float ArrivalAngleThreshold => currentArrivalAngleThreshold;
    public float PathPointArrivalThreshold => currentPathPointArrivalThreshold;

    // Setters with option to modify at runtime
    public void SetMoveSpeed(float value) => currentMoveSpeed = value;
    public void SetRotationSpeed(float value) => currentRotationSpeed = value;
    public void SetAcceleration(float value) => currentAcceleration = value;
    public void SetDeceleration(float value) => currentDeceleration = value;
    public void SetSteeringFactor(float value) => currentSteeringFactor = value;
    public void SetArrivalDistanceThreshold(float value) => currentArrivalDistanceThreshold = value;
    public void SetArrivalAngleThreshold(float value) => currentArrivalAngleThreshold = value;
    public void SetPathPointArrivalThreshold(float value) => currentPathPointArrivalThreshold = value;

    // Reset to config values
    public void ResetToConfigValues() => InitializeFromConfig();

#if UNITY_EDITOR
    [Button("Apply Current Config")]
    private void ApplyCurrentConfig()
    {
        if (config != null)
        {
            InitializeFromConfig();
            Debug.Log($"Applied config to {gameObject.name}");
        }
        else
        {
            Debug.LogError("No config assigned to apply");
        }
    }
#endif
}