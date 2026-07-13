using TMPro;
using UnityEngine;

public class TaskBoardRowUI : MonoBehaviour
{
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
            nameText.text = NpcText.CleanDisplayText(offer.taskName);
        }

        if (typeText != null)
        {
            typeText.text = Format(
                "typeFormat",
                Get("taskTypes", offer.taskType.ToString(), offer.taskType.ToString()));
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
            if (hasRequire)
            {
                result += Text("separator");
            }

            if (offer.useMonsterRealmStageRequirement)
            {
                result += BuildHuntRealmObjective(
                    offer.requiredMonsterKills,
                    NpcText.RealmWithStage(
                        offer.requiredMonsterRealm,
                        offer.requiredMonsterMaxStage));
            }
            else if (offer.requiredItem == null && offer.requiredBeastLevel > 0)
            {
                result += Format(
                    "beastLevelObjective",
                    offer.requiredBeastLevel,
                    offer.requiredMonsterKills);
            }
            else
            {
                result += Format("huntObjective", offer.requiredMonsterKills);
            }

            hasRequire = true;
        }
        else if (offer.taskType == NpcTaskType.Escort)
        {
            result += Text("escortObjective");
            hasRequire = true;
        }

        if (!hasRequire)
        {
            result += Text("none");
        }

        return result;
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

    static string BuildHuntRealmObjective(
        int requiredMonsterKills,
        string realmWithStage)
    {
        switch (LocalizationSettings.CurrentLanguageCode)
        {
            case "en":
                return "Defeat " +
                    requiredMonsterKills +
                    " beasts at " +
                    realmWithStage +
                    " or below";
            case "zh":
                return "\u51fb\u8d25 " +
                    requiredMonsterKills +
                    " \u53ea " +
                    realmWithStage +
                    " \u53ca\u4ee5\u4e0b\u5996\u517d";
            default:
                return "Di\u1ec7t " +
                    requiredMonsterKills +
                    " y\u00eau th\u00fa " +
                    realmWithStage +
                    " tr\u1edf xu\u1ed1ng";
        }
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
}
