using UnityEngine;

public class UIWorldBillboard : MonoBehaviour
{
    [SerializeField] private bool lockYAxis = true;

    private Camera mainCamera;
    private Canvas parentCanvas;

    void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("No main camera found. Billboard effect won't work.");
        }

        parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas == null || parentCanvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogError("This script must be attached to a UI element inside a World Space Canvas.");
        }
    }

    void LateUpdate()
    {
        if (mainCamera != null && parentCanvas != null)
        {
            FaceCamera();
        }
    }

    void FaceCamera()
    {
        Transform targetTransform = transform;

        Vector3 directionToCamera = mainCamera.transform.position - targetTransform.position;

        if (lockYAxis)
        {
            directionToCamera.y = 0;
        }

        if (directionToCamera != Vector3.zero)
        {
            targetTransform.rotation = Quaternion.LookRotation(directionToCamera);

            targetTransform.rotation *= Quaternion.Euler(0, 180f, 0);
        }
    }
}