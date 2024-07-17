
/// <summary>
/// Various events that can be invoked
/// </summary>
/// <author>Michal Mráz</author>
public static class EventManager 
{
    public delegate void ItemPickedAction(string itemPicked);    
    public static event ItemPickedAction OnItemPicked;
    
    public delegate void QuestAcceptedAction();
    public static event QuestAcceptedAction OnQuestAccepted;
    
    public delegate void QuestUpdatedAction();
    public static event QuestUpdatedAction OnQuestUpdated;

    public delegate void PickingFromTooFarAction();
    public static event PickingFromTooFarAction OnPickingFromTooFar;
    
    public delegate void WorldCenterSetAction();
    public static event WorldCenterSetAction OnWorldCenterSet;
    
    public delegate void AcceptButtonClickedAction();
    public static event AcceptButtonClickedAction OnAcceptButtonClicked;
    
    public delegate void CompleteButtonClickedAction();
    public static event CompleteButtonClickedAction OnCompleteButtonClicked;

    public delegate void GpsSimulationEnabled();
    public static event GpsSimulationEnabled OnGpsSimulationEnabled;
    
    public static void InvokeEventGpsSimulationEnabled() {
        OnGpsSimulationEnabled?.Invoke();
    }
    
    public static void InvokeEventItemPicked(string itemName) {
        OnItemPicked?.Invoke(itemName);
    }
    
    public static void InvokeEventQuestUpdated() {
        OnQuestUpdated?.Invoke();
    }
    
    public static void InvokeEventPickingFromTooFar() {
        OnPickingFromTooFar?.Invoke();
    }

    public static void InvokeEventWorldCenterSet()
    {
        OnWorldCenterSet?.Invoke();
    }

    public static void InvokeEventAcceptButtonClicked()
    {
        OnAcceptButtonClicked?.Invoke();
    }

    public static void InvokeEventCompleteButtonClicked()
    {
        OnCompleteButtonClicked?.Invoke();
    }

}
