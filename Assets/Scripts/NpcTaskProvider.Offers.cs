using System.Collections.Generic;
using UnityEngine;

public partial class NpcTaskProvider
{
    void EnsureExpandedDefaultOffers()
    {
        if (!useExpandedDefaultTaskCatalog)
        {
            return;
        }

        offers = BuildExpandedDefaultOffers();
    }

    void NormalizeConfiguredOfferText()
    {
        if (offers == null)
        {
            return;
        }

        foreach (NpcTaskOffer offer in offers)
        {
            if (offer == null)
            {
                continue;
            }

            NormalizeConfiguredOfferMetadata(offer);

            if (string.IsNullOrWhiteSpace(offer.taskName) || LooksCorruptedText(offer.taskName))
            {
                offer.taskName = GetDefaultTaskNameForOffer(offer);
            }
        }
    }

    static void NormalizeConfiguredOfferMetadata(
        NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return;
        }

        if (!IsLegacyFrontierWatchTaskId(offer.customTaskId))
        {
            return;
        }

        offer.taskType = NpcTaskType.FrontierWatch;
        offer.customTaskId = "frontier_watch";

        if (!string.IsNullOrWhiteSpace(offer.customTargetId))
        {
            offer.customTargetId =
                FrontierDefenseCoordinator.ResolveCanonicalPostId(
                    offer.customTargetId);
        }
    }

    static bool IsLegacyFrontierWatchTaskId(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return false;
        }

        return taskId.Trim().StartsWith(
            "frontier_watch",
            System.StringComparison.OrdinalIgnoreCase);
    }

    void ResolveConfiguredOfferItemReferences()
    {
        if (offers == null)
        {
            return;
        }

        foreach (NpcTaskOffer offer in offers)
        {
            ResolveOfferItemReferences(offer);
        }
    }

    static void ResolveOfferItemReferences(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return;
        }

        offer.ResolveItemReferences();
    }

    string TaskName(string key)
    {
        return NpcText.Get("taskNames", key, key);
    }

    string TaskNameFormat(string key, params object[] args)
    {
        return NpcText.Format(TaskName(key), args);
    }

    string TaskAction(string key)
    {
        return NpcText.Get("taskActions", key, key);
    }

    string TaskActionFormat(string key, params object[] args)
    {
        return NpcText.Format(TaskAction(key), args);
    }

    string TaskChoiceAction(GameObject npc, RunningNpcTask task)
    {
        string taskText =
            task != null
                ? GetTaskDisplayText(task)
                : "";
        string generic =
            TaskActionFormat("chooseTask", taskText);
        if (string.IsNullOrWhiteSpace(taskText))
        {
            return generic;
        }

        EntityPersonality personality =
            GetNpcPersonality(npc);

        if (personality == null)
        {
            return generic;
        }

        if (personality.bravery >= 70 &&
            IsDangerousTask(task))
        {
            return "Chọn nhiệm vụ " + taskText +
                " - việc hiểm mới đáng thử tay.";
        }

        if (personality.greed >= 70 &&
            task != null &&
            task.rewardSpiritStone >= 80)
        {
            return "Chọn nhiệm vụ " + taskText +
                " - phần thưởng này không thể bỏ qua.";
        }

        if (personality.kindness >= 70 &&
            IsHelpfulTask(task))
        {
            return "Chọn nhiệm vụ " + taskText +
                " - giúp được người thì nên nhận.";
        }

        if (personality.diligence >= 70)
        {
            return "Chọn nhiệm vụ " + taskText +
                " - làm chắc từng bước là ổn.";
        }

        if (personality.funSeeking >= 70)
        {
            return "Chọn nhiệm vụ " + taskText +
                " - nghe có vẻ thú vị.";
        }

        return generic;
    }

    EntityPersonality GetNpcPersonality(GameObject npc)
    {
        if (npc == null)
        {
            return null;
        }

        EntityProfile profile =
            npc.GetComponent<EntityProfile>();
        if (profile != null &&
            profile.personality != null)
        {
            return profile.personality;
        }

        SmartNpcAI smartNpc =
            npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return new EntityPersonality
            {
                bravery = smartNpc.bravery,
                greed = smartNpc.greed,
                kindness = smartNpc.kindness,
                diligence = 50,
                funSeeking = 50
            };
        }

        VillagerAI villager =
            npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return new EntityPersonality
            {
                bravery = villager.bravery,
                greed = villager.greed,
                kindness = 50,
                diligence = villager.diligence,
                funSeeking = 50
            };
        }

        return null;
    }

    bool IsDangerousTask(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null)
        {
            return false;
        }

        return task.offer.taskType == NpcTaskType.HuntMonster ||
            task.offer.taskType == NpcTaskType.Patrol ||
            task.offer.taskType == NpcTaskType.Escort ||
            task.offer.taskType == NpcTaskType.FrontierWatch;
    }

    bool IsHelpfulTask(RunningNpcTask task)
    {
        if (task == null ||
            task.offer == null)
        {
            return false;
        }

        return task.offer.taskType == NpcTaskType.Deliver ||
            task.offer.taskType == NpcTaskType.Escort ||
            task.offer.taskType == NpcTaskType.HarvestAndDeliver;
    }

    string TaskDisplay(string key)
    {
        return NpcText.Get("taskDisplay", key, key);
    }

    string TaskDisplayFormat(string key, params object[] args)
    {
        return NpcText.Format(TaskDisplay(key), args);
    }

    bool LooksCorruptedText(string value)
    {
        return !string.IsNullOrEmpty(value) &&
            (value.Contains("Ã") ||
                value.Contains("Â") ||
                value.Contains("â€") ||
                value.Contains("\u0081") ||
                value.Contains("\u008D") ||
                value.Contains("\u0090"));
    }

    string GetOfferTaskName(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(offer.taskName) && !LooksCorruptedText(offer.taskName))
        {
            return offer.taskName;
        }

        return GetDefaultTaskNameForOffer(offer);
    }

    string GetDefaultTaskNameForOffer(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return TaskName("gatherResource");
        }

        switch (offer.taskType)
        {
            case NpcTaskType.Patrol:
                return offer.rank == NpcTaskRank.Ha
                    ? TaskName("patrolVillageEdge")
                    : TaskName("patrolOutsideVillage");
            case NpcTaskType.HuntMonster:
                return TaskName("huntDangerousMonster");
            case NpcTaskType.Cultivate:
                return TaskName("protectCultivation");
            case NpcTaskType.Deliver:
                return TaskName("transportSpiritMaterial");
            case NpcTaskType.Escort:
                return TaskName("escortCaravan");
            case NpcTaskType.HarvestAndDeliver:
                return TaskName("harvestLinhRice");
            case NpcTaskType.GatherResource:
                if (offer.rank == NpcTaskRank.Thuong)
                {
                    return TaskName("findRareHerbDeepMountain");
                }

                if (offer.rank == NpcTaskRank.Trung)
                {
                    return TaskName("gatherSpiritMaterialsNearMaThuSon");
                }

                return TaskName("gatherLowSpiritHerb");
            default:
                return TaskName("gatherResource");
        }
    }
    NpcTaskOffer[] BuildExpandedDefaultOffers()
    {
        List<NpcTaskOffer> defaultOffers = new List<NpcTaskOffer>
        {
        };

        if (includeLinhRiceHarvestTask)
        {
            NpcTaskOffer linhRiceOffer = CreateLinhRiceHarvestOffer();
            if (linhRiceOffer != null)
            {
                defaultOffers.Add(linhRiceOffer);
            }
        }

        AppendMapDrivenGatherOffers(defaultOffers);
        AppendMapDrivenHuntOffers(defaultOffers);

        if (defaultOffers.Count == 0)
        {
            defaultOffers.Add(
                CreateGatherOffer(
                    TaskName("gatherLowSpiritHerb"),
                    NpcTaskRank.Ha,
                    CultivationRealm.Mortal,
                    1,
                    6,
                    1200,
                    0,
                    10f));
        }

        SortOffersByDisplayOrder(defaultOffers);
        return defaultOffers.ToArray();
    }

    void SortOffersByDisplayOrder(List<NpcTaskOffer> targetOffers)
    {
        if (targetOffers == null ||
            targetOffers.Count <= 1)
        {
            return;
        }

        targetOffers.Sort(CompareOffersForDisplay);
    }

    int CompareOffersForDisplay(
        NpcTaskOffer left,
        NpcTaskOffer right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int rankCompare =
            GetOfferRankSortValue(left.rank).CompareTo(
                GetOfferRankSortValue(right.rank));
        if (rankCompare != 0)
        {
            return rankCompare;
        }

        int typeCompare =
            left.taskType.CompareTo(right.taskType);
        if (typeCompare != 0)
        {
            return typeCompare;
        }

        return string.Compare(
            left.taskName,
            right.taskName,
            System.StringComparison.OrdinalIgnoreCase);
    }

    int GetOfferRankSortValue(NpcTaskRank rank)
    {
        switch (rank)
        {
            case NpcTaskRank.Ha:
                return 0;
            case NpcTaskRank.Trung:
                return 1;
            case NpcTaskRank.Thuong:
                return 2;
            default:
                return 99;
        }
    }

    NpcTaskOffer CreateLinhRiceHarvestOffer()
    {
        StatItemData item = ResolveLinhRiceItem();
        if (item == null)
        {
            return null;
        }

        NpcTaskOffer offer = CreateSimpleOffer(
            BuildHarvestTaskName(item),
            NpcTaskType.HarvestAndDeliver,
            NpcTaskRank.Ha,
            CultivationRealm.Mortal,
            1,
            linhRiceRewardSpiritStone,
            0,
            linhRiceHarvestDuration);

        offer.requiredItem = item;
        offer.requiredAmount = Mathf.Max(1, linhRiceAmountMin);
        offer.randomizeRequiredItemAmount = true;
        offer.requiredItemAmountMin = Mathf.Max(1, linhRiceAmountMin);
        offer.requiredItemAmountMax = Mathf.Max(offer.requiredItemAmountMin, linhRiceAmountMax);
        offer.autoPriceRequiredItemReward = true;
        ConfigureOfferRewardMarkup(offer, NpcTaskRank.Ha);
        offer.rewardSpiritStone = EstimateOfferRewardSpiritStone(offer, item);
        return offer;
    }

    NpcTaskOffer CreateGatherOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int amount, int reward, int exp, float duration)
    {
        NpcTaskOffer offer = CreateSimpleOffer(name, NpcTaskType.GatherResource, rank, realm, stage, reward, exp, duration);
        offer.requiredAmount = Mathf.Max(1, amount);
        offer.randomizeRequiredItemAmount = true;
        offer.requiredItemAmountMin = Mathf.Max(1, amount - 2);
        offer.requiredItemAmountMax = Mathf.Max(offer.requiredItemAmountMin, amount + 2);
        offer.autoPriceRequiredItemReward = true;
        return offer;
    }

    NpcTaskOffer CreateHuntOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int beastLevel, int amount, int reward, int exp, float duration)
    {
        NpcTaskOffer offer = CreateSimpleOffer(name, NpcTaskType.HuntMonster, rank, realm, stage, reward, exp, duration);
        offer.requiredBeastLevel = Mathf.Max(1, beastLevel);
        offer.requiredMonsterKills = Mathf.Max(1, amount);
        offer.requiredAmount = Mathf.Max(1, amount);
        offer.consumeRequiredItemsOnTurnIn = true;
        offer.autoPriceRequiredItemReward = true;
        offer.useRankRewardMultiplier = false;
        return offer;
    }

    NpcTaskOffer CreateHuntAnimalOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int amount, int reward, int exp, float duration)
    {
        NpcTaskOffer offer = CreateHuntOffer(name, rank, realm, stage, 0, amount, reward, exp, duration);
        offer.requiredHuntTargetType = HuntTargetType.Animal;
        return offer;
    }

    NpcTaskOffer CreatePatrolOffer(string name, NpcTaskRank rank, CultivationRealm realm, int stage, int reward, int exp, float duration)
    {
        return CreateSimpleOffer(name, NpcTaskType.Patrol, rank, realm, stage, reward, exp, duration);
    }

    NpcTaskOffer CreateSimpleOffer(string name, NpcTaskType type, NpcTaskRank rank, CultivationRealm realm, int stage, int reward, int exp, float duration)
    {
        return new NpcTaskOffer
        {
            taskName = name,
            taskType = type,
            rank = rank,
            minRealm = realm,
            minRealmStage = Mathf.Clamp(stage, 1, CultivationProgression.MaxStage),
            rewardSpiritStone = Mathf.Max(0, reward),
            rewardCultivationExp = 0,
            workDuration = Mathf.Max(1f, duration)
        };
    }

    void AppendMapDrivenGatherOffers(List<NpcTaskOffer> targetOffers)
    {
        if (targetOffers == null)
        {
            return;
        }

        List<StatItemData> items = new List<StatItemData>();
        HashSet<StatItemData> seen = new HashSet<StatItemData>();

        foreach (WorldResourceField field in WorldResourceField.Fields)
        {
            if (field == null || field.items == null)
            {
                continue;
            }

            for (int i = 0; i < field.items.Count; i++)
            {
                ResourceFieldItemEntry entry = field.items[i];
                StatItemData item = entry != null ? entry.item : null;
                if (item == null ||
                    IsLinhRiceItem(item) ||
                    !seen.Add(item))
                {
                    continue;
                }

                items.Add(item);
            }
        }

        items.Sort((a, b) =>
        {
            int gradeCompare = a.grade.CompareTo(b.grade);
            if (gradeCompare != 0)
            {
                return gradeCompare;
            }

            return string.Compare(
                ItemText.Name(a),
                ItemText.Name(b),
                System.StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < items.Count; i++)
        {
            NpcTaskOffer offer = CreateGatherOfferForItem(items[i]);
            if (offer != null)
            {
                targetOffers.Add(offer);
            }
        }
    }

    void AppendMapDrivenHuntOffers(List<NpcTaskOffer> targetOffers)
    {
        if (targetOffers == null)
        {
            return;
        }

        List<HuntOfferSeed> seeds = new List<HuntOfferSeed>();

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            if (!IsWorkThreatMonster(monster))
            {
                continue;
            }

            if (monster.huntTargetType != HuntTargetType.Beast)
            {
                continue;
            }

            NpcMapArea area = NpcMapArea.FindArea(monster.transform.position);
            if (area == null || area.zone != NpcMapZone.MaThuSonMach)
            {
                continue;
            }

            StatItemData loot = monster.GetDeathLoot();
            if (loot == null)
            {
                continue;
            }

            HuntOfferSeed existing = null;
            for (int i = 0; i < seeds.Count; i++)
            {
                HuntOfferSeed seed = seeds[i];
                if (seed != null &&
                    seed.lootItem == loot &&
                    seed.realm == monster.realm &&
                    seed.targetType == monster.huntTargetType)
                {
                    existing = seed;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new HuntOfferSeed
                {
                    lootItem = loot,
                    targetType = monster.huntTargetType,
                    realm = monster.realm,
                    maxStage = Mathf.Clamp(
                        monster.realmStage,
                        1,
                        CultivationProgression.MaxStage)
                };
                seeds.Add(existing);
                continue;
            }

            existing.maxStage = Mathf.Max(
                existing.maxStage,
                Mathf.Clamp(monster.realmStage, 1, CultivationProgression.MaxStage));
        }

        seeds.Sort((a, b) =>
        {
            int powerCompare = CultivationProgression.GetRealmPower(a.realm, a.maxStage)
                .CompareTo(CultivationProgression.GetRealmPower(b.realm, b.maxStage));
            if (powerCompare != 0)
            {
                return powerCompare;
            }

            return string.Compare(
                ItemText.Name(a.lootItem),
                ItemText.Name(b.lootItem),
                System.StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < seeds.Count; i++)
        {
            NpcTaskOffer offer = CreateHuntOfferForSeed(seeds[i]);
            if (offer != null)
            {
                targetOffers.Add(offer);
            }
        }
    }

    NpcTaskOffer CreateGatherOfferForItem(StatItemData item)
    {
        if (item == null)
        {
            return null;
        }

        NpcTaskRank rank = GetRankForItemGrade(item.grade);
        CultivationRealm realm;
        int stage;
        ResolveGatherMinimumRequirement(rank, out realm, out stage);

        Vector2Int amountRange = GetGatherAmountRange(item.grade);
        float duration = Mathf.Lerp(8f, 18f, Mathf.InverseLerp(0f, 3f, (int)item.grade));

        NpcTaskOffer offer = CreateSimpleOffer(
            BuildGatherTaskName(item),
            NpcTaskType.GatherResource,
            rank,
            realm,
            stage,
            0,
            0,
            duration);

        offer.requiredItem = item;
        offer.requiredAmount = amountRange.x;
        offer.randomizeRequiredItemAmount = true;
        offer.requiredItemAmountMin = amountRange.x;
        offer.requiredItemAmountMax = amountRange.y;
        offer.autoPriceRequiredItemReward = true;
        offer.useRankRewardMultiplier = false;
        ConfigureOfferRewardMarkup(offer, rank);
        offer.rewardSpiritStone = EstimateOfferRewardSpiritStone(offer, item);
        return offer;
    }

    NpcTaskOffer CreateHuntOfferForSeed(HuntOfferSeed seed)
    {
        if (seed == null || seed.lootItem == null)
        {
            return null;
        }

        NpcTaskRank rank = GetRankForMonsterRealm(seed.realm);
        CultivationRealm minRealm;
        int minStage;
        ResolveSafeHuntNpcRequirement(
            seed.realm,
            seed.maxStage,
            out minRealm,
            out minStage);

        int killAmount = seed.maxStage <= 4 ? 2 : 1;
        float duration = Mathf.Lerp(
            20f,
            52f,
            Mathf.InverseLerp(
                CultivationProgression.GetRealmPower(CultivationRealm.QiRefining, 1),
                CultivationProgression.GetRealmPower(CultivationRealm.NascentSoul, CultivationProgression.MaxStage),
                CultivationProgression.GetRealmPower(seed.realm, seed.maxStage)));

        NpcTaskOffer offer = CreateSimpleOffer(
            BuildHuntTaskName(seed.realm, seed.maxStage),
            NpcTaskType.HuntMonster,
            rank,
            minRealm,
            minStage,
            0,
            0,
            duration);

        offer.requiredItem = seed.lootItem;
        offer.requiredAmount = killAmount;
        offer.requiredMonsterKills = killAmount;
        offer.consumeRequiredItemsOnTurnIn = true;
        offer.autoPriceRequiredItemReward = true;
        offer.useRankRewardMultiplier = false;
        offer.requiredHuntTargetType = seed.targetType;
        offer.useMonsterRealmStageRequirement = true;
        offer.requiredMonsterRealm = seed.realm;
        offer.requiredMonsterMaxStage = Mathf.Clamp(
            seed.maxStage,
            1,
            CultivationProgression.MaxStage);
        offer.matchMonsterRealmExactly = true;
        ConfigureOfferRewardMarkup(offer, rank);
        offer.rewardSpiritStone = EstimateOfferRewardSpiritStone(offer, seed.lootItem);
        return offer;
    }

    NpcTaskRank GetRankForItemGrade(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return NpcTaskRank.Trung;

            case ItemGrade.Thuong:
            case ItemGrade.Tien:
                return NpcTaskRank.Thuong;

            default:
                return NpcTaskRank.Ha;
        }
    }

    NpcTaskRank GetRankForMonsterRealm(CultivationRealm realm)
    {
        if (realm >= CultivationRealm.NascentSoul)
        {
            return NpcTaskRank.Thuong;
        }

        if (realm >= CultivationRealm.GoldenCore)
        {
            return NpcTaskRank.Trung;
        }

        return NpcTaskRank.Ha;
    }

    void ResolveGatherMinimumRequirement(
        NpcTaskRank rank,
        out CultivationRealm realm,
        out int stage)
    {
        switch (rank)
        {
            case NpcTaskRank.Trung:
                realm = CultivationRealm.QiRefining;
                stage = 7;
                return;

            case NpcTaskRank.Thuong:
                realm = CultivationRealm.Foundation;
                stage = 7;
                return;

            default:
                realm = CultivationRealm.Mortal;
                stage = 1;
                return;
        }
    }

    Vector2Int GetGatherAmountRange(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return new Vector2Int(3, 5);

            case ItemGrade.Thuong:
                return new Vector2Int(2, 3);

            case ItemGrade.Tien:
                return new Vector2Int(1, 2);

            default:
                return new Vector2Int(5, 8);
        }
    }

    void ConfigureOfferRewardMarkup(
        NpcTaskOffer offer,
        NpcTaskRank rank)
    {
        if (offer == null)
        {
            return;
        }

        float minMarkup = Mathf.Max(minimumTaskRewardMarkup, 0.2f);
        float maxMarkup = minMarkup + 0.15f;

        switch (rank)
        {
            case NpcTaskRank.Trung:
                minMarkup += 0.1f;
                maxMarkup += 0.2f;
                break;

            case NpcTaskRank.Thuong:
                minMarkup += 0.25f;
                maxMarkup += 0.4f;
                break;
        }

        offer.requiredItemRewardMarkupMin = minMarkup;
        offer.requiredItemRewardMarkupMax = Mathf.Max(minMarkup, maxMarkup);
    }

    int EstimateOfferRewardSpiritStone(
        NpcTaskOffer offer,
        StatItemData item)
    {
        if (offer == null || item == null)
        {
            return 0;
        }

        int min = Mathf.Max(1, offer.requiredItemAmountMin);
        int max = Mathf.Max(min, offer.requiredItemAmountMax);
        int estimatedAmount = Mathf.RoundToInt((min + max) * 0.5f);

        return ResolveTaskRewardSpiritStone(
            offer,
            item,
            Mathf.Max(1, estimatedAmount));
    }

    string BuildGatherTaskName(StatItemData item)
    {
        if (item == null)
        {
            return TaskName("gatherResource");
        }

        string itemName = ItemText.Name(item);
        if (item.itemType == ItemType.DanDuoc)
        {
            return TaskNameFormat("pickItemFormat", itemName);
        }

        if (item.itemType == ItemType.ThucPham)
        {
            return BuildHarvestTaskName(item);
        }

        return TaskNameFormat("gatherItemFormat", itemName);
    }

    string BuildHarvestTaskName(StatItemData item)
    {
        return item == null
            ? TaskName("harvestLinhRice")
            : TaskNameFormat("harvestItemFormat", ItemText.Name(item));
    }

    string BuildHuntTaskName(CultivationRealm realm, int stage)
    {
        return TaskNameFormat(
            "huntRealmStageBelowFormat",
            NpcText.Realm(realm),
            Mathf.Clamp(stage, 1, CultivationProgression.MaxStage));
    }

    void ResolveSafeHuntNpcRequirement(
        CultivationRealm targetRealm,
        int targetStage,
        out CultivationRealm npcRealm,
        out int npcStage)
    {
        int targetPower = CultivationProgression.GetRealmPower(
            targetRealm,
            Mathf.Clamp(targetStage, 1, CultivationProgression.MaxStage));
        int requiredPower = targetPower + Mathf.Max(1, huntRequiredPowerMargin);

        int realmIndex =
            Mathf.Clamp(
                requiredPower / CultivationProgression.MaxStage,
                0,
                (int)CultivationRealm.Tribulation);
        int stage = requiredPower % CultivationProgression.MaxStage;
        if (stage == 0)
        {
            stage = CultivationProgression.MaxStage;
            realmIndex = Mathf.Max(0, realmIndex - 1);
        }

        npcRealm = (CultivationRealm)realmIndex;
        npcStage = Mathf.Clamp(stage, 1, CultivationProgression.MaxStage);
    }
    readonly List<RunningNpcTask> runningTasks =
        new List<RunningNpcTask>();

    readonly List<RunningTavernMeal> runningMeals =
        new List<RunningTavernMeal>();

    readonly List<PendingTaskGoods> pendingTaskGoods =
        new List<PendingTaskGoods>();

    float assignTimer;
    int lastTaskGoodsTransferDay = -1;
    Rigidbody2D providerRb;
    Vector3 stationaryPosition;

    void OnEnable()
    {
        if (!providers.Contains(this))
        {
            providers.Add(this);
        }

        CaptureStationaryPosition();
        CaptureEscortAnchorPositions();
        FreezeEscortAnchors();
        ConfigureStationaryProvider();
        EnsureExpandedDefaultOffers();
        NormalizeConfiguredOfferText();
        ResolveConfiguredOfferItemReferences();
    }

    void OnDisable()
    {
        CompleteInterruptedWork();
        providers.Remove(this);
    }

    void RefreshExpandedCatalogWhenIdle()
    {
        if (!useExpandedDefaultTaskCatalog ||
            runningTasks.Count > 0)
        {
            return;
        }

        if (offers == null ||
            offers.Length < Mathf.Max(1, minExpandedTaskOffers))
        {
            offers = BuildExpandedDefaultOffers();
            NormalizeConfiguredOfferText();
        }
    }

    void UpdateTaskCatalogDailyReset()
    {
        int currentDay = GetCurrentWorldDay();
        if (currentDay < 0)
        {
            return;
        }

        if (lastTaskCatalogRefreshDay < 0)
        {
            lastTaskCatalogRefreshDay = currentDay;
            return;
        }

        if (currentDay == lastTaskCatalogRefreshDay &&
            !pendingTaskCatalogRefresh)
        {
            return;
        }

        if (runningTasks.Count > 0)
        {
            pendingTaskCatalogRefresh = true;
            return;
        }

        ResetTaskCatalogForNewDay(currentDay);
    }

    void ResetTaskCatalogForNewDay(int currentDay)
    {
        CleanupCompletedOfferHistory();
        lastCompletedOfferByNpc.Clear();

        if (useExpandedDefaultTaskCatalog)
        {
            offers = BuildExpandedDefaultOffers();
        }

        NormalizeConfiguredOfferText();
        lastTaskCatalogRefreshDay = currentDay;
        pendingTaskCatalogRefresh = false;
    }

}
