using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Máu")]
    public int finalHP = 1000;

    public int currentHP = 1000;

    [Header("UI")]
    public GameObject statusBarPrefab;

    void Start()
    {
        if (statusBarPrefab != null)
        {
            GameObject bar =
                Instantiate(
                    statusBarPrefab,
                    transform);

            mau ui =
                bar.GetComponent<mau>();

            ui.targetStats = this;
        }
    }

    public void ApplyItem(StatItemData item)
    {
        ApplyItem(item, 1);
    }

    public void ApplyItem(StatItemData item, int direction)
    {
        if (item == null)
        {
            return;
        }

        foreach (StatModifier modifier in item.GetAllModifiers())
        {
            if (modifier == null)
            {
                continue;
            }

            switch (modifier.statType)
            {
                case StatType.MaxHP:
                    finalHP += modifier.intValue * direction;
                    currentHP += modifier.intValue * direction;
                    break;

                case StatType.CurrentHP:
                    currentHP += modifier.intValue * direction;
                    break;
            }
        }

        currentHP =
            Mathf.Clamp(currentHP, 0, finalHP);
    }
}
