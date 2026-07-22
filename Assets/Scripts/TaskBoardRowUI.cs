using TMPro;
using UnityEngine;

public class TaskBoardRowUI : MonoBehaviour
{
    const string LinhRicePlantTaskId = "linh_rice_plant";
    const string LinhRiceCareTaskId = "linh_rice_care";
    const string LinhRiceHarvestTaskId = "linh_rice_harvest";

    public TMP_Text rankText;
    public TMP_Text nameText;
    public TMP_Text typeText;
    public TMP_Text requireText;
    public TMP_Text rewardText;

    public void SetData(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return;
        }

        if (rankText != null)
        {
            rankText.text = Format(
                "rankFormat",
                Get("taskRanks", offer.rank.ToString(), offer.rank.ToString()));
        }

        if (nameText != null)
        {
            nameText.text = BuildDisplayTaskName(offer);
        }

        if (typeText != null)
        {
            typeText.text = Format(
                "typeFormat",
                GetTaskTypeLabel(offer));
        }

        if (requireText != null)
        {
            requireText.text = BuildRequireText(offer);
        }

        if (rewardText != null)
        {
            rewardText.text = BuildRewardText(offer);
        }
    }

    string BuildRequireText(NpcTaskOffer offer)
    {
        string result = Text("requirePrefix");
        bool hasRequire = false;

        if (IsLinhRiceVillageTask(offer))
        {
            result += Format(
                "itemAmountFormat",
                GetLinhRiceObjectiveLabel(offer),
                Mathf.Max(1, offer.requiredAmount));
            return result;
        }

        if (offer.requiredItem != null && offer.requiredAmount > 0)
        {
            result += Format(
                "itemAmountFormat",
                GetItemName(offer.requiredItem),
                offer.requiredAmount);
            hasRequire = true;
        }

        if (offer.taskType == NpcTaskType.HuntMonster)
        {
            if (!hasRequire && offer.useMonsterRealmStageRequirement)
            {
                result += BuildHuntObjective(offer);
            }
            else if (!hasRequire && offer.requiredBeastLevel > 0)
            {
                result += Format(
                    "beastLevelObjective",
                    offer.requiredBeastLevel,
                    offer.requiredMonsterKills);
            }
            else if (!hasRequire)
            {
                result += Format("huntObjective", offer.requiredMonsterKills);
            }

            hasRequire = hasRequire || offer.requiredMonsterKills > 0;
        }
        else if (offer.taskType == NpcTaskType.Escort)
        {
            result += Text("escortObjective");
            hasRequire = true;
        }
        else if (offer.taskType == NpcTaskType.FrontierWatch)
        {
            result += "Trấn thủ Ma Thú Sơn Mạch";
            hasRequire = true;
        }

        if (!hasRequire)
        {
            result += Text("none");
        }

        return result;
    }

    static string BuildDisplayTaskName(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return string.Empty;
        }

        if (IsLinhRiceVillageTask(offer))
        {
            string linhRiceName = GetLinhRiceTaskName(offer);
            if (!string.IsNullOrWhiteSpace(linhRiceName))
            {
                return linhRiceName;
            }
        }

        if (offer.taskType == NpcTaskType.HuntMonster)
        {
            return BuildHuntDisplayTaskName(offer);
        }

        string cleaned =
            NpcText.CleanDisplayText(offer.taskName);
        return string.IsNullOrWhiteSpace(cleaned)
            ? GetShortTaskTypeLabel(offer.taskType)
            : cleaned;
    }

    static string GetTaskTypeLabel(NpcTaskOffer offer)
    {
        if (IsLinhRiceVillageTask(offer))
        {
            return GetLinhRiceTaskTypeLabel(offer);
        }

        return GetShortTaskTypeLabel(
            offer != null
                ? offer.taskType
                : NpcTaskType.GatherResource);
    }

    static string BuildHuntDisplayTaskName(NpcTaskOffer offer)
    {
        string targetLabel = BuildHuntTargetLabel(offer);
        if (string.IsNullOrWhiteSpace(targetLabel))
        {
            return NpcText.CleanDisplayText(offer.taskName);
        }

        return "Tiêu diệt yêu thú " + targetLabel;
    }

    static string BuildHuntObjective(NpcTaskOffer offer)
    {
        string targetLabel = BuildHuntTargetLabel(offer);
        if (string.IsNullOrWhiteSpace(targetLabel))
        {
            return Format("huntObjective", offer.requiredMonsterKills);
        }

        if (offer.requiredMonsterKills <= 1)
        {
            return "Diệt yêu thú " + targetLabel;
        }

        return "Diệt " +
            offer.requiredMonsterKills +
            " yêu thú " +
            targetLabel;
    }

    static string BuildHuntTargetLabel(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return string.Empty;
        }

        if (offer.useMonsterRealmStageRequirement)
        {
            return GetCompactRealmLabel(
                offer.requiredMonsterRealm,
                offer.requiredMonsterMaxStage);
        }

        if (offer.requiredBeastLevel > 0)
        {
            return "bậc " + offer.requiredBeastLevel;
        }

        return string.Empty;
    }

    static string GetCompactRealmLabel(
        CultivationRealm realm,
        int stage)
    {
        int clampedStage =
            Mathf.Clamp(stage, 1, CultivationProgression.MaxStage);

        switch (realm)
        {
            case CultivationRealm.QiRefining:
                return GetCompactRealmTierLabel(
                    "Luyện sơ",
                    "Luyện trung",
                    "Luyện hậu",
                    clampedStage);

            case CultivationRealm.Foundation:
                return GetCompactRealmTierLabel(
                    "Trúc sơ",
                    "Trúc trung",
                    "Trúc hậu",
                    clampedStage);

            case CultivationRealm.GoldenCore:
                return GetCompactRealmTierLabel(
                    "Kim sơ",
                    "Kim trung",
                    "Kim hậu",
                    clampedStage);

            case CultivationRealm.NascentSoul:
                return GetCompactRealmTierLabel(
                    "Nguyên sơ",
                    "Nguyên trung",
                    "Nguyên hậu",
                    clampedStage);

            case CultivationRealm.Tribulation:
                return "Độ kiếp";

            default:
                return NpcText.RealmWithStage(realm, clampedStage);
        }
    }

    static string GetCompactRealmTierLabel(
        string earlyLabel,
        string midLabel,
        string lateLabel,
        int stage)
    {
        if (stage <= 3)
        {
            return earlyLabel;
        }

        if (stage <= 6)
        {
            return midLabel;
        }

        return lateLabel;
    }

    static string GetShortTaskTypeLabel(NpcTaskType taskType)
    {
        switch (taskType)
        {
            case NpcTaskType.GatherResource:
            case NpcTaskType.HarvestAndDeliver:
                return "Thu thập";

            case NpcTaskType.HuntMonster:
                return "Săn";

            case NpcTaskType.Cultivate:
                return "Tu luyện";

            case NpcTaskType.Patrol:
            case NpcTaskType.FrontierWatch:
                return "Tuần tra";

            case NpcTaskType.Deliver:
                return "Vận chuyển";

            case NpcTaskType.Escort:
                return "Hộ tống";

            default:
                return Get("taskTypes", taskType.ToString(), taskType.ToString());
        }
    }

    string GetItemName(StatItemData item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        return ItemText.Name(item);
    }

    string BuildRewardText(NpcTaskOffer offer)
    {
        string result = Text("rewardPrefix");
        bool hasReward = false;

        if (offer.rewardSpiritStone > 0)
        {
            result += Format("spiritStoneReward", offer.rewardSpiritStone);
            hasReward = true;
        }

        if (offer.rewardItem != null && offer.rewardItemAmount > 0)
        {
            if (hasReward)
            {
                result += Text("separator");
            }

            result += Format(
                "itemAmountFormat",
                GetItemName(offer.rewardItem),
                offer.rewardItemAmount);
            hasReward = true;
        }

        if (!hasReward)
        {
            result += Text("none");
        }

        return result;
    }

    static string Text(string key)
    {
        return NpcText.Get("taskBoard", key, key);
    }

    static string Format(string key, params object[] args)
    {
        return NpcText.Format(Text(key), args);
    }

    static string Get(string category, string key, string fallback)
    {
        return NpcText.Get(category, key, fallback);
    }

    static bool IsLinhRiceVillageTask(NpcTaskOffer offer)
    {
        if (offer == null ||
            offer.taskType != NpcTaskType.GatherResource ||
            string.IsNullOrWhiteSpace(offer.customTaskId))
        {
            return false;
        }

        string taskId = offer.customTaskId.Trim();
        return taskId == LinhRicePlantTaskId ||
            taskId == LinhRiceCareTaskId ||
            taskId == LinhRiceHarvestTaskId;
    }

    static string GetLinhRiceTaskName(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return string.Empty;
        }

        if (offer.customTaskId == LinhRicePlantTaskId)
        {
            return "Trồng Linh Mễ";
        }

        if (offer.customTaskId == LinhRiceCareTaskId)
        {
            return "Chăm sóc Linh Mễ";
        }

        if (offer.customTaskId == LinhRiceHarvestTaskId)
        {
            return "Thu hoạch Linh Mễ";
        }

        return string.Empty;
    }

    static string GetLinhRiceTaskTypeLabel(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return string.Empty;
        }

        if (offer.customTaskId == LinhRicePlantTaskId)
        {
            return "Trồng";
        }

        if (offer.customTaskId == LinhRiceCareTaskId)
        {
            return "Chăm sóc";
        }

        if (offer.customTaskId == LinhRiceHarvestTaskId)
        {
            return "Thu hoạch";
        }

        return string.Empty;
    }

    static string GetLinhRiceObjectiveLabel(NpcTaskOffer offer)
    {
        if (offer == null)
        {
            return string.Empty;
        }

        if (offer.customTaskId == LinhRicePlantTaskId)
        {
            return "Trồng";
        }

        if (offer.customTaskId == LinhRiceCareTaskId)
        {
            return "Chăm sóc";
        }

        if (offer.customTaskId == LinhRiceHarvestTaskId)
        {
            return "Linh Mễ";
        }

        return string.Empty;
    }
}
