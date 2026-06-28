using UnityEngine;

public partial class MonsterAI
{
    void UpdateBeastNeeds()
    {
        hunger = Mathf.Clamp(hunger + Time.deltaTime * 0.4f, 0f, 100f);
        AbsorbWorldSpiritualEnergy();

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null)
        {
            aggression = Mathf.Clamp(
                aggression + weather.BeastAggressionBonus() * Time.deltaTime * 0.01f,
                0f,
                100f);
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            timeSystem.IsDangerousNight())
        {
            bloodlust = Mathf.Clamp(
                bloodlust + Time.deltaTime * 0.2f,
                0f,
                100f);
        }

        if (entityProfile != null)
        {
            entityProfile.needs.hunger = hunger;
            entityProfile.emotion.fear = fear;
            SyncEntityProfileStats();
        }
    }

    void AbsorbWorldSpiritualEnergy()
    {
        if (naturalCultivationExpPerSecond <= 0f ||
            realm == CultivationRealm.Tribulation)
        {
            return;
        }

        float hungerRatio = Mathf.Clamp01(hunger / 100f);
        float hungerEfficiency =
            Mathf.Lerp(1f, hungryCultivationEfficiency, hungerRatio);

        float realmEfficiency =
            Mathf.Clamp01(
                CultivationProgression.GetSpiritStoneEfficiency(realm));

        float realmMultiplier =
            CultivationProgression.GetCultivationRealmMultiplier(
                realm,
                realmStage);

        naturalCultivationRemainder +=
            naturalCultivationExpPerSecond *
            hungerEfficiency *
            Mathf.Max(0.05f, realmEfficiency) *
            Mathf.Max(0.1f, realmMultiplier) *
            Time.deltaTime;

        int wholeExp =
            Mathf.FloorToInt(naturalCultivationRemainder);

        if (wholeExp <= 0)
        {
            return;
        }

        naturalCultivationRemainder -= wholeExp;
        AddCultivationExp(wholeExp);
    }

    public long ExpToNextRealm()
    {
        return CultivationProgression.GetExpToNextLong(
            realm,
            realmStage,
            baseExpToNextRealm);
    }

    public void AddCultivationExp(int amount)
    {
        if (amount <= 0 ||
            waitingForHeavenlyTribulation ||
            realm == CultivationRealm.Tribulation)
        {
            return;
        }

        cultivationExp += amount;

        while (!waitingForHeavenlyTribulation &&
            cultivationExp >= ExpToNextRealm() &&
            realm != CultivationRealm.Tribulation)
        {
            cultivationExp -= ExpToNextRealm();
            Breakthrough();
        }

        SyncEntityProfileStats();
    }

    public void Breakthrough()
    {
        if (waitingForHeavenlyTribulation)
        {
            return;
        }

        if (realm == CultivationRealm.Tribulation)
        {
            cultivationExp = 0;
            return;
        }

        if (realm == CultivationRealm.Mortal &&
            realmStage >= CultivationProgression.MaxStage)
        {
            realmStage = 1;
            realm = CultivationRealm.QiRefining;
            RecalculateRealmStats(true);
            currentAction = "Dot pha " + GetRealmText();
            return;
        }

        if (CultivationProgression.RequiresHeavenlyTribulation(
                realm,
                realmStage))
        {
            CultivationRealm targetRealm =
                CultivationProgression.GetNextRealm(realm);

            waitingForHeavenlyTribulation = true;
            currentAction = "Cho thien kiep";
            HeavenlyTribulationSystem.Request(
                gameObject,
                monsterName,
                targetRealm,
                () => CompleteMajorBreakthrough(targetRealm));
            return;
        }

        realmStage += 1;

        RecalculateRealmStats(true);
        currentAction = "Dot pha " + GetRealmText();
    }

    void CompleteMajorBreakthrough(CultivationRealm targetRealm)
    {
        waitingForHeavenlyTribulation = false;
        if (IsDead)
        {
            return;
        }

        realmStage = 1;
        realm = targetRealm;
        RecalculateRealmStats(true);
        currentAction = "Dot pha " + GetRealmText();
    }
}
