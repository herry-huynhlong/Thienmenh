using UnityEngine;

// Cultivation actions, travel intent, and breakthrough progression.
public partial class SmartNpcAI
{
    void Cultivate()
    {
        if (IsDead || IsRestrictedMapSessionActive())
        {
            return;
        }

        currentAction = NpcText.Action("cultivate");
        NpcSpeechController.TryShowSpeech(gameObject, null, "cultivate_self");
        float cultivateSeconds =
            GameHoursToSeconds(
                Random.Range(
                    cultivationSessionMinGameHours,
                    cultivationSessionMaxGameHours));
        if (dailyRoutineEnabled)
        {
            cultivateSeconds =
                Mathf.Min(
                    cultivateSeconds,
                    GetRemainingScheduledCultivationSeconds());
        }

        actionTimer = Mathf.Max(actionTimer, cultivateSeconds);

        if (TryConsumeAvailablePill())
        {
            int gain =
                Mathf.RoundToInt(
                    50f *
                    GetCultivationMultiplier());

            AddCultivationProgress(gain);

            Debug.Log(NpcText.Format(NpcText.Get("logs", "absorbPill"), npcName, gain));
        }
        else if (spiritStone > 0)
        {
            spiritStone -= 1;

            int gain =
                Mathf.RoundToInt(
                    CultivationProgression.GetSpiritStoneExp(
                        realm,
                        realmStage) *
                    GetCultivationMultiplier());

            AddCultivationProgress(gain);

            Debug.Log(NpcText.Format(NpcText.Get("logs", "absorbSpiritStone"), npcName, gain));
        }
    }

    void CultivateNaturally()
    {
        if (IsDead ||
            waitingForHeavenlyTribulation ||
            IsRestrictedMapSessionActive())
        {
            return;
        }

        if (actionTimer > 0f &&
            (currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi")))
        {
            return;
        }

        if (TryGoToCultivationPoint())
        {
            return;
        }

        if (TryGetCultivationZoneMismatch(
                out NpcMapZone preferredZone,
                out NpcMapZone currentZone))
        {
            ClearTravelTargetsAndStop();
            UpdateCultivationEffect(false);
            actionTimer = Mathf.Max(
                actionTimer,
                GameHoursToSeconds(5f / 60f));
            currentAction = "waitSchedule" + NpcScheduleActivity.Cultivate;
            DebugFlow(
                "Cultivate",
                "Blocked natural cultivate outside preferred zone currentZone=" +
                currentZone +
                " preferredZone=" +
                preferredZone);
            return;
        }

        ClearTravelTargetsAndStop();

        int gain =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    50f *
                    GetCultivationMultiplier()));

        AddCultivationProgress(gain);
        float cultivateSeconds =
            GameHoursToSeconds(
                Random.Range(
                    cultivationSessionMinGameHours,
                    cultivationSessionMaxGameHours));
        if (dailyRoutineEnabled)
        {
            cultivateSeconds =
                Mathf.Min(
                    cultivateSeconds,
                    GetRemainingScheduledCultivationSeconds());
        }

        actionTimer = Mathf.Max(actionTimer, cultivateSeconds);
        currentAction = NpcText.Action("cultivateAbsorbQi");
        NpcSpeechController.TryShowSpeech(gameObject, null, "cultivate_self");
    }

