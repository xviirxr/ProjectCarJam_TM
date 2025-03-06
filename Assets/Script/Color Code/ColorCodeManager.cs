using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorCodeManager : MonoBehaviour
{
    public enum ColorCode
    {
        Red,
        Blue,
        Yellow
    }

    [System.Serializable]
    public class ColorMapping
    {
        public ColorCode colorCode;
        public Color materialColor;
        public Material uiMaterial;
    }

    [Header("Color Settings")]
    [SerializeField] private ColorMapping[] colorMappings;
    [SerializeField] private bool enforceColorDistribution = true;
    [SerializeField] private float redProbability = 0.33f;
    [SerializeField] private float blueProbability = 0.33f;
    [SerializeField] private float yellowProbability = 0.34f;

    [Header("Distribution Tracking")]
    [SerializeField] private int redVehiclesAssigned = 0;
    [SerializeField] private int blueVehiclesAssigned = 0;
    [SerializeField] private int yellowVehiclesAssigned = 0;

    [SerializeField] private int redNPCsAssigned = 0;
    [SerializeField] private int blueNPCsAssigned = 0;
    [SerializeField] private int yellowNPCsAssigned = 0;

    private static ColorCodeManager _instance;
    public static ColorCodeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ColorCodeManager>();
                if (_instance == null)
                {
                    Debug.LogError("No ColorCodeManager found in scene!");
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }

        ValidateColorMappings();
    }

    private void ValidateColorMappings()
    {
        if (colorMappings == null || colorMappings.Length == 0)
        {
            Debug.LogWarning("No color mappings defined, creating defaults");
            colorMappings = new ColorMapping[3];

            colorMappings[0] = new ColorMapping
            {
                colorCode = ColorCode.Red,
                materialColor = new Color(0.9f, 0.2f, 0.2f)
            };

            colorMappings[1] = new ColorMapping
            {
                colorCode = ColorCode.Blue,
                materialColor = new Color(0.2f, 0.3f, 0.9f)
            };

            colorMappings[2] = new ColorMapping
            {
                colorCode = ColorCode.Yellow,
                materialColor = new Color(0.9f, 0.9f, 0.2f)
            };
        }

        float totalProb = redProbability + blueProbability + yellowProbability;
        if (Mathf.Abs(totalProb - 1f) > 0.01f)
        {
            Debug.LogWarning("Color probabilities don't sum to 1. Normalizing.");
            redProbability /= totalProb;
            blueProbability /= totalProb;
            yellowProbability /= totalProb;
        }
    }

    public Color GetColor(ColorCode colorCode)
    {
        foreach (ColorMapping mapping in colorMappings)
        {
            if (mapping.colorCode == colorCode)
            {
                return mapping.materialColor;
            }
        }

        Debug.LogWarning($"Color mapping not found for {colorCode}, returning white");
        return Color.white;
    }

    public Material GetUIMaterial(ColorCode colorCode)
    {
        foreach (ColorMapping mapping in colorMappings)
        {
            if (mapping.colorCode == colorCode && mapping.uiMaterial != null)
            {
                return mapping.uiMaterial;
            }
        }

        Debug.LogWarning($"UI material not found for {colorCode}, returning null");
        return null;
    }

    public ColorCode AssignVehicleColor()
    {
        ColorCode assignedColor;

        if (enforceColorDistribution)
        {
            int totalVehicles = redVehiclesAssigned + blueVehiclesAssigned + yellowVehiclesAssigned;

            if (totalVehicles == 0)
            {
                assignedColor = GetWeightedRandomColor();
            }
            else
            {
                float redRatio = (float)redVehiclesAssigned / totalVehicles;
                float blueRatio = (float)blueVehiclesAssigned / totalVehicles;
                float yellowRatio = (float)yellowVehiclesAssigned / totalVehicles;

                if (redRatio < redProbability && (redRatio <= blueRatio && redRatio <= yellowRatio))
                {
                    assignedColor = ColorCode.Red;
                }
                else if (blueRatio < blueProbability && (blueRatio <= redRatio && blueRatio <= yellowRatio))
                {
                    assignedColor = ColorCode.Blue;
                }
                else
                {
                    assignedColor = ColorCode.Yellow;
                }
            }
        }
        else
        {
            assignedColor = GetWeightedRandomColor();
        }

        TrackVehicleColorAssignment(assignedColor);
        return assignedColor;
    }

    public ColorCode AssignNPCColor()
    {
        ColorCode assignedColor;

        if (enforceColorDistribution)
        {
            int totalNPCs = redNPCsAssigned + blueNPCsAssigned + yellowNPCsAssigned;

            if (totalNPCs == 0)
            {
                assignedColor = GetWeightedRandomColor();
            }
            else
            {
                float redRatio = (float)redNPCsAssigned / totalNPCs;
                float blueRatio = (float)blueNPCsAssigned / totalNPCs;
                float yellowRatio = (float)yellowNPCsAssigned / totalNPCs;

                if (redRatio < redProbability && (redRatio <= blueRatio && redRatio <= yellowRatio))
                {
                    assignedColor = ColorCode.Red;
                }
                else if (blueRatio < blueProbability && (blueRatio <= redRatio && blueRatio <= yellowRatio))
                {
                    assignedColor = ColorCode.Blue;
                }
                else
                {
                    assignedColor = ColorCode.Yellow;
                }
            }
        }
        else
        {
            assignedColor = GetWeightedRandomColor();
        }

        TrackNPCColorAssignment(assignedColor);
        return assignedColor;
    }

    private ColorCode GetWeightedRandomColor()
    {
        float random = Random.value;

        if (random < redProbability)
            return ColorCode.Red;
        else if (random < redProbability + blueProbability)
            return ColorCode.Blue;
        else
            return ColorCode.Yellow;
    }

    private void TrackVehicleColorAssignment(ColorCode color)
    {
        switch (color)
        {
            case ColorCode.Red:
                redVehiclesAssigned++;
                break;
            case ColorCode.Blue:
                blueVehiclesAssigned++;
                break;
            case ColorCode.Yellow:
                yellowVehiclesAssigned++;
                break;
        }
    }

    private void TrackNPCColorAssignment(ColorCode color)
    {
        switch (color)
        {
            case ColorCode.Red:
                redNPCsAssigned++;
                break;
            case ColorCode.Blue:
                blueNPCsAssigned++;
                break;
            case ColorCode.Yellow:
                yellowNPCsAssigned++;
                break;
        }
    }

    public void UnregisterVehicleColor(ColorCode color)
    {
        switch (color)
        {
            case ColorCode.Red:
                redVehiclesAssigned = Mathf.Max(0, redVehiclesAssigned - 1);
                break;
            case ColorCode.Blue:
                blueVehiclesAssigned = Mathf.Max(0, blueVehiclesAssigned - 1);
                break;
            case ColorCode.Yellow:
                yellowVehiclesAssigned = Mathf.Max(0, yellowVehiclesAssigned - 1);
                break;
        }
    }

    public void UnregisterNPCColor(ColorCode color)
    {
        switch (color)
        {
            case ColorCode.Red:
                redNPCsAssigned = Mathf.Max(0, redNPCsAssigned - 1);
                break;
            case ColorCode.Blue:
                blueNPCsAssigned = Mathf.Max(0, blueNPCsAssigned - 1);
                break;
            case ColorCode.Yellow:
                yellowNPCsAssigned = Mathf.Max(0, yellowNPCsAssigned - 1);
                break;
        }
    }

    public bool DoColorsMatch(ColorCode color1, ColorCode color2)
    {
        return color1 == color2;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Create Default ColorCodeManager")]
    public static void CreateDefaultColorCodeManager()
    {
        if (FindFirstObjectByType<ColorCodeManager>() != null)
        {
            Debug.LogWarning("ColorCodeManager already exists in scene!");
            return;
        }

        GameObject managerObject = new GameObject("ColorCodeManager");
        managerObject.AddComponent<ColorCodeManager>();
        UnityEditor.Selection.activeGameObject = managerObject;
    }
#endif
}