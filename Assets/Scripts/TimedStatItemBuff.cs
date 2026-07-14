using UnityEngine;

public class TimedStatItemBuff : MonoBehaviour
{
    StatItemData item;
    float endTimeScaledSeconds;
    bool active;

    public void StartBuff(StatItemData newItem)
    {
        if (newItem == null)
        {
            Destroy(this);
            return;
        }

        item = newItem;
        endTimeScaledSeconds =
            GameTime.ScaledNowSeconds + item.durationScaledSeconds;
        active = item.ApplyTo(gameObject, 1);

        if (!active)
        {
            Destroy(this);
        }
    }

    void Update()
    {
        if (!active)
        {
            return;
        }

        if (GameTime.ScaledNowSeconds < endTimeScaledSeconds)
        {
            return;
        }

        item.RemoveFrom(gameObject);
        active = false;
        Destroy(this);
    }
}
