using UnityEngine;

public partial class NpcTaskProvider
{
    NpcTaskOffer PickOfferFor(GameObject npc)
    {
        return PickOfferFor(npc, false);
    }

    NpcTaskOffer PickOfferFor(GameObject npc, bool autoAssigned)
    {
        if (npc == null ||
            offers == null ||
            offers.Length == 0)
        {
            return null;
        }

        NpcTaskOffer best = null;
        float minScore = GetMinAcceptanceScore(autoAssigned);
        float bestScore = minScore - 0.01f;

        foreach (NpcTaskOffer offer in offers)
        {
            if (!IsOfferWorldAvailable(offer))
            {
                continue;
            }

            float score = GetOfferAcceptanceScore(npc, offer, autoAssigned);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = offer;
        }

        return best;
    }

    bool CanNpcAcceptOffer(GameObject npc, NpcTaskOffer offer)
    {
        return GetOfferAcceptanceScore(npc, offer, false) >=
            GetMinAcceptanceScore(false);
    }

    float GetOfferAcceptanceScore(
        GameObject npc,
        NpcTaskOffer offer,
        bool autoAssigned)
    {
        float score = GetOfferSuitabilityScore(npc, offer);
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

            if (smartNpc.realm < CultivationRealm.Foundation &&
                smartNpc.hunger >= maxTaskAcceptHunger)
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
            CurrentActionContains(action, "receiveTask");
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
            offer.taskType != NpcTaskType.HuntMonster ||
            offer.requiredHuntTargetType == HuntTargetType.Animal ||
            offer.requiredBeastLevel <= 0)
        {
            return true;
        }

        CultivationRealm beastRealm = GetRealmForBeastLevel(offer.requiredBeastLevel);
        int beastPower = CultivationProgression.GetRealmPower(beastRealm, 1);
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

    float GetOfferSuitabilityScore(GameObject npc, NpcTaskOffer offer)
    {
        if (npc == null ||
            offer == null ||
            !NpcRoleUtility.MeetsRealm(npc, offer.minRealm, offer.minRealmStage))
        {
            return 0f;
        }

        if (!IsOfferWorldAvailable(offer))
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

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            switch (villager.job)
            {
                case VillagerJob.Hunter:
                case VillagerJob.Guard:
                    if (offer.taskType == NpcTaskType.HuntMonster ||
                        offer.taskType == NpcTaskType.Patrol ||
                        offer.taskType == NpcTaskType.Escort)
                    {
                        score += 35f;
                    }
                    break;

                case VillagerJob.Farmer:
                case VillagerJob.Fisher:
                    if (offer.taskType == NpcTaskType.GatherResource ||
                        offer.taskType == NpcTaskType.Deliver ||
                        offer.taskType == NpcTaskType.HarvestAndDeliver)
                    {
                        score += 30f;
                    }
                    break;

                case VillagerJob.Alchemist:
                case VillagerJob.Blacksmith:
                    if (offer.taskType == NpcTaskType.Deliver ||
                        offer.taskType == NpcTaskType.GatherResource)
                    {
                        score += 20f;
                    }
                    break;

                case VillagerJob.Trader:
                    if (offer.taskType == NpcTaskType.Deliver ||
                        offer.taskType == NpcTaskType.GatherResource ||
                        offer.taskType == NpcTaskType.HarvestAndDeliver)
                    {
                        score += 20f;
                    }
                    break;
            }

            if (villager.bravery < 45 &&
                (offer.taskType == NpcTaskType.HuntMonster ||
                    offer.taskType == NpcTaskType.Escort))
            {
                score -= 45f;
            }

            if (villager.fatigue >= 70f)
            {
                score -= 20f;
            }
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

            if (offer.taskType == NpcTaskType.Cultivate &&
                smartNpc.canCultivate)
            {
                score += 25f;
            }
        }

        if ((offer.taskType == NpcTaskType.HuntMonster ||
            offer.taskType == NpcTaskType.Patrol ||
            offer.taskType == NpcTaskType.Escort) &&
            NpcMapArea.FindNearestAreaInZone(NpcMapZone.MaThuSonMach, transform.position) != null)
        {
            score += 10f;
        }

        return Mathf.Max(0f, score);
    }

    bool IsOfferWorldAvailable(NpcTaskOffer offer)
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
