using UnityEngine;

[RequireComponent(typeof(VillagerAI))]
public class HunterJob : MonoBehaviour
{
    [Header("Hunter")]
    public bool enabledHunterJob = true;
    public NpcMapZone huntZone = NpcMapZone.MaThuSonMach;
    public Transform huntPoint;
    public StatItemData meatProduct;
    public bool requireMonsterDropsMeat;
    public bool guaranteeMeatDrop = true;
    public int guaranteedMeatAmount = 1;

    [Header("Search")]
    public float monsterSearchRadius = 14f;
    public float retargetDistance = 18f;
    public float arriveDistance = 0.55f;
    public LayerMask monsterLayers = ~0;

    [Header("Combat")]
    // Hunter targets in this project have fairly large colliders, so the
    // hunter needs a wider reach to actually enter the attack branch.
    public float attackRange = 1.7f;
    public float attackInterval = 1.15f;
    public int attackDamage = 8;
    public bool useVillagerAttackStat = true;
    public bool useCombatTechniqueModifier = true;

    [Header("Loot")]
    public float lootSearchRadius = 1.2f;
    public float lootCollectDistance = 0.45f;

    [Header("Runtime")]
    public NpcJobState currentState = NpcJobState.Idle;
    public MonsterAI currentMonsterTarget;
    public WorldStatItemPickup currentLootTarget;

    VillagerAI villager;
    NpcItemCollector collector;
    Rigidbody2D rb;
    float nextAttackTime;
    bool running;

    void Awake()
    {
        RefreshReferences();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        AssignDefaultProducts();
    }

    void AssignDefaultProducts()
    {
        if (meatProduct == null)
        {
            HarvestJob harvestJob = GetComponent<HarvestJob>();
            if (harvestJob != null && harvestJob.huntingProduct != null)
            {
                meatProduct = harvestJob.huntingProduct;
                return;
            }

            meatProduct =
                UnityEditor.AssetDatabase.LoadAssetAtPath<StatItemData>(
                    "Assets/Item/ThucPham/thit.asset");
        }
    }
#endif

    void Update()
    {
        if (ShouldReturnHomeNow())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[HunterJob] Abort for home -> " +
                gameObject.name +
                " action=" + villager.currentAction +
                " state=" + currentState +
                " running=" + running +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
#endif
            villager.GoHomeToRest();
            return;
        }

        if (!running || !IsAllowedJob())
        {
            return;
        }

        TickHunterJob();
    }

    public bool TryRun()
    {
        if (ShouldReturnHomeNow())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[HunterJob] TryRun aborted for home -> " +
                gameObject.name +
                " action=" + villager.currentAction +
                " state=" + currentState +
                " running=" + running +
                " hour=" + (WorldTimeSystem.Instance != null
                    ? WorldTimeSystem.Instance.CurrentHour.ToString("0.##")
                    : "null"));
