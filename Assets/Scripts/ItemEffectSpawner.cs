using UnityEngine;

public static class ItemEffectSpawner
{
    public static void PlayUseEffect(StatItemData itemData, Transform target)
    {
        PlayEffect(
            itemData,
            target,
            itemData != null && itemData.playBuyEffect,
            itemData != null ? itemData.buyEffectPrefab : null,
            itemData != null ? itemData.buyEffectOffset : Vector3.zero,
            itemData != null ? itemData.buyEffectScale : 1f);
    }

    public static void PlayBuyEffect(StatItemData itemData, Transform buyer)
    {
        PlayUseEffect(itemData, buyer);
    }

    public static void PlayPickupEffect(StatItemData itemData, Transform target)
    {
        if (itemData == null)
        {
            return;
        }

        GameObject prefab =
            itemData.pickupEffectPrefab != null
            ? itemData.pickupEffectPrefab
            : itemData.buyEffectPrefab;

        bool shouldPlay =
            itemData.playPickupEffect ||
            (itemData.playPickupEffect == false &&
             itemData.playBuyEffect &&
             itemData.pickupEffectPrefab == null);

        PlayEffect(
            itemData,
            target,
            shouldPlay,
            prefab,
            itemData.pickupEffectPrefab != null
                ? itemData.pickupEffectOffset
                : itemData.buyEffectOffset,
            itemData.pickupEffectPrefab != null
                ? itemData.pickupEffectScale
                : itemData.buyEffectScale);
    }

    static void PlayEffect(
        StatItemData itemData,
        Transform target,
        bool enabled,
        GameObject prefab,
        Vector3 localPosition,
        float localScale)
    {
        if (itemData == null ||
            target == null ||
            !enabled ||
            prefab == null)
        {
            return;
        }

        GameObject effect = Object.Instantiate(prefab, target);
        effect.transform.localPosition = localPosition;
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one * localScale;
    }
}
