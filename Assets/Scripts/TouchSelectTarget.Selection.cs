using System;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class TouchSelectTarget
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            pointerDownPosition =
                Input.mousePosition;

            pointerStartedOverUI =
                IsPointerOverUI();

            pointerMoved = false;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 pointerDelta =
                Input.mousePosition -
                pointerDownPosition;

            if (!pointerStartedOverUI &&
                !pointerMoved &&
                pointerDelta.magnitude > tapThreshold)
            {
                pointerMoved = true;
                HidePanel();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            Vector3 pointerDelta =
                Input.mousePosition -
                pointerDownPosition;

            if (!pointerStartedOverUI &&
                !IsPointerOverUI() &&
                !pointerMoved &&
                pointerDelta.magnitude <= tapThreshold)
            {
                SelectTarget();
            }
        }

        UpdatePanelPosition();
    }

    void SelectTarget()
    {
        if (IsPointerOverUI())
        {
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null)
        {
            Debug.Log("Kh\u00F4ng t\u00ECm th\u1EA5y Camera Main");
            return;
        }

        Vector2 worldPos =
            CameraWorldPlaneUtility.ScreenToWorldOnPlane(
                cam,
                Input.mousePosition);

        Transform selectedTarget =
            GetClosestSelectableTarget(
                Physics2D.OverlapPointAll(worldPos),
                worldPos,
                false);

        if (selectedTarget == null &&
            selectionAssistRadius > 0f)
        {
            selectedTarget =
                GetClosestSelectableTarget(
                    Physics2D.OverlapCircleAll(
                        worldPos,
                        selectionAssistRadius),
                    worldPos,
                    true);
        }

        if (selectedTarget == null)
        {
            HidePanel();
            return;
        }

        currentTarget =
            selectedTarget;

        CurrentTarget =
            selectedTarget;

        if (cameraController != null)
        {
            cameraController.FollowImmediately(
                selectedTarget);
        }

        bool isWorldItem =
            IsWorldItemTarget(selectedTarget);

        if (infoPanel != null)
        {
            infoPanel.SetActive(!isWorldItem);
        }

        SetWorldItemPanelVisible(isWorldItem);

        if (isWorldItem)
        {
            ShowWorldItemInfo(selectedTarget);

            if (npcInventoryPanel != null)
            {
                npcInventoryPanel.Hide();
            }

            return;
        }

        if (npcInventoryPanel != null)
        {
            if (CanShowInventoryForTarget(selectedTarget))
            {
                npcInventoryPanel.Show(selectedTarget);
            }
            else
            {
                npcInventoryPanel.HideContentOnly();
            }
        }

        ShowInfoTab();
    }

    Transform GetSelectableTarget(Collider2D hit)
    {
        WorldStatItemPickup pickup =
            hit.GetComponentInParent<WorldStatItemPickup>();

        if (pickup != null &&
            pickup.item != null &&
            pickup.amount > 0)
        {
            return pickup.transform;
        }

        Transform taggedNpc =
            GetTaggedNpcTarget(hit);

        if (taggedNpc != null)
        {
            return taggedNpc;
        }

        SmartNpcAI smartNpc =
            hit.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        VillagerAI villager =
            hit.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.transform;
        }

        MonsterAI monster =
            hit.GetComponentInParent<MonsterAI>();

        if (monster != null)
        {
            return monster.transform;
        }

        return null;
    }

    Transform GetTaggedNpcTarget(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        Transform taggedTarget = null;
        Transform current = hit.transform;

        while (current != null)
        {
            if (IsNpcTag(current))
            {
                taggedTarget = current;
                break;
            }

            current = current.parent;
        }

        if (taggedTarget == null)
        {
            return null;
        }

        SmartNpcAI smartNpc =
            taggedTarget.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.transform;
        }

        VillagerAI villager =
            taggedTarget.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.transform;
        }

        CharacterStats stats =
            taggedTarget.GetComponentInParent<CharacterStats>();

        if (stats != null)
        {
            return stats.transform;
        }

        ItemInventory inventory =
            taggedTarget.GetComponentInParent<ItemInventory>();

        if (inventory != null)
        {
            return inventory.transform;
        }

        return taggedTarget;
    }

    bool IsNpcTag(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return string.Equals(
            target.gameObject.tag,
            "npc",
            StringComparison.OrdinalIgnoreCase);
    }

    Transform GetClosestSelectableTarget(
        Collider2D[] hits,
        Vector2 worldPos,
        bool allowNearbyAssist)
    {
        Transform closestTarget = null;
        int closestPriority = int.MaxValue;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            Transform target =
                GetSelectableTarget(hit);

            if (target == null)
            {
                continue;
            }

            float distance =
                GetSelectionDistance(
                    hit,
                    target,
                    worldPos,
                    allowNearbyAssist);

            if (float.IsInfinity(distance))
            {
                continue;
            }

            int priority = GetSelectableTargetPriority(target);

            if (priority < closestPriority ||
                (priority == closestPriority &&
                distance < closestDistance))
            {
                closestPriority = priority;
                closestDistance = distance;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    float GetSelectionDistance(
        Collider2D hit,
        Transform target,
        Vector2 worldPos,
        bool allowNearbyAssist)
    {
        if (hit == null ||
            target == null)
        {
            return Mathf.Infinity;
        }

        Vector2 closestPoint =
            hit.OverlapPoint(worldPos)
            ? worldPos
            : hit.ClosestPoint(worldPos);

        float colliderDistance =
            Vector2.Distance(worldPos, closestPoint);

        if (!allowNearbyAssist)
        {
            return colliderDistance <= 0.001f
                ? colliderDistance
                : Mathf.Infinity;
        }

        return colliderDistance;
    }

    int GetSelectableTargetPriority(Transform target)
    {
        if (target == null)
        {
            return int.MaxValue;
        }

        if (IsNpcTarget(target) ||
            target.GetComponent<MonsterAI>() != null ||
            target.GetComponent<BicanhBoneMonsterAI>() != null)
        {
            return 0;
        }

        if (IsWorldItemTarget(target))
        {
            return 1;
        }

        return 2;
    }

    bool IsPointerOverUI()
    {
        Vector2 screenPosition =
            Input.touchCount > 0
            ? Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;

        if (IsScreenPositionInsideKnownUi(screenPosition))
        {
            return true;
        }

        if (EventSystem.current != null)
        {
            if (Input.touchCount > 0)
            {
                return EventSystem.current.IsPointerOverGameObject(
                    Input.GetTouch(0).fingerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        return false;
    }

    bool IsScreenPositionInsideKnownUi(Vector2 screenPosition)
    {
        if (IsScreenPositionInsideRect(
                infoPanel != null
                ? infoPanel.transform as RectTransform
                : null,
                screenPosition) ||
            IsScreenPositionInsideRect(
                worldItemInfoPanel != null
                ? worldItemInfoPanel.transform as RectTransform
                : null,
                screenPosition))
        {
            return true;
        }

        InventoryPanelUI[] inventoryPanels =
            FindObjectsByType<InventoryPanelUI>(
                FindObjectsInactive.Include);

        foreach (InventoryPanelUI panel in inventoryPanels)
        {
            if (panel == null)
            {
                continue;
            }

            if (IsScreenPositionInsideRect(
                    panel.panelRoot != null
                    ? panel.panelRoot.transform as RectTransform
                    : null,
                    screenPosition) ||
                IsScreenPositionInsideRect(
                    panel.itemGridParent as RectTransform,
                    screenPosition) ||
                IsScreenPositionInsideRect(
                    panel.detailPanel != null
                    ? panel.detailPanel.transform as RectTransform
                    : null,
                    screenPosition))
            {
                return true;
            }
        }

        InventoryItemButtonUI[] itemButtons =
            FindObjectsByType<InventoryItemButtonUI>(
                FindObjectsInactive.Include);

        foreach (InventoryItemButtonUI button in itemButtons)
        {
            if (button == null ||
                !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (IsScreenPositionInsideRect(
                    button.transform as RectTransform,
                    screenPosition))
            {
                return true;
            }
        }

        return false;
    }

    bool IsScreenPositionInsideRect(
        RectTransform rect,
        Vector2 screenPosition)
    {
        if (rect == null ||
            !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas canvas =
            rect.GetComponentInParent<Canvas>();

        Camera eventCamera = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera =
                canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            screenPosition,
            eventCamera);
    }
}
