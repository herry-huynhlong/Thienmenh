using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvasScaler : MonoBehaviour
{
    public Vector2 referenceResolution =
        new Vector2(1080f, 1920f);

    CanvasScaler scaler;

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        Apply();
    }

    void Apply()
    {
        if (scaler == null)
        {
            scaler = GetComponent<CanvasScaler>();
        }

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            referenceResolution;

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        float aspect =
            Screen.height <= 0
            ? 1f
            : (float)Screen.width / Screen.height;

        scaler.matchWidthOrHeight =
            aspect < 0.65f
            ? 0f
            : 0.5f;
    }
}
