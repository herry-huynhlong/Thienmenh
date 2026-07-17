using System;
using UnityEngine;

public partial class BicanhSessionManager
{
    BicanhParticipantSaveData CaptureParticipantSaveData(
        ParticipantSnapshot snapshot)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return null;
        }

        GameObject entity = snapshot.entity;
        BicanhParticipantSaveData data =
            new BicanhParticipantSaveData
            {
                stateKey = GetEntityStateKey(entity),
                objectName = entity.name,
                currentPosition = entity.transform.position,
                currentRotation = entity.transform.rotation,
                currentActiveSelf = entity.activeSelf,
                originalPosition = snapshot.position,
                originalRotation = snapshot.rotation,
                originalActiveSelf = snapshot.wasActiveSelf,
                originalWasDead = snapshot.wasDead
            };

        NPCIdentity identity = entity.GetComponent<NPCIdentity>();
        if (identity != null)
        {
            data.npcId = identity.npcId;
        }

        NpcData npcData = entity.GetComponent<NpcData>();
        if (npcData != null)
        {
            npcData.EnsurePersistentId();
            data.npcDataPersistentId = npcData.persistentId;
        }

        SpawnedWorldActor actor = entity.GetComponent<SpawnedWorldActor>();
        if (actor != null)
        {
            actor.EnsurePersistentId();
            data.worldActorPersistentId = actor.persistentId;
        }

        NpcSocialIdentity socialIdentity =
            entity.GetComponent<NpcSocialIdentity>();
        if (socialIdentity != null)
        {
            data.socialId = socialIdentity.socialId;
        }

        data.hasRigidbody = snapshot.rb != null;
        if (snapshot.rb != null)
        {
            data.rbBodyType = (int)snapshot.rbBodyType;
            data.rbSimulated = snapshot.rbSimulated;
            data.rbGravityScale = snapshot.rbGravityScale;
            data.rbConstraints = (int)snapshot.rbConstraints;
            data.rbLinearVelocity = snapshot.rbLinearVelocity;
            data.rbAngularVelocity = snapshot.rbAngularVelocity;
        }

        CaptureSmartNpcSaveData(snapshot, data);
        CaptureVillagerSaveData(snapshot, data);
        CaptureMonsterSaveData(snapshot, data);
        CaptureBehaviourSaveData(snapshot, data);
        return data;
    }

    void CaptureSmartNpcSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        SmartNpcAI smartNpc = snapshot.smartNpc;
        data.hasSmartNpc = smartNpc != null;
        if (smartNpc == null)
        {
            return;
        }

        data.smartNpcAutonomousActivitiesEnabled =
            snapshot.smartNpcAutonomousActivitiesEnabled;
        data.smartNpcCanCultivate = snapshot.smartNpcCanCultivate;
        data.smartNpcCanFight = snapshot.smartNpcCanFight;
        data.smartNpcCanTrade = snapshot.smartNpcCanTrade;
        data.smartNpcCanGather = snapshot.smartNpcCanGather;
        data.smartNpcCanSellGoods = snapshot.smartNpcCanSellGoods;
        data.smartNpcCanMakeFriends = snapshot.smartNpcCanMakeFriends;
        data.smartNpcCanKillOthers = snapshot.smartNpcCanKillOthers;
        data.smartNpcCanCompeteResource =
            snapshot.smartNpcCanCompeteResource;
        data.smartNpcCanCreateSect = snapshot.smartNpcCanCreateSect;
        data.smartNpcOriginalHP = snapshot.smartNpcCurrentHP;
        data.smartNpcOriginalAction = snapshot.smartNpcCurrentAction;
        NpcActionState originalAction =
            NpcActionState.FromDisplayText(snapshot.smartNpcCurrentAction);
        data.smartNpcOriginalActionId = (int)originalAction.id;
        data.smartNpcOriginalActionKey = originalAction.key;
        data.smartNpcCurrentHP = smartNpc.AuthoritativeCurrentHP;
        data.smartNpcCurrentAction = smartNpc.currentAction;
        NpcActionState currentAction = smartNpc.CurrentActionState;
        data.smartNpcCurrentActionId = (int)currentAction.id;
        data.smartNpcCurrentActionKey = currentAction.key;
    }

    void CaptureVillagerSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        VillagerAI villager = snapshot.villager;
        data.hasVillager = villager != null;
        if (villager == null)
        {
            return;
        }

        data.villagerHomeRoutineManagedExternally =
            snapshot.villagerHomeRoutineManagedExternally;
        data.villagerDailyTaskPlanEnabled =
            snapshot.villagerDailyTaskPlanEnabled;
        data.villagerDailyRoutineEnabled =
            snapshot.villagerDailyRoutineEnabled;
        data.villagerAutonomousWorkEnabled =
            snapshot.villagerAutonomousWorkEnabled;
        data.villagerAutonomousResourceWorkEnabled =
            snapshot.villagerAutonomousResourceWorkEnabled;
        data.villagerAutonomousDangerousWorkEnabled =
            snapshot.villagerAutonomousDangerousWorkEnabled;
        data.villagerStrongNpcAvoidMortalWork =
            snapshot.villagerStrongNpcAvoidMortalWork;
        data.villagerHideAtHome = snapshot.villagerHideAtHome;
        data.villagerOriginalHP = snapshot.villagerCurrentHP;
        data.villagerOriginalAction = snapshot.villagerCurrentAction;
        NpcActionState originalAction =
            NpcActionState.FromDisplayText(snapshot.villagerCurrentAction);
        data.villagerOriginalActionId = (int)originalAction.id;
        data.villagerOriginalActionKey = originalAction.key;
        data.villagerCurrentHP = villager.AuthoritativeCurrentHP;
        data.villagerCurrentAction = villager.currentAction;
        NpcActionState currentAction = villager.CurrentActionState;
        data.villagerCurrentActionId = (int)currentAction.id;
        data.villagerCurrentActionKey = currentAction.key;
    }

    void CaptureMonsterSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        MonsterAI monster = snapshot.monster;
        data.hasMonster = monster != null;
        if (monster == null)
        {
            return;
        }

        data.monsterGuardTerritory = snapshot.monsterGuardTerritory;
        data.monsterOriginalHP = snapshot.monsterCurrentHP;
        data.monsterOriginalAction = snapshot.monsterCurrentAction;
        NpcActionState originalAction =
            NpcActionState.FromDisplayText(snapshot.monsterCurrentAction);
        data.monsterOriginalActionId = (int)originalAction.id;
        data.monsterOriginalActionKey = originalAction.key;
        data.monsterCurrentHP = monster.currentHP;
        data.monsterCurrentAction = monster.currentAction;
        NpcActionState currentAction = monster.CurrentActionState;
        data.monsterCurrentActionId = (int)currentAction.id;
        data.monsterCurrentActionKey = currentAction.key;
    }

    void CaptureBehaviourSaveData(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        for (int i = 0; i < snapshot.behaviourStates.Count; i++)
        {
            BehaviourState state = snapshot.behaviourStates[i];
            if (state == null || state.behaviour == null)
            {
                continue;
            }

            data.behaviourStates.Add(new BicanhBehaviourSaveData
            {
                typeName = state.behaviour.GetType().FullName,
                enabled = state.enabled
            });
        }
    }

    void RestoreParticipantSaveData(BicanhParticipantSaveData data)
    {
        if (data == null)
        {
            return;
        }

        GameObject entity = FindEntityBySaveData(data);
        if (entity == null)
        {
            Debug.LogWarning(
                "[Bicanh] Khong khoi phuc duoc participant: " +
                data.stateKey);
            return;
        }

        ParticipantSnapshot snapshot = new ParticipantSnapshot(entity);
        ApplySavedSnapshotState(snapshot, data);
        ApplySavedDungeonState(snapshot, data);

        snapshots.Add(snapshot);
        spawnedThisSession.Add(entity);
        occupiedSpawnPositions.Add(entity.transform.position);
    }

    void ApplySavedSnapshotState(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        snapshot.position = data.originalPosition;
        snapshot.rotation = data.originalRotation;
        snapshot.wasActiveSelf = data.originalActiveSelf;
        snapshot.wasDead = data.originalWasDead;

        if (snapshot.rb != null && data.hasRigidbody)
        {
            snapshot.rbBodyType = (RigidbodyType2D)data.rbBodyType;
            snapshot.rbSimulated = data.rbSimulated;
            snapshot.rbGravityScale = data.rbGravityScale;
            snapshot.rbConstraints =
                (RigidbodyConstraints2D)data.rbConstraints;
            snapshot.rbLinearVelocity = data.rbLinearVelocity;
            snapshot.rbAngularVelocity = data.rbAngularVelocity;
        }

        if (snapshot.smartNpc != null && data.hasSmartNpc)
        {
            snapshot.smartNpcAutonomousActivitiesEnabled =
                data.smartNpcAutonomousActivitiesEnabled;
            snapshot.smartNpcCanCultivate = data.smartNpcCanCultivate;
            snapshot.smartNpcCanFight = data.smartNpcCanFight;
            snapshot.smartNpcCanTrade = data.smartNpcCanTrade;
            snapshot.smartNpcCanGather = data.smartNpcCanGather;
            snapshot.smartNpcCanSellGoods = data.smartNpcCanSellGoods;
            snapshot.smartNpcCanMakeFriends = data.smartNpcCanMakeFriends;
            snapshot.smartNpcCanKillOthers = data.smartNpcCanKillOthers;
            snapshot.smartNpcCanCompeteResource =
                data.smartNpcCanCompeteResource;
            snapshot.smartNpcCanCreateSect = data.smartNpcCanCreateSect;
            snapshot.smartNpcCurrentHP = data.smartNpcOriginalHP;
            snapshot.smartNpcCurrentAction = data.smartNpcOriginalAction;
        }

        if (snapshot.villager != null && data.hasVillager)
        {
            snapshot.villagerHomeRoutineManagedExternally =
                data.villagerHomeRoutineManagedExternally;
            snapshot.villagerDailyTaskPlanEnabled =
                data.villagerDailyTaskPlanEnabled;
            snapshot.villagerDailyRoutineEnabled =
                data.villagerDailyRoutineEnabled;
            snapshot.villagerAutonomousWorkEnabled =
                data.villagerAutonomousWorkEnabled;
            snapshot.villagerAutonomousResourceWorkEnabled =
                data.villagerAutonomousResourceWorkEnabled;
            snapshot.villagerAutonomousDangerousWorkEnabled =
                data.villagerAutonomousDangerousWorkEnabled;
            snapshot.villagerStrongNpcAvoidMortalWork =
                data.villagerStrongNpcAvoidMortalWork;
            snapshot.villagerHideAtHome = data.villagerHideAtHome;
            snapshot.villagerCurrentHP = data.villagerOriginalHP;
            snapshot.villagerCurrentAction = data.villagerOriginalAction;
        }

        if (snapshot.monster != null && data.hasMonster)
        {
            snapshot.monsterCurrentHP = data.monsterOriginalHP;
            snapshot.monsterCurrentAction = data.monsterOriginalAction;
            snapshot.monsterGuardTerritory = data.monsterGuardTerritory;
        }
    }

    void ApplySavedDungeonState(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        entity.SetActive(data.currentActiveSelf);
        SetWorldPosition(entity, data.currentPosition);
        entity.transform.rotation = data.currentRotation;
        NotifyTeleported(entity);

        if (snapshot.smartNpc != null && data.hasSmartNpc)
        {
            snapshot.smartNpc.isBicanhParticipant = true;
        }

        RestoreCurrentCombatState(snapshot, data);
        ApplyDungeonRestrictions(snapshot);
        RestoreSavedBehaviourSuspension(snapshot, data);
        RestoreRenderersAndColliders(entity);
    }

    void RestoreCurrentCombatState(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        if (snapshot.smartNpc != null && data.hasSmartNpc)
        {
            snapshot.smartNpc.SetCurrentHealth(data.smartNpcCurrentHP);

            if (!string.IsNullOrWhiteSpace(data.smartNpcCurrentAction))
            {
                snapshot.smartNpc.SetCurrentActionState(
                    ResolveSavedActionState(
                        data.smartNpcCurrentActionKey,
                        data.smartNpcCurrentActionId,
                        data.smartNpcCurrentAction));
            }
        }

        if (snapshot.villager != null && data.hasVillager)
        {
            snapshot.villager.SetCurrentHealth(data.villagerCurrentHP);

            if (!string.IsNullOrWhiteSpace(data.villagerCurrentAction))
            {
                snapshot.villager.SetCurrentActionState(
                    ResolveSavedActionState(
                        data.villagerCurrentActionKey,
                        data.villagerCurrentActionId,
                        data.villagerCurrentAction));
            }
        }

        if (snapshot.monster != null && data.hasMonster)
        {
            snapshot.monster.currentHP = data.monsterCurrentHP;
            if (snapshot.monster.entityProfile != null)
            {
                snapshot.monster.entityProfile.stats.currentHP =
                    data.monsterCurrentHP;
            }

            if (!string.IsNullOrWhiteSpace(data.monsterCurrentAction))
            {
                snapshot.monster.SetCurrentActionState(
                    ResolveSavedActionState(
                        data.monsterCurrentActionKey,
                        data.monsterCurrentActionId,
                        data.monsterCurrentAction));
            }
        }
    }

    void ApplyDungeonRestrictions(ParticipantSnapshot snapshot)
    {
        if (snapshot.smartNpc != null)
        {
            snapshot.smartNpc.autonomousActivitiesEnabled = true;
            snapshot.smartNpc.canCultivate = false;
            snapshot.smartNpc.canFight = true;
            snapshot.smartNpc.canTrade = false;
            snapshot.smartNpc.canGather = false;
            snapshot.smartNpc.canSellGoods = false;
            snapshot.smartNpc.canMakeFriends = false;
            snapshot.smartNpc.canKillOthers = true;
            snapshot.smartNpc.canCompeteResource = true;
            snapshot.smartNpc.canCreateSect = false;
        }

        if (snapshot.villager != null)
        {
            snapshot.villager.homeRoutineManagedExternally = true;
            snapshot.villager.dailyTaskPlanEnabled = false;
            snapshot.villager.dailyRoutineEnabled = false;
            snapshot.villager.autonomousWorkEnabled = false;
            snapshot.villager.autonomousResourceWorkEnabled = false;
            snapshot.villager.autonomousDangerousWorkEnabled = false;
            snapshot.villager.strongNpcAvoidMortalWork = false;
            snapshot.villager.hideAtHome = false;
        }

        if (snapshot.monster != null)
        {
            snapshot.monster.guardTerritory = false;
        }
    }

    void RestoreSavedBehaviourSuspension(
        ParticipantSnapshot snapshot,
        BicanhParticipantSaveData data)
    {
        snapshot.behaviourStates.Clear();

        MonoBehaviour[] behaviours =
            snapshot.entity.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                !ShouldSuspendBehaviour(behaviour) ||
                behaviour is SmartNpcAI ||
                behaviour is VillagerAI ||
                behaviour is MonsterAI)
            {
                continue;
            }

            bool originalEnabled =
                FindSavedBehaviourEnabled(data, behaviour, behaviour.enabled);
            snapshot.behaviourStates.Add(
                new BehaviourState(behaviour, originalEnabled));
            behaviour.enabled = false;
        }
    }

    bool FindSavedBehaviourEnabled(
        BicanhParticipantSaveData data,
        Behaviour behaviour,
        bool fallback)
    {
        if (data == null ||
            data.behaviourStates == null ||
            behaviour == null)
        {
            return fallback;
        }

        string fullName = behaviour.GetType().FullName;
        for (int i = 0; i < data.behaviourStates.Count; i++)
        {
            BicanhBehaviourSaveData saved = data.behaviourStates[i];
            if (saved != null &&
                string.Equals(
                    saved.typeName,
                    fullName,
                    StringComparison.Ordinal))
            {
                return saved.enabled;
            }
        }

        return fallback;
    }

    NpcActionState ResolveSavedActionState(
        string actionKey,
        int actionId,
        string displayText)
    {
        NpcActionState state =
            !string.IsNullOrWhiteSpace(actionKey)
                ? NpcActionState.FromKey(actionKey)
                : NpcActionState.FromDisplayText(displayText);

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            state.displayText = displayText;
        }

        if (state.id == NpcActionId.Unknown &&
            Enum.IsDefined(typeof(NpcActionId), actionId))
        {
            state.id = (NpcActionId)actionId;
        }

        return state;
    }
}
