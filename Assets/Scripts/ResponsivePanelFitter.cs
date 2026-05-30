using UnityEngine;

public class ResponsivePanelFitter : MonoBehaviour
{
    public RectTransform targetRoot;
    public Vector2 portraitDesignSize = new Vector2(520f, 760f);
    public Vector2 landscapeDesignSize = new Vector2(840f, 520f);
    public Vector2 portraitPadding = new Vector2(24f, 24f);
    public Vector2 landscapePadding = new Vector2(36f, 24f);
    public float topPadding = 8f;
    public float bottomReservedHeight = 90f;
    public float minScale = 0.75f;
    public float maxScale = 1.15f;
    public bool preferWidthFit;
    public bool useSafeArea;

    [ContextMenu("Apply Responsive Fit")]
    public void Apply()
    {
    }
}
