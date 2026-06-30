using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeavenNurtureListItemUI : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image avatarImage;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text realmText;
    [SerializeField] TMP_Text relationText;
    [SerializeField] TMP_Text stateTagText;
    [SerializeField] Image stateIcon;
    [SerializeField] GameObject selectedHighlight;
    [SerializeField] Outline selectedOutline;

    HeavenNurtureTargetData boundData;

    public HeavenNurtureTargetData BoundData => boundData;

    public void Setup(
        HeavenNurtureTargetData data,
        Action<HeavenNurtureTargetData, HeavenNurtureListItemUI> onClick)
    {
        boundData = data;

        if (nameText != null)
        {
            nameText.text = data != null ? data.displayName : string.Empty;
        }

        if (realmText != null)
        {
            realmText.text = data != null ? data.realm : string.Empty;
        }

        if (relationText != null)
        {
            relationText.text = data != null
                ? UiText.Format("heavenNurture", "fearFormat", data.fear)
                : string.Empty;
        }

        if (stateTagText != null)
        {
            stateTagText.text = data != null ? data.fateState : string.Empty;
        }

        if (avatarImage != null)
        {
            avatarImage.sprite = data != null ? data.portrait : null;
            avatarImage.enabled = avatarImage.sprite != null;
        }

        if (stateIcon != null)
        {
            stateIcon.enabled = false;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (data != null && onClick != null)
            {
                button.onClick.AddListener(() => onClick(data, this));
            }
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null)
        {
            selectedHighlight.SetActive(selected);
        }

        if (selectedOutline != null)
        {
            selectedOutline.enabled = selected;
        }
    }
}
