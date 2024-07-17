using UnityEngine;

/// <summary>
/// Quest items to generate in chunk
/// </summary>
/// <author>Michal Mráz</author>
public class ItemsPicker : MonoBehaviour
{
    public static ItemsPicker instance;
    public GameObject[] items;
    
    private void Awake()
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
