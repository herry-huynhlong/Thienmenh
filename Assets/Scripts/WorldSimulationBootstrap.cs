using UnityEngine;

public static class WorldSimulationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureWorldSimulationSystems()
    {
        WorldTimeSystem.EnsureInstance();

        if (DayNightLightingSystem.Instance == null)
        {
            GameObject lightingObject = new GameObject("DayNightLightingSystem");
            DayNightLightingSystem lightingSystem =
                lightingObject.AddComponent<DayNightLightingSystem>();
            lightingSystem.MarkCreatedAtRuntime();
        }

        if (WeatherSystem.Instance == null)
        {
            GameObject weatherObject = new GameObject("WeatherSystem");
            WeatherSystem weatherSystem =
                weatherObject.AddComponent<WeatherSystem>();
            weatherSystem.MarkCreatedAtRuntime();
        }

        if (WeatherVisualSystem.Instance == null)
        {
            GameObject weatherVisualObject = new GameObject("WeatherVisualSystem");
            WeatherVisualSystem weatherVisualSystem =
                weatherVisualObject.AddComponent<WeatherVisualSystem>();
            weatherVisualSystem.MarkCreatedAtRuntime();
        }

        if (WeatherAccumulationSystem.Instance == null)
        {
            GameObject accumulationObject = new GameObject("WeatherAccumulationSystem");
            WeatherAccumulationSystem accumulationSystem =
                accumulationObject.AddComponent<WeatherAccumulationSystem>();
            accumulationSystem.MarkCreatedAtRuntime();
        }

        if (HeavenSystem.Instance == null)
        {
            GameObject heavenObject = new GameObject("HeavenSystem");
            HeavenSystem heavenSystem =
                heavenObject.AddComponent<HeavenSystem>();
            heavenSystem.MarkCreatedAtRuntime();
        }

        if (WorldEventSystem.Instance == null)
        {
            GameObject eventObject = new GameObject("WorldEventSystem");
            WorldEventSystem eventSystem =
                eventObject.AddComponent<WorldEventSystem>();
            eventSystem.MarkCreatedAtRuntime();
        }
    }
}
