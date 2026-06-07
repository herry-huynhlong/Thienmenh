using UnityEngine;

public static class ItemEffectSpawner
{
    public static void PlayBuyEffect(StatItemData itemData, Transform buyer)
    {
        if (itemData == null)
            return;

        if (buyer == null)
            return;

        if (itemData.playBuyEffect == false)
            return;

        if (itemData.buyEffectPrefab == null)
            return;

        GameObject effect = Object.Instantiate(itemData.buyEffectPrefab, buyer);

        effect.transform.localPosition = itemData.buyEffectOffset;
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one * itemData.buyEffectScale;
    }
}