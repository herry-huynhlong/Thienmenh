using UnityEngine;

public class StatItemApplier : MonoBehaviour
{
    public StatItemData item;

    public void ApplyTo(GameObject target)
    {
        if (item == null ||
            target == null)
        {
            return;
        }

        item.ApplyTo(target);
    }

    public void ApplyToSelf()
    {
        ApplyTo(gameObject);
    }
}
