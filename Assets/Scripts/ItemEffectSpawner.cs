using UnityEngine;

public static class ItemEffectSpawner
{
    public static void PlayUseEffect(StatItemData itemData, Transform target)
    {
        if (itemData == null)
            return;

        if (target == null)
            return;

        if (itemData.playBuyEffect == false)
            return;

        if (itemData.buyEffectPrefab == null)
            return;

        GameObject effect = Object.Instantiate(itemData.buyEffectPrefab, target);

        effect.transform.localPosition = itemData.buyEffectOffset;
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one * itemData.buyEffectScale;
    }

    public static void PlayBuyEffect(StatItemData itemData, Transform buyer)
    {
        PlayUseEffect(itemData, buyer);
    }
}
