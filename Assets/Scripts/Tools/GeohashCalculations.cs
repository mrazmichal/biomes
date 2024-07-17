using NGeoHash;

/// <summary>
/// Calculations with geohash.
/// Uses the geohash plugin.
/// </summary>
/// <author>Michal Mráz</author>
public static class GeohashCalculations
{
    private static int ZOOM_LEVEL = 32;
    
    /// <summary>
    /// Get center of geohash area identified by geohash
    /// </summary>
    /// <param name="geohash">geohash area identifier</param>
    /// <returns>gps coordinates of center</returns>
    public static GpsVector getGeohashCenter(long geohash)
    {
        BoundingBox bb = getGeohashBounds(geohash);
        double minLat = bb.Minimum.Lat;
        double minLon = bb.Minimum.Lon;
        double maxLat = bb.Maximum.Lat;
        double maxLon = bb.Maximum.Lon;
        double centerLat = minLat + (maxLat - minLat) / 2;
        double centerLon = minLon + (maxLon - minLon) / 2;
        GpsVector res = new GpsVector(centerLat, centerLon);
        return res;
    }

    /// <summary>
    /// get geohash area identifier for given gps coordinates
    /// </summary>
    /// <param name="gps"></param>
    /// <returns></returns>
    public static long getGeohash(GpsVector gps)
    {
        long geohash = GeoHash.EncodeInt(gps.latitude, gps.longitude, ZOOM_LEVEL);
        return geohash;
    }

    /// <summary>
    /// Get bounds of geohash area identified by geohash
    /// </summary>
    /// <param name="geohash"></param>
    /// <returns></returns>
    public static BoundingBox getGeohashBounds(long geohash)
    {
        BoundingBox bbox = GeoHash.DecodeBboxInt(geohash, ZOOM_LEVEL);
        return bbox;
    }

    /// <summary>
    /// Get geohash identifiers of neighbouring geohash areas
    /// </summary>
    /// <param name="geohash">geohash area identifier</param> 
    /// <returns></returns>
    public static long[] getGeohashNeighbours(long geohash)
    {
        long[] neighbours = GeoHash.NeighborsInt(geohash, ZOOM_LEVEL);
        return neighbours;
    }

}
