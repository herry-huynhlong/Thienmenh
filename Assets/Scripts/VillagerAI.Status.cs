using UnityEngine;

public partial class VillagerAI
{
    public string GetPlayerActionText()
    {
        if (IsDead)
        {
            return NpcText.Action("dead");
        }

        if (waitingForHeavenlyTribulation ||
            (characterStats != null &&
            characterStats.waitingForHeavenlyTribulation))
        {
            return NpcText.Action("waitTribulation");
        }

        string action = NormalizeDisplayAction(currentAction);
        if (!string.IsNullOrWhiteSpace(action))
        {
            return RuntimeStatusText.Translate(action);
        }

        return NpcText.Action("idle");
    }

    string NormalizeDisplayAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        if (IsIdleLikeDisplayAction(action))
        {
            return "";
        }

        if (IsTravelIntentAction(action) &&
            !HasActiveTravelContext())
        {
            return "";
        }

        return action;
    }

    bool IsIdleLikeDisplayAction(string action)
    {
        return action == NpcText.Action("idle") ||
            action == NpcText.Action("rest") ||
            action == NpcText.Action("restNearHome") ||
            action == NpcText.Action("restVillageNoon") ||
            action == NpcText.Action("stayNearHome") ||
            action == NpcText.Action("calm") ||
            action.StartsWith("waitSchedule", System.StringComparison.OrdinalIgnoreCase);
    }

    bool HasActiveTravelContext()
    {
        return currentTarget != null ||
            hasWorkTarget ||
            hasTradeTarget ||
            hasBuyTarget ||
            hasEatTarget ||
            hasSellTarget;
    }
}
