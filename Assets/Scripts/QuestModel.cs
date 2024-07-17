using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Model of quest. This object is informed when an item is picked, keeps track of objectives, and informs about changes in quest state by invoking events. UI managers read data from this model and display them. 
/// </summary>
/// <author>Michal Mráz</author>
public class QuestModel : MonoBehaviour
{
    public readonly string questName = "Gathering Gear";
    public bool swordPickedUp { get; private set; }
    public bool shieldPickedUp { get; private set; }
    public bool questAccepted { get; private set; }
    public bool questCompletionConditionsFulfilled { get; private set; }
    public bool questCompleted { get; private set; }
    
    public class Objective
    {
        public string name;
        public int amount;
        public int currentAmount;
    }

    public List<Objective> objectives;
    
    public static QuestModel Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        } else
        {
            Destroy(gameObject);
        }

        initializeObjectives();
    }
    
    private void initializeObjectives()
    {
        objectives = new List<Objective>
        {
            new Objective { name = "sword", amount = 1, currentAmount = 0 },
            new Objective { name = "shield", amount = 1, currentAmount = 0 },
        };
    }

    void Start()
    {
        EventManager.OnItemPicked += ItemPicked;
        EventManager.OnAcceptButtonClicked += acceptQuest;
        EventManager.OnCompleteButtonClicked += tryToCompleteQuest;
        
        EventManager.InvokeEventQuestUpdated();
    }

    private void ItemPicked(string itemName)
    {
        if (itemName == "sword")
        {
            swordPickedUp = true;    
            objectives.Find(x => x.name == itemName).currentAmount++;
            EventManager.InvokeEventQuestUpdated();
        } else if (itemName == "shield")
        {
            shieldPickedUp = true;
            objectives.Find(x => x.name == itemName).currentAmount++;
            EventManager.InvokeEventQuestUpdated();
        }
        
        if (swordPickedUp && shieldPickedUp)
        {
            questCompletionConditionsFulfilled = true;
            EventManager.InvokeEventQuestUpdated();
            
            // tryToCompleteQuest();
        }
    }
    
    public void acceptQuest()
    {
        questAccepted = true;
        EventManager.InvokeEventQuestUpdated();
    }

    public void tryToCompleteQuest()
    {
        if (questCompletionConditionsFulfilled)
        {
            questCompleted = true;
            EventManager.InvokeEventQuestUpdated();
        }
    }
    
}

