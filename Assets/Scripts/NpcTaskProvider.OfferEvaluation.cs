using UnityEngine;

public partial class NpcTaskProvider
{
    NpcTaskOffer PickOfferFor(GameObject npc)
    {
        return PickOfferFor(npc, false);
    }

    NpcTaskOffer PickOfferFor(GameObject npc, bool autoAssigned)
    {
        RefreshExpandedCatalogWhenIdle();

        if (npc == null)
        {
            return null;
        }

        NpcTaskOffer lastCompletedOffer =
            GetLastCompletedOfferForNpc(npc);
        NpcTaskOffer best = null;
        NpcTaskOffer bestNonRepeated = null;
        float minScore = GetMinAcceptanceScore(autoAssigned);
        float bestScore = minScore - 0.01f;
        float bestNonRepeatedScore = minScore - 0.01f;

        foreach (NpcTaskOffer offer in GetVisibleOffers())
        {
            if (!IsOfferWorldAvailable(offer))
            {
                continue;
            }

            float score = GetOfferAcceptanceScore(npc, offer, autoAssigned);
            if (offer != lastCompletedOffer &&
                score > bestNonRepeatedScore)
            {
                bestNonRepeatedScore = score;
                bestNonRepeated = offer;
            }

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = offer;
        }

        return bestNonRepeated ?? best;
    }

    bool CanNpcAcceptOffer(GameObject npc, NpcTaskOffer offer)
    {
        return CanNpcAcceptOffer(npc, offer, false);
    }

    bool CanNpcAcceptOffer(
        GameObject npc,
        NpcTaskOffer offer,
        bool autoAssigned)
    {
        if (!IsOfferAudienceCompatible(npc, offer))
        {
            return false;
        }

        return GetOfferAcceptanceScore(npc, offer, autoAssigned) >=
            GetMinAcceptanceScore(autoAssigned);
    }

    bool IsOfferAudienceCompatible(
        GameObject npc,
        NpcTaskOffer offer)
    {
        if (npc == null ||
            offer == null)
        {
            return false;
        }

        switch (offer.audience)
        {
            case NpcTaskAudience.VillagerOnly:
                return npc.GetComponent<VillagerAI>() != null;

            case NpcTaskAudience.SmartNpcOnly:
                return npc.GetComponent<SmartNpcAI>() != null;

            default:
                return npc.GetComponent<VillagerAI>() != null ||
                    npc.GetComponent<SmartNpcAI>() != null;
        }
    }

    float GetOfferAcceptanceScore(
        GameObject npc,
        NpcTaskOffer offer,
        bool autoAssigned)
    {
        float score = GetOfferSuitabilityScore(npc, offer, autoAssigned);
        if (score <= 0f)
        {
            return 0f;
        }

        if (!requireNpcTaskWillingness)
        {
            return score;
        }

        if (autoAssigned &&
            autoAssignRequiresTaskIntent &&
            !ShouldBypassAutoAssignTaskIntent(offer) &&
            !HasNpcTaskIntent(npc))
        {
            return 0f;
        }

        if (ShouldDeclineTaskByState(npc, offer))
        {
            return 0f;
        }

        return Mathf.Max(0f, score + GetNpcTaskWillingnessBonus(npc, offer));
    }

    static bool ShouldBypassAutoAssignTaskIntent(NpcTaskOffer offer)
    {
        return false;
    }

    float GetMinAcceptanceScore(bool autoAssigned)
    {
        if (!requireNpcTaskWillingness)
        {
            return 0.01f;
        }

        float score = Mathf.Max(0f, minTaskWillingnessScore);
        if (autoAssigned)
        {
            score = Mathf.Max(score, minAutoAssignWillingnessScore);
        }

        return score;
    }

    bool ShouldDeclineTaskByState(GameObject npc, NpcTaskOffer offer)
    {
        if (npc == null || offer == null)
        {
            return true;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            if (rejectNonAdultVillagerTasks &&
                villager.ageGroup != VillagerAgeGroup.Adult)
            {
                return true;
            }

            if (villager.fatigue >= maxTaskAcceptFatigue)
            {
                return true;
            }

            if (villager.realm < CultivationRealm.Foundation &&
                villager.hunger >= maxTaskAcceptHunger)
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.HuntMonster &&
                villager.bravery < minHuntTaskBravery)
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.FrontierWatch &&
                villager.bravery <
                    FrontierDefenseCoordinator.GetMinimumFrontierWatchBravery())
            {
                return true;
            }

