using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Manage settings values and manage UI associated with changing settings 
/// </summary>
/// <author>Michal Mráz</author>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager instance;
    
    private float minVegetationDensity = 0f;
    private float defaultVegetationDensity = 0.69f;
    private float maxVegetationDensity = 1f;
    
    private float minChunkGenerationDistance = 250f;
    private float defaultChunkGenerationDistance = 530f;
    private float maxChunkGenerationDistance = 1000f;
    
    private float minVegetationLushness = 0f;
    private float defaultVegetationLushness = 0.9f;
    private float maxVegetationLushness = 1f;

    private bool defaultUseBuildingHeights = false;
    
    public struct SimulatedLocation
    {
        public string name;
        public GpsVector gps;
    }

    private SimulatedLocation[] simulatedLocations = new SimulatedLocation[]
    {
        new SimulatedLocation {name = "Barrandov", gps = new GpsVector((float)50.026896, (float)14.393643)},
        new SimulatedLocation {name = "Karlovo náměstí", gps = new GpsVector((float)50.076104, (float)14.418764)},
        new SimulatedLocation {name = "Palata", gps = new GpsVector((float)50.076169, (float)14.388643)},
        new SimulatedLocation {name = "Depo Vršovice", gps = new GpsVector((float)50.05959618, (float)14.46194237)},
        new SimulatedLocation {name = "Eiffel Tower", gps = new GpsVector((float)48.856973, (float)2.296475)},
        new SimulatedLocation {name = "Buckingham Palace", gps = new GpsVector((float)51.501704, (float)-0.141128)},
        new SimulatedLocation {name = "Central Park", gps = new GpsVector((float)40.774031, (float)-73.970967)},
        new SimulatedLocation {name = "Murmansk", gps = new GpsVector((float)68.981292, (float)33.099693)},
        new SimulatedLocation {name = "Santo Domingo", gps = new GpsVector((float)-0.246972, (float)-79.191681)},
        new SimulatedLocation {name = "Falkland Islands", gps = new GpsVector((float)-51.894376, (float)-58.445879)},
        new SimulatedLocation {name = "New York", gps = new GpsVector(40.7128, -74.0060)},
        new SimulatedLocation {name = "San Francisco", gps = new GpsVector((float)37.802854, (float)-122.406010)},
        new SimulatedLocation {name = "San Francisco 2", gps = new GpsVector((float)37.800944, (float)-122.410077)},
        new SimulatedLocation {name = "Los Angeles", gps = new GpsVector(34.0522, -118.2437)},
        new SimulatedLocation {name = "Charlotte", gps = new GpsVector(35.2271, -80.8431)},
        
    };
        
    public Slider vegetationDensitySlider;
    public Slider chunkGenerationDistanceSlider;
    public Slider vegetationLushnessSlider;
    public TMP_Dropdown simulatedGpsLocationDropdown;
    public TextMeshProUGUI vegetationDensityText;
    public TextMeshProUGUI chunkGenerationDistanceText;
    public TextMeshProUGUI vegetationLushnessText;
    
    private void Awake()
    {
        // what if there is already an instance?
        if ((Object)instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    void Update()
    {
        if (String.Equals(Settings.googleApiKey, ""))
        {
            Debug.Log("Google API key is not set. Please set it in the Settings class in Assets/Scripts/Common.");
        }
    }

    void Start()
    {
        vegetationDensitySlider.minValue = minVegetationDensity;
        vegetationDensitySlider.maxValue = maxVegetationDensity;
        vegetationDensitySlider.value = defaultVegetationDensity; // should also set the value in the Settings class
        
        chunkGenerationDistanceSlider.minValue = minChunkGenerationDistance;
        chunkGenerationDistanceSlider.maxValue = maxChunkGenerationDistance;
        chunkGenerationDistanceSlider.value = defaultChunkGenerationDistance;
        
        vegetationLushnessSlider.minValue = minVegetationLushness;
        vegetationLushnessSlider.maxValue = maxVegetationLushness;
        vegetationLushnessSlider.value = defaultVegetationLushness;
        
        // set dropdown options
        simulatedGpsLocationDropdown.ClearOptions();
        List<string> locationNames = new List<string>();
        foreach (SimulatedLocation location in simulatedLocations)
        {
            locationNames.Add(location.name);
        }
        simulatedGpsLocationDropdown.AddOptions(locationNames);
        // Add Real GPS as the last option
        simulatedGpsLocationDropdown.options.Add(new TMP_Dropdown.OptionData("Real GPS"));
        
    }
    
    public void setVegetationDensityValue(float value)
    {
        Settings.vegetationDensity = value;
        vegetationDensityText.text = floatToString(value);
    }

    private string floatToString(float value)
    {
        return value.ToString("0.00");
    }
    
    public void setChunkGenerationDistanceValue(float value)
    {
        Settings.chunkGenerationDistance = value;
        chunkGenerationDistanceText.text = floatToString(value);
    }
    
    public void setVegetationLushnessValue(float value)
    {
        Settings.vegetationLushness = value;
        vegetationLushnessText.text = floatToString(value);
    }

    public void setSimulatedGpsUsingDropdown()
    {
        int optionIndex = simulatedGpsLocationDropdown.value;
        if (optionIndex >= simulatedLocations.Length)
        {
            Gps.instance.setSimulatedGpsToRealGps();
        }
        else
        {
            GpsVector gps = simulatedLocations[optionIndex].gps;
            Gps.instance.setSimulatedGps(gps);    
        }
        
    }
    
    public void setUseBuildingHeights(bool value)
    {
        Settings.useBuildingHeights = value;
    }
    
}
