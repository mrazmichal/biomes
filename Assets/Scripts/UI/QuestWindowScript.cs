using UnityEngine;

/// <summary>
/// Control and change the quest window based on current quest state. 
/// </summary>
/// <author>Michal Mráz</author>
public class QuestWindowScript : MonoBehaviour
{
    public GameObject questWindow;
    public GameObject questTextBeforeAccept;
    public GameObject questTextAfterAccept;
    public GameObject questTextAfterComplete;
    public GameObject closeButton;
    public GameObject acceptButton;
    public GameObject completeButton;
    
    void Awake()
    {
        EventManager.OnQuestUpdated += QuestUpdated;
    }
    
    void QuestUpdated()
    {
        if (QuestModel.Instance.questCompleted)
        {
            questTextBeforeAccept.SetActive(false);
            questTextAfterAccept.SetActive(false);
            questTextAfterComplete.SetActive(true);
            closeButton.SetActive(true);
            acceptButton.SetActive(false);
            completeButton.SetActive(false);
        } else if (QuestModel.Instance.questCompletionConditionsFulfilled)
        {
            questTextBeforeAccept.SetActive(false);
            questTextAfterAccept.SetActive(true);
            questTextAfterComplete.SetActive(false);
            closeButton.SetActive(true);
            acceptButton.SetActive(false);
            completeButton.SetActive(true);
        } else if (QuestModel.Instance.questAccepted)
        {
            questTextBeforeAccept.SetActive(false);
            questTextAfterAccept.SetActive(true);
            questTextAfterComplete.SetActive(false);
            closeButton.SetActive(true);
            acceptButton.SetActive(false);
            completeButton.SetActive(false);
        } else
        {
            questTextBeforeAccept.SetActive(true);
            questTextAfterAccept.SetActive(false);
            questTextAfterComplete.SetActive(false);
            closeButton.SetActive(true);
            acceptButton.SetActive(true);
            completeButton.SetActive(false);
        }
    }

    public void clickClose()
    {
        questWindow.SetActive(false);
    }

    public void clickAccept()
    {
        EventManager.InvokeEventAcceptButtonClicked();
        questWindow.SetActive(false);
    }
    
    public void clickComplete()
    {
        EventManager.InvokeEventCompleteButtonClicked();
    }
    
}
