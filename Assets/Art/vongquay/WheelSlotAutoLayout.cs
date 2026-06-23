using UnityEngine;
using System;
using TMPro;

public class WheelSlotAutoLayout : MonoBehaviour
{
    public enum RotationMode
    {
        Upright,
        RadialOut,
        RadialIn
    }

    [Header("Circle Layout")]
    public float radius = 200f;
    public float startAngle = 90f;
    public float globalAngleOffset = -18f;
    public bool clockwise = true;
    public Vector2 centerOffset = Vector2.zero;

    [Header("Child Names")]
    public string tierFrameName = "TierFrame";
    public string itemIconName = "ItemIcon";
    public string itemTextName = "ItemName";

    [Header("TierFrame Layout")]
    public RotationMode tierFrameRotation = RotationMode.RadialOut;
    public float tierFrameRotationOffset = 0f;
    public float tierFrameRadiusOffset = 0f;
    public Vector2 tierFrameLocalOffset = Vector2.zero;
    public bool flipTierFrameOnBottomHalf = false;

    [Header("ItemIcon Layout")]
    public RotationMode itemIconRotation = RotationMode.Upright;
    public float itemIconRotationOffset = 0f;
    public float itemIconRadiusOffset = 0f;
    public Vector2 itemIconLocalOffset = new Vector2(0f, 8f);

    [Header("ItemText Layout")]
    public RotationMode itemTextRotation = RotationMode.RadialOut;
    public float itemTextRotationOffset = 0f;
    public float itemTextRadiusOffset = 0f;
    public Vector2 itemTextLocalOffset = new Vector2(0f, -18f);
    public bool textStayOneLine = true;
    public Vector2 textSize = new Vector2(90f, 22f);

    [Header("Manual Slot Tweaks")]
    public SlotTweak[] slotTweaks;

    [Serializable]
    public class SlotTweak
    {
        public int slotIndex;

        [Header("Whole Slot")]
        public float extraAngle = 0f;
        public float extraRadius = 0f;
        public Vector2 extraSlotPosition = Vector2.zero;

        [Header("TierFrame")]
        public Vector2 extraTierFramePosition = Vector2.zero;
        public float extraTierFrameRotation = 0f;

        [Header("ItemIcon")]
        public Vector2 extraItemIconPosition = Vector2.zero;
        public float extraItemIconRotation = 0f;

        [Header("ItemText")]
        public Vector2 extraItemTextPosition = Vector2.zero;
        public float extraItemTextRotation = 0f;
    }

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        int count = transform.childCount;
        if (count == 0) return;

        float step = 360f / count;
        float dir = clockwise ? -1f : 1f;

        for (int i = 0; i < count; i++)
        {
            RectTransform slot = transform.GetChild(i) as RectTransform;
            if (slot == null) continue;

            SlotTweak tweak = GetTweak(i);

            float angleDeg = startAngle + globalAngleOffset + dir * i * step;
            if (tweak != null) angleDeg += tweak.extraAngle;

            float finalRadius = radius;
            if (tweak != null) finalRadius += tweak.extraRadius;

            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 radial = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            Vector2 slotPos = radial * finalRadius + centerOffset;
            if (tweak != null) slotPos += tweak.extraSlotPosition;

            slot.anchoredPosition = slotPos;
            slot.localEulerAngles = Vector3.zero;

            RectTransform tierFrame = FindChildRecursive(slot, tierFrameName);
            RectTransform itemIcon = FindChildRecursive(slot, itemIconName);
            RectTransform itemText = FindChildRecursive(slot, itemTextName);

            if (tierFrame != null)
            {
                Vector2 pos = radial * tierFrameRadiusOffset + tierFrameLocalOffset;
                if (tweak != null) pos += tweak.extraTierFramePosition;

                tierFrame.anchoredPosition = pos;

                float rot = GetRotation(angleDeg, tierFrameRotation, tierFrameRotationOffset);
                if (flipTierFrameOnBottomHalf && radial.y < 0f)
                    rot += 180f;

                if (tweak != null) rot += tweak.extraTierFrameRotation;

                tierFrame.localEulerAngles = new Vector3(0f, 0f, rot);
            }

            if (itemIcon != null)
            {
                Vector2 pos = radial * itemIconRadiusOffset + itemIconLocalOffset;
                if (tweak != null) pos += tweak.extraItemIconPosition;

                itemIcon.anchoredPosition = pos;

                float rot = GetRotation(angleDeg, itemIconRotation, itemIconRotationOffset);
                if (tweak != null) rot += tweak.extraItemIconRotation;

                itemIcon.localEulerAngles = new Vector3(0f, 0f, rot);
            }

            if (itemText != null)
            {
                Vector2 pos = radial * itemTextRadiusOffset + itemTextLocalOffset;
                if (tweak != null) pos += tweak.extraItemTextPosition;

                itemText.anchoredPosition = pos;
                itemText.sizeDelta = textSize;

                float rot = GetRotation(angleDeg, itemTextRotation, itemTextRotationOffset);
                if (tweak != null) rot += tweak.extraItemTextRotation;

                itemText.localEulerAngles = new Vector3(0f, 0f, rot);

                TMP_Text tmp = itemText.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.alignment = TextAlignmentOptions.Center;
                    if (textStayOneLine)
                    {
                        tmp.enableWordWrapping = false;
                        tmp.overflowMode = TextOverflowModes.Overflow;
                    }
                }
            }
        }
    }

    float GetRotation(float angleDeg, RotationMode mode, float offset)
    {
        switch (mode)
        {
            case RotationMode.RadialOut:
                return angleDeg + offset;

            case RotationMode.RadialIn:
                return angleDeg + 180f + offset;

            case RotationMode.Upright:
            default:
                return offset;
        }
    }

    SlotTweak GetTweak(int slotIndex)
    {
        if (slotTweaks == null) return null;

        for (int i = 0; i < slotTweaks.Length; i++)
        {
            if (slotTweaks[i] != null && slotTweaks[i].slotIndex == slotIndex)
                return slotTweaks[i];
        }

        return null;
    }

    RectTransform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child as RectTransform;

            RectTransform found = FindChildRecursive(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void OnValidate()
    {
        ApplyLayout();
    }

    private void Start()
    {
        ApplyLayout();
    }
}