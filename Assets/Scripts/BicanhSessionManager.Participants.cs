using UnityEngine;

public partial class BicanhSessionManager
{
    void ApplyDungeonMode(ParticipantSnapshot snapshot, Vector3 destination)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        SetWorldPosition(entity, destination);
        if (!entity.activeSelf)
        {
            entity.SetActive(true);
        }

        SmartNpcAI smartNpc = snapshot.smartNpc;
        if (smartNpc != null)
        {
            snapshot.smartNpcAutonomousActivitiesEnabled = smartNpc.autonomousActivitiesEnabled;
            snapshot.smartNpcCanCultivate = smartNpc.canCultivate;
            snapshot.smartNpcCanFight = smartNpc.canFight;
            snapshot.smartNpcCanTrade = smartNpc.canTrade;
            snapshot.smartNpcCanGather = smartNpc.canGather;
            snapshot.smartNpcCanSellGoods = smartNpc.canSellGoods;
            snapshot.smartNpcCanMakeFriends = smartNpc.canMakeFriends;
            snapshot.smartNpcCanKillOthers = smartNpc.canKillOthers;
            snapshot.smartNpcCanCompeteResource = smartNpc.canCompeteResource;
            snapshot.smartNpcCanCreateSect = smartNpc.canCreateSect;
            snapshot.smartNpcCurrentAction = smartNpc.currentAction;

            smartNpc.autonomousActivitiesEnabled = true;
            smartNpc.canCultivate = false;
            smartNpc.canFight = true;
            smartNpc.canTrade = false;
            smartNpc.canGather = false;
            smartNpc.canSellGoods = false;
            smartNpc.canMakeFriends = false;
            smartNpc.canKillOthers = true;
            smartNpc.canCompeteResource = true;
            smartNpc.canCreateSect = false;
            smartNpc.EnterBicanhSessionMode();
        }

        NotifyTeleported(entity);

        NpcRoleUtility.StopForConversation(entity);
        NpcRoleUtility.SetAction(entity, NpcText.Action("idle"));

        VillagerAI villager = snapshot.villager;
        if (villager != null)
        {
            snapshot.villagerHomeRoutineManagedExternally = villager.homeRoutineManagedExternally;
            snapshot.villagerDailyTaskPlanEnabled = villager.dailyTaskPlanEnabled;
            snapshot.villagerDailyRoutineEnabled = villager.dailyRoutineEnabled;
            snapshot.villagerAutonomousWorkEnabled = villager.autonomousWorkEnabled;
            snapshot.villagerAutonomousResourceWorkEnabled = villager.autonomousResourceWorkEnabled;
            snapshot.villagerAutonomousDangerousWorkEnabled = villager.autonomousDangerousWorkEnabled;
            snapshot.villagerStrongNpcAvoidMortalWork = villager.strongNpcAvoidMortalWork;
            snapshot.villagerHideAtHome = villager.hideAtHome;
            snapshot.villagerCurrentAction = villager.currentAction;

            villager.homeRoutineManagedExternally = true;
            villager.dailyTaskPlanEnabled = false;
            villager.dailyRoutineEnabled = false;
            villager.autonomousWorkEnabled = false;
            villager.autonomousResourceWorkEnabled = false;
            villager.autonomousDangerousWorkEnabled = false;
            villager.strongNpcAvoidMortalWork = false;
            villager.hideAtHome = false;
        }

        MonsterAI monster = snapshot.monster;
        if (monster != null)
        {
            snapshot.monsterGuardTerritory = monster.guardTerritory;
            monster.guardTerritory = false;
        }

