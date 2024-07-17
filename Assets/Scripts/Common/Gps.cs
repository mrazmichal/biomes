using UnityEngine;
using UnityEngine.Android;
using System;
using static Helpers;

/// <summary>
/// Manages real and simulated GPS data. Converts GPS coordinates to Unity coordinates and vice versa. 
/// </summary>
/// <author>Michal Mráz</author>
public class Gps : MonoBehaviour
{
    public static Gps instance { get; private set; }

    public static GpsVector inaccuracyTolerance = new GpsVector(0.00000000025, 0.0000000125); // this should correspond to about 0.001 meters and 0.000935 meters in Prague

    public const double EARTH_RADIUS = 6378137; // meters
    public const double EARTH_CIRCUMFERENCE = 2 * Math.PI * EARTH_RADIUS; // meters

    public bool worldCenterNotYetSet { get; private set; } = true;
    GpsVector worldOriginGps; // world origin is used because of limited float type precision. Transforms of GameObjects use float type
    Double3 worldOriginMeters;
    GpsVector referenceOriginGps = new GpsVector(50.075340, 14.436288); // nam. Miru // we will use the reference origin's latitude for conversions 

    public GpsVector gameGps { get; private set; }
    private GpsVector oldGameGps = new GpsVector(0, 0);
    
    private GpsVector simulatedGps = new GpsVector(0, 0);
    private GpsVector realGps = new GpsVector(0, 0);

    private bool realGpsNotYetSet = true;
    private bool simulatedGpsNotYetSet = true;
    public bool gameGpsNotYetSet = true;
    
    bool _enableGpsSimulation;
    public bool enableGpsSimulation {
        get
        {
            return _enableGpsSimulation;
        }
        set
        {
            _enableGpsSimulation = value;
            
            // We add this so that something happens when user just clicks enable simulated gps without setting simulated gps value first
            if (_enableGpsSimulation)
            {
                // if simulated gps was not yet set
                if (simulatedGpsNotYetSet)
                {
                    // try to set copy the real gps to simulated gps
                    setSimulatedGpsToRealGps();
                }
            }
            
            EventManager.InvokeEventGpsSimulationEnabled();
        }
    }
    
