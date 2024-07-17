using System.Collections;
using UnityEngine;

/// <author>Michal Mráz</author>
public class ScreenshotHandler : MonoBehaviour
{
    public KeyCode screenshotKey = KeyCode.P; // Set the key to trigger the screenshot
    public int resolutionMultiplier = 1;      // Set the resolution multiplier for higher resolution screenshots

    void Update()
    {
        if (Input.GetKeyDown(screenshotKey))
        {
            StartCoroutine(TakeScreenshotWithoutUI());
        }
    }

    private IEnumerator TakeScreenshotWithoutUI()
    {
        // Find all Canvas objects in the scene
        Canvas[] allCanvas = FindObjectsOfType<Canvas>();

        // Disable all Canvas objects
        foreach (Canvas canvas in allCanvas)
        {
            canvas.enabled = false;
        }

        // Wait until the end of the frame to ensure the UI is disabled
        yield return new WaitForEndOfFrame();

        // Capture the screenshot
        string screenshotFilename = "Screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        ScreenCapture.CaptureScreenshot(screenshotFilename, resolutionMultiplier);

        // Wait for one frame to ensure the screenshot is captured
        yield return null;

        // Re-enable all Canvas objects
        foreach (Canvas canvas in allCanvas)
        {
            canvas.enabled = true;
        }

        Debug.Log("Screenshot taken: " + screenshotFilename);
    }
}
