using System;
using UnityEngine;

/// <summary>
/// Holds trees to generate in chunk
/// </summary>
/// <author>Michal Mráz</author>
public class VegetationPicker : MonoBehaviour
{
    public static VegetationPicker instance;
    public GameObject[] trees;

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
