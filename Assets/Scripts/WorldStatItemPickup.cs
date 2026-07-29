using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class WorldStatItemPickup : MonoBehaviour
{
    public StatItemData item;
    public int amount = 1;
    public bool allowNpcPickup = true;
    public bool allowPlayerPickup = false;
    public bool allowNpcPassivePickup;
    public bool destroyWhenEmpty = true;
    public bool requireNpcHarvestAction;
    public bool treatAsDroppedWorldItem;
    [FormerlySerializedAs("harvestDuration")]
    public float harvestDurationScaledSeconds = 8f;
    public bool trackReceiverInHeavenNurture;
    public event Action OnDepleted;

    GameObject reservedBy;
    float reservationExpiresAtScaledSeconds;

    public bool RequiresNpcHarvestAction()
    {
        if (treatAsDroppedWorldItem)
        {
            return false;
        }

        return requireNpcHarvestAction ||
            (item != null &&
            (item.materialKind == MaterialKind.Herb ||
            ResourceNode.InferKindFromItem(item) == HarvestResourceKind.ThaoDuoc)) ||
            HasHarvestResourceMarker();
    }

    public void ConfigureAsDroppedWorldItem()
    {
        treatAsDroppedWorldItem = true;
        requireNpcHarvestAction = false;
        allowNpcPassivePickup = true;
        allowPlayerPickup = true;
    }

    public bool HasValidNpcPickupArea()
    {
        IReadOnlyList<NpcMapArea> areas = NpcMapArea.Areas;

        if (areas == null ||
            areas.Count == 0)
        {
            return true;
        }

        return NpcMapArea.FindArea(transform.position) != null;
    }

    public bool CanNpcActorCollect(GameObject actor)
    {
        if (!HasValidNpcPickupArea())
        {
            return false;
        }

        if (actor == null)
        {
            return true;
        }

        NpcMapArea pickupArea =
            NpcMapArea.FindArea(transform.position);

        if (pickupArea == null)
        {
            return true;
        }

        NpcMapArea actorArea =
            NpcMapArea.FindArea(actor.transform.position);

        if (actorArea != null)
        {
            return actorArea.zone == pickupArea.zone;
        }

        NpcMapZone? actorZone =
            NpcMapNavigator.ResolveActorZone(actor);

        return !actorZone.HasValue ||
            actorZone.Value == pickupArea.zone;
    }

    public bool CanNpcPassivelyCollect(GameObject actor)
    {
        if (!allowNpcPickup ||
            item == null ||
            actor == null)
        {
            return false;
        }

        if (!CanNpcActorCollect(actor))
        {
            return false;
        }

        if (ShouldBlockNpcPassivePickup())
        {
            return false;
        }

        if (IsDroppedItemStillLanding())
        {
            return false;
        }

        if (allowNpcPassivePickup)
        {
            return true;
        }

        return trackReceiverInHeavenNurture &&
            treatAsDroppedWorldItem;
    }

    bool IsDroppedItemStillLanding()
    {
        if (!treatAsDroppedWorldItem)
        {
            return false;
        }

        return GetComponent<SkyDropToPosition>() != null;
    }

    bool ShouldBlockNpcPassivePickup()
    {
        if (HasHarvestResourceMarker())
        {
            return true;
        }

        if (item == null)
        {
            return false;
        }

        return item.materialKind == MaterialKind.Herb ||
            ResourceNode.InferKindFromItem(item) == HarvestResourceKind.ThaoDuoc;
    }

    bool HasHarvestResourceMarker()
    {
        return HasComponentInPickupHierarchy<WorldResourceNode>() ||
            HasComponentInPickupHierarchy<ResourceNode>() ||
            HasComponentInPickupHierarchy<GrowingHerbNode>() ||
            GetComponentInParent<WorldResourceField>() != null ||
            GetComponentInParent<GrowingHerbField>() != null;
    }

    bool HasComponentInPickupHierarchy<T>() where T : Component
    {
        return GetComponent<T>() != null ||
            GetComponentInParent<T>() != null ||
            GetComponentInChildren<T>(true) != null;
    }

    public bool TryTake(int takeAmount)
    {
        if (item == null ||
            takeAmount <= 0 ||
            amount < takeAmount)
        {
            return false;
        }

        amount -= takeAmount;

        if (amount <= 0)
        {
            OnDepleted?.Invoke();

            if (destroyWhenEmpty)
            {
                Destroy(gameObject);
            }
        }

        return true;
    }

    public bool TryReserve(
        GameObject reserver,
        float durationScaledSeconds)
    {
        if (reserver == null ||
            item == null ||
            amount <= 0)
        {
            return false;
        }

        CleanupInvalidReservation();

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.TryGetExistingInstance();
        if (reservationSystem != null &&
            reservationSystem.IsReservedByOther(gameObject, reserver))
        {
            return false;
        }

        if (reservedBy != null &&
            reservedBy != reserver &&
            reservationExpiresAtScaledSeconds > GameTime.ScaledNowSeconds)
        {
            return false;
        }

        reservedBy = reserver;
        reservationExpiresAtScaledSeconds =
            GameTime.ScaledNowSeconds +
            Mathf.Max(0.25f, durationScaledSeconds);
        if (reservationSystem != null)
        {
            reservationSystem.TryReserve(
                gameObject,
                reserver,
                durationScaledSeconds,
                "Pickup");
        }

        return true;
    }

    public bool IsReservedByOther(GameObject requester)
    {
        CleanupInvalidReservation();

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.TryGetExistingInstance();
        if (reservationSystem != null &&
            reservationSystem.IsReservedByOther(gameObject, requester))
        {
            return true;
        }

        if (reservedBy == null ||
            reservedBy == requester ||
            reservationExpiresAtScaledSeconds <= GameTime.ScaledNowSeconds)
        {
            return false;
        }

        return true;
    }

    public void RefreshReservation(
        GameObject reserver,
        float durationScaledSeconds)
    {
        if (reserver == null)
        {
            return;
        }

        CleanupInvalidReservation();

        if (reservedBy != null &&
            reservedBy != reserver &&
            reservationExpiresAtScaledSeconds > GameTime.ScaledNowSeconds)
        {
            return;
        }

        reservedBy = reserver;
        reservationExpiresAtScaledSeconds =
            GameTime.ScaledNowSeconds +
            Mathf.Max(0.25f, durationScaledSeconds);

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.TryGetExistingInstance();
        if (reservationSystem != null)
        {
            reservationSystem.TryReserve(
                gameObject,
                reserver,
                durationScaledSeconds,
                "Pickup");
        }
    }

    public void ClearReservation(GameObject reserver)
    {
        if (reserver == null)
        {
            return;
        }

        CleanupInvalidReservation();

        if (reservedBy != null &&
            reservedBy != reserver)
        {
            return;
        }

        reservedBy = null;
        reservationExpiresAtScaledSeconds = 0f;

        TargetReservationSystem reservationSystem =
            TargetReservationSystem.TryGetExistingInstance();
        if (reservationSystem != null)
        {
            reservationSystem.Release(gameObject, reserver);
        }
    }

    void OnDisable()
    {
        if (reservedBy != null)
        {
            ClearReservation(reservedBy);
        }
    }

    void OnDestroy()
    {
        if (reservedBy != null)
        {
            ClearReservation(reservedBy);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryGiveToWorldActor(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryGiveToWorldActor(other);
    }

    public bool TryGiveToWorldActor(Collider2D other)
    {
        if (item == null ||
            other == null)
        {
            return false;
        }

        if (allowPlayerPickup &&
            TryGiveToPlayer(other))
        {
            return true;
        }

        if (!allowNpcPickup ||
            item == null ||
            other == null)
        {
            return false;
        }

        Transform target =
            GetWorldActorTarget(other);

        if (target == null)
        {
            return false;
        }

        if (!CanNpcPassivelyCollect(target.gameObject))
        {
            return false;
        }

        if (IsReservedByOther(target.gameObject))
        {
            return false;
        }

        NpcItemCollector collector =
            target.GetComponent<NpcItemCollector>();

        if (collector == null ||
            !collector.canPickupItems)
        {
            return false;
        }

        StatItemData pickedItem = item;

        if (!TryTake(1))
        {
            return false;
        }

        bool prioritizeImmediateUse =
            trackReceiverInHeavenNurture &&
            target.GetComponent<SmartNpcAI>() != null;

        collector.ReceiveItem(
            pickedItem,
            ItemLifecycleEventType.Picked,
            prioritizeImmediateUse);
        TrackReceiver(target.gameObject);
        ClearReservation(target.gameObject);
        return true;
    }

    bool TryGiveToPlayer(Collider2D other)
    {
        Transform target =
            GetPlayerTarget(other);

        if (target == null)
        {
            return false;
        }

        StatItemData pickedItem = item;

        if (!TryTake(1))
        {
            return false;
        }

        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            inventory =
                target.gameObject.AddComponent<ItemInventory>();
        }

        ItemEffectSpawner.PlayPickupEffect(pickedItem, target);
        inventory.AddItem(pickedItem, 1);
        ClearReservation(target.gameObject);
        return true;
    }

    Transform GetWorldActorTarget(Collider2D other)
    {
        VillagerAI villager =
            other.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.transform;
        }

        SmartNpcAI smartNpc =
            other.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        MonsterAI monster =
            other.GetComponentInParent<MonsterAI>();

        if (monster != null)
        {
            return monster.transform;
        }

        return null;
    }

    Transform GetPlayerTarget(Collider2D other)
    {
        CharacterStats characterStats =
            other.GetComponentInParent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.transform;
        }

        PlayerHealth player =
            other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            return player.transform;
        }

        if (other.CompareTag("Player"))
        {
            return other.transform;
        }

        return null;
    }

    void CleanupInvalidReservation()
    {
        if (reservedBy == null ||
            !reservedBy.activeInHierarchy ||
            reservationExpiresAtScaledSeconds <= GameTime.ScaledNowSeconds)
        {
            reservedBy = null;
            reservationExpiresAtScaledSeconds = 0f;
        }
    }

    public void TrackReceiver(GameObject target)
    {
        if (!trackReceiverInHeavenNurture ||
            target == null)
        {
            return;
        }

        NpcFavoriteManager manager =
            NpcFavoriteManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        NpcFavorite favorite =
            manager.GetOrCreateFavorite(target);
        int fearGain =
            GetHeavenFavorFearGain(item);
        bool fearChanged =
            favorite != null &&
            favorite.AddHeavenFavorFear(fearGain);
        bool added =
            manager.AddFavorite(favorite);

        if (fearChanged && !added)
        {
            manager.NotifyFavoritesChanged();
        }

        Debug.LogWarning(
            "[HeavenNurtureTrack] target=" +
            NpcRoleUtility.GetDisplayName(target) +
            " favoriteTarget=" +
            (favorite != null ? favorite.gameObject.name : "null") +
            " favorites=" +
            manager.Favorites.Count +
            " added=" +
            added +
            " heavenFearGain=" +
            fearGain +
            " heavenFearTotal=" +
            (favorite != null ? favorite.GetHeavenFavorFear() : 0));

        if (added)
        {
            LogReceiverTracked(target);
        }
    }

    void LogReceiverTracked(GameObject target)
    {
        if (target == null ||
            item == null ||
            WorldEventManager.Instance == null)
        {
            return;
        }

        string receiverName =
            NpcRoleUtility.GetDisplayName(target);
        string itemName =
            ItemText.Name(item);

        if (string.IsNullOrWhiteSpace(receiverName) ||
            string.IsNullOrWhiteSpace(itemName))
        {
            return;
        }

        string message =
            receiverName +
            " nhận được " +
            itemName +
            " từ Thiên Đạo.";

        WorldEventManager.Instance.AddLog(message, 1, true);
    }

    int GetHeavenFavorFearGain(StatItemData pickedItem)
    {
        if (pickedItem == null)
        {
            return 0;
        }

        switch (pickedItem.grade)
        {
            case ItemGrade.Trung:
                return 3;
            case ItemGrade.Thuong:
                return 5;
            case ItemGrade.Tien:
                return 10;
            default:
                return 1;
        }
    }
}
