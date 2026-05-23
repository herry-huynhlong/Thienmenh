using UnityEngine;

public class TimedStatItemBuff : MonoBehaviour
{
    StatItemData item;
    float endTime;
    bool active;

    public void StartBuff(StatItemData newItem)
    {
        if (newItem == null)
        {
            Destroy(this);
            return;
        }

        item = newItem;
        endTime = Time.time + item.duration;
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

        if (Time.time < endTime)
        {
            return;
        }

        item.RemoveFrom(gameObject);
        active = false;
        Destroy(this);
    }
}
