using UnityEngine;

public static class NpcAreaUtility
{
    public static bool IsSameArea(GameObject a, GameObject b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        NpcMapArea mapAreaA = NpcMapArea.FindArea(a.transform.position);
        NpcMapArea mapAreaB = NpcMapArea.FindArea(b.transform.position);
        if (mapAreaA != null && mapAreaB != null)
        {
            return mapAreaA.zone == mapAreaB.zone;
        }

        NpcLocationArea locationAreaA = NpcLocationArea.FindArea(a.transform.position);
        NpcLocationArea locationAreaB = NpcLocationArea.FindArea(b.transform.position);
        if (locationAreaA != null && locationAreaB != null)
        {
            if (locationAreaA == locationAreaB)
            {
                return true;
            }

            return locationAreaA.zone == locationAreaB.zone &&
                locationAreaA.purpose == locationAreaB.purpose &&
                locationAreaA.activity == locationAreaB.activity &&
                locationAreaA.job == locationAreaB.job &&
                locationAreaA.lifePath == locationAreaB.lifePath;
        }

        if (a.scene.IsValid() && b.scene.IsValid())
        {
            if (a.scene == b.scene)
            {
                return true;
            }
        }

#if UNITY_EDITOR
        Debug.LogWarning(
            "[NpcAreaUtility] Fallback same-area check for " +
            a.name + " and " + b.name);
#endif
        return true;
    }
}
