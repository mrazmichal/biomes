using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Display message asking the user to move closer to the object, when he tries clicking it from too far away
/// And slowly fade the message away.
/// </summary>
public class MoveCloserMessageScript : MonoBehaviour
{

    public GameObject messageText;
    TextMeshProUGUI textMesh1;
    Coroutine coroutine;
    
    void Start()
    {
        textMesh1 = messageText.GetComponent<TextMeshProUGUI>();
    }
    
    void OnEnable()
    {
        EventManager.OnPickingFromTooFar += PickingFromTooFar;
    }

    void OnDisable()
    {
        EventManager.OnPickingFromTooFar -= PickingFromTooFar;
    }
    
    void PickingFromTooFar()
    {
        messageText.SetActive(true);
        // start coroutine that fades the message away, then set inactive
        // also when starting new routine, stop the previous one
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
        coroutine = StartCoroutine(FadeMessage());
    }
    
    /// <summary>
    /// fade the message in every frame
    /// </summary>
    /// <returns></returns>
    IEnumerator FadeMessage()
    {
        const float secondsToStartFade = 2.0f;
        const float secondsToFinishFade = 4.0f;
        float secondsPassed = 0.0f;
        
        Color c = textMesh1.color;

        while (true)
        {
            secondsPassed += Time.deltaTime;
            
            if (secondsPassed < secondsToStartFade)
            {
                c.a = 1;
                textMesh1.color = c;
            } else if (secondsPassed < secondsToFinishFade)
            {
                float alpha = (secondsPassed - secondsToStartFade) / (secondsToFinishFade - secondsToStartFade);
                c.a = 1 - alpha;
                textMesh1.color = c;
            }
            else
            {
                break;
            }
            
            yield return null;
        }
        
        c.a = 1;
        textMesh1.color = c;
        messageText.SetActive(false);
    }
    
}