#endif
            villager.GoHomeToRest();
            return true;
        }

        if (!IsAllowedJob())
        {
            running = false;
            return false;
        }

        RefreshReferences();
        running = true;
        TickHunterJob();
        return true;
    }

    bool ShouldReturnHomeNow()
    {
        if (villager == null ||
            villager.IsReturningHome ||
            !villager.ShouldGoHomeForRest())
        {
            return false;
        }

        return true;
    }

    public void CancelHunterNow()
    {
        running = false;
        currentState = NpcJobState.Idle;
        currentMonsterTarget = null;
        currentLootTarget = null;
        nextAttackTime = 0f;

        if (villager != null)
        {
            villager.StopMoving();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        SetAction("idle");
    }

    bool IsAllowedJob()
    {
        return enabledHunterJob &&
            villager != null &&
            !villager.IsDead &&
            villager.job == VillagerJob.Hunter;
    }

    void RefreshReferences()
    {
        if (villager == null)
        {
            villager = GetComponent<VillagerAI>();
        }

        if (collector == null)
        {
            collector = GetComponent<NpcItemCollector>();
        }

        if (collector == null)
        {
            collector = gameObject.AddComponent<NpcItemCollector>();
        }

        collector.canPickupItems = true;

        rb = GetComponent<Rigidbody2D>();

        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.canGather = false;
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null)
        {
            harvestJob.enabledHarvestJob = false;
        }
    }

    void TickHunterJob()
    {
        RefreshReferences();

        if (TryCollectCurrentLoot())
        {
            return;
        }

        if (!IsValidMonster(currentMonsterTarget))
        {
            currentMonsterTarget = FindNearestMonster();
        }

        if (currentMonsterTarget == null)
        {
            currentState = NpcJobState.Moving;
            MoveToHuntArea();
            return;
        }

        float distance = Vector2.Distance(
            transform.position,
            currentMonsterTarget.transform.position);

        if (distance > Mathf.Max(attackRange, arriveDistance))
        {
            currentState = NpcJobState.Moving;
            MoveToMonster(currentMonsterTarget);
            return;
        }

        currentState = NpcJobState.Working;
        StopMotion();
        string attackAction =
            NpcText.ActionFormat(
                "attackMonsterNamed",
                GetMonsterName(currentMonsterTarget));

        if (villager != null)
        {
            villager.SetActionImmediate(attackAction, 0.45f);
        }
        else
        {
            SetAction(attackAction);
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + Mathf.Max(0.15f, attackInterval);
        AttackMonster(currentMonsterTarget);
    }

    void MoveToHuntArea()
    {
        Vector3 target = GetNextHuntPatrolPoint();
        MoveTo(
            target,
            huntZone,
            "Đi tới bãi săn");
    }

    Vector3 GetNextHuntPatrolPoint()
    {
        WorldTilemapManager tilemap = WorldTilemapManager.Instance;
        if (tilemap != null)
        {
            Vector3 huntingTile = tilemap.GetHuntingTile(huntZone);
            if (huntingTile != Vector3.zero &&
                Vector2.Distance(transform.position, huntingTile) >
                Mathf.Max(arriveDistance, 0.65f))
            {
                return huntingTile;
            }
        }

        if (huntPoint != null)
        {
            Vector3 huntPosition = huntPoint.position;
            if (Vector2.Distance(transform.position, huntPosition) >
                Mathf.Max(arriveDistance, 0.65f))
            {
                return huntPosition;
            }
        }

        NpcMapArea area = NpcMapArea.FindAreaByZone(huntZone);
        if (area != null && area.areaBounds != null)
        {
            Bounds bounds = area.areaBounds.bounds;
            Vector3 seed = transform.position + new Vector3(
                Random.Range(-4f, 4f),
                Random.Range(-4f, 4f),
                0f);

            Vector3 patrolPoint = new Vector3(
                Mathf.Clamp(seed.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(seed.y, bounds.min.y, bounds.max.y),
                transform.position.z);

            if (Vector2.Distance(transform.position, patrolPoint) >
                Mathf.Max(arriveDistance, 0.65f))
            {
                return patrolPoint;
            }
        }

        return GetHuntCenter();
    }

    void MoveToMonster(MonsterAI monster)
    {
        if (monster == null)
        {
            return;
        }

        NpcMapZone? targetZone = GetZoneOf(monster.transform.position);
        MoveTo(
            monster.transform.position,
            targetZone.HasValue ? targetZone.Value : huntZone,
            "Truy đuổi " + GetMonsterName(monster));
    }

    void MoveTo(
        Vector3 target,
        NpcMapZone targetZone,
        string action)
    {
        if (villager != null && villager.enabled)
        {
            villager.ForceJobMoveTo(target, action, targetZone);
            return;
        }

        bool usingTeleportRoute;
        string routeAction;
        Vector3 moveTarget = NpcMapNavigator.GetNextMoveTarget(
            gameObject,
            target,
            targetZone,
            out usingTeleportRoute,
            out routeAction);

        SetAction(usingTeleportRoute ? routeAction : action);

        float speed = NpcRoleUtility.GetMoveSpeed(gameObject, 2f);
        Vector3 next = Vector3.MoveTowards(
            transform.position,
            moveTarget,
            Mathf.Max(0.1f, speed) * Time.deltaTime);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.MovePosition(next);
        }
        else
        {
            transform.position = next;
        }
    }

    void AttackMonster(MonsterAI monster)
    {
        if (!IsValidMonster(monster))
        {
            currentMonsterTarget = null;
            return;
        }

        int damage = useVillagerAttackStat
            ? NpcRoleUtility.GetAttack(gameObject)
            : attackDamage;

        damage = Mathf.Max(1, damage);

        monster.TakeDamage(damage);

        if (monster == null || monster.IsDead)
        {
            OnMonsterKilled(monster);
        }
    }

    void OnMonsterKilled(MonsterAI monster)
    {
        Vector3 deathPosition = monster != null
            ? monster.transform.position
            : transform.position;

        StatItemData meat = ResolveMeatProduct();
        WorldStatItemPickup loot = meat != null
            ? FindNearestLoot(deathPosition, meat, lootSearchRadius)
            : null;

        if (loot == null && meat != null && guaranteeMeatDrop)
        {
            loot = SpawnMeatPickup(deathPosition, meat);
        }

        currentMonsterTarget = null;
        currentLootTarget = loot;

        if (currentLootTarget == null)
        {
            currentState = NpcJobState.Idle;
            SetAction("Săn xong nhưng không thấy thịt rơi");
        }
    }

    bool TryCollectCurrentLoot()
    {
        if (!IsValidPickup(currentLootTarget))
        {
            currentLootTarget = null;
            return false;
        }

        float distance = Vector2.Distance(
            transform.position,
            currentLootTarget.transform.position);

        if (distance > lootCollectDistance)
        {
            currentState = NpcJobState.Moving;
            NpcMapZone? targetZone = GetZoneOf(currentLootTarget.transform.position);
            MoveTo(
                currentLootTarget.transform.position,
                targetZone.HasValue ? targetZone.Value : huntZone,
                "Đi nhặt " + ItemText.Name(currentLootTarget.item));
            return true;
        }

        StatItemData item = currentLootTarget.item;
        if (item != null && currentLootTarget.TryTake(1))
        {
            collector.ReceiveItemWithoutUse(
                item,
                ItemLifecycleEventType.Picked);
        }

        currentLootTarget = null;
        currentState = NpcJobState.Idle;
        SetAction("Đã thu thịt");
        return true;
    }

    MonsterAI FindNearestMonster()
    {
        MonsterAI[] monsters = FindObjectsByType<MonsterAI>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        MonsterAI best = null;
        float bestDistance = float.MaxValue;
        Vector3 center = GetHuntCenter();

        foreach (MonsterAI monster in monsters)
        {
            if (!IsValidMonster(monster))
            {
                continue;
            }

            if (!IsMonsterInHuntZone(monster))
            {
                continue;
            }

            if (requireMonsterDropsMeat &&
                !MonsterDropsMeat(monster))
            {
                continue;
            }

            float distance = Vector2.Distance(center, monster.transform.position);
            if (distance > monsterSearchRadius)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = monster;
            }
        }

        return best;
    }

    bool IsValidMonster(MonsterAI monster)
    {
        return monster != null &&
            monster.gameObject.activeInHierarchy &&
            !monster.IsDead;
    }

    bool IsMonsterInHuntZone(MonsterAI monster)
    {
        if (monster == null)
        {
            return false;
        }

        NpcMapZone? zone = GetZoneOf(monster.transform.position);
        if (zone.HasValue)
        {
            return zone.Value == huntZone;
        }

        return Vector2.Distance(
            GetHuntCenter(),
            monster.transform.position) <= monsterSearchRadius;
    }

    bool MonsterDropsMeat(MonsterAI monster)
    {
        StatItemData meat = ResolveMeatProduct();
        if (meat == null || monster == null)
        {
            return true;
        }

        StatItemData loot = monster.GetDeathLoot();
        return ItemsMatch(loot, meat);
    }

    StatItemData ResolveMeatProduct()
    {
        if (meatProduct != null)
        {
            return meatProduct;
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null && harvestJob.huntingProduct != null)
        {
            // Keeps older scenes alive until their hunter data is migrated over.
            return harvestJob.huntingProduct;
        }

        return null;
    }

    public bool IsProducedItem(StatItemData item)
    {
        return ItemsMatch(item, ResolveMeatProduct());
    }

    Vector3 GetHuntCenter()
    {
        if (huntPoint != null)
        {
            return huntPoint.position;
        }

        if (villager != null && villager.workPoint != null)
        {
            return villager.workPoint.position;
        }

        NpcMapArea area = NpcMapArea.FindAreaByZone(huntZone);
        if (area != null && area.areaBounds != null)
        {
            return area.areaBounds.bounds.center;
        }

        return transform.position;
    }

    NpcMapZone? GetZoneOf(Vector3 position)
    {
        NpcMapArea area = NpcMapArea.FindArea(position);
        return area != null ? area.zone : (NpcMapZone?)null;
    }

    WorldStatItemPickup FindNearestLoot(
        Vector3 position,
        StatItemData item,
        float radius)
    {
        WorldStatItemPickup[] pickups = FindObjectsByType<WorldStatItemPickup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        WorldStatItemPickup best = null;
        float bestDistance = float.MaxValue;

        foreach (WorldStatItemPickup pickup in pickups)
        {
            if (!IsValidPickup(pickup) ||
                !ItemsMatch(pickup.item, item))
            {
                continue;
            }

            float distance = Vector2.Distance(position, pickup.transform.position);
            if (distance > radius)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    bool IsValidPickup(WorldStatItemPickup pickup)
    {
        return pickup != null &&
            pickup.gameObject.activeInHierarchy &&
            pickup.item != null &&
            pickup.amount > 0 &&
            pickup.allowNpcPickup &&
            !pickup.IsReservedByOther(gameObject);
    }

    WorldStatItemPickup SpawnMeatPickup(Vector3 position, StatItemData item)
    {
        GameObject lootObject = new GameObject(ItemText.Name(item) + " Pickup");

        // Drop it a little off the corpse so the pickup stays visible before
        // the hunter walks over and collects it.
        Vector2 dropOffset = Random.insideUnitCircle;
        if (dropOffset.sqrMagnitude < 0.0001f)
        {
            dropOffset = Vector2.right;
        }

        float dropRadius =
            Mathf.Max(0.55f, lootCollectDistance + 0.1f);
        lootObject.transform.position =
            position + (Vector3)(dropOffset.normalized * dropRadius);

        WorldStatItemPickup pickup = lootObject.AddComponent<WorldStatItemPickup>();
        pickup.item = item;
        pickup.amount = Mathf.Max(1, guaranteedMeatAmount);
        pickup.allowNpcPickup = true;
        pickup.allowPlayerPickup = false;
        pickup.requireNpcHarvestAction = true;
        pickup.destroyWhenEmpty = true;

        CircleCollider2D collider = lootObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.25f;

        PickupVisualUtility.ApplySprite(lootObject, item.icon, 20);
        GameSaveSystem.RegisterItem(item);
        return pickup;
    }

    bool ItemsMatch(StatItemData a, StatItemData b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        return !string.IsNullOrEmpty(a.ItemId) &&
            !string.IsNullOrEmpty(b.ItemId) &&
            a.ItemId == b.ItemId;
    }

    void StopMotion()
    {
        if (villager != null)
        {
            villager.StopMoving();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void SetAction(string action)
    {
        NpcRoleUtility.SetAction(gameObject, action);
    }

    string GetMonsterName(MonsterAI monster)
    {
        if (monster == null)
        {
            return "yêu thú";
        }

        if (!string.IsNullOrWhiteSpace(monster.monsterName))
        {
            return monster.monsterName;
        }

        return monster.name;
    }
}
