/// <summary>
/// A struct that holds 3 double values.
/// </summary>
/// <author>Michal Mráz</author>
public struct Double3
{
    public double x;
    public double y;
    public double z;
        
    public Double3(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
        
    public static Double3 operator -(Double3 a, Double3 b)
    {
        return new Double3(a.x - b.x, a.y - b.y, a.z - b.z);
    }
        
    public static Double3 operator +(Double3 a, Double3 b)
    {
        return new Double3(a.x + b.x, a.y + b.y, a.z + b.z);
    }
}
