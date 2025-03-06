using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class NPCColorController : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private Renderer[] coloredRenderers;
    [SerializeField] private Renderer colorIndicatorRenderer;
    [SerializeField] private Material originalMaterial;

    private NPCController npcController;
    private Material coloredMaterial;
    private bool colorAssigned = false;
    private ColorCodeManager.ColorCode npcColor;

    private void Awake()
    {
        npcController = GetComponent<NPCController>();
        if (npcController == null)
        {
            Debug.LogError("No NPCController found on this object!");
        }

        if (coloredRenderers == null || coloredRenderers.Length == 0)
        {
            coloredRenderers = GetComponentsInChildren<Renderer>();
            if (coloredRenderers.Length == 0)
            {
                Debug.LogError("No Renderers found for NPC coloring!");
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
            npcColor = ColorCodeManager.Instance.AssignNPCColor();
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

        Color color = ColorCodeManager.Instance.GetColor(npcColor);

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

        if (colorIndicatorRenderer != null)
        {
            Material indicatorMaterial = new Material(Shader.Find("Standard"));
            indicatorMaterial.color = color;
            colorIndicatorRenderer.material = indicatorMaterial;
        }
    }

    public ColorCodeManager.ColorCode GetNPCColor()
    {
        return npcColor;
    }

    public void SetNPCColor(ColorCodeManager.ColorCode newColor)
    {
        if (colorAssigned && ColorCodeManager.Instance != null)
        {
            ColorCodeManager.Instance.UnregisterNPCColor(npcColor);
        }

        npcColor = newColor;
        colorAssigned = true;
        ApplyColor();
    }

    public bool CanBoardVehicleWithColor(ColorCodeManager.ColorCode vehicleColor)
    {
        if (ColorCodeManager.Instance != null)
        {
            return ColorCodeManager.Instance.DoColorsMatch(npcColor, vehicleColor);
        }

        return npcColor == vehicleColor;
    }

#if UNITY_EDITOR
    [Button("Force Assign New Color")]
    private void EditorForceNewColor()
    {
        if (ColorCodeManager.Instance != null)
        {
            if (colorAssigned)
            {
                ColorCodeManager.Instance.UnregisterNPCColor(npcColor);
            }

            npcColor = ColorCodeManager.Instance.AssignNPCColor();
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
            ColorCodeManager.Instance.UnregisterNPCColor(npcColor);
        }

        if (coloredMaterial != null)
        {
            Destroy(coloredMaterial);
        }
    }
}