    void ClearCompletedCultivationAction()
    {
        if (actionTimer > 0f)
        {
            return;
        }

        if (currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi"))
        {
            currentAction = NpcText.Action("idle");
            SyncCultivationEffect();
        }

        StopNpcMovement();
    }

    void ClearCompletedMissionAction()
    {
        if (actionTimer > 0f)
        {
            return;
        }

        ClearTravelTargetsAndStop();

        if (!IsStationaryAction(currentAction))
        {
            currentAction = NpcText.Action("idle");
        }
    }

    void ClearCultivationTravelState()
    {
        bool wasCultivatingAction =
            currentAction == NpcText.Action("goCultivatePoint") ||
            currentAction == NpcText.Action("cultivate") ||
            currentAction == NpcText.Action("cultivateAbsorbQi");

        if (IsCultivationTarget(currentTarget))
        {
            currentTarget = null;
        }

        hasCultivationTarget = false;

        if (wasCultivatingAction)
        {
            hasWanderTarget = false;
            if (currentAction != NpcText.Action("idle"))
            {
                currentAction = NpcText.Action("idle");
            }
        }

        UpdateCultivationEffect(false);
    }

    bool TryClearStaleCultivationTravelState()
    {
        if (HasCultivationIntent())
        {
            return false;
        }

        if (!hasCultivationTarget &&
            !IsCultivationTarget(currentTarget) &&
            currentAction != NpcText.Action("goCultivatePoint") &&
            currentAction != NpcText.Action("cultivate") &&
            currentAction != NpcText.Action("cultivateAbsorbQi"))
        {
            return false;
        }

        ClearCultivationTravelState();
        return true;
    }

    bool HasCultivationIntent()
    {
        if (currentSmartTask != null &&
            currentSmartTask.IsValid &&
            currentSmartTask.goal == SmartAITaskGoal.Cultivate)
        {
            return true;
        }

        if (scheduleSmartTask != null &&
            scheduleSmartTask.IsValid &&
            scheduleSmartTask.goal == SmartAITaskGoal.Cultivate)
        {
            return true;
        }

        NpcScheduleController schedule =
            GetComponent<NpcScheduleController>();
        return schedule != null &&
            schedule.enforceSchedule &&
            schedule.CurrentActivity == NpcScheduleActivity.Cultivate;
    }

    bool IsCultivationTarget(Transform target)
    {
        if (target == null ||
            cultivationPoint == null)
        {
            return false;
        }

        return target == cultivationPoint ||
            target.IsChildOf(cultivationPoint) ||
            cultivationPoint.IsChildOf(target);
    }

    bool TryGoToCultivationPoint()
    {
        if (IsRestrictedMapSessionActive())
        {
            return false;
        }

        float cultivationArriveDistance =
            GetCultivationArriveDistance();

        if (hasCultivationTarget &&
            Vector2.Distance(
                transform.position,
                cultivationTarget) <=
            cultivationArriveDistance)
        {
            return false;
        }

        if (currentAction == NpcText.Action("goCultivatePoint") &&
            hasWanderTarget)
        {
            if (Vector2.Distance(transform.position, wanderTarget) <=
                cultivationArriveDistance)
            {
                if (TryRefreshPendingCultivationTravelTarget())
                {
                    return true;
                }

                return false;
            }

            ClearTravelTargets(false);
            return true;
        }

        if (!TryResolveCultivationTravelDestination(
                out Transform targetPoint,
                out Vector3 cultivationPosition))
        {
            return false;
        }

        if (Vector2.Distance(transform.position, cultivationPosition) <=
            cultivationArriveDistance)
        {
            return false;
        }

        ClearTravelTargets();
        currentTarget = targetPoint;
        wanderTarget = cultivationPosition;
        hasWanderTarget = targetPoint == null;
        cultivationTarget = cultivationPosition;
        hasCultivationTarget = true;
        currentAction = NpcText.Action("goCultivatePoint");
        TraceRuntime(
            "TryGoToCultivationPoint",
            "set targetPoint=" +
            (targetPoint != null ? targetPoint.name : "null") +
            " cultivationPosition=" +
            cultivationPosition +
            " hasWander=" +
            hasWanderTarget);
        return true;
    }

    float GetCultivationArriveDistance()
    {
        return Mathf.Max(
            escapeTargetReachDistance,
            targetClearRadius * 2f);
    }

    bool TryResolveCultivationTravelDestination(
        out Transform targetPoint,
        out Vector3 cultivationPosition)
    {
        NpcMapZone? preferredZone =
            ResolveCultivationPreferredZone();
        bool requirePreferredZone =
            TryGetCultivationZoneMismatch(
                out NpcMapZone mismatchedPreferredZone,
                out NpcMapZone currentZone);
        if (requirePreferredZone)
        {
            preferredZone = mismatchedPreferredZone;
        }

        if (TryGetCultivationAreaPosition(
                preferredZone,
                requirePreferredZone,
                out cultivationPosition,
                out NpcMapZone? configuredZone))
        {
            targetPoint = null;
            hasCultivationTarget = false;
            DebugFlow(
                "Cultivate",
                "Use configured cultivation area cultivationPosition=" +
                cultivationPosition +
                " cultivationZone=" +
                (configuredZone.HasValue
                    ? configuredZone.Value.ToString()
                    : "None") +
                " preferredZone=" +
                (preferredZone.HasValue
                    ? preferredZone.Value.ToString()
                    : "None") +
                " currentPos=" +
                transform.position);
            TraceRuntime(
                "TryResolveCultivationTravelDestination",
                "configured cultivationPosition=" +
                cultivationPosition +
                " zone=" +
                (configuredZone.HasValue
                    ? configuredZone.Value.ToString()
                    : "None") +
                " preferredZone=" +
                (preferredZone.HasValue
                    ? preferredZone.Value.ToString()
                    : "None"));
            return true;
        }

        targetPoint = cultivationPoint;
        cultivationPosition =
            targetPoint != null
                ? targetPoint.position
                : Vector3.zero;

        if (targetPoint != null)
        {
            NpcMapZone? destinationZone =
                NpcMapNavigator.GetDestinationZone(targetPoint);
            NpcMapArea targetArea =
                NpcMapArea.FindArea(targetPoint.position);
            if (destinationZone.HasValue &&
                (targetArea == null ||
                targetArea.zone != destinationZone.Value))
            {
                if (TryGetCultivationAreaPosition(
                        destinationZone,
                        true,
                        out cultivationPosition,
                        out NpcMapZone? fallbackZone))
                {
                    targetPoint = null;
                    DebugFlow(
                        "Cultivate",
                        "Fallback to configured cultivation area targetPoint=null destinationZone=" +
                        destinationZone.Value +
                        " cultivationZone=" +
                        (fallbackZone.HasValue
                            ? fallbackZone.Value.ToString()
                            : "None") +
                        " cultivationPosition=" +
                        cultivationPosition +
                        " currentPos=" +
                        transform.position);
                    TraceRuntime(
                        "TryResolveCultivationTravelDestination",
                        "fallback-configured destinationZone=" +
                        destinationZone.Value +
                        " cultivationZone=" +
                        (fallbackZone.HasValue
                            ? fallbackZone.Value.ToString()
                            : "None") +
                        " cultivationPosition=" +
                        cultivationPosition);
                    return true;
                }

                NpcMapArea destinationArea =
                    NpcMapArea.FindNearestAreaInZone(
                        destinationZone.Value,
                        targetPoint.position);
                if (destinationArea != null)
                {
                    cultivationPosition =
                        destinationArea.ClosestPoint(targetPoint.position);
                    targetPoint = null;
                    DebugFlow(
                        "Cultivate",
                        "Fallback to nearest area destinationZone=" +
                        destinationZone.Value +
                        " area=" +
                        destinationArea.name +
                        " cultivationPosition=" +
                        cultivationPosition +
                        " currentPos=" +
                        transform.position);
                    TraceRuntime(
                        "TryResolveCultivationTravelDestination",
                        "fallback-nearest destinationZone=" +
                        destinationZone.Value +
                        " area=" +
                        destinationArea.name +
                        " cultivationPosition=" +
                        cultivationPosition);
                    return true;
                }
            }

            DebugFlow(
                "Cultivate",
                "Use direct cultivation target targetPoint=" +
                targetPoint.name +
                " targetZone=" +
                (destinationZone.HasValue
                    ? destinationZone.Value.ToString()
                    : "None") +
                " cultivationPosition=" +
                cultivationPosition);
            return true;
        }

        bool foundCultivationPosition =
            TryGetCultivationAreaPosition(
                preferredZone,
                requirePreferredZone,
                out cultivationPosition,
                out NpcMapZone? fallbackCultivationZone);

        if (!foundCultivationPosition &&
            requirePreferredZone &&
            preferredZone.HasValue &&
            TryResolveCultivationZoneEntryPosition(
                preferredZone.Value,
                out cultivationPosition))
        {
            foundCultivationPosition = true;
            fallbackCultivationZone = preferredZone.Value;
            DebugFlow(
                "Cultivate",
                "Fallback to preferred zone entry preferredZone=" +
                preferredZone.Value +
                " currentZone=" +
                currentZone +
                " cultivationPosition=" +
                cultivationPosition +
                " currentPos=" +
                transform.position);
            TraceRuntime(
                "TryResolveCultivationTravelDestination",
                "fallback-zone-entry preferredZone=" +
                preferredZone.Value +
                " cultivationPosition=" +
                cultivationPosition);
        }

        DebugFlow(
            "Cultivate",
            foundCultivationPosition
                ? "Use fallback cultivation area cultivationPosition=" +
                    cultivationPosition +
                    " cultivationZone=" +
                    (fallbackCultivationZone.HasValue
                        ? fallbackCultivationZone.Value.ToString()
                        : "None") +
                    " preferredZone=" +
                    (preferredZone.HasValue
                        ? preferredZone.Value.ToString()
                        : "None") +
                    " currentPos=" +
                    transform.position
                : "No cultivation destination found currentPos=" +
                    transform.position);

        return foundCultivationPosition;
    }

    bool TryRefreshPendingCultivationTravelTarget()
    {
        float cultivationArriveDistance =
            GetCultivationArriveDistance();

        if (currentTarget == null &&
            hasWanderTarget &&
            hasCultivationTarget &&
            Vector2.Distance(wanderTarget, cultivationTarget) <=
                cultivationArriveDistance)
        {
            return false;
        }

        if (!TryResolveCultivationTravelDestination(
                out Transform targetPoint,
                out Vector3 cultivationPosition))
        {
            return false;
        }

        if (Vector2.Distance(transform.position, cultivationPosition) <=
            cultivationArriveDistance)
        {
            return false;
        }

        bool alreadyUsingTarget =
            currentTarget == targetPoint &&
            targetPoint != null;
        if (!alreadyUsingTarget &&
            targetPoint == null &&
            hasWanderTarget &&
            Vector2.Distance(wanderTarget, cultivationPosition) <=
                cultivationArriveDistance)
        {
            alreadyUsingTarget = true;
        }

        if (alreadyUsingTarget)
        {
            return false;
        }

        ClearTravelTargets();
        currentTarget = targetPoint;
        wanderTarget = cultivationPosition;
        hasWanderTarget = targetPoint == null;
        cultivationTarget = cultivationPosition;
        hasCultivationTarget = true;
        currentAction = NpcText.Action("goCultivatePoint");
        TraceRuntime(
            "TryRefreshPendingCultivationTravelTarget",
            "refresh targetPoint=" +
            (targetPoint != null ? targetPoint.name : "null") +
            " cultivationPosition=" +
            cultivationPosition +
            " hasWander=" +
            hasWanderTarget);
        return true;
    }

    void AddCultivationProgress(int amount)
    {
        if (IsDead || waitingForHeavenlyTribulation)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.AddCultivationExp(amount);
            SyncFromCharacterStats();
            return;
        }

        cultivation += amount;

        while (!waitingForHeavenlyTribulation &&
            cultivation >= breakthroughNeed &&
            realm != CultivationRealm.Tribulation)
        {
            cultivation -= breakthroughNeed;
            Breakthrough();
        }
    }