    public Joystick joystick;
    public Transform cameraTransform;
    
    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        Input.location.Start(0, 0);
    }
    
    void Update()
    {
        if (enableGpsSimulation)
        {
            updateSimulatedGps();
        }
        else
        {
            updateRealGps();
        }

        gameGpsNotYetSet = enableGpsSimulation ? simulatedGpsNotYetSet : realGpsNotYetSet;

        if (gameGpsNotYetSet)
        {
            return;
        }
        
        // set game gps to simulated or real gps
        gameGps = enableGpsSimulation ? simulatedGps : realGps;

        // set world center if it was not yet set
        if ((worldCenterNotYetSet))
        {
            setWorldCenterGps(gameGps);
        }
        
        // set new world center if the game gps is too far away from it
        if (!worldCenterNotYetSet)
        {
            Vector3 gameGpsInUnityCoords = ConvertGpsToUnityCoords(gameGps);
            Vector3 worldCenterGpsInUnityCoords = ConvertGpsToUnityCoords(worldOriginGps);
            float distance = Vector3.Distance(gameGpsInUnityCoords, worldCenterGpsInUnityCoords);
            if (distance > 50000) // 50 km
            {
                setWorldCenterGps(gameGps);
            }
        }
        
        // check distance of game gps from old gps
        // if it is too far, let the game gps be as it is
        // if it is not too far, set the game gps to interpolated value between old gps and game gps
        float distanceFromOldGps = Vector3.Distance(ConvertGpsToUnityCoords(oldGameGps), ConvertGpsToUnityCoords(gameGps));
        if (distanceFromOldGps < 200)
        {
            // float alpha = Time.deltaTime; // results in jumping gps :(
            float alpha = 0.05f;
            if (alpha > 1)
            {
                alpha = 1;
            }
            float newFactor = alpha;
            float oldFactor = 1 - alpha;
            // Debug.Log("New factor: " + newFactor + ", old factor: " + oldFactor);
            gameGps = new GpsVector(newFactor * gameGps.latitude + oldFactor * oldGameGps.latitude, newFactor * gameGps.longitude + oldFactor * oldGameGps.longitude);    
        }

        oldGameGps = gameGps;
    }
    
    void updateRealGps()
    {
        if (!Input.location.isEnabledByUser)
        {
            // Debug.Log("GPS is not enabled by user, requesting user permission");
            Permission.RequestUserPermission(Permission.FineLocation);
            return;
        }

        if (Input.location.status == LocationServiceStatus.Failed || Input.location.status == LocationServiceStatus.Stopped)
        {
            Debug.Log("GPS is not running");
            return;
        }

        if (Input.location.status == LocationServiceStatus.Initializing)
        {
            Debug.Log("GPS status: " + Input.location.status);    
            return;
        }
        
        realGps = new GpsVector(Input.location.lastData.latitude, Input.location.lastData.longitude);
        realGpsNotYetSet = false;
    }

    void updateSimulatedGps()
    {
        if (simulatedGpsNotYetSet)
        {
            return;
        }

        if (worldCenterNotYetSet)
        {
            return;
        }
        
        if ((System.Object)joystick == null)
        {
            return;
        }
        
        if (joystick.value == Vector2.zero)
        {
            return;
        }
        
        Vector3 currentPosInUnityCoords = ConvertGpsToUnityCoords(simulatedGps);
        float speed = 100f * Time.deltaTime;
        Vector3 joystickDirection = new Vector3(joystick.value.x, 0, joystick.value.y);
        Quaternion rotation = Quaternion.Euler(0, cameraTransform.rotation.eulerAngles.y, 0);
        Vector3 joystickDirectionRotated = rotation * joystickDirection;
        Vector3 newPosInUnityCoords = currentPosInUnityCoords + new Vector3(joystickDirectionRotated.x * speed, 0, joystickDirectionRotated.z * speed);
        simulatedGps = ConvertUnityCoordsToGps(newPosInUnityCoords);
        
    }

    public GpsVector getGps()
    {
        return gameGps;
    }
    
    public void setSimulatedGpsToBarrandov()
    {
        GpsVector newGps = new GpsVector((float)50.026896, (float)14.393643);
        setSimulatedGps(newGps);
    }
    
    public void setSimulatedGps(GpsVector newGps)
    {
        simulatedGps = newGps;
        simulatedGpsNotYetSet = false;
    }

    public void setSimulatedGpsToKarlovoNamesti()
    {
        GpsVector newGps = new GpsVector((float)50.076104, (float)14.418764);
        setSimulatedGps(newGps);
    }
    
    public void setSimulatedGpsToPalata()
    {
        GpsVector newGps = new GpsVector((float)50.076169, (float)14.388643);
        setSimulatedGps(newGps);
    }
    
    public void setSimulatedGpsToRealGps()
    {
        if (!realGpsNotYetSet)
        {
            setSimulatedGps(realGps);
        }
    }

    public void setWorldCenterGps(GpsVector gps)
    {
        worldOriginGps = gps;
        worldCenterNotYetSet = false;
        worldOriginMeters = ConvertGpsToMetersOnlyUsingReferenceOrigin(worldOriginGps);
        Debug.Log("World center set to: " + worldOriginGps.latitude + ", " + worldOriginGps.longitude);
        EventManager.InvokeEventWorldCenterSet();
    }
    
    /// <summary>
    /// Take gps coordinates and convert them into Unity coordinates in meters. For that use reference origin and world origin.
    /// Because of converting with reference origin, we are acting as if the Earth was a cylinder with perimeter the same as where the reference origin is.
    /// So for locations near the reference origin (latitude-wise) the conversion is quite accurate, but for locations further away from the reference origin, the conversion is less accurate.
    /// World origin is used because of limited float type precision. Unity uses float type for Transforms of GameObjects.
    /// Without world origin various graphical glitches would appear.
    /// </summary>
    /// <param name="pointGps"></param>
    /// <returns></returns>
    public Vector3 ConvertGpsToUnityCoords(GpsVector pointGps)
    {
        // calculate distance in meters from reference origin
        Double3 pointMeters = ConvertGpsToMetersOnlyUsingReferenceOrigin(pointGps);
        
        // shift it so we get meters from world origin
        Double3 pointMetersInNewFrame = pointMeters - worldOriginMeters;
        
        return new Vector3((float)pointMetersInNewFrame.x, 0, (float)pointMetersInNewFrame.z);
    }
    
    Double3 ConvertGpsToMetersOnlyUsingReferenceOrigin (GpsVector gps)
    {
        double refLat = referenceOriginGps.latitude;
        double refLon = referenceOriginGps.longitude;
        double lat = gps.latitude;
        double lon = gps.longitude;
        
        double diffLat = lat - refLat;
        double diffLon = lon - refLon;
        
        double diffLatInMeters = diffLat / 360 * EARTH_CIRCUMFERENCE;
        double smallerCircumferenceAtLat = 2 * Math.PI * EARTH_RADIUS * Math.Cos(deg2rad(refLat));
        double diffLonInMeters = diffLon / 360 * smallerCircumferenceAtLat;
        
        return new Double3(diffLonInMeters, 0, diffLatInMeters); 
    }

    public GpsVector ConvertUnityCoordsToGps(Vector3 point)
    {
        double refLat = referenceOriginGps.latitude;
        double refLon = referenceOriginGps.longitude;

        double diffLonInMeters = point.x + worldOriginMeters.x;
        double diffLatInMeters = point.z + worldOriginMeters.z;
        
        double diffLat = diffLatInMeters / EARTH_CIRCUMFERENCE * 360;
        double smallerCircumferenceAtLat = 2 * Math.PI * EARTH_RADIUS * Math.Cos(deg2rad(refLat));
        double diffLon = diffLonInMeters / smallerCircumferenceAtLat * 360;
        
        double lat = diffLat + refLat;
        double lon = diffLon + refLon;

        return new GpsVector(lat, lon);
    }
    
}
