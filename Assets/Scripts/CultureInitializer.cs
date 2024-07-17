using System.Globalization;
using UnityEngine;

/// <summary>
/// Initialize culture settings - important for printing numbers for example
/// </summary>
/// <author>Michal Mráz</author>
public class CultureInitializer : MonoBehaviour
{
    void Awake()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }
    
}