    public float GetCultivationMultiplier()
    {
        float multiplier =
            CultivationProgression.GetCultivationRealmMultiplier(
                realm,
                realmStage);

        if (physique ==
            PhysiqueType.MortalBody)
        {
            multiplier *= 1f;
        }
        else if (physique ==
            PhysiqueType.FiveElementBody)
        {
            multiplier *= 10f;
        }
        else if (physique ==
            PhysiqueType.ChaosBody)
        {
            multiplier *= 100f;
        }

        multiplier +=
            Mathf.Max(1f, comprehension / 10f);

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null)
        {
            multiplier *= weather.CultivationMultiplier();
        }

        HeavenDaoSystem heavenDao = HeavenDaoSystem.Instance;
        if (heavenDao != null)
        {
            multiplier *= heavenDao.GetWorldSpiritQiMultiplier();
        }

        return multiplier;
    }

    void Breakthrough()
    {
        if (waitingForHeavenlyTribulation)
        {
            return;
        }

        if (characterStats != null)
        {
            characterStats.Breakthrough();
            SyncFromCharacterStats();
            currentAction = characterStats.waitingForHeavenlyTribulation
                ? NpcText.Action("waitTribulation")
                : NpcText.Action("breakthrough");
            return;
        }

        if (realm ==
            CultivationRealm.Tribulation)
        {
            readyForHeavenlyTribulation = true;

            currentAction = NpcText.Action("waitTribulation");

            Debug.Log(NpcText.Format(NpcText.Get("logs", "tribulationReady"), npcName));

            return;
        }

        cultivation = 0;

        if (realm == CultivationRealm.Mortal &&
            realmStage >= CultivationProgression.MaxStage)
        {
            realmStage = 1;
            realm = CultivationRealm.QiRefining;
            ApplyRealmPower(true);
            lifespan = GetLifespanForRealm(realm);
            currentAction = NpcText.Action("breakthrough");
            return;
        }

        if (CultivationProgression.RequiresHeavenlyTribulation(
                realm,
                realmStage))
        {
            CultivationRealm targetRealm =
                CultivationProgression.GetNextRealm(realm);

            waitingForHeavenlyTribulation = true;
            currentAction = NpcText.Action("waitTribulation");
            HeavenlyTribulationSystem.Request(
                gameObject,
                npcName,
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

        ApplyRealmPower(true);
        lifespan = GetLifespanForRealm(realm);

        currentAction = NpcText.Action("breakthrough");

        Debug.Log(NpcText.Format(NpcText.Get("logs", "breakthrough"), npcName, GetRealmName(), realmStage));
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
        ApplyRealmPower(true);
        lifespan = GetLifespanForRealm(realm);
        currentAction = NpcText.Action("breakthrough");
        Debug.Log(NpcText.Format(NpcText.Get("logs", "breakthrough"), npcName, GetRealmName(), realmStage));
    }
}
