using System.Collections;
using UnityEngine;

public partial class MonsterAI
{
    void DevourDefeatedNpc(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (target.GetComponentInParent<PlayerHealth>() != null ||
            target.GetComponentInParent<MonsterAI>() != null)
        {
            return;
        }

        int exp = GetDevourExp(target);
        if (exp <= 0)
        {
            return;
        }

        AddCultivationExp(exp);
        hunger = Mathf.Clamp(hunger - 35f, 0f, 100f);
        ApplyTemperamentSurge(0f, 10f);

        if (healAfterDevouringNpc)
        {
            currentHP =
                Mathf.Clamp(
                    currentHP + Mathf.Max(1, maxHP / 5),
                    0,
                    maxHP);
        }

        if (entityProfile != null)
        {
            entityProfile.Remember(
                target.name,
                "devoured_npc",
                Mathf.Clamp(exp / 100, 1, 100));
        }

        currentAction = "An thit hap thu " + exp + " tu vi";
        SyncEntityProfileStats();
    }

    int GetDevourExp(Transform target)
    {
        CharacterStats stats = target.GetComponentInParent<CharacterStats>();
        if (stats != null)
        {
            return CalculateDevourExp(
                stats.realm,
                stats.realmStage,
                stats.attack + stats.defense + stats.finalHP / 10);
        }

        VillagerAI villager = target.GetComponentInParent<VillagerAI>();
        if (villager != null &&
            villager.enabled)
        {
            return CalculateDevourExp(
                CultivationRealm.Mortal,
                1,
                Mathf.Max(1, villager.maxHP / 10));
        }

        SmartNpcAI smartNpc = target.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return CalculateDevourExp(
                smartNpc.realm,
                smartNpc.realmStage,
                smartNpc.attack + smartNpc.defense + smartNpc.maxHP / 10);
        }

