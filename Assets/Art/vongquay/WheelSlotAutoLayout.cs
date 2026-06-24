using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;

public class WheelSlotAutoLayout : MonoBehaviour
{
    public enum RotationMode
    {
        Upright,
        RadialOut,
        RadialIn
    }

    [Header("Auto Apply")]
    public bool applyOnStart = true;
    public bool applyOnValidate = true;

    [Header("Circle Layout")]
    public float radius = 230f;
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
    public float tierFrameRotationOffset = -100f;
    public float tierFrameRadiusOffset = 2f;
    public Vector2 tierFrameLocalOffset = new Vector2(2f, -5f);
    public bool flipTierFrameOnBottomHalf = false;

    [Header("ItemIcon Layout")]
    public RotationMode itemIconRotation = RotationMode.Upright;
    public float itemIconRotationOffset = 0f;
    public float itemIconRadiusOffset = 0f;
    public Vector2 itemIconLocalOffset = new Vector2(0f, 8f);

    [Header("ItemText Layout")]
    public RotationMode itemTextRotation = RotationMode.RadialOut;
    public float itemTextRotationOffset = 0f;
    public float itemTextRadiusOffset = -58f;
    public Vector2 itemTextLocalOffset = Vector2.zero;
    public bool flipTextOnBottomHalf = true;
    public bool textStayOneLine = true;
    public Vector2 textSize = new Vector2(105f, 28f);
    public float textFontSize = 16f;

    [Header("Text Colors")]
    public bool autoTextColorBySlot = true;

    [Tooltip("Nếu Slot_00 đang nằm trên ô trắng thì bật. Nếu Slot_00 nằm trên ô xanh thì tắt.")]
    public bool slot0IsWhite = true;

    public Color textColorOnWhite = new Color32(75, 45, 18, 255);
    public Color textColorOnBlue = Color.white;

    [Header("Text Shadow / Outline")]
    public bool addTMPOutline = false;
    [Range(0f, 1f)] public float outlineWidth = 0.15f;
    public Color outlineColor = new Color32(55, 32, 8, 255);

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

    void Start()
    {
        if (applyOnStart)
        {
            ApplyLayout();
        }
    }

    void OnValidate()
    {
        if (!applyOnValidate) return;
        if (!gameObject.activeInHierarchy) return;

        ApplyLayout();
    }

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        List<RectTransform> slots = CollectActiveSlots();
        int count = slots.Count;

        if (count == 0) return;

        float step = 360f / count;
        float dir = clockwise ? -1f : 1f;

