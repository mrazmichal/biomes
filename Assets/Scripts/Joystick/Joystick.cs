using UnityEngine;

/// <summary>
/// Move joystick handle according to user input, update the value of the joystick
/// </summary>
/// <author>Michal Mráz</author>
public class Joystick : MonoBehaviour
{
    public GameObject background;
    public GameObject handle;

    Vector2 middle;
    float width;
    float radius;
    bool isHeld = false;
    RectTransform backgroundRectTransform;
    RectTransform handleRectTransform;
    
    public Vector2 value { get; private set; }
    
    // Start is called before the first frame update
    void Start()
    {
        backgroundRectTransform = background.GetComponent<RectTransform>();
        handleRectTransform = handle.GetComponent<RectTransform>();
        middle = backgroundRectTransform.pivot;
        width = backgroundRectTransform.rect.width;
        radius = width / 2;
        radius *= 0.8f;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 touchPos = Input.mousePosition;
            touchPos = convertToRectCoordinates(touchPos);
            float distance = Vector2.Distance(touchPos, middle);
            if (distance < radius)
            {
                isHeld = true;
            }
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            isHeld = false;
        }

        if (!Input.GetMouseButton(0))
        {
            isHeld = false;
        }
                
        if (isHeld)
        {
            Vector2 touchPos = Input.mousePosition;
            touchPos = convertToRectCoordinates(touchPos);
            Vector2 direction = touchPos - middle;
            float distance = Vector2.Distance(touchPos, middle);
            if (distance > radius)
            {
                direction = direction.normalized * radius;
            }
            handleRectTransform.anchoredPosition = direction;
            
            value = direction / radius;
            
        }
        
        if (!isHeld)
        {
            handleRectTransform.anchoredPosition = Vector2.zero;
            value = Vector2.zero;
        }
        
    }
    
    Vector2 convertToRectCoordinates(Vector2 touchPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(backgroundRectTransform, touchPos, null, out touchPos);
        return touchPos;
    }
    
}
