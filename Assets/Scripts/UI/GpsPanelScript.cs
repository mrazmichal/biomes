using UnityEngine;
using TMPro;

/// <summary>
/// Write current gps location onto the GUI panel, if it is enabled
/// </summary>
/// <author>Michal Mráz</author>
public class GpsPanelScript : MonoBehaviour
{
    public GameObject textObject;
    TextMeshProUGUI textMesh1;

    // Start is called before the first frame update
    void Start()
    {
        textMesh1 = textObject.GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        // string message = "Gps: " + Gps.instance.getGps().latitude.ToString("F12") + ", " + Gps.instance.getGps().longitude.ToString("F12");
        string message = "GPS: " + Gps.instance.getGps().latitude.ToString("F8") + ", " + Gps.instance.getGps().longitude.ToString("F8");
        textMesh1.text = message;
    }
    
}
