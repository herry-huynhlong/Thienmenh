using UnityEngine;

public partial class MonsterAI
{
    public string GetPlayerActionText()
    {
        if (isDead)
        {
            return NpcText.Action("dead");
        }

        if (!string.IsNullOrWhiteSpace(currentAction))
        {
            if (string.Equals(
                currentAction,
                "Idle",
                System.StringComparison.OrdinalIgnoreCase))
            {
                return NpcText.Action("idle");
            }

            return RuntimeStatusText.Translate(currentAction);
        }

        return NpcText.Action("idle");
    }
}
