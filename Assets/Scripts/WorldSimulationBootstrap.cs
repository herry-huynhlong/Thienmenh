using UnityEngine;

public static class WorldSimulationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureWorldSimulationSystems()
    {
        if (DayNightLightingSystem.Instance == null)
        {
            GameObject lightingObject = new GameObject("DayNightLightingSystem");
            lightingObject.AddComponent<DayNightLightingSystem>();
        }

        if (WeatherSystem.Instance == null)
        {
            GameObject weatherObject = new GameObject("WeatherSystem");
            weatherObject.AddComponent<WeatherSystem>();
        }

        if (WeatherVisualSystem.Instance == null)
        {
            GameObject weatherVisualObject = new GameObject("WeatherVisualSystem");
            weatherVisualObject.AddComponent<WeatherVisualSystem>();
        }

        if (WeatherAccumulationSystem.Instance == null)
        {
            GameObject accumulationObject = new GameObject("WeatherAccumulationSystem");
            accumulationObject.AddComponent<WeatherAccumulationSystem>();
        }

        if (HeavenSystem.Instance == null)
        {
            GameObject heavenObject = new GameObject("HeavenSystem");
            heavenObject.AddComponent<HeavenSystem>();
        }

        if (WorldEventSystem.Instance == null)
        {
            GameObject eventObject = new GameObject("WorldEventSystem");
            eventObject.AddComponent<WorldEventSystem>();
        }
    }
}
