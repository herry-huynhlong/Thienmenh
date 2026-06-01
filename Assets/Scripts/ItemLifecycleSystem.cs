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
            actor != null ? actor.name : "Vô danh";

        string otherName =
            other != null ? other.name : "";

        switch (eventType)
        {
            case ItemLifecycleEventType.Broken:
                return item.itemName + " của " + actorName + " đã hỏng.";

            case ItemLifecycleEventType.Refined:
                return actorName + " luyện hóa " + item.itemName +
                    " thành đan dược.";

            case ItemLifecycleEventType.Stolen:
                return actorName + " đoạt " + item.itemName +
                    " từ " + otherName + ".";

            case ItemLifecycleEventType.Taught:
                return actorName + " truyền thụ " + item.itemName +
                    " cho " + otherName + ".";

            default:
                return actorName + " " + eventType + " " + item.itemName;
        }
    }
}
