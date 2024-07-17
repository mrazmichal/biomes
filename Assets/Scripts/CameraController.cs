using UnityEngine;

/// <summary>
/// Move camera using one and two finger gestures, keep camera above terrain
/// </summary>
/// <author>Michal Mráz</author>
public class CameraController : MonoBehaviour
{
    static bool atLeastTwoFingersWereActiveInPreviousFrame = false;
    static float oldDistanceBetweenFingers = 0;

    static float cameraDistance = 1f;
    static GameObject target;
    static GameObject camera;
    static float upDown = 0.5f;
    static float leftRight = 0.5f;

    static float MIN_CAMERA_DISTANCE = 70f;
    // static float MAX_CAMERA_DISTANCE = 500f;
    static float MAX_CAMERA_DISTANCE = 1500f;
    
    public Joystick joystick;
    public GameObject settingsWindow;
    public GameObject questWindow;

    Vector2 oldTouchPos;

    // Start is called before the first frame update
    void Start()
    {
        // target = GameObject.Find("Player");
        target = GameObject.Find("watchingTarget");
        camera = GameObject.Find("Main Camera");
    }

    // Update is called once per frame
    void Update()
    {
        bool joystickIsHeld = joystick.value != Vector2.zero;
        bool noWindowIsActive = !settingsWindow.activeInHierarchy && !questWindow.activeInHierarchy;
        bool shouldCameraControlsBeActive = !joystickIsHeld && noWindowIsActive;

        // if no window is displayed and joystick is not active, camera can be controlled using touch gestures
        if (shouldCameraControlsBeActive)
        {
            // Move camera closer or further using two fingers
            if (Input.touchCount >= 2)
            {
                Vector2 touch0 = Input.GetTouch(0).position;
                Vector2 touch1 = Input.GetTouch(1).position;
                float distanceBetweenFingers = Vector2.Distance(touch0, touch1);

                if (atLeastTwoFingersWereActiveInPreviousFrame)
                {
                    float distanceComparison = distanceBetweenFingers - oldDistanceBetweenFingers;
                    cameraDistance -= distanceComparison;
                }
            
                atLeastTwoFingersWereActiveInPreviousFrame = true;
                oldDistanceBetweenFingers = distanceBetweenFingers;
            } else
            {
                atLeastTwoFingersWereActiveInPreviousFrame = false;
            }
            
            // zoom using mouse scroll
            if (Input.mouseScrollDelta.y != 0)
            {
                cameraDistance -= Input.mouseScrollDelta.y * 40f;
            }

            // Rotate camera around target using one finger
            if (Input.touchCount < 2)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Vector2 touchPos = Input.mousePosition;
                    oldTouchPos = touchPos;
                }
                
                if (Input.GetMouseButton(0))
                {
                    Vector2 touchPos = Input.mousePosition;
                    
                    if (touchPos != oldTouchPos)
                    {
                        Vector2 delta = touchPos - oldTouchPos;
                        upDown -= delta.y/2f;
                        upDown = Mathf.Clamp(upDown, -80f, 80f);
                        leftRight += delta.x/2f;
                        leftRight = (leftRight + 360f) % 360f;
                    }
                    oldTouchPos = touchPos;
                }
                
            }
            
        }
        
        
        cameraDistance = Mathf.Clamp(cameraDistance, MIN_CAMERA_DISTANCE, MAX_CAMERA_DISTANCE);
        Vector3 cameraOffset = new Vector3(0, 0, -cameraDistance);

        // Place camera into new position based target position, camera offset from target and the two angles 
        Vector3 newCameraPosition = Quaternion.Euler(upDown, leftRight, 0) * cameraOffset + target.transform.position;

        // check if camera is less then 5 meters above ground - if yes, adjust the upDown angle to 5 meters above ground
        float cameraPushAboveGroundYOffset = 5f;
        
        Vector3 point = newCameraPosition;
        float newCameraDistance = Vector3.Distance(target.transform.position, point);
        Vector3 snappedPoint = point;
        Vector3 hitPoint = Vector3.zero;
        if (Chunk.snapPointToTerrain(point, ref hitPoint))
        {
            snappedPoint = hitPoint;
            float cameraDistanceTiltingYOffset = newCameraDistance / 10; // minimum camera height above terrain. Dependent on camera distance from target
            float cameraMinY = snappedPoint.y + cameraPushAboveGroundYOffset + cameraDistanceTiltingYOffset;
            
            if (cameraMinY > point.y)
            {
                Vector3 desiredPoint = new Vector3(point.x, cameraMinY, point.z);
                float angle = 90 - Vector3.Angle(Vector3.up, desiredPoint - target.transform.position);
                upDown = angle;
            }
        }

        // Assign the new camera position again, now with the possibly adjusted upDown angle
        newCameraPosition = Quaternion.Euler(upDown, leftRight, 0) * cameraOffset + target.transform.position;
        
        camera.transform.position = newCameraPosition;
        camera.transform.LookAt(target.transform);

    }
}