        DisableNonCombatBehaviours(snapshot);
    }

    void DisableNonCombatBehaviours(ParticipantSnapshot snapshot)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        MonoBehaviour[] behaviours =
            snapshot.entity.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                !ShouldSuspendBehaviour(behaviour))
            {
                continue;
            }

            if (behaviour is SmartNpcAI ||
                behaviour is VillagerAI ||
                behaviour is MonsterAI)
            {
                continue;
            }

            snapshot.behaviourStates.Add(new BehaviourState(behaviour, behaviour.enabled));
            behaviour.enabled = false;
        }
    }

    bool ShouldSuspendBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null)
        {
            return false;
        }

        string name = behaviour.GetType().Name;
        return name == nameof(NpcTaskProvider) ||
            name == nameof(NpcResourceGatherer) ||
            name == nameof(NpcTradeAgent);
    }

    void RestoreParticipant(ParticipantSnapshot snapshot, bool restoreInventory)
    {
        if (snapshot == null || snapshot.entity == null)
        {
            return;
        }

        GameObject entity = snapshot.entity;
        RestoreNonCombatBehaviours(snapshot);
        RestoreTypeSpecificState(snapshot);
        RestoreRenderersAndColliders(entity);

        if (entity.activeSelf != snapshot.wasActiveSelf)
        {
            entity.SetActive(snapshot.wasActiveSelf);
        }

        if (snapshot.rb != null)
        {
            snapshot.rb.bodyType = snapshot.rbBodyType;
            snapshot.rb.simulated = snapshot.rbSimulated;
            snapshot.rb.gravityScale = snapshot.rbGravityScale;
            snapshot.rb.constraints = snapshot.rbConstraints;
            snapshot.rb.linearVelocity = snapshot.rbLinearVelocity;
            snapshot.rb.angularVelocity = snapshot.rbAngularVelocity;
        }

        SetWorldPosition(entity, ResolveReturnPosition(snapshot));
        entity.transform.rotation = snapshot.rotation;

        NotifyTeleported(entity);

        if (IsDead(entity) || snapshot.wasDead)
        {
            RestoreDeathState(snapshot);
        }
    }

    Vector3 ResolveReturnPosition(ParticipantSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return GetFallbackReturnPosition();
        }

        if (IsValidReturnPosition(snapshot.position))
        {
            return snapshot.position;
        }

        return GetFallbackReturnPosition();
    }

    bool IsValidReturnPosition(Vector3 position)
    {
        if (!float.IsFinite(position.x) ||
            !float.IsFinite(position.y) ||
            !float.IsFinite(position.z))
        {
            return false;
        }

        return NpcMapArea.FindArea(position) != null;
    }

    Vector3 GetFallbackReturnPosition()
    {
        if (fallbackReturnPoint != null)
        {
            return fallbackReturnPoint.position;
        }

        return transform.position;
    }

    void RestoreInventory(ParticipantSnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.inventory == null ||
            snapshot.inventorySnapshot == null)
        {
            return;
        }

        CopyInventoryItems(snapshot.inventorySnapshot, snapshot.inventory.items);
        snapshot.inventory.MarkDirty();
    }

    void RestoreRenderersAndColliders(GameObject entity)
    {
        if (entity == null)
        {
            return;
        }

        Renderer[] renderers = entity.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }

        Collider2D[] colliders = entity.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }
    }

    void RestoreNonCombatBehaviours(ParticipantSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.behaviourStates.Count; i++)
        {
            BehaviourState state = snapshot.behaviourStates[i];
            if (state.behaviour != null)
            {
                state.behaviour.enabled = state.enabled;
            }
        }
    }

    void RestoreTypeSpecificState(ParticipantSnapshot snapshot)
    {
        SmartNpcAI smartNpc = snapshot.smartNpc;
        if (smartNpc != null)
        {
            smartNpc.autonomousActivitiesEnabled = snapshot.smartNpcAutonomousActivitiesEnabled;
            smartNpc.canCultivate = snapshot.smartNpcCanCultivate;
            smartNpc.canFight = snapshot.smartNpcCanFight;
            smartNpc.canTrade = snapshot.smartNpcCanTrade;
            smartNpc.canGather = snapshot.smartNpcCanGather;
            smartNpc.canSellGoods = snapshot.smartNpcCanSellGoods;
            smartNpc.canMakeFriends = snapshot.smartNpcCanMakeFriends;
            smartNpc.canKillOthers = snapshot.smartNpcCanKillOthers;
            smartNpc.canCompeteResource = snapshot.smartNpcCanCompeteResource;
            smartNpc.canCreateSect = snapshot.smartNpcCanCreateSect;
            smartNpc.ExitBicanhSessionMode();
            if (!string.IsNullOrEmpty(snapshot.smartNpcCurrentAction))
            {
                smartNpc.ForceSetCurrentAction(snapshot.smartNpcCurrentAction);
            }
        }

        VillagerAI villager = snapshot.villager;
        if (villager != null)
        {
            villager.homeRoutineManagedExternally = snapshot.villagerHomeRoutineManagedExternally;
            villager.dailyTaskPlanEnabled = snapshot.villagerDailyTaskPlanEnabled;
            villager.dailyRoutineEnabled = snapshot.villagerDailyRoutineEnabled;
            villager.autonomousWorkEnabled = snapshot.villagerAutonomousWorkEnabled;
            villager.autonomousResourceWorkEnabled = snapshot.villagerAutonomousResourceWorkEnabled;
            villager.autonomousDangerousWorkEnabled = snapshot.villagerAutonomousDangerousWorkEnabled;
            villager.strongNpcAvoidMortalWork = snapshot.villagerStrongNpcAvoidMortalWork;
            villager.hideAtHome = snapshot.villagerHideAtHome;
            if (!string.IsNullOrEmpty(snapshot.villagerCurrentAction))
            {
                villager.SetCurrentActionState(
                    NpcActionState.FromDisplayText(
                        snapshot.villagerCurrentAction));
            }
        }
    }

    void RestoreDeathState(ParticipantSnapshot snapshot)
    {
        if (snapshot.smartNpc != null)
        {
            snapshot.smartNpc.SetCurrentHealth(snapshot.smartNpcCurrentHP);

            SetPrivateBool(snapshot.smartNpc, "isDead", false);
            snapshot.smartNpc.ExitBicanhSessionMode();
            if (!string.IsNullOrEmpty(snapshot.smartNpcCurrentAction))
            {
                snapshot.smartNpc.ForceSetCurrentAction(snapshot.smartNpcCurrentAction);
            }
        }

        if (snapshot.villager != null)
        {
            snapshot.villager.SetCurrentHealth(snapshot.villagerCurrentHP);

            if (!string.IsNullOrEmpty(snapshot.villagerCurrentAction))
            {
                snapshot.villager.SetCurrentActionState(
                    NpcActionState.FromDisplayText(
                        snapshot.villagerCurrentAction));
            }
            RestoreRenderersAndColliders(snapshot.villager.gameObject);
        }

        if (snapshot.monster != null)
        {
            snapshot.monster.currentHP = snapshot.monsterCurrentHP;
            if (snapshot.monster.entityProfile != null)
            {
                snapshot.monster.entityProfile.stats.currentHP = snapshot.monsterCurrentHP;
            }
            snapshot.monster.guardTerritory = snapshot.monsterGuardTerritory;

            SetPrivateBool(snapshot.monster, "isDead", false);
            SetPrivateBool(snapshot.monster, "isRespawning", false);
            if (!string.IsNullOrEmpty(snapshot.monsterCurrentAction))
            {
                snapshot.monster.SetCurrentActionState(
                    NpcActionState.FromDisplayText(
                        snapshot.monsterCurrentAction));
            }
            RestoreRenderersAndColliders(snapshot.monster.gameObject);

            if (snapshot.rb != null)
            {
                snapshot.rb.simulated = true;
            }
        }
    }
}
