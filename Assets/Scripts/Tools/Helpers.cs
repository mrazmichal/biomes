using System;

/// <summary>
/// Some helper methods, degree to radians conversions for example
/// </summary>
/// <author>Michal Mráz</author>
public static class Helpers
{
    public static double deg2rad(double value)
    {
        return value * Math.PI / 180;
    }
    
    public static double rad2deg(double value)
    {
        return value * 180 / Math.PI;
    }
}
