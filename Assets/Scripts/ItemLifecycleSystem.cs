using UnityEngine;

public enum ItemLifecycleEventType
{
    Created,
    Picked,
    Sold,
    Used,
    Broken,
    Refined,
    Forged,
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
            eventType == ItemLifecycleEventType.Forged ||
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
                return ItemText.Name(item) + " cua " + actorName + " da hong.";

            case ItemLifecycleEventType.Refined:
                return actorName + " luyen hoa " + ItemText.Name(item) +
                    " thanh dan duoc.";

            case ItemLifecycleEventType.Forged:
                return actorName + " ren " + ItemText.Name(item) + ".";

            case ItemLifecycleEventType.Stolen:
                return actorName + " doat " + ItemText.Name(item) +
                    " tu " + otherName + ".";

            case ItemLifecycleEventType.Taught:
                return actorName + " truyen thu " + ItemText.Name(item) +
                    " cho " + otherName + ".";

            default:
                return actorName + " " + eventType + " " + ItemText.Name(item);
        }
    }
}
