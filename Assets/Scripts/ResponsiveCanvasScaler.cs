using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvasScaler : MonoBehaviour
{
    public Vector2 portraitReferenceResolution = new Vector2(1080f, 1920f);
    public Vector2 landscapeReferenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float portraitMatchWidthOrHeight = 0f;
    [Range(0f, 1f)] public float landscapeMatchWidthOrHeight = 0.5f;
    public bool applyInEditMode;

    [ContextMenu("Apply Responsive Canvas")]
    public void ApplyNow()
    {
    }
}
