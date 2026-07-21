using System.Collections.Generic;
using UnityEngine;

public partial class FrontierDefenseCoordinator
{
    void UpdateBeastWave()
    {
        if (activeBeastWave != null)
        {
            UpdateActiveBeastWave();
            return;
        }

        if (Time.time < nextBeastWaveAllowedTime)
        {
            return;
        }

        float triggerThreat =
            Mathf.Clamp(
                beastWaveTriggerThreatPercent,
                1f,
                100f);
        float currentThreatPercent =
            Mathf.Clamp(
                currentFrontierThreat /
                Mathf.Max(1f, maxFrontierThreat) * 100f,
                0f,
                100f);
        if (currentThreatPercent < triggerThreat)
        {
            return;
        }

        TryStartBeastWave();
    }

    void TryStartBeastWave()
    {
        FrontierBattleLine line = FindBestBattleLine();
        if (line == null)
        {
            return;
        }

        ActiveBeastWave wave = new ActiveBeastWave
        {
            line = line,
            startedAtTime = Time.time
        };

        DraftDefenders(wave);
        if (wave.defenders.Count == 0)
        {
            return;
        }

        SpawnWaveMonsters(wave);

        if (wave.monsters.Count == 0)
        {
            ReleaseWaveDefenders(wave);
            return;
        }

        activeBeastWave = wave;
        CommandWaveMonstersToStaging(wave);
        TriggerWaveWarningSignals(wave);
        float preparationHours =
            Random.Range(
                Mathf.Max(0.5f, beastWavePreparationMinWorldHours),
                Mathf.Max(
                    beastWavePreparationMinWorldHours,
                    beastWavePreparationMaxWorldHours));
        float preparationSeconds =
            GameTime.WorldHoursToScaledSeconds(
                preparationHours);
        wave.combatStartsAtTime =
            Time.time + preparationSeconds;
        wave.endAtTime =
            wave.combatStartsAtTime +
            GameTime.WorldHoursToScaledSeconds(
                beastWaveDurationWorldHours);
        nextBeastWaveAllowedTime =
            Time.time +
            GameTime.WorldHoursToScaledSeconds(
                beastWaveCooldownWorldHours);

        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                "beastWaveWarning",
                line.GetDisplayName(),
                wave.defenders.Count,
                wave.monsters.Count,
                Mathf.CeilToInt(preparationHours)));
    }

    void UpdateActiveBeastWave()
    {
        if (activeBeastWave == null)
        {
            return;
        }

        if (!activeBeastWave.combatStarted)
        {
            if (Time.time < activeBeastWave.combatStartsAtTime)
            {
                return;
            }

            StartBeastWaveCombat(activeBeastWave);
        }

        int aliveMonsters =
            CountAliveMonsters(activeBeastWave.monsters);
        int aliveDefenders =
            CountAliveDefenders(activeBeastWave.defenders);
        int retreatThreshold =
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    activeBeastWave.totalSpawnedMonsters * 0.5f));

        if (aliveMonsters <= retreatThreshold)
        {
            FinishBeastWave(true, aliveMonsters, aliveDefenders);
            return;
        }

        if (aliveDefenders <= 0 &&
            Time.time >= activeBeastWave.startedAtTime + 5f)
        {
            FinishBeastWave(false, aliveMonsters, aliveDefenders);
            return;
        }

        if (Time.time >= activeBeastWave.endAtTime)
        {
            bool success =
                aliveMonsters <=
                Mathf.CeilToInt(
                    activeBeastWave.totalSpawnedMonsters * 0.6f);
            FinishBeastWave(success, aliveMonsters, aliveDefenders);
        }
    }

    void StartBeastWaveCombat(ActiveBeastWave wave)
    {
        if (wave == null ||
            wave.combatStarted)
        {
            return;
        }

        wave.combatStarted = true;

        for (int i = 0; i < wave.monsters.Count; i++)
        {
            ActivateWaveMonsterCombat(
                wave.monsters[i],
                wave.line,
                i);
        }

        if (WorldEventSystem.Instance != null)
        {
            WorldEventSystem.Instance.TriggerEvent(WorldEventType.BeastWave);
        }

        AddUrgentLog(
            UiText.Format(
                "frontierDefense",
                "beastWaveStarted",
                wave.line != null
                    ? wave.line.GetDisplayName()
                    : "Ma Thu Son Mach",
                wave.defenders.Count,
                wave.monsters.Count));
    }

    void FinishBeastWave(
        bool success,
        int aliveMonsters,
        int aliveDefenders)
    {
        if (activeBeastWave == null)
        {
            return;
        }

        FrontierBattleLine line = activeBeastWave.line;
        if (success)
        {
            currentFrontierThreat =
                Mathf.Max(
                    0f,
                    currentFrontierThreat -
                    Mathf.Max(0f, threatReductionOnVictory));
            AddUrgentLog(
                UiText.Format(
                    "frontierDefense",
                    "beastWaveRepelled",
                    line != null ? line.GetDisplayName() : "Ma Thu Son Mach",
                    aliveDefenders));
        }
        else
        {
            currentFrontierThreat =
                Mathf.Max(
                    0f,
                    currentFrontierThreat -
                    Mathf.Max(0f, threatReductionOnFailure));
            AddUrgentLog(
                UiText.Format(
                    "frontierDefense",
                    "beastWaveBreached",
                    line != null ? line.GetDisplayName() : "Ma Thu Son Mach",
                    aliveMonsters));
        }

        ReleaseWaveDefenders(activeBeastWave);
        ReleaseWaveMonsters(activeBeastWave);
        activeBeastWave = null;
    }

    FrontierBattleLine FindBestBattleLine()
    {
        FrontierBattleLine best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < battleLines.Count; i++)
        {
            FrontierBattleLine line = battleLines[i];
            if (line == null ||
                !line.isActiveAndEnabled ||
                !line.HasBattleArea)
            {
                continue;
            }

            int score =
                Mathf.Max(1, line.defenderCount) +
                Mathf.Max(1, line.monsterCount);
            if (score > bestScore)
            {
                best = line;
                bestScore = score;
            }
        }

        return best;
    }

    void DraftDefenders(ActiveBeastWave wave)
    {
        if (wave == null ||
            wave.line == null)
        {
            return;
        }

        SmartNpcAI[] npcs =
            FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude);
        System.Array.Sort(
            npcs,
            (left, right) =>
            {
                float rightScore = GetDefenderScore(right, wave.line);
                float leftScore = GetDefenderScore(left, wave.line);
                return rightScore.CompareTo(leftScore);
            });

        int targetCount =
            Mathf.Max(
                1,
                wave.line.defenderCount);
        float defenseDuration =
            GameTime.WorldHoursToScaledSeconds(
                defenderDraftWorldHours);

        for (int i = 0; i < npcs.Length && wave.defenders.Count < targetCount; i++)
        {
            SmartNpcAI npc = npcs[i];
            if (!IsValidWaveDefender(npc))
            {
                continue;
            }

            wave.defenders.Add(npc);
            Vector3 defensePoint =
                wave.line.GetDefenderPoint(
                    wave.defenders.Count - 1);
            npc.EnterFrontierDefenseMode(
                defensePoint,
                defenseDuration,
                "frontier beast wave");
        }
    }

    float GetDefenderScore(
        SmartNpcAI npc,
        FrontierBattleLine line)
    {
        if (!IsValidWaveDefender(npc))
        {
            return float.NegativeInfinity;
        }

        float power =
            CultivationProgression.GetRealmPower(
                npc.realm,
                npc.realmStage);
        float distance =
            Vector2.Distance(
                npc.transform.position,
                line.GetBattleCenter());
        float emergencyBonus =
            npc.IsInFrontierDefenseMode
                ? 1000f
                : 0f;

        return emergencyBonus +
            power * 0.035f +
            npc.bravery * 0.75f -
            distance * 0.15f;
    }

    bool IsValidWaveDefender(SmartNpcAI npc)
    {
        return npc != null &&
            npc.enabled &&
            !npc.IsDead &&
            !BicanhSessionManager.IsDungeonParticipant(npc.gameObject) &&
            !NpcMapBehaviorPolicy.IsRestrictedSessionParticipant(npc.gameObject) &&
            npc.canFight &&
            NpcRoleUtility.MeetsRealm(
                npc.gameObject,
                CultivationRealm.Foundation,
                1);
    }

    void SpawnWaveMonsters(ActiveBeastWave wave)
    {
        if (wave == null ||
            wave.line == null)
        {
            return;
        }

        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);
        System.Array.Sort(
            monsters,
            (left, right) =>
            {
                float leftDistance =
                    GetBattleMonsterDistanceScore(left, wave.line);
                float rightDistance =
                    GetBattleMonsterDistanceScore(right, wave.line);
                return leftDistance.CompareTo(rightDistance);
            });

        int targetCount =
            Mathf.Max(1, wave.line.monsterCount);
        for (int i = 0; i < monsters.Length && wave.monsters.Count < targetCount; i++)
        {
            MonsterAI monster = monsters[i];
            if (!IsValidWaveMonster(monster, wave.line))
            {
                continue;
            }

            PrepareWaveMonster(
                monster,
                wave.line,
                wave.monsters.Count);
            wave.monsters.Add(monster);
        }

        wave.totalSpawnedMonsters = wave.monsters.Count;
    }

    int CountEligibleWaveMonsters(FrontierBattleLine line)
    {
        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);
        int count = 0;

        for (int i = 0; i < monsters.Length; i++)
        {
            if (IsValidWaveMonster(monsters[i], line))
            {
                count++;
            }
        }

        return count;
    }

    bool IsValidWaveMonster(
        MonsterAI monster,
        FrontierBattleLine line)
    {
        if (monster == null ||
            monster.IsDead ||
            !monster.gameObject.activeInHierarchy)
        {
            return false;
        }

        NpcMapZone expectedZone =
            line != null
                ? line.battleZone
                : NpcMapZone.MaThuSonMach;
        NpcMapZone? actualZone =
            NpcMapNavigator.ResolveActorZone(
                monster.gameObject);
        return actualZone.HasValue &&
            actualZone.Value == expectedZone;
    }

    float GetBattleMonsterDistanceScore(
        MonsterAI monster,
        FrontierBattleLine line)
    {
        if (monster == null ||
            line == null)
        {
            return float.PositiveInfinity;
        }

        return Vector2.Distance(
            monster.transform.position,
            line.GetBattleCenter());
    }

    void PrepareWaveMonster(
        MonsterAI monster,
        FrontierBattleLine line,
        int index)
    {
        if (monster == null)
        {
            return;
        }

        monster.generateFromEntityProfile = true;
        monster.autoStatsFromRealm = true;
        monster.syncBeastLevelFromRealm = true;
        monster.guardTerritory = true;
        monster.roamRadius = Mathf.Max(monster.roamRadius, 6f);
        monster.territoryRadius = Mathf.Max(monster.territoryRadius, 6f);
        monster.returnHomeDistance = Mathf.Max(monster.returnHomeDistance, 10f);
        monster.attackPlayer = false;
        monster.attackVillagers = false;
        monster.attackSmartNpcs = false;
        monster.attackOtherMonsters = false;
        monster.huntTargetType = HuntTargetType.Any;

        NpcMapNavigator.ReportNpcZone(
            monster.gameObject,
            line != null
                ? line.battleZone
                : NpcMapZone.MaThuSonMach);
    }

    void ActivateWaveMonsterCombat(
        MonsterAI monster,
        FrontierBattleLine line,
        int index)
    {
        if (monster == null ||
            monster.IsDead)
        {
            return;
        }

        if (line != null &&
            line.HasBattleArea)
        {
            monster.BeginFrontierBeastWaveCombat(
                line.GetDistributedBattlePoint(index),
                line.monstersAttackPlayer,
                line.monsterAggressionBonus);
            return;
        }

        monster.BeginFrontierBeastWaveCombat(
            monster.transform.position,
            false,
            35f);
    }

    int CountAliveMonsters(List<MonsterAI> monsters)
    {
        int alive = 0;
        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster != null &&
                !monster.IsDead)
            {
                alive++;
            }
        }

        return alive;
    }

    int CountAliveDefenders(List<SmartNpcAI> defenders)
    {
        int alive = 0;
        for (int i = 0; i < defenders.Count; i++)
        {
            SmartNpcAI defender = defenders[i];
            if (defender != null &&
                !defender.IsDead)
            {
                alive++;
            }
        }

        return alive;
    }

    void ReleaseWaveDefenders(ActiveBeastWave wave)
    {
        if (wave == null)
        {
            return;
        }

        for (int i = 0; i < wave.defenders.Count; i++)
        {
            SmartNpcAI defender = wave.defenders[i];
            if (defender != null &&
                !defender.IsDead)
            {
                defender.ExitFrontierDefenseMode();
            }
        }
    }

    void CommandWaveMonstersToStaging(ActiveBeastWave wave)
    {
        if (wave == null ||
            wave.line == null)
        {
            return;
        }

        for (int i = 0; i < wave.monsters.Count; i++)
        {
            MonsterAI monster = wave.monsters[i];
            if (monster == null ||
                monster.IsDead)
            {
                continue;
            }

            Vector3 stagingPoint =
                wave.line.HasStagingArea
                    ? wave.line.GetStagingPoint(i)
                    : wave.line.GetBattleCenter();
            monster.EnterFrontierBeastWaveStaging(stagingPoint);
        }
    }

    void ReleaseWaveMonsters(ActiveBeastWave wave)
    {
        if (wave == null)
        {
            return;
        }

        for (int i = 0; i < wave.monsters.Count; i++)
        {
            MonsterAI monster = wave.monsters[i];
            if (monster == null)
            {
                continue;
            }

            monster.ExitFrontierBeastWave();
        }
    }
}
