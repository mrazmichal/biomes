using UnityEngine;

/// <summary>
/// Makes item levitate, floating up and down
/// </summary>
/// <author>Michal Mráz</author>
public class Levitate : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }
    
    // Update is called once per frame
    void Update()
    {
        // levitate the object lightly up and down
        transform.position = new Vector3(transform.position.x, transform.position.y + Mathf.Sin(Time.time) * 0.002f, transform.position.z);
        
        transform.rotation = Quaternion.Euler(0, Time.time *25, 0);
    }
    
    
}
