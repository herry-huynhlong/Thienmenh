using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class TouchSelectTarget
{
    bool UpdateTargetDetailPanel(Transform target)
    {
        if (target == null ||
            (!IsNpcTarget(target) && !IsMonsterTarget(target)) ||
            infoPanel == null)
        {
            ClearTargetDetailPanel();
            return false;
        }

        AutoFindTabReferences();

        string lockedMessage =
            NpcText.Get("dialogue", "heavenDaoBasicLocked");

        if (false && !HasHeavenDaoPower(HeavenDaoPower.ViewBasicNpcInfo))
        {
            SetValueText(damageValueText, "-");
            SetValueText(defenseValueText, "-");
            SetValueText(lifespanValueText, "-");
            SetValueText(jobValueText, "-");
            SetValueText(statusText, lockedMessage);
            ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);
            ClearSpawnedRows(spawnedSkillRows, skillListRoot);
            return true;
        }

        bool hasAnyDetail = false;

        hasAnyDetail |=
            SetValueText(
                damageValueText,
                FormatMaybeInt(GetTargetAttack(target)));

        hasAnyDetail |=
            SetValueText(
                defenseValueText,
                FormatMaybeInt(GetTargetDefense(target)));

        hasAnyDetail |=
            SetValueText(
                lifespanValueText,
                GetTargetLifespan(target));

        hasAnyDetail |=
            SetValueText(
                jobValueText,
                GetTargetJob(target));

        hasAnyDetail |=
            SetOptionalValueText(
                maritalStatusText,
                GetTargetMarriageStatus(target));

        hasAnyDetail |=
            SetStatusText(
                statusText,
                FormatTargetActionText(target));

        hasAnyDetail |=
            RefreshEquipmentRows(target);

        hasAnyDetail |=
            RefreshSkillRows(target);

        return hasAnyDetail;
    }

    void ClearTargetDetailPanel()
    {
        SetValueText(damageValueText, "-");
        SetValueText(defenseValueText, "-");
        SetValueText(lifespanValueText, "-");
        SetValueText(jobValueText, "-");
        SetValueText(maritalStatusText, "-");
        SetValueText(statusText, "-");
        ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);
        ClearSpawnedRows(spawnedSkillRows, skillListRoot);
        SetSectionVisible(equipmentListRoot, false);
        SetSectionVisible(skillListRoot, false);
    }

    bool RefreshEquipmentRows(Transform target)
    {
        ItemInventory inventory =
            target != null
            ? target.GetComponent<ItemInventory>()
            : null;

        if (inventory == null ||
            equipmentListRoot == null)
        {
            ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);
            SetSectionVisible(equipmentListRoot, false);
            return false;
        }

        List<ItemStack> equipmentStacks =
            CollectEquipmentStacks(inventory);

        ClearSpawnedRows(spawnedEquipmentRows, equipmentListRoot);

        if (equipmentStacks.Count <= 0)
        {
            SetSectionVisible(equipmentListRoot, false);
            return false;
        }

        SetSectionVisible(equipmentListRoot, true);

        GameObject template =
            GetRowTemplate(equipmentListRoot, equipmentRowTemplate);

        if (template == null)
        {
            return false;
        }

        for (int i = 0; i < equipmentStacks.Count; i++)
        {
            GameObject row =
                GetOrCreateRow(
                    equipmentListRoot,
                    template,
                    i,
                    spawnedEquipmentRows);

            if (row == null)
            {
                continue;
            }

            row.SetActive(true);
            BindEquipmentRow(row, equipmentStacks[i]);
        }

        return true;
    }

    bool RefreshSkillRows(Transform target)
    {
        ItemInventory inventory =
            target != null
            ? target.GetComponent<ItemInventory>()
            : null;

        if (inventory == null ||
            skillListRoot == null)
        {
            ClearSpawnedRows(spawnedSkillRows, skillListRoot);
            SetSectionVisible(skillListRoot, false);
            return false;
        }

        NpcHideCultivationInfo hideInfo =
            target.GetComponentInParent<NpcHideCultivationInfo>();

        if (hideInfo != null &&
            hideInfo.hideCultivationSkills)
        {
            ClearSpawnedRows(spawnedSkillRows, skillListRoot);
            SetSectionVisible(skillListRoot, false);
            return false;
        }

        List<ItemStack> skillStacks =
            CollectSkillStacks(inventory);

        ClearSpawnedRows(spawnedSkillRows, skillListRoot);

        if (skillStacks.Count <= 0)
        {
            SetSectionVisible(skillListRoot, false);
            return false;
        }

        SetSectionVisible(skillListRoot, true);

        GameObject template =
            GetRowTemplate(skillListRoot, skillRowTemplate);

        if (template == null)
        {
            return false;
        }

        for (int i = 0; i < skillStacks.Count; i++)
        {
            GameObject row =
                GetOrCreateRow(
                    skillListRoot,
                    template,
                    i,
                    spawnedSkillRows);

            if (row == null)
            {
                continue;
            }

            row.SetActive(true);
            BindSkillRow(row, skillStacks[i]);
        }

        return true;
    }

    void ClearSpawnedRows(
        List<GameObject> spawnedRows,
        Transform root)
    {
        if (spawnedRows != null)
        {
            spawnedRows.RemoveAll(item => item == null);
        }

        if (root == null)
        {
            return;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    void SetSectionVisible(
        Transform root,
        bool visible)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.SetActive(visible);
    }

    GameObject FindSectionContainer(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform current = root;
        while (current != null)
        {
            string key = NormalizeSectionName(current.name);
            if (key == "equipmentpanel" ||
                key == "skillpanel")
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return root.gameObject;
    }

    string NormalizeSectionName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    GameObject GetRowTemplate(
        Transform root,
        GameObject fallbackTemplate)
    {
        if (fallbackTemplate != null)
        {
            return fallbackTemplate;
        }

        if (root != null &&
            root.childCount > 0)
        {
            return root.GetChild(0).gameObject;
        }

        return null;
    }

    GameObject GetOrCreateRow(
        Transform root,
        GameObject template,
        int index,
        List<GameObject> spawnedRows)
    {
        if (root == null ||
            template == null ||
            index < 0)
        {
            return null;
        }

        if (index < root.childCount)
        {
            return root.GetChild(index).gameObject;
        }

        GameObject row =
            Instantiate(
                template,
                root,
                false);

        row.name = template.name;

        if (spawnedRows != null)
        {
            spawnedRows.Add(row);
        }

        return row;
    }

    List<ItemStack> CollectEquipmentStacks(ItemInventory inventory)
    {
        List<ItemStack> stacks =
            new List<ItemStack>();

        if (inventory == null)
        {
            return stacks;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.PhapBao ||
                !stack.applied)
            {
                continue;
            }

            EquipmentSlot slot =
                stack.item.GetResolvedEquipmentSlot();

            if (slot == EquipmentSlot.None)
            {
                continue;
            }

            stacks.Add(stack);
        }

        stacks.Sort(
            (left, right) =>
            {
                int leftApplied =
                    left != null && left.applied ? 0 : 1;
                int rightApplied =
                    right != null && right.applied ? 0 : 1;

                int compare =
                    leftApplied.CompareTo(rightApplied);
                if (compare != 0)
                {
                    return compare;
                }

                EquipmentSlot leftSlot =
                    left != null && left.item != null
                    ? left.item.GetResolvedEquipmentSlot()
                    : EquipmentSlot.None;

                EquipmentSlot rightSlot =
                    right != null && right.item != null
                    ? right.item.GetResolvedEquipmentSlot()
                    : EquipmentSlot.None;

                return leftSlot.CompareTo(rightSlot);
            });

        return stacks;
    }

    List<ItemStack> CollectSkillStacks(ItemInventory inventory)
    {
        List<ItemStack> stacks =
            new List<ItemStack>();

        if (inventory == null)
        {
            return stacks;
        }

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.CongPhap ||
                stack.broken ||
                stack.item.IsManualBroken(stack))
            {
                continue;
            }

            if (!stack.applied &&
                stack.mastery == CultivationManualMastery.None)
            {
                continue;
            }

            stacks.Add(stack);
        }

        stacks.Sort(
            (left, right) =>
            {
                float leftPower =
                    GetManualPower(left);
                float rightPower =
                    GetManualPower(right);

                int compare =
                    rightPower.CompareTo(leftPower);
                if (compare != 0)
                {
                    return compare;
                }

                string leftName =
                    left != null && left.item != null
                    ? ItemText.Name(left.item)
                    : "";
                string rightName =
                    right != null && right.item != null
                    ? ItemText.Name(right.item)
                    : "";
                return string.Compare(
                    leftName,
                    rightName,
                    StringComparison.Ordinal);
            });

        return stacks;
    }

    void BindEquipmentRow(
        GameObject row,
        ItemStack stack)
    {
        if (row == null ||
            stack == null ||
            stack.item == null)
        {
            return;
        }

        Image icon =
            FindRowImage(row, "Icon");
        if (icon == null)
        {
            icon =
                row.GetComponentInChildren<Image>(true);
        }

        if (icon != null)
        {
            Sprite itemIcon = stack.item.icon;
            icon.sprite = itemIcon;
            icon.enabled = itemIcon != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        TMP_Text nameText =
            FindRowText(row, "ItemNameText");
        if (nameText == null)
        {
            nameText = FindRowText(row, "NameText");
        }

        if (nameText != null)
        {
            nameText.text = ItemText.Name(stack.item);
            nameText.raycastTarget = false;
        }

        TMP_Text slotText =
            FindRowText(row, "SlotText");

        if (slotText != null)
        {
            slotText.text = GetEquipmentSlotLabel(stack.item);
            slotText.raycastTarget = false;
        }
    }

    void BindSkillRow(
        GameObject row,
        ItemStack stack)
    {
        if (row == null ||
            stack == null ||
            stack.item == null)
        {
            return;
        }

        Image icon =
            FindRowImage(row, "Icon");
        if (icon == null)
        {
            icon =
                row.GetComponentInChildren<Image>(true);
        }

        if (icon != null)
        {
            Sprite itemIcon = stack.item.icon;
            icon.sprite = itemIcon;
            icon.enabled = itemIcon != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        TMP_Text nameText =
            FindRowText(row, "SkillNameText");
        if (nameText == null)
        {
            nameText = FindRowText(row, "NameText");
        }

        if (nameText != null)
        {
            nameText.text = ItemText.Name(stack.item);
            nameText.raycastTarget = false;
        }

        TMP_Text percentText =
            FindRowText(row, "PercentText");

        if (percentText != null)
        {
            percentText.text =
                FormatPercentText(
                    GetManualPower(stack));
            percentText.raycastTarget = false;
        }

        Image fillImage =
            FindRowImage(row, "ProficiencyFill");
        if (fillImage == null)
        {
            Transform fillTransform =
                FindChildByName(row.transform, "ProficiencyFill");

            if (fillTransform != null)
            {
                fillImage = fillTransform.GetComponent<Image>();
            }
        }

        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillAmount =
                Mathf.Clamp01(GetManualPower(stack));
            fillImage.raycastTarget = false;
        }
    }

    TMP_Text FindRowText(
        GameObject row,
        string childName)
    {
        if (row == null ||
            string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform child =
            FindChildByName(row.transform, childName);

        if (child == null)
        {
            return null;
        }

        return child.GetComponent<TMP_Text>();
    }

    Image FindRowImage(
        GameObject row,
        string childName)
    {
        if (row == null ||
            string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform child =
            FindChildByName(row.transform, childName);

        if (child == null)
        {
            return null;
        }

        return child.GetComponent<Image>();
    }

    TMP_Text FindRowValueText(
        Transform root,
        params string[] rowNames)
    {
        if (root == null ||
            rowNames == null ||
            rowNames.Length <= 0)
        {
            return null;
        }

        Transform row = null;

        for (int i = 0; i < rowNames.Length && row == null; i++)
        {
            string rowName = rowNames[i];
            if (string.IsNullOrWhiteSpace(rowName))
            {
                continue;
            }

            row = FindChildByName(root, rowName);
        }

        if (row == null)
        {
            TMP_Text labelText =
                FindTextByDisplayedText(root, rowNames);

            if (labelText != null &&
                labelText.transform.parent != null)
            {
                row = labelText.transform.parent;
            }
        }

        if (row == null)
        {
            return null;
        }

        Transform value =
            FindChildByName(row, "ValueText");

        if (value == null)
        {
            value = FindChildByName(row, "PercentText");
        }

        if (value == null)
        {
            value = FindChildByName(row, "SlotText");
        }

        if (value == null)
        {
            value = FindChildByName(row, "LabelText");
        }

        if (value == null)
        {
            return row.GetComponent<TMP_Text>();
        }

        return value.GetComponent<TMP_Text>();
    }

    bool IsRowLabelText(
        TMP_Text text,
        params string[] labels)
    {
        if (text == null ||
            labels == null ||
            labels.Length <= 0)
        {
            return false;
        }

        string current =
            NormalizeLookupText(text.text);

        for (int i = 0; i < labels.Length; i++)
        {
            string label =
                NormalizeLookupText(labels[i]);

            if (!string.IsNullOrEmpty(label) &&
                current == label)
            {
                return true;
            }
        }

        return false;
    }

    TMP_Text FindTextByDisplayedText(
        Transform root,
        params string[] candidates)
    {
        if (root == null ||
            candidates == null ||
            candidates.Length <= 0)
        {
            return null;
        }

        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            string current =
                NormalizeLookupText(text.text);
            string objectName =
                NormalizeLookupText(text.gameObject.name);

            for (int j = 0; j < candidates.Length; j++)
            {
                string candidate =
                    NormalizeLookupText(candidates[j]);

                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                if (current == candidate ||
                    objectName == candidate)
                {
                    return text;
                }
            }
        }

        return null;
    }

    string NormalizeLookupText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c) ||
                c == '_' ||
                c == ':')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    string GetEquipmentSlotLabel(StatItemData item)
    {
        if (item == null)
        {
            return UiText.Get("touchSelect", "placeholder");
        }

        EquipmentSlot slot =
            item.GetResolvedEquipmentSlot();

        switch (slot)
        {
            case EquipmentSlot.Weapon:
                return UiText.Get("touchSelect", "equipmentWeapon");
            case EquipmentSlot.Armor:
                return UiText.Get("touchSelect", "equipmentArmor");
            case EquipmentSlot.Accessory:
                return UiText.Get("touchSelect", "equipmentAccessory");
        }

        return item.itemType == ItemType.PhapBao
            ? UiText.Get("touchSelect", "equipmentAccessory")
            : UiText.Get("touchSelect", "placeholder");
    }

    float GetManualPower(ItemStack stack)
    {
        if (stack == null ||
            stack.item == null ||
            stack.item.itemType != ItemType.CongPhap)
        {
            return 0f;
        }

        switch (stack.mastery)
        {
            case CultivationManualMastery.TieuThanh:
                return stack.item.tieuThanhPower;
            case CultivationManualMastery.TrungThanh:
                return stack.item.trungThanhPower;
            case CultivationManualMastery.DaiThanh:
                return stack.item.daiThanhPower;
        }

        return 0f;
    }

    string FormatPercentText(float percentValue)
    {
        return Mathf.Clamp(Mathf.RoundToInt(percentValue * 100f), 0, 100) + "%";
    }

    string FormatTargetActionText(Transform target)
    {
        string action =
            GetDisplayAction(
                GetTargetAction(target));

        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        if (action.StartsWith("đang", StringComparison.OrdinalIgnoreCase) ||
            action.StartsWith("Đang", StringComparison.OrdinalIgnoreCase))
        {
            return action;
        }

        return action;
    }

    bool SetStatusText(
        TMP_Text text,
        string action)
    {
        if (text == null)
        {
            return false;
        }

        ConfigureStatusTextLayout(text);

        string label =
            UiText.Get("touchSelect", "statusRowAlt");
        if (string.IsNullOrWhiteSpace(label))
        {
            label = UiText.Get("touchSelect", "statusRow");
        }

        label = (label ?? "").Trim();
        action = string.IsNullOrWhiteSpace(action)
            ? NpcText.Action("idle")
            : action.Trim();

        SetValueText(text, label + "\n" + action);
        return true;
    }

    void ConfigureStatusTextLayout(TMP_Text text)
    {
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax =
            Mathf.Min(
                text.fontSize > 0f ? text.fontSize : 28f,
                28f);
        text.alignment = TextAlignmentOptions.TopLeft;

        RectTransform rect =
            text.rectTransform;
        if (rect != null)
        {
            Vector2 size =
                rect.sizeDelta;
            rect.sizeDelta =
                new Vector2(
                    Mathf.Max(size.x, 260f),
                    Mathf.Max(size.y, 86f));
        }
    }

    bool SetValueText(
        TMP_Text text,
        int value)
    {
        if (text == null)
        {
            return false;
        }

        SetValueText(text, value.ToString());
        return true;
    }

    bool SetValueText(
        TMP_Text text,
        string value)
    {
        if (text == null)
        {
            return false;
        }

        text.text =
            string.IsNullOrEmpty(value)
            ? "-"
            : value;
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        return true;
    }

    bool SetOptionalValueText(
        TMP_Text text,
        string value)
    {
        if (text == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            text.text = "";
            text.gameObject.SetActive(false);
            return false;
        }

        SetValueText(text, value);
        return true;
    }

    bool HasSplitTargetHeaderLayout()
    {
        return nameText != null ||
            realmText != null ||
            hpText != null ||
            hpFillImage != null;
    }

    bool HasTargetDetailPanelLayout()
    {
        return damageValueText != null ||
            defenseValueText != null ||
            lifespanValueText != null ||
            jobValueText != null ||
            statusText != null ||
            equipmentListRoot != null ||
            skillListRoot != null;
    }

    Button FindButtonByName(
        Transform parent,
        params string[] names)
    {
        foreach (string targetName in names)
        {
            Transform found =
                FindChildByName(parent, targetName);

            if (found == null)
            {
                continue;
            }

            Button button =
                found.GetComponent<Button>();

            if (button != null)
            {
                return button;
            }

            button =
                found.GetComponentInParent<Button>();

            if (button != null &&
                button.transform.IsChildOf(parent))
            {
                return button;
            }

            button =
                found.GetComponentInChildren<Button>(true);

            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    Transform FindChildByName(
        Transform parent,
        string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found =
                FindChildByName(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    void SetInfoContentVisible(bool visible)
    {
        if (infoContentRoot != null)
        {
            SetCanvasGroupVisible(infoContentRoot, visible);
        }

        if (infoPanel != null)
        {
            Image panelImage = infoPanel.GetComponent<Image>();

            if (panelImage != null)
            {
                panelImage.raycastTarget = false;
            }
        }

        if (infoText != null)
        {
            infoText.gameObject.SetActive(
                visible &&
                !HasTargetDetailPanelLayout());
        }

        if (!visible)
        {
            SetInfoIconVisible(false);
        }
    }

    void SetInventoryContentVisible(bool visible)
    {
        if (inventoryContentRoot != null)
        {
            SetCanvasGroupVisible(inventoryContentRoot, visible);
        }

        if (npcInventoryPanel != null &&
            npcInventoryPanel.panelRoot != null)
        {
            npcInventoryPanel.SetContentVisible(visible);
        }
    }

    void SetCanvasGroupVisible(GameObject target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        if (!target.activeSelf)
        {
            target.SetActive(true);
        }

        CanvasGroup group =
            target.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
