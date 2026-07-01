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

    void Awake()
    {
        AutoResolveReferences();
    }

    public void Setup(
        HeavenNurtureTargetData data,
        Action<HeavenNurtureTargetData, HeavenNurtureListItemUI> onClick)
    {
        AutoResolveReferences();
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
            avatarImage.sprite = null;
            avatarImage.enabled = false;
        }

        if (stateIcon != null)
        {
            stateIcon.sprite = data != null ? data.portrait : null;
            stateIcon.enabled = stateIcon.sprite != null;
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

    void AutoResolveReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (selectedOutline == null)
        {
            selectedOutline = GetComponent<Outline>();
        }

        nameText = nameText != null
            ? nameText
            : FindChildText("NameText");
        realmText = realmText != null
            ? realmText
            : FindChildText("RealmText");
        relationText = relationText != null
            ? relationText
            : FindChildText("RelationText");
        stateTagText = stateTagText != null
            ? stateTagText
            : FindChildText("StateTagText");
        stateIcon = stateIcon != null
            ? stateIcon
            : FindChildImage("StateIcon");
        avatarImage = avatarImage != null
            ? avatarImage
            : FindChildImage("Image");
    }

    TMP_Text FindChildText(string objectName)
    {
        Transform child = FindChildRecursive(transform, objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    Image FindChildImage(string objectName)
    {
        Transform child = FindChildRecursive(transform, objectName);
        if (child != null)
        {
            return child.GetComponent<Image>();
        }

        return null;
    }

    static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursive(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
