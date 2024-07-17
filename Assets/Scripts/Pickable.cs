using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes item pickable
/// </summary>
/// <author>Michal Mráz</author>
public class Pickable : MonoBehaviour
{
    public string itemName;
    float tooFarDistance = 100f;
    
    void OnMouseOver () {
        if (Input.GetMouseButtonDown(0)) {
            Debug.Log("Clicked on " + itemName);

            if (objectIsTooFar())
            {
                EventManager.InvokeEventPickingFromTooFar();
            }
            else
            {
                EventManager.InvokeEventItemPicked(itemName);
                Destroy(gameObject);
            }
            
        }
    }
    
    bool objectIsTooFar()
    {
        Vector2 objectPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 playerPos = new Vector2(Player.Instance.transform.position.x, Player.Instance.transform.position.z);
        float distance = Vector2.Distance(objectPos, playerPos);
        return distance > tooFarDistance;
    }

}
