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
            actor != null
                ? actor.name
                : UiText.Get(
                    "itemLifecycle",
                    "anonymous",
                    "Vo danh");

        string otherName =
            other != null ? other.name : "";

        switch (eventType)
        {
            case ItemLifecycleEventType.Broken:
                return UiText.Format(
                    "itemLifecycle",
                    "broken",
                    ItemText.Name(item),
                    actorName);

            case ItemLifecycleEventType.Refined:
                return UiText.Format(
                    "itemLifecycle",
                    "refined",
                    actorName,
                    ItemText.Name(item));

            case ItemLifecycleEventType.Forged:
                return UiText.Format(
                    "itemLifecycle",
                    "forged",
                    actorName,
                    ItemText.Name(item));

            case ItemLifecycleEventType.Stolen:
                return UiText.Format(
                    "itemLifecycle",
                    "stolen",
                    actorName,
                    ItemText.Name(item),
                    otherName);

            case ItemLifecycleEventType.Taught:
                return UiText.Format(
                    "itemLifecycle",
                    "taught",
                    actorName,
                    ItemText.Name(item),
                    otherName);

            default:
                return UiText.Format(
                    "itemLifecycle",
                    "default",
                    actorName,
                    eventType,
                    ItemText.Name(item));
        }
    }
}