        return minNpcDevourExp;
    }

    int CalculateDevourExp(
        CultivationRealm targetRealm,
        int targetStage,
        int targetPower)
    {
        long realmExp =
            CultivationProgression.GetExpToNextLong(
                targetRealm,
                targetStage,
                baseExpToNextRealm);

        if (realmExp == long.MaxValue)
        {
            realmExp = int.MaxValue;
        }

        long value =
            Mathf.Max(0, minNpcDevourExp) +
            Mathf.Max(0, targetPower) * 3L +
            (long)(realmExp * Mathf.Clamp01(npcDevourExpMultiplier));

        long minValue = Mathf.Max(0, minNpcDevourExp);
        value = System.Math.Max(minValue, value);
        value = System.Math.Min(int.MaxValue, value);
        return (int)value;
    }

    void Die()
    {
        isDead = true;
        waitingForHeavenlyTribulation = false;
        isAttacking = false;
        hasTarget = false;
        desiredVelocity = Vector2.zero;
        waitTimer = waitTime;
        nextThinkTime = Time.time + Random.Range(0f, GetThinkDelay());
        nextDetectTime = Time.time + Random.Range(0f, GetDetectDelay());
        nextReducedFixedUpdateTime = Time.time;
        ClearCurrentTarget();
        NpcSocialEventBus.PublishMonsterDefeated(this);
        bool preserveInDungeon =
            BicanhSessionManager.ShouldPreserveDungeonDeath(gameObject);

        CancelInvoke(nameof(ApplyAttackDamage));
        CancelInvoke(nameof(EndAttack));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (useAnimation)
        {
            if (directionalAnimator != null)
            {
                directionalAnimator.PlayDeath();
            }
            else if (animator != null)
            {
                SetAnimatorBoolIfExists("isDead", true);
            }
        }

        DropDeathLoot();

        if (preserveInDungeon)
        {
            return;
        }

        NpcInventoryDropper.DropAll(gameObject);

        if (respawnAfterDeath)
        {
            StartCoroutine(RespawnRoutine());
        }
        else
        {
            Destroy(gameObject, Mathf.Max(0f, corpseVisibleSeconds));
        }
    }

    IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        yield return new WaitForSeconds(Mathf.Max(0f, corpseVisibleSeconds));

        SetMonsterVisible(false);
        SetMonsterColliders(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        float targetWorldHour =
            GetAbsoluteWorldHour() + Mathf.Max(1, respawnAfterDays) * 24f;
        while (GetAbsoluteWorldHour() < targetWorldHour)
        {
            yield return null;
        }

        RespawnAtHome();
    }

    void RespawnAtHome()
    {
        transform.position = startPosition;
        currentHP = maxHP;
        isDead = false;
        waitingForHeavenlyTribulation = false;
        isAttacking = false;
        isRespawning = false;
        desiredVelocity = Vector2.zero;
        waitTimer = waitTime;
        nextThinkTime = Time.time + Random.Range(0f, GetThinkDelay());
        nextDetectTime = Time.time + Random.Range(0f, GetDetectDelay());
        hasTarget = false;
        ClearCurrentTarget();
        currentAction = "Hoi sinh trong lanh dia";

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
        }

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        lastDamageSource = null;
        lastSmartNpcAttacker = null;

        SetMonsterVisible(true);
        SetMonsterColliders(true);
        if (directionalAnimator != null)
        {
            directionalAnimator.PlayRespawn();
        }

        SetMovingAnimation(false);
    }

    float GetAbsoluteWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return Time.time / 3600f;
        }

        int year = Mathf.Max(1, timeSystem.currentYear);
        int month = Mathf.Max(1, timeSystem.currentMonth);
        int day = Mathf.Max(1, timeSystem.currentDay);
        int absoluteDay = (year - 1) * 360 + (month - 1) * 30 + (day - 1);
        return absoluteDay * 24f + Mathf.Max(0f, timeSystem.currentHour);
    }

    void SetMonsterVisible(bool visible)
    {
        if (cachedRenderers == null)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (Renderer targetRenderer in cachedRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = visible;
            }
        }
    }

    void SetMonsterColliders(bool enabledValue)
    {
        if (cachedColliders == null)
        {
            cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }

        foreach (Collider2D targetCollider in cachedColliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = enabledValue;
            }
        }
    }

    void DropDeathLoot()
    {
        if (!dropLootOnDeath ||
            lootDropChance <= 0f ||
            Random.value > lootDropChance)
        {
            return;
        }

        StatItemData loot = GetDeathLoot();
        if (loot == null)
        {
            return;
        }

        GameSaveSystem.RegisterItem(loot);

        int dropAmount = Mathf.Max(1, lootAmount);
        if (TryGiveDeathLootToNpcInventory(loot, dropAmount))
        {
            return;
        }

        Vector2 offset =
            Random.insideUnitCircle * Mathf.Max(0f, lootDropOffsetRadius);
        Vector3 dropPosition = transform.position + (Vector3)offset;
        GameObject lootObject = new GameObject(loot.itemName + " Pickup");
        lootObject.transform.position = dropPosition;

        WorldStatItemPickup pickup =
            lootObject.AddComponent<WorldStatItemPickup>();
        pickup.item = loot;
        pickup.amount = dropAmount;
        pickup.allowPlayerPickup = allowPlayerLootPickup;
        pickup.allowNpcPickup = allowNpcLootPickup;
        pickup.destroyWhenEmpty = true;
        pickup.ConfigureAsDroppedWorldItem();

        CircleCollider2D collider = lootObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.25f;

        PickupVisualUtility.ApplySprite(lootObject, loot.icon, 20);
    }

    bool TryGiveDeathLootToNpcInventory(
        StatItemData loot,
        int dropAmount)
    {
        if (loot == null ||
            dropAmount <= 0)
        {
            return false;
        }

        GameObject recipient = ResolveNpcLootRecipient();
        if (recipient == null)
        {
            return false;
        }

        ItemInventory inventory =
            recipient.GetComponent<ItemInventory>();
        if (inventory == null)
        {
            inventory =
                recipient.AddComponent<ItemInventory>();
        }

        NpcItemCollector collector =
            recipient.GetComponent<NpcItemCollector>();

        if (collector != null)
        {
            for (int i = 0; i < dropAmount; i++)
            {
                collector.ReceiveItemWithoutUse(
                    loot,
                    ItemLifecycleEventType.Picked);
            }
        }
        else
        {
            ItemEffectSpawner.PlayPickupEffect(
                loot,
                recipient.transform);
            inventory.AddItem(loot, dropAmount);
            for (int i = 0; i < dropAmount; i++)
            {
                ItemLifecycleSystem.Notify(
                    ItemLifecycleEventType.Picked,
                    loot,
                    recipient);
            }

            TreasureHeatSystem.NotifyNpcReceivedItem(
                recipient,
                loot);
        }

        currentAction =
            "Thu duoc " +
            ItemText.Name(loot) +
            " vao tui NPC";
        return true;
    }

    GameObject ResolveNpcLootRecipient()
    {
        if (lastSmartNpcAttacker != null)
        {
            return lastSmartNpcAttacker.gameObject;
        }

        if (lastDamageSource == null)
        {
            return null;
        }

        SmartNpcAI smartNpc =
            lastDamageSource.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        VillagerAI villager =
            lastDamageSource.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.gameObject;
        }

        return null;
    }

    public StatItemData GetDeathLoot()
    {
        if (lootByBeastLevel == null ||
            lootByBeastLevel.Length == 0)
        {
            return null;
        }

        int index =
            Mathf.Clamp(beastLevel - 1, 0, lootByBeastLevel.Length - 1);
        return lootByBeastLevel[index];
    }
}
