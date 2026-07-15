using System.Collections.Generic;
using UnityEngine;

public static class NpcMapBehaviorPolicy
{
    struct ForcedCombatZoneState
    {
        public NpcMapZone zone;
        public float expiresAt;
    }

    static readonly Dictionary<NpcMapZone, MonsterAI> sharedCombatTargets =
        new Dictionary<NpcMapZone, MonsterAI>();
    static readonly Dictionary<GameObject, ForcedCombatZoneState> forcedCombatZones =
        new Dictionary<GameObject, ForcedCombatZoneState>();

    public static void RegisterForcedCombatZone(
        GameObject actor,
        NpcMapZone zone,
        float duration)
    {
        if (actor == null)
        {
            return;
        }

        forcedCombatZones[actor] = new ForcedCombatZoneState
        {
            zone = zone,
            expiresAt = Time.time + Mathf.Max(0.1f, duration)
        };
    }

    public static void ClearForcedCombatZone(GameObject actor)
    {
        if (actor == null)
        {
            return;
        }

        forcedCombatZones.Remove(actor);
    }

    public static bool HasForcedCombatZone(
        GameObject actor,
        NpcMapZone? expectedZone = null)
    {
        if (!TryGetForcedCombatZone(actor, out NpcMapZone zone))
        {
            return false;
        }

        return !expectedZone.HasValue ||
            zone == expectedZone.Value;
    }

    public static bool IsRestrictedSessionParticipant(GameObject actor)
    {
        return BicanhSessionManager.IsDungeonParticipant(actor);
    }

    public static bool IsRestrictedCombatMap(GameObject actor)
    {
        if (HasForcedCombatZone(actor))
        {
            return true;
        }

        NpcMapZone? zone = NpcMapNavigator.ResolveActorZone(actor);
        return zone.HasValue && zone.Value == NpcMapZone.BichAnh;
    }

    public static bool AllowsSchedule(GameObject actor)
    {
        if (IsRestrictedSessionParticipant(actor) ||
            IsRestrictedCombatMap(actor))
        {
            return false;
        }

        return UsesDailySchedule(NpcMapNavigator.ResolveActorZone(actor));
    }

    public static bool AllowsNormalWorldTravel(GameObject actor)
    {
        if (IsRestrictedSessionParticipant(actor) ||
            IsRestrictedCombatMap(actor))
        {
            return false;
        }

        NpcMapZone? zone = NpcMapNavigator.ResolveActorZone(actor);
        return !zone.HasValue || zone.Value != NpcMapZone.BichAnh;
    }

    public static bool ForcesCombatLoop(GameObject actor)
    {
        return HasForcedCombatZone(actor) ||
            IsRestrictedSessionParticipant(actor) ||
            IsRestrictedCombatMap(actor);
    }

    public static bool UsesDailySchedule(NpcMapZone? zone)
    {
        if (!zone.HasValue)
        {
            return true;
        }

        switch (zone.Value)
        {
            case NpcMapZone.Lang:
            case NpcMapZone.VanBaoLau:
            case NpcMapZone.MaThuSonMach:
                return true;

            case NpcMapZone.BichAnh:
                return false;

            default:
                return true;
        }
    }

    public static NpcMapZone? GetAllowedCombatZone(GameObject actor)
    {
        if (TryGetForcedCombatZone(actor, out NpcMapZone forcedZone))
        {
            return forcedZone;
        }

        if (!IsRestrictedSessionParticipant(actor) &&
            !IsRestrictedCombatMap(actor))
        {
            return null;
        }

        return NpcMapNavigator.ResolveActorZone(actor);
    }

    public static bool IsAllowedCombatTask(SmartAITaskGoal goal)
    {
        return goal == SmartAITaskGoal.Combat ||
            goal == SmartAITaskGoal.SupportAlly ||
            goal == SmartAITaskGoal.Pursued ||
            goal == SmartAITaskGoal.LowHpRecovery;
    }

    public static MonsterAI GetSharedCombatTarget(NpcMapZone zone)
    {
        if (!sharedCombatTargets.TryGetValue(zone, out MonsterAI target))
        {
            return null;
        }

        if (target == null ||
            target.currentHP <= 0 ||
            !target.gameObject.activeInHierarchy)
        {
            sharedCombatTargets.Remove(zone);
            return null;
        }

        return target;
    }

