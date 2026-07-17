using UnityEngine;

public partial class MonsterAI
{
    void UpdateBeastNeeds()
    {
        EnsureTemperamentInitialized();
        hunger = Mathf.Clamp(hunger + Time.deltaTime * 0.4f, 0f, 100f);
        AbsorbWorldSpiritualEnergy();

        transientAggressionBonus = Mathf.MoveTowards(
            transientAggressionBonus,
            0f,
            Mathf.Max(0f, aggressionSurgeDecayPerSecond) * Time.deltaTime);
        transientBloodlustBonus = Mathf.MoveTowards(
            transientBloodlustBonus,
            0f,
            Mathf.Max(0f, bloodlustSurgeDecayPerSecond) * Time.deltaTime);

        float weatherAggressionBonus = 0f;
        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null)
        {
            weatherAggressionBonus = Mathf.Max(0f, weather.BeastAggressionBonus());
        }

        float dangerousNightBonus = 0f;
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null &&
            timeSystem.IsDangerousNight())
        {
            dangerousNightBonus =
                Mathf.Clamp(dangerousNightBloodlustBonus, 0f, 100f);
        }

        float targetAggression = Mathf.Clamp(
            baseAggression +
            transientAggressionBonus +
            weatherAggressionBonus,
            0f,
            100f);
        float targetBloodlust = Mathf.Clamp(
            baseBloodlust +
            transientBloodlustBonus +
            dangerousNightBonus,
            0f,
            100f);

        aggression = Mathf.MoveTowards(
            aggression,
            targetAggression,
            Mathf.Max(0f, aggressionResponsePerSecond) * Time.deltaTime);
        bloodlust = Mathf.MoveTowards(
            bloodlust,
            targetBloodlust,
            Mathf.Max(0f, bloodlustResponsePerSecond) * Time.deltaTime);

        if (entityProfile != null)
        {
            entityProfile.needs.hunger = hunger;
            entityProfile.emotion.fear = fear;
            SyncEntityProfileStats();
        }
    }

    void EnsureTemperamentInitialized()
    {
        if (temperamentInitialized)
        {
            return;
        }

        CaptureTemperamentBaselineFromCurrent(false);
    }

    void CaptureTemperamentBaselineFromCurrent(bool snapCurrentValues)
    {
        baseAggression = Mathf.Clamp(aggression, 0f, 100f);
        baseBloodlust = Mathf.Clamp(bloodlust, 0f, 100f);
        transientAggressionBonus = 0f;
        transientBloodlustBonus = 0f;
        temperamentInitialized = true;

        if (!snapCurrentValues)
        {
            return;
        }

        aggression = baseAggression;
        bloodlust = baseBloodlust;
    }

    public void ApplyTemperamentSurge(
        float aggressionBonus,
        float bloodlustBonus)
    {
        EnsureTemperamentInitialized();

        transientAggressionBonus = Mathf.Clamp(
            transientAggressionBonus + Mathf.Max(0f, aggressionBonus),
            0f,
            100f);
        transientBloodlustBonus = Mathf.Clamp(
            transientBloodlustBonus + Mathf.Max(0f, bloodlustBonus),
            0f,
            100f);

        aggression = Mathf.Clamp(
            aggression + Mathf.Max(0f, aggressionBonus),
            0f,
            100f);
        bloodlust = Mathf.Clamp(
            bloodlust + Mathf.Max(0f, bloodlustBonus),
            0f,
            100f);
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
            ResolveWorldSpiritQiMultiplier() *
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

    float ResolveWorldSpiritQiMultiplier()
    {
        HeavenDaoSystem heavenDao = HeavenDaoSystem.Instance;
        return heavenDao != null
            ? heavenDao.GetWorldSpiritQiMultiplier()
            : 1f;
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
                () => CompleteMajorBreakthrough(targetRealm),
                passed =>
                {
                    if (!passed)
                    {
                        waitingForHeavenlyTribulation = false;
                    }
                });
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
