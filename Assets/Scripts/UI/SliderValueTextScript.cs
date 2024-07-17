using TMPro;
using UnityEngine;

/// <summary>
/// Display slider value
/// </summary>
/// <author>Michal Mráz</author>
public class SliderValueTextScript : MonoBehaviour
{
    public TextMeshProUGUI text;

    public void SetText(float value)
    {
        text.text = value.ToString("0.00");
    }
}
