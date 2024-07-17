using UnityEngine;

public class MaterialPicker : MonoBehaviour
{
    public static MaterialPicker instance;
    
    public Material highwayMaterial;
    public Material terrainShaderMaterial;
    public Material defaultTerrainMaterial;
    public Material buildingMaterial;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
