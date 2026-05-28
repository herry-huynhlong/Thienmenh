using UnityEngine;

public enum ItemLifecycleEventType
{
    Created,
    Picked,
    Sold,
    Used,
    Broken,
    Refined,
    Stolen,
    Taught
}

public static class ItemLifecycleSystem
{
    public static void Notify(
        ItemLifecycleEventType eventType,
        StatItemData item,
        GameObject actor,
        GameObject other = null)
    {
        if (item == null)
        {
            return;
        }

        if (eventType == ItemLifecycleEventType.Broken ||
            eventType == ItemLifecycleEventType.Refined ||
            item.grade == ItemGrade.Tien)
        {
            Debug.Log(
                BuildMessage(eventType, item, actor, other));
        }
    }

    static string BuildMessage(
        ItemLifecycleEventType eventType,
        StatItemData item,
        GameObject actor,
        GameObject other)
    {
        string actorName =
            actor != null ? actor.name : "Vo danh";

        string otherName =
            other != null ? other.name : "";

        switch (eventType)
        {
            case ItemLifecycleEventType.Broken:
                return item.itemName + " cua " + actorName + " da hong.";

            case ItemLifecycleEventType.Refined:
                return actorName + " luyen hoa " + item.itemName +
                    " thanh dan duoc.";

            case ItemLifecycleEventType.Stolen:
                return actorName + " doat " + item.itemName +
                    " tu " + otherName + ".";

            case ItemLifecycleEventType.Taught:
                return actorName + " truyen thu " + item.itemName +
                    " cho " + otherName + ".";

            default:
                return actorName + " " + eventType + " " + item.itemName;
        }
    }
}
