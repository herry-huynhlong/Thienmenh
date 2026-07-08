using UnityEngine;

public class GrowingHerbField : MonoBehaviour
{
    [Header("Target")]
    public StatItemData herbItem;

    [Header("Stage Sprites")]
    public Sprite smallSprite;
    public Sprite midSprite;
    public Sprite largeSprite;

    [Header("Growth")]
    [Min(0.5f)] public float matureAfterGameHours = 48f;
    [Range(0.05f, 0.95f)] public float midStageThreshold = 0.5f;

    [Header("Initial Spawn Weights")]
    [Min(0)] public int initialSmallWeight = 30;
    [Min(0)] public int initialMidWeight = 40;
    [Min(0)] public int initialLargeWeight = 30;

    [Header("Variation")]
    [Min(0.1f)] public float randomScaleMin = 0.92f;
    [Min(0.1f)] public float randomScaleMax = 1.08f;

    public bool AppliesTo(StatItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (herbItem == null)
        {
            return item.materialKind == MaterialKind.Herb ||
                ResourceNode.InferKindFromItem(item) == HarvestResourceKind.ThaoDuoc;
        }

        if (item == herbItem)
        {
            return true;
        }

        return !string.IsNullOrEmpty(item.ItemId) &&
            !string.IsNullOrEmpty(herbItem.ItemId) &&
            item.ItemId == herbItem.ItemId;
    }

    public bool ConfigureResourceNode(
        GameObject resourceObject,
        WorldStatItemPickup pickup,
        WorldResourceNode worldNode,
        float targetVisualSize,
        int sortingOrder,
        bool initializeGrowthStage)
    {
        if (resourceObject == null ||
            pickup == null ||
            !AppliesTo(pickup.item))
        {
            return false;
        }

        GrowingHerbNode herbNode =
            resourceObject.GetComponent<GrowingHerbNode>();

        if (herbNode == null)
        {
            herbNode = resourceObject.AddComponent<GrowingHerbNode>();
        }

        herbNode.Configure(
            pickup,
            worldNode,
            smallSprite,
            midSprite,
            largeSprite,
            matureAfterGameHours,
            midStageThreshold,
            initialSmallWeight,
            initialMidWeight,
            initialLargeWeight,
            randomScaleMin,
            randomScaleMax,
            targetVisualSize,
            sortingOrder);

        if (initializeGrowthStage)
        {
            herbNode.InitializeForInitialSpawn();
        }
        else
        {
            herbNode.RefreshVisualState();
        }

        return true;
    }

    void OnValidate()
    {
        matureAfterGameHours = Mathf.Max(0.5f, matureAfterGameHours);
        midStageThreshold = Mathf.Clamp(midStageThreshold, 0.05f, 0.95f);
        initialSmallWeight = Mathf.Max(0, initialSmallWeight);
        initialMidWeight = Mathf.Max(0, initialMidWeight);
        initialLargeWeight = Mathf.Max(0, initialLargeWeight);
        randomScaleMin = Mathf.Max(0.1f, randomScaleMin);
        randomScaleMax = Mathf.Max(randomScaleMin, randomScaleMax);
    }
}
