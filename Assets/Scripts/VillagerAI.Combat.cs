using UnityEngine;

public partial class VillagerAI
{
    public void TakeDamage(int damage)
    {
        DamageSystem.Apply(this, DamageContext.Legacy(damage));
    }

    public DamageResult ReceiveDamage(DamageContext context)
    {
        EnsureCharacterStatsHealthSource();

        if (characterStats != null)
        {
            DamageResult result = characterStats.ReceiveDamage(context);
            SyncFromCharacterStats();

            if (result.wasApplied)
            {
                InterruptGatheringForCombat();
            }

            if (IsDead)
            {
                Die();
            }
            else if (bravery < 50)
            {
                currentAction = NpcText.Action("panicBurned");
                currentTarget = homePoint;
                NpcSpeechController.TryShowSpeech(gameObject, null, "flee_self");
            }

            result.receiver = this;
            result.target = gameObject;
            return result;
        }

        if (IsDead)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.TargetAlreadyDead);
        }

        int healthBefore = currentHP;
        int finalDamage =
            DamageSystem.CalculateFinalDamage(
                context,
                defense);

        if (finalDamage <= 0)
        {
            return DamageResult.Blocked(
                context,
                this,
                gameObject,
                DamageBlockReason.InvalidAmount);
        }

        currentHP -= finalDamage;
        currentHP = Mathf.Clamp(
            currentHP,
            0,
            Mathf.Max(1, maxHP));

        if (entityProfile != null)
        {
            entityProfile.stats.currentHP = currentHP;
        }

        if (currentHP > 0)
        {
            InterruptGatheringForCombat();
            NpcCombatTechniqueSystem.ReactToDamageTaken(
                gameObject,
                finalDamage);
        }

        if (currentHP <= 0)
        {
            Die();
        }
        else if (bravery < 50)
        {
            currentAction = NpcText.Action("panicBurned");
            currentTarget = homePoint;
            NpcSpeechController.TryShowSpeech(gameObject, null, "flee_self");
        }

        return DamageResult.Applied(
            context,
            this,
            gameObject,
            finalDamage,
            healthBefore,
            currentHP);
    }

    void InterruptGatheringForCombat()
    {
        NpcResourceGatherer gatherer = GetComponent<NpcResourceGatherer>();
        if (gatherer != null)
        {
            gatherer.CancelGatheringNow();
        }

        HarvestJob harvestJob = GetComponent<HarvestJob>();
        if (harvestJob != null)
        {
            harvestJob.CancelHarvestNow();
        }

        ClearMovementTargets();
        StopMoving();
        actionTimer = 0f;
        currentAction = NpcText.Action("injured");
    }
}
