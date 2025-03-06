using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class VehicleColorController : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private Renderer[] coloredRenderers;
    [SerializeField] private Material originalMaterial;
    [SerializeField] private GameObject colorIndicator;

    private VehicleController vehicleController;
    private Material coloredMaterial;
    private bool colorAssigned = false;
    private ColorCodeManager.ColorCode vehicleColor;

    private void Awake()
    {
        vehicleController = GetComponent<VehicleController>();
        if (vehicleController == null)
        {
            Debug.LogError("No VehicleController found on this object!");
        }

        if (coloredRenderers == null || coloredRenderers.Length == 0)
        {
            coloredRenderers = GetComponentsInChildren<Renderer>();
            if (coloredRenderers.Length == 0)
            {
                Debug.LogError("No Renderers found for vehicle coloring!");
            }
        }

        if (originalMaterial == null && coloredRenderers.Length > 0)
        {
            originalMaterial = coloredRenderers[0].material;
        }
    }

    private void Start()
    {
        if (ColorCodeManager.Instance != null)
        {
            vehicleColor = ColorCodeManager.Instance.AssignVehicleColor();
            colorAssigned = true;
            ApplyColor();
        }
        else
        {
            Debug.LogError("No ColorCodeManager found in scene!");
        }
    }

    private void ApplyColor()
    {
        if (ColorCodeManager.Instance == null)
        {
            Debug.LogError("No ColorCodeManager found, can't apply color!");
            return;
        }

        Color color = ColorCodeManager.Instance.GetColor(vehicleColor);

        if (originalMaterial != null)
        {
            coloredMaterial = new Material(originalMaterial);
            coloredMaterial.color = color;
        }
        else
        {
            coloredMaterial = new Material(Shader.Find("Standard"));
            coloredMaterial.color = color;
        }

        foreach (Renderer renderer in coloredRenderers)
        {
            if (renderer != null)
            {
                renderer.material = coloredMaterial;
            }
        }

        if (colorIndicator != null)
        {
            Renderer indicatorRenderer = colorIndicator.GetComponent<Renderer>();
            if (indicatorRenderer != null)
            {
                indicatorRenderer.material.color = color;
            }
        }
    }

    public ColorCodeManager.ColorCode GetVehicleColor()
    {
        return vehicleColor;
    }

    public void SetVehicleColor(ColorCodeManager.ColorCode newColor)
    {
        if (colorAssigned && ColorCodeManager.Instance != null)
        {
            ColorCodeManager.Instance.UnregisterVehicleColor(vehicleColor);
        }

        vehicleColor = newColor;
        colorAssigned = true;
        ApplyColor();
    }

    public bool AcceptsPassengerColor(ColorCodeManager.ColorCode passengerColor)
    {
        if (ColorCodeManager.Instance != null)
        {
            return ColorCodeManager.Instance.DoColorsMatch(vehicleColor, passengerColor);
        }

        return vehicleColor == passengerColor;
    }

#if UNITY_EDITOR
    [Button("Force Assign New Color")]
    private void EditorForceNewColor()
    {
        if (ColorCodeManager.Instance != null)
        {
            if (colorAssigned)
            {
                ColorCodeManager.Instance.UnregisterVehicleColor(vehicleColor);
            }

            vehicleColor = ColorCodeManager.Instance.AssignVehicleColor();
            colorAssigned = true;
            ApplyColor();
        }
        else
        {
            Debug.LogError("No ColorCodeManager found in scene!");
        }
    }

    [Button("Apply Current Color")]
    private void EditorApplyCurrentColor()
    {
        ApplyColor();
    }
#endif

    private void OnDestroy()
    {
        if (colorAssigned && ColorCodeManager.Instance != null)
        {
            ColorCodeManager.Instance.UnregisterVehicleColor(vehicleColor);
        }

        if (coloredMaterial != null)
        {
            Destroy(coloredMaterial);
        }
    }
}