    public static void SetSharedCombatTarget(
        NpcMapZone zone,
        MonsterAI target)
    {
        if (target == null ||
            target.currentHP <= 0)
        {
            sharedCombatTargets.Remove(zone);
            return;
        }

        sharedCombatTargets[zone] = target;
    }

    public static void ClearSharedCombatTarget(
        NpcMapZone zone,
        MonsterAI target)
    {
        if (!sharedCombatTargets.TryGetValue(zone, out MonsterAI current))
        {
            return;
        }

        if (current == null ||
            current == target)
        {
            sharedCombatTargets.Remove(zone);
        }
    }

    public static bool IsNormalWorldTravelAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return IsTeleportRouteAction(action) ||
            action.StartsWith("No route to ") ||
            action == NpcText.Action("goTaskProviderDaily") ||
            action == NpcText.Action("visitedTaskProvider") ||
            action == NpcText.Action("goVanBaoLauBroker") ||
            action == NpcText.Action("goVanBaoLauTask") ||
            action == NpcText.Action("tradeSeek") ||
            action == NpcText.Action("goTavern") ||
            action == NpcText.Action("buyPill") ||
            action == NpcText.Action("goMarketTrade") ||
            action == NpcText.Action("goWorkTask") ||
            action == NpcText.Action("goCultivatePoint") ||
            action == NpcText.Action("gatherResource") ||
            action == NpcText.Action("pickItem") ||
            action == NpcText.Action("pickHuntEvidence") ||
            action == NpcText.Action("goHomeRest") ||
            action == NpcText.Action("workingTask");
    }

    public static bool IsTeleportRouteAction(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return false;
        }

        string teleportPrefix =
            NpcText.Action("teleportGateTo").Replace("{0}", "");
        return action.StartsWith(teleportPrefix) ||
            action.StartsWith("Di cong dich chuyen") ||
            action.StartsWith("\u0110i c\u1ed5ng d\u1ecbch chuy\u1ec3n");
    }

    public static bool CanUseMonsterTarget(
        GameObject actor,
        MonsterAI monster)
    {
        if (actor == null ||
            monster == null)
        {
            return false;
        }

        if (!IsRestrictedSessionParticipant(actor) &&
            !IsRestrictedCombatMap(actor))
        {
            return true;
        }

        NpcMapZone? allowedZone = GetAllowedCombatZone(actor);
        if (!allowedZone.HasValue)
        {
            return false;
        }

        NpcMapZone? monsterZone =
            NpcMapNavigator.ResolveActorZone(monster.gameObject);
        return monsterZone.HasValue &&
            monsterZone.Value == allowedZone.Value;
    }

    static bool TryGetForcedCombatZone(
        GameObject actor,
        out NpcMapZone zone)
    {
        zone = default;

        if (actor == null ||
            !forcedCombatZones.TryGetValue(actor, out ForcedCombatZoneState state))
        {
            return false;
        }

        if (Time.time >= state.expiresAt)
        {
            forcedCombatZones.Remove(actor);
            return false;
        }

        zone = state.zone;
        return true;
    }

    public static bool CanUseHelpRequest(
        GameObject actor,
        GameObject requester,
        GameObject monster)
    {
        if (actor == null ||
            requester == null ||
            monster == null)
        {
            return false;
        }

        if (!IsRestrictedSessionParticipant(actor) &&
            !IsRestrictedCombatMap(actor))
        {
            return true;
        }

        NpcMapZone? allowedZone = GetAllowedCombatZone(actor);
        if (!allowedZone.HasValue)
        {
            return false;
        }

        NpcMapZone? requesterZone =
            NpcMapNavigator.ResolveActorZone(requester);
        NpcMapZone? monsterZone =
            NpcMapNavigator.ResolveActorZone(monster);

        return requesterZone.HasValue &&
            monsterZone.HasValue &&
            requesterZone.Value == allowedZone.Value &&
            monsterZone.Value == allowedZone.Value;
    }
}
