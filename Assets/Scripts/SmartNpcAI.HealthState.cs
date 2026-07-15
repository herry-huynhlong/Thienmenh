using UnityEngine;

// Character-stat synchronization and core health-state accessors.
public partial class SmartNpcAI
{
    void SyncFromCharacterStats()
    {
        if (characterStats == null)
        {
            return;
        }

        realm = characterStats.realm;
        realmStage = characterStats.realmStage;
        cultivation = characterStats.cultivationExp;
        breakthroughNeed = characterStats.ExpToNextRealm();
        waitingForHeavenlyTribulation =
            characterStats.waitingForHeavenlyTribulation;
        maxHP = characterStats.finalHP;
        currentHP = characterStats.currentHP;
        attack = characterStats.attack;
        defense = characterStats.defense;
        effectResistance = characterStats.effectResistance;
        moveSpeed = characterStats.moveSpeed;
    }

    public int AuthoritativeCurrentHP
    {
        get
        {
            characterStats = characterStats != null
                ? characterStats
                : GetComponent<CharacterStats>();
            return characterStats != null
                ? characterStats.CurrentHP
                : Mathf.Clamp(currentHP, 0, Mathf.Max(1, maxHP));
        }
    }

    public int AuthoritativeMaxHP
    {
        get
        {
            characterStats = characterStats != null
                ? characterStats
                : GetComponent<CharacterStats>();
            return characterStats != null
                ? characterStats.MaxHP
                : Mathf.Max(1, maxHP);
        }
    }

    public void RestoreHealthState(int restoredMaxHP, int restoredCurrentHP)
    {
        EnsureCharacterStatsHealthSource();
        characterStats.RestoreHealthState(restoredMaxHP, restoredCurrentHP);
        SyncFromCharacterStats();

        if (currentHP > 0)
        {
            isDead = false;
        }
    }

    public void SetCurrentHealth(int value)
    {
        EnsureCharacterStatsHealthSource();
        characterStats.SetCurrentHP(value);
        SyncFromCharacterStats();

        if (currentHP > 0)
        {
            isDead = false;
        }
    }

    public int Heal(int amount)
    {
        EnsureCharacterStatsHealthSource();
        int healed = characterStats.Heal(amount);
        SyncFromCharacterStats();

        if (currentHP > 0)
        {
            isDead = false;
        }

        return healed;
    }

    void EnsureCharacterStatsHealthSource()
    {
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }

        if (characterStats == null)
        {
            characterStats = gameObject.AddComponent<CharacterStats>();
        }

        characterStats.generatedEntityKind = EntityKind.Cultivator;
        characterStats.generateFromEntityProfile = generateFromEntityProfile;
        if (entityProfile == null)
        {
            entityProfile = characterStats.entityProfile;
        }

        if (entityProfile != null)
        {
            characterStats.entityProfile = entityProfile;
        }
    }
}