        for (int i = 0; i < count; i++)
        {
            RectTransform slot = slots[i];
            if (slot == null) continue;

            SlotTweak tweak = GetTweak(i);

            float angleDeg = startAngle + globalAngleOffset + dir * i * step;

            if (tweak != null)
            {
                angleDeg += tweak.extraAngle;
            }

            float finalRadius = radius;

            if (tweak != null)
            {
                finalRadius += tweak.extraRadius;
            }

            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 radial = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            Vector2 slotPos = radial * finalRadius + centerOffset;

            if (tweak != null)
            {
                slotPos += tweak.extraSlotPosition;
            }

            slot.anchoredPosition = slotPos;
            slot.localEulerAngles = Vector3.zero;

            RectTransform tierFrame = FindChildRecursive(slot, tierFrameName);
            RectTransform itemIcon = FindChildRecursive(slot, itemIconName);
            RectTransform itemText = FindChildRecursive(slot, itemTextName);

            ApplyTierFrameLayout(tierFrame, radial, angleDeg, tweak);
            ApplyItemIconLayout(itemIcon, radial, angleDeg, tweak);
            ApplyItemTextLayout(itemText, radial, angleDeg, i, tweak);
        }
    }

    void ApplyTierFrameLayout(RectTransform tierFrame, Vector2 radial, float angleDeg, SlotTweak tweak)
    {
        if (tierFrame == null) return;

        Vector2 pos = radial * tierFrameRadiusOffset + tierFrameLocalOffset;

        if (tweak != null)
        {
            pos += tweak.extraTierFramePosition;
        }

        tierFrame.anchoredPosition = pos;

        float rot = GetRotation(angleDeg, tierFrameRotation, tierFrameRotationOffset);

        if (flipTierFrameOnBottomHalf && radial.y < 0f)
        {
            rot += 180f;
        }

        if (tweak != null)
        {
            rot += tweak.extraTierFrameRotation;
        }

        tierFrame.localEulerAngles = new Vector3(0f, 0f, rot);
    }

    void ApplyItemIconLayout(RectTransform itemIcon, Vector2 radial, float angleDeg, SlotTweak tweak)
    {
        if (itemIcon == null) return;

        Vector2 pos = radial * itemIconRadiusOffset + itemIconLocalOffset;

        if (tweak != null)
        {
            pos += tweak.extraItemIconPosition;
        }

        itemIcon.anchoredPosition = pos;

        float rot = GetRotation(angleDeg, itemIconRotation, itemIconRotationOffset);

        if (tweak != null)
        {
            rot += tweak.extraItemIconRotation;
        }

        itemIcon.localEulerAngles = new Vector3(0f, 0f, rot);
    }

    void ApplyItemTextLayout(RectTransform itemText, Vector2 radial, float angleDeg, int slotIndex, SlotTweak tweak)
    {
        if (itemText == null) return;

        Vector2 pos = radial * itemTextRadiusOffset + itemTextLocalOffset;

        if (tweak != null)
        {
            pos += tweak.extraItemTextPosition;
        }

        itemText.anchoredPosition = pos;
        itemText.sizeDelta = textSize;

        float rot = GetRotation(angleDeg, itemTextRotation, itemTextRotationOffset);

        if (flipTextOnBottomHalf && radial.y < 0f)
        {
            rot += 180f;
        }

        if (tweak != null)
        {
            rot += tweak.extraItemTextRotation;
        }

        itemText.localEulerAngles = new Vector3(0f, 0f, rot);

        TMP_Text tmp = itemText.GetComponent<TMP_Text>();

        if (tmp != null)
        {
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = textFontSize;

            if (textStayOneLine)
            {
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
            }

            if (autoTextColorBySlot)
            {
                bool isWhiteSlot = slot0IsWhite ? slotIndex % 2 == 0 : slotIndex % 2 != 0;
                tmp.color = isWhiteSlot ? textColorOnWhite : textColorOnBlue;
            }

            if (addTMPOutline)
            {
                ApplyTextOutline(tmp);
            }
        }
    }

    void ApplyTextOutline(TMP_Text tmp)
    {
        if (tmp == null) return;
        if (tmp.fontMaterial == null) return;

        Material mat = tmp.fontMaterial;

        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
        {
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        }

        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
        {
            mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        }

        tmp.UpdateMeshPadding();
    }

    List<RectTransform> CollectActiveSlots()
    {
        List<RectTransform> slots = new List<RectTransform>();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child == null) continue;
            if (!child.gameObject.activeSelf) continue;
            if (!child.name.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase)) continue;

            RectTransform slot = child as RectTransform;

            if (slot != null)
            {
                slots.Add(slot);
            }
        }

        slots.Sort(CompareSlotName);

        return slots;
    }

    int CompareSlotName(RectTransform a, RectTransform b)
    {
        int indexA = ExtractSlotIndex(a != null ? a.name : "");
        int indexB = ExtractSlotIndex(b != null ? b.name : "");

        return indexA.CompareTo(indexB);
    }

    int ExtractSlotIndex(string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return int.MaxValue;

        int underscoreIndex = slotName.IndexOf('_');

        if (underscoreIndex < 0 || underscoreIndex >= slotName.Length - 1)
        {
            return int.MaxValue;
        }

        string numberPart = slotName.Substring(underscoreIndex + 1);

        int result;

        if (int.TryParse(numberPart, out result))
        {
            return result;
        }

        return int.MaxValue;
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
            {
                return slotTweaks[i];
            }
        }

        return null;
    }

    RectTransform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
            {
                return child as RectTransform;
            }

            RectTransform found = FindChildRecursive(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
        [ContextMenu("Capture Current Layout As Tweaks")]
    public void CaptureCurrentLayoutAsTweaks()
    {
        List<RectTransform> slots = CollectActiveSlots();
        int count = slots.Count;

        if (count == 0) return;

        float step = 360f / count;
        float dir = clockwise ? -1f : 1f;

        SlotTweak[] newTweaks = new SlotTweak[count];

        for (int i = 0; i < count; i++)
        {
            RectTransform slot = slots[i];
            if (slot == null) continue;

            float angleDeg = startAngle + globalAngleOffset + dir * i * step;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 radial = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            SlotTweak tweak = new SlotTweak();
            tweak.slotIndex = i;

            // Whole Slot
            Vector2 expectedSlotPos = radial * radius + centerOffset;
            tweak.extraSlotPosition = slot.anchoredPosition - expectedSlotPos;

            // TierFrame
            RectTransform tierFrame = FindChildRecursive(slot, tierFrameName);
            if (tierFrame != null)
            {
                Vector2 expectedTierPos = radial * tierFrameRadiusOffset + tierFrameLocalOffset;
                tweak.extraTierFramePosition = tierFrame.anchoredPosition - expectedTierPos;

                float expectedRot = GetRotation(angleDeg, tierFrameRotation, tierFrameRotationOffset);
                if (flipTierFrameOnBottomHalf && radial.y < 0f)
                {
                    expectedRot += 180f;
                }

                tweak.extraTierFrameRotation = Mathf.DeltaAngle(expectedRot, tierFrame.localEulerAngles.z);
            }

            // ItemIcon
            RectTransform itemIcon = FindChildRecursive(slot, itemIconName);
            if (itemIcon != null)
            {
                Vector2 expectedIconPos = radial * itemIconRadiusOffset + itemIconLocalOffset;
                tweak.extraItemIconPosition = itemIcon.anchoredPosition - expectedIconPos;

                float expectedRot = GetRotation(angleDeg, itemIconRotation, itemIconRotationOffset);
                tweak.extraItemIconRotation = Mathf.DeltaAngle(expectedRot, itemIcon.localEulerAngles.z);
            }

            // ItemText
            RectTransform itemText = FindChildRecursive(slot, itemTextName);
            if (itemText != null)
            {
                Vector2 expectedTextPos = radial * itemTextRadiusOffset + itemTextLocalOffset;
                tweak.extraItemTextPosition = itemText.anchoredPosition - expectedTextPos;

                float expectedRot = GetRotation(angleDeg, itemTextRotation, itemTextRotationOffset);
                if (flipTextOnBottomHalf && radial.y < 0f)
                {
                    expectedRot += 180f;
                }

                tweak.extraItemTextRotation = Mathf.DeltaAngle(expectedRot, itemText.localEulerAngles.z);
            }

            newTweaks[i] = tweak;
        }

        slotTweaks = newTweaks;

    #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
    #endif

        Debug.Log("Captured current slot positions into slotTweaks.");
    }
}