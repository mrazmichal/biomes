using UnityEngine;

/// <summary>
/// Gps vector representation
/// </summary>
/// <author>Michal Mráz</author>
public struct GpsVector
{
    public double latitude;
    public double longitude;
    
    public GpsVector(float latitude, float longitude)
    {
        this.latitude = latitude;
        this.longitude = longitude;
    }

    public GpsVector(double latitude, double longitude)
    {
        this.latitude = latitude;
        this.longitude = longitude;
    }
    
    public static bool operator ==(GpsVector a, GpsVector b)
    {
        return a.latitude == b.latitude && a.longitude == b.longitude;
    }
    
    public static GpsVector operator +(GpsVector a, GpsVector b)
    {
        return new GpsVector(a.latitude + b.latitude, a.longitude + b.longitude);
    }
    
    public static GpsVector operator -(GpsVector a, GpsVector b)
    {
        return new GpsVector(a.latitude - b.latitude, a.longitude - b.longitude);
    }
    
    public static bool operator !=(GpsVector a, GpsVector b)
    {
        return a.latitude != b.latitude || a.longitude != b.longitude;
    }
    
    public GpsVector(GpsVector vector)
    {
        this.latitude = vector.latitude;
        this.longitude = vector.longitude;
    }
    
    public static GpsVector Lerp(GpsVector a, GpsVector b, float t)
    {
        t = Mathf.Clamp01(t);
        return new GpsVector(a.latitude + (b.latitude - a.latitude) * t, a.longitude + (b.longitude - a.longitude) * t);
    }
    
}