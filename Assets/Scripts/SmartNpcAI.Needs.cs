using UnityEngine;

public partial class SmartNpcAI
{
    void UpdateNeeds()
    {
        if (IsDead)
        {
            return;
        }

        if (NeedsFood())
        {
            hunger += Time.deltaTime *
                (realm == CultivationRealm.QiRefining ? 0.015f : 0.05f);
        }
        else
        {
            hunger = 0f;
        }

        fatigue += Time.deltaTime * 0.04f;

        if (entityProfile != null)
        {
            entityProfile.needs.hunger = Mathf.Clamp(hunger, 0f, 100f);
            entityProfile.needs.fatigue = Mathf.Clamp(fatigue, 0f, 100f);
        }
    }

    void Eat()
    {
        hunger = 0;

        money -= 5;

        if (money < 0)
        {
            money = 0;
        }

        currentAction = NpcText.Action("eating");
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(0.5f));

        Debug.Log(NpcText.Format(NpcText.Get("logs", "eat"), npcName));
    }

    void Sleep()
    {
        currentAction = NpcText.Action("rest");
        actionTimer = Mathf.Max(actionTimer, GameHoursToSeconds(2f));

        fatigue = 0;

        if (characterStats != null)
        {
            characterStats.currentHP =
                Mathf.Min(
                    characterStats.finalHP,
                    characterStats.currentHP + 30);
            SyncFromCharacterStats();
        }
        else
        {
            currentHP += 30;

            if (currentHP > maxHP)
            {
                currentHP = maxHP;
            }
        }

        Debug.Log(NpcText.Format(NpcText.Get("logs", "sleep"), npcName));
    }
}
