using UnityEngine;

/// <summary>
/// Names the time domain used by gameplay code. World simulation should use
/// world hours; short actions use scaled seconds; UI may use unscaled seconds.
/// </summary>
public static class GameTime
{
    public const float LegacyRealSecondsPerWorldDay = 900f;
    public const float LegacyRealSecondsPerWorldHour =
        LegacyRealSecondsPerWorldDay / 24f;

    public static float ScaledNowSeconds => Time.time;
    public static float ScaledDeltaSeconds => Time.deltaTime;
    public static float UnscaledNowSeconds => Time.unscaledTime;
    public static float UnscaledDeltaSeconds => Time.unscaledDeltaTime;

    public static bool TryGetCurrentWorldHour(out double worldHour)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            worldHour = 0d;
            return false;
        }

        worldHour = timeSystem.CurrentWorldHourExact;
        return true;
    }

    public static float ScaledSecondsToWorldHours(
        float scaledSeconds,
        float realSecondsPerWorldDay)
    {
        float secondsPerDay = Mathf.Max(0.001f, realSecondsPerWorldDay);
        return Mathf.Max(0f, scaledSeconds) * 24f / secondsPerDay;
    }

    public static float WorldHoursToScaledSeconds(
        float worldHours,
        float realSecondsPerWorldDay)
    {
        return
            Mathf.Max(0f, worldHours) *
            Mathf.Max(0.001f, realSecondsPerWorldDay) /
            24f;
    }

    public static float LegacyScaledSecondsToWorldHours(float scaledSeconds)
    {
        return ScaledSecondsToWorldHours(
            scaledSeconds,
            LegacyRealSecondsPerWorldDay);
    }

    public static float WorldHoursToScaledSeconds(float worldHours)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        float secondsPerDay = timeSystem != null
            ? timeSystem.realSecondsPerGameDay
            : LegacyRealSecondsPerWorldDay;
        return WorldHoursToScaledSeconds(worldHours, secondsPerDay);
    }

    public static WaitForSeconds WaitForScaledSeconds(float scaledSeconds)
    {
        return new WaitForSeconds(Mathf.Max(0f, scaledSeconds));
    }

    public static WaitForSecondsRealtime WaitForUnscaledSeconds(
        float unscaledSeconds)
    {
        return new WaitForSecondsRealtime(Mathf.Max(0f, unscaledSeconds));
    }
}