            return offer.taskType == NpcTaskType.Escort &&
                villager.bravery < minHuntTaskBravery;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null && smartNpc.enabled)
        {
            if (smartNpc.fatigue >= maxTaskAcceptFatigue)
            {
                return true;
            }

            if (smartNpc.hunger >= maxTaskAcceptHunger)
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.HuntMonster &&
                (!smartNpc.canFight || smartNpc.bravery < minHuntTaskBravery))
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.Escort &&
                (!smartNpc.canFight || smartNpc.bravery < minHuntTaskBravery))
            {
                return true;
            }

            if (offer.taskType == NpcTaskType.FrontierWatch &&
                (!smartNpc.canFight ||
                smartNpc.bravery <
                    FrontierDefenseCoordinator.GetMinimumFrontierWatchBravery()))
            {
                return true;
            }

            return offer.taskType == NpcTaskType.Cultivate &&
                !smartNpc.canCultivate;
        }

        return false;
    }

    float GetNpcTaskWillingnessBonus(GameObject npc, NpcTaskOffer offer)
    {
        float bonus = Mathf.Clamp(
            Mathf.Max(0, offer.rewardSpiritStone) / 1000f,
            0f,
            20f);

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            bonus += Mathf.Clamp(100f - villager.fatigue, 0f, 100f) * 0.08f;

            if (villager.realm < CultivationRealm.Foundation)
            {
                bonus += Mathf.Clamp(100f - villager.hunger, 0f, 100f) * 0.05f;
            }

            bonus += (villager.diligence - 50) * 0.2f;

            if (offer.taskType == NpcTaskType.HuntMonster ||
                offer.taskType == NpcTaskType.Patrol ||
                offer.taskType == NpcTaskType.Escort)
            {
                bonus += (villager.bravery - 50) * 0.25f;
            }

            if (offer.taskType == NpcTaskType.FrontierWatch)
            {
                bonus += (villager.bravery - 35) * 0.2f;
            }
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null && smartNpc.enabled)
        {
            bonus += Mathf.Clamp(100f - smartNpc.fatigue, 0f, 100f) * 0.08f;

            if (smartNpc.realm < CultivationRealm.Foundation)
            {
                bonus += Mathf.Clamp(100f - smartNpc.hunger, 0f, 100f) * 0.05f;
            }

            if ((offer.taskType == NpcTaskType.HuntMonster ||
                offer.taskType == NpcTaskType.Escort) &&
                smartNpc.canFight)
            {
                bonus += 20f;
            }

            if (offer.taskType == NpcTaskType.Cultivate && smartNpc.canCultivate)
            {
                bonus += 20f;
            }

            if (offer.taskType == NpcTaskType.HuntMonster ||
                offer.taskType == NpcTaskType.Patrol ||
                offer.taskType == NpcTaskType.Escort)
            {
                bonus += (smartNpc.bravery - 50) * 0.25f;
            }

            if (offer.taskType == NpcTaskType.FrontierWatch)
            {
                bonus += (smartNpc.bravery - 35) * 0.2f;
            }
        }

        return bonus;
    }

    bool HasNpcTaskIntent(GameObject npc)
    {
        string action = GetNpcCurrentAction(npc);
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        return CurrentActionContains(action, "goTaskProviderDaily") ||
            CurrentActionContains(action, "goVanBaoLauTask") ||
            CurrentActionContains(action, "askProviderFindTask") ||
            CurrentActionContains(action, "returnProviderReceiveTask") ||
            CurrentActionContains(action, "receiveTask") ||
            HasScheduledTaskIntent(npc);
    }

    bool HasScheduledTaskIntent(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        NpcScheduleController schedule = NpcScheduleController.GetSchedule(npc);
        if (schedule == null ||
            !schedule.enforceSchedule ||
            schedule.CurrentSlot == null)
        {
            return false;
        }

        return schedule.CurrentActivity == NpcScheduleActivity.DoMission ||
            schedule.CurrentActivity == NpcScheduleActivity.TakeTask;
    }

    bool CurrentActionContains(string action, string key)
    {
        string text = TaskAction(key);
        if (!string.IsNullOrEmpty(text) &&
            action.Contains(text))
        {
            return true;
        }

        text = NpcText.Action(key);
        return !string.IsNullOrEmpty(text) &&
            action.Contains(text);
    }

    string GetNpcCurrentAction(GameObject npc)
    {
        if (npc == null)
        {
            return string.Empty;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.currentAction;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        return smartNpc != null
            ? smartNpc.currentAction
            : string.Empty;
    }

    bool MeetsHuntBeastPowerRequirement(int npcPower, NpcTaskOffer offer)
    {
        if (!requireNpcPowerAboveBeastLevel ||
            offer == null ||
            offer.taskType != NpcTaskType.HuntMonster)
        {
            return true;
        }

        int beastPower = 0;
        if (offer.useMonsterRealmStageRequirement)
        {
            beastPower = CultivationProgression.GetRealmPower(
                offer.requiredMonsterRealm,
                offer.requiredMonsterMaxStage);
        }
        else if (offer.requiredHuntTargetType != HuntTargetType.Animal &&
            offer.requiredBeastLevel > 0)
        {
            CultivationRealm beastRealm =
                GetRealmForBeastLevel(offer.requiredBeastLevel);
            beastPower = CultivationProgression.GetRealmPower(beastRealm, 1);
        }

        if (beastPower <= 0)
        {
            return true;
        }

        return npcPower >= beastPower + Mathf.Max(0, huntRequiredPowerMargin);
    }

    CultivationRealm GetRealmForBeastLevel(int beastLevel)
    {
        switch (Mathf.Max(1, beastLevel))
        {
            case 1:
                return CultivationRealm.QiRefining;
            case 2:
                return CultivationRealm.Foundation;
            case 3:
                return CultivationRealm.GoldenCore;
            default:
                return CultivationRealm.NascentSoul;
        }
    }

    float GetOfferSuitabilityScore(
        GameObject npc,
        NpcTaskOffer offer,
        bool autoAssigned)
    {
        if (npc == null ||
            offer == null ||
            !NpcRoleUtility.MeetsRealm(npc, offer.minRealm, offer.minRealmStage))
        {
            return 0f;
        }

        if (!IsOfferWorldAvailable(offer, npc))
        {
            return 0f;
        }

        if (autoAssigned &&
            !IsAutoAssignRankAllowed(npc, offer))
        {
            return 0f;
        }

        float score = 10f;
        int npcPower = NpcRoleUtility.GetRealmPower(npc);
        int requiredPower = CultivationProgression.GetRealmPower(
            offer.minRealm,
            Mathf.Clamp(offer.minRealmStage, 1, CultivationProgression.MaxStage));

        score += Mathf.Clamp(npcPower - requiredPower, 0, 80) * 0.25f;

        if (!MeetsHuntBeastPowerRequirement(npcPower, offer))
        {
            return 0f;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null && smartNpc.enabled)
        {
            if (offer.taskType == NpcTaskType.HuntMonster &&
                smartNpc.canFight)
            {
                score += 30f;
            }

            if (offer.taskType == NpcTaskType.Escort &&
                smartNpc.canFight)
            {
                score += 25f;
            }

            if (offer.taskType == NpcTaskType.FrontierWatch &&
                smartNpc.canFight)
            {
                score += 35f;
            }

            if (offer.taskType == NpcTaskType.Cultivate &&
                smartNpc.canCultivate)
            {
                score += 25f;
            }
        }

        if ((offer.taskType == NpcTaskType.HuntMonster ||
            offer.taskType == NpcTaskType.Patrol ||
            offer.taskType == NpcTaskType.FrontierWatch ||
            offer.taskType == NpcTaskType.Escort) &&
            NpcMapArea.FindNearestAreaInZone(NpcMapZone.MaThuSonMach, transform.position) != null)
        {
            score += 10f;
        }

        return Mathf.Max(0f, score);
    }

    bool IsAutoAssignRankAllowed(GameObject npc, NpcTaskOffer offer)
    {
        if (!enforceAutoAssignRankGates ||
            npc == null ||
            offer == null)
        {
            return true;
        }

        switch (offer.rank)
        {
            case NpcTaskRank.Trung:
                return NpcRoleUtility.MeetsRealm(
                    npc,
                    autoAssignTrungMinRealm,
                    autoAssignTrungMinStage);

            case NpcTaskRank.Thuong:
                return NpcRoleUtility.MeetsRealm(
                    npc,
                    autoAssignThuongMinRealm,
                    autoAssignThuongMinStage);

            default:
                return true;
        }
    }

    bool IsOfferWorldAvailable(NpcTaskOffer offer)
    {
        return IsOfferWorldAvailable(offer, null);
    }

    bool IsOfferWorldAvailable(
        NpcTaskOffer offer,
        GameObject npc)
    {
        if (offer == null)
        {
            return false;
        }

        if (IsTaskOfferClaimed(offer))
        {
            return false;
        }

        switch (offer.taskType)
        {
            case NpcTaskType.GatherResource:
            {
                StatItemData requiredItem = ResolveTaskRequiredItem(null, offer);
                return HasAvailableTaskPickup(offer, requiredItem);
            }

            case NpcTaskType.HuntMonster:
                return FindDeathLootForHuntOffer(offer) != null;

            case NpcTaskType.HarvestAndDeliver:
            {
                StatItemData linhRice = ResolveLinhRiceItem();
                return linhRice != null &&
                    HasAvailableTaskPickup(offer, linhRice);
            }

            case NpcTaskType.Patrol:
                return patrolPoint != null || patrolPointB != null;

            case NpcTaskType.Deliver:
                return deliverPoint != null;

            case NpcTaskType.Escort:
                return IsEscortConfigured(offer) &&
                    !IsEscortOfferLocked(offer) &&
                    HasEscortParticipantsAvailable(offer);

            case NpcTaskType.FrontierWatch:
                return FrontierDefenseCoordinator.IsOfferStartAvailable(
                    offer.customTargetId,
                    npc);

            case NpcTaskType.Cultivate:
                return false;

            default:
                return false;
        }
    }

    NpcTaskOffer[] ShuffleOffers()
    {
        if (offers == null ||
            offers.Length == 0)
        {
            return new NpcTaskOffer[0];
        }

        NpcTaskOffer[] shuffled =
            new NpcTaskOffer[offers.Length];

        offers.CopyTo(shuffled, 0);

        for (int i = 0; i < shuffled.Length; i++)
        {
            int swapIndex = Random.Range(i, shuffled.Length);
            NpcTaskOffer temp = shuffled[i];
            shuffled[i] = shuffled[swapIndex];
            shuffled[swapIndex] = temp;
        }

        return shuffled;
    }
}
