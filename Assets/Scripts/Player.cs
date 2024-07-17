using UnityEngine;

/// <summary>
/// Move player according to gps, rotate him according to compass and keep him on terrain
/// </summary>
/// <author>Michal Mráz</author>
public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }
    
    const int TERRAIN_LAYER = 3;
    
    // Start is called before the first frame update
    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
            
    }

    // Update is called once per frame
    void Update()
    {
        updatePlayerRotationFromCompassSensor();      
        
        // Move player vertically onto terrain
        Vector3 playerLoc = Gps.instance.ConvertGpsToUnityCoords(Gps.instance.getGps());
        Vector3 rayStart = playerLoc + Vector3.up * 20000f;
        Vector3 rayDirection = Vector3.down;
        RaycastHit hit;
        int layerMask = 1 << TERRAIN_LAYER;
        if (Physics.Raycast(rayStart, rayDirection, out hit, 40000f, layerMask)) // we assigned a layer to terrain which will interact with this raycast // and the layer settings are set in Unity accordingly
        {
            playerLoc = hit.point;
            transform.position = playerLoc;
        }
        
    }

    private void updatePlayerRotationFromCompassSensor()
    {
        // enable compass sensor
        Input.compass.enabled = true;
        // get it's rotation's value
        float heading = Input.compass.trueHeading;
        Quaternion rotation = Quaternion.Euler(0, 90 + heading, 0);
        // interpolate from old rotation to new
        Quaternion oldRotation = transform.rotation;
        Quaternion newRotation = Quaternion.Lerp(oldRotation, rotation, 0.1f);
        // apply
        transform.rotation = newRotation;
    }
}
