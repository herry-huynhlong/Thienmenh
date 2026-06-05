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
            return;

        if (rankText != null)
            rankText.text = "[" + offer.rank.ToString() + "]";

        if (nameText != null)
            nameText.text = offer.taskName;

        if (typeText != null)
            typeText.text = "Loại: " + offer.taskType.ToString();

        if (requireText != null)
            requireText.text = BuildRequireText(offer);

        if (rewardText != null)
            rewardText.text = BuildRewardText(offer);
    }

    private string BuildRequireText(NpcTaskOffer offer)
    {
        string result = "Yeu cau: ";
        bool hasRequire = false;

        if (offer.requiredItem != null && offer.requiredAmount > 0)
        {
            result += GetItemName(offer.requiredItem) + " x" + offer.requiredAmount;
            hasRequire = true;
        }

        if (offer.taskType == NpcTaskType.HuntMonster)
        {
            if (hasRequire)
            {
                result += ", ";
            }

            if (offer.requiredItem == null && offer.requiredBeastLevel > 0)
            {
                result += "Yeu thu cap " + offer.requiredBeastLevel + " x" + offer.requiredMonsterKills;
            }
            else
            {
                result += "Diet yeu thu x" + offer.requiredMonsterKills;
            }

            hasRequire = true;
        }

        if (!hasRequire)
        {
            result += "Khong co";
        }

        return result;
    }

    string GetItemName(StatItemData item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrEmpty(item.itemName)
            ? item.itemName
            : item.name;
    }

    private string BuildRewardText(NpcTaskOffer offer)
    {
        string result = "Thưởng: ";

        bool hasReward = false;

        if (offer.rewardSpiritStone > 0)
        {
            result += offer.rewardSpiritStone + " Linh Thạch";
            hasReward = true;
        }

        if (offer.rewardCultivationExp > 0)
        {
            if (hasReward)
                result += ", ";

            result += offer.rewardCultivationExp + " EXP";
            hasReward = true;
        }

        if (offer.rewardItem != null && offer.rewardItemAmount > 0)
        {
            if (hasReward)
                result += ", ";

            result += GetItemName(offer.rewardItem) + " x" + offer.rewardItemAmount;
            hasReward = true;
        }

        if (!hasReward)
        {
            result += "Không có";
        }

        return result;
    }
}