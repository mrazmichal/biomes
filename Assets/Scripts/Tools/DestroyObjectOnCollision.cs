using UnityEngine;

/// <summary>
/// Destroys the object when it collides with another object.
/// </summary>
/// <author>Michal Mráz</author>
public class DestroyObjectOnCollision : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Destroy(gameObject);
    }
    
    void OnTriggerStay(Collider other)
    {
        Destroy(gameObject);
    }
}
