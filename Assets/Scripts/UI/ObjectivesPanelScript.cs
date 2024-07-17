using TMPro;
using UnityEngine;

/// <summary>
/// Write current quest objectives onto the GUI panel.
/// When quest state changes, rewrite the displayed objectives. 
/// </summary>
/// <author>Michal Mráz</author>
public class ObjectivesPanelScript : MonoBehaviour
{
    public GameObject objectivesPanel;
    public GameObject objectivesContent;
    public GameObject objectivesTitle;
    
    void Start()
    {
        EventManager.OnQuestUpdated += QuestUpdated;
    }
    
    void printObjectives()
    {
        if (QuestModel.Instance.objectives == null)
        {
            return;
        }
        
        TextMeshProUGUI tmpText = objectivesContent.GetComponent<TextMeshProUGUI>();
        
        tmpText.text = "";
        
        if (QuestModel.Instance.questAccepted)
        {
            tmpText.text += QuestModel.Instance.questName + "\n";
        
            foreach (var objective in QuestModel.Instance.objectives)
            {
                tmpText.text += "- " + capitalizeFirstLetter(objective.name) + ": " + objective.currentAmount + "/" + objective.amount + "\n";
            }
            
            if (QuestModel.Instance.questCompletionConditionsFulfilled)
            {
                tmpText.text += "Objectives Achieved!\n";
            }
        }
        
    }
    
    public string capitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Convert the first character to uppercase and concatenate it with the rest of the string.
        return char.ToUpper(input[0]) + input.Substring(1);
    }

    void QuestUpdated()
    {
        if (QuestModel.Instance.questCompleted)
        {
            objectivesPanel.SetActive(false);
        } else if (QuestModel.Instance.questAccepted)
        {
            objectivesPanel.SetActive(true);
            printObjectives();
        }
    }
    
}
