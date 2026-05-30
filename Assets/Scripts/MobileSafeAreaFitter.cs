using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MobileSafeAreaFitter : MonoBehaviour
{
    public bool useSafeArea;
    public bool applyLeft = true;
    public bool applyRight = true;
    public bool applyTop = true;
    public bool applyBottom = true;

    [ContextMenu("Apply Safe Area")]
    public void ApplyNow()
    {
    }
}
