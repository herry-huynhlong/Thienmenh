using UnityEngine;

public partial class NpcFixedBlacksmithController
{
    bool IsNear(Vector3 targetPosition)
    {
        if (villager == null)
        {
            return Vector2.Distance(transform.position, targetPosition) <= 0.25f;
        }

        return Vector2.Distance(transform.position, targetPosition) <=
            Mathf.Max(0.25f, villager.arriveDistance);
    }

    float GetArrivalDistance()
    {
        return villager != null
            ? Mathf.Max(0.25f, villager.arriveDistance)
            : 0.25f;
    }

    NpcMapZone? GetCurrentZone()
    {
        NpcMapArea area =
            NpcMapArea.FindArea(transform.position);
        if (area != null)
        {
            return area.zone;
        }

        area =
            NpcMapArea.FindNearestArea(transform.position);
        return area != null
            ? area.zone
            : (NpcMapZone?)null;
    }

    float CalculateWorkHoursBetween(float startWorldHour, float endWorldHour)
    {
        if (endWorldHour <= startWorldHour)
        {
            return 0f;
        }

        float total = 0f;
        int startDayIndex = Mathf.FloorToInt(startWorldHour / 24f);
        int endDayIndex = Mathf.FloorToInt(endWorldHour / 24f);

        for (int dayIndex = startDayIndex; dayIndex <= endDayIndex; dayIndex++)
        {
            float dayBaseHour = dayIndex * 24f;
            total += Overlap(
                startWorldHour,
                endWorldHour,
                dayBaseHour + morningWorkStart,
                dayBaseHour + morningWorkEnd);
            total += Overlap(
                startWorldHour,
                endWorldHour,
                dayBaseHour + afternoonWorkStart,
                dayBaseHour + afternoonWorkEnd);
        }

        return total;
    }

    static float Overlap(
        float rangeStart,
        float rangeEnd,
        float slotStart,
        float slotEnd)
    {
        float start = Mathf.Max(rangeStart, slotStart);
        float end = Mathf.Min(rangeEnd, slotEnd);
        return Mathf.Max(0f, end - start);
    }

    float GetCurrentWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentWorldHour
            : 0f;
    }

    float GetCurrentClockHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentHour
            : 0f;
    }

    int GetCurrentWorldDay()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        return timeSystem != null
            ? timeSystem.CurrentDay
            : 0;
    }

    bool IsDedicatedRestWindow(float hour)
    {
        if (IsHourInRange(hour, sleepStart, sleepEnd))
        {
            return true;
        }

        return IsHourInRange(
            hour,
            morningWorkEnd,
            afternoonWorkStart);
    }

    static bool IsHourInRange(
        float hour,
        float startHour,
        float endHour)
    {
        if (Mathf.Approximately(startHour, endHour))
        {
            return false;
        }

        if (startHour < endHour)
        {
            return hour >= startHour &&
                hour < endHour;
        }

        return hour >= startHour ||
            hour < endHour;
    }

    void LogDebug(string stage, string detail)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.LogWarning(
            "[NpcFixedBlacksmith] " +
            gameObject.name +
            " stage=" + stage +
            " state=" + state +
            " detail=" + detail,
            this);
    }

    string DescribeBlockingCollidersAtPoint(
        Vector3 position,
        float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
        if (hits == null || hits.Length == 0)
        {
            return "none";
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();
        bool wroteAny = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null ||
                hit.isTrigger ||
                IsSelfCollider(hit))
            {
                continue;
            }

            if (wroteAny)
            {
                builder.Append(" | ");
            }

            builder.Append(hit.name)
                .Append("@")
                .Append(hit.bounds.center)
                .Append(" layer=")
                .Append(hit.gameObject.layer);
            wroteAny = true;
        }

        return wroteAny ? builder.ToString() : "none";
    }

    string DescribeRaycastBlockersTo(
        Vector3 targetPosition,
        float radius)
    {
        Vector2 origin = transform.position;
        Vector2 delta = (Vector2)targetPosition - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return "none";
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin,
            Mathf.Max(0.01f, radius),
            delta.normalized,
            distance);
        if (hits == null || hits.Length == 0)
        {
            return "none";
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();
        bool wroteAny = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null ||
                hit.isTrigger ||
                IsSelfCollider(hit))
            {
                continue;
            }

            if (wroteAny)
            {
                builder.Append(" | ");
            }

            builder.Append(hit.name)
                .Append("@")
                .Append(hit.bounds.center)
                .Append(" dist=")
                .Append(hits[i].distance.ToString("0.00"));
            wroteAny = true;
        }

        return wroteAny ? builder.ToString() : "none";
    }

    static string GetZoneLabel(NpcMapZone zone)
    {
        switch (zone)
        {
            case NpcMapZone.VanBaoLau:
                return "Van Bao Lau";
            case NpcMapZone.MaThuSonMach:
                return "Ma Thu Son Mach";
            case NpcMapZone.BichAnh:
                return "Bich Anh";
            default:
                return "Lang";
        }
    }

    static string GetZoneText(NpcMapZone? zone)
    {
        return zone.HasValue
            ? GetZoneLabel(zone.Value)
            : "None";
    }

    void SyncSleepVisibility()
    {
        if (villager == null)
        {
            return;
        }

        bool shouldHide =
            IsHideAtHomeScheduleActive() &&
            IsAtHomePoint();

        if (shouldHide)
        {
            if (!villager.IsHiddenAtHome)
            {
                villager.ForceHiddenAtHome(true);
                LogDebug("Visibility", "hideAtHome scheduleSlot=1");
            }

            return;
        }

        if (villager.IsHiddenAtHome)
        {
            villager.ForceHiddenAtHome(false);
            LogDebug("Visibility", "hideAtHome scheduleSlot=0");
        }
    }

    bool IsHideAtHomeScheduleActive()
    {
        if (schedule == null ||
            !schedule.enforceSchedule)
        {
            return false;
        }

        return schedule.CurrentActivity == NpcScheduleActivity.Sleep ||
            schedule.CurrentActivity == NpcScheduleActivity.ReturnHome;
    }

    bool IsAtHomePoint()
    {
        if (villager == null ||
            villager.homePoint == null)
        {
            return false;
        }

        return Vector2.Distance(
            transform.position,
            villager.homePoint.position) <=
            Mathf.Max(0.25f, villager.arriveDistance);
    }
}
