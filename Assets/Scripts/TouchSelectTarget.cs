using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Text;

public class TouchSelectTarget : MonoBehaviour
{
    public static Transform CurrentTarget { get; private set; }

    [Header("Camera")]
    public MobileCameraController cameraController;

    [Header("UI")]
    public GameObject infoPanel;

    public GameObject infoContentRoot;

    public GameObject inventoryContentRoot;

    public TMP_Text infoText;

    public NpcInventoryPanelUI npcInventoryPanel;

    public Button infoButton;

    public Button inventoryButton;

    [Header("Panel Follow")]
    public Vector3 panelOffset =
        new Vector3(0, 2f, 0);

    [Header("Tap")]
    public float tapThreshold = 10f;

    Camera cam;

    RectTransform panelRect;

    Transform currentTarget;

    Vector3 pointerDownPosition;

    bool pointerStartedOverUI;

    bool pointerMoved;

    bool showingInventory;

    void Start()
    {
        cam = Camera.main;

        if (cameraController == null &&
            cam != null)
        {
            cameraController =
                cam.GetComponent<MobileCameraController>();

            if (cameraController == null)
            {
                cameraController =
                    FindObjectOfType<MobileCameraController>(true);
            }

            if (cameraController == null)
            {
                cameraController =
                    cam.gameObject.AddComponent<MobileCameraController>();
            }
        }

        if (infoPanel != null)
        {
            panelRect =
                infoPanel.GetComponent<RectTransform>();
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.Hide();
        }

        if (npcInventoryPanel == null)
        {
            npcInventoryPanel =
                FindObjectOfType<NpcInventoryPanelUI>(true);
        }

        BindTabButtons();
    }

    void OnEnable()
    {
        BindTabButtons();
    }

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
            Debug.Log("Không tìm thấy Camera Main");
            return;
        }

        Vector2 worldPos =
            cam.ScreenToWorldPoint(
                Input.mousePosition);

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                worldPos,
                0.5f);

        Transform selectedTarget =
            GetClosestSelectableTarget(
                hits,
                worldPos);

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

        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }

        if (false && infoText != null)
        {
            infoText.text =
                "Tên: " +
                GetTargetName(selectedTarget) +

                "\nTu Vi: " +
                GetTargetRealm(selectedTarget) +

                "\nMáu: " +
                GetTargetCurrentHP(selectedTarget) +
                " / " +
                GetTargetMaxHP(selectedTarget) +

                "\nHành động: " +
                GetTargetAction(selectedTarget);
        }

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.Show(selectedTarget);
        }

        ShowInfoTab();
    }

    public void ShowInfoTab()
    {
        showingInventory = false;

        if (infoText != null &&
            currentTarget != null)
        {
            infoText.text =
                BuildTargetInfo(currentTarget);
        }

        SetInfoContentVisible(true);
        SetInventoryContentVisible(false);

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.SetContentVisible(false);
        }
    }

    public void ShowInventoryTab()
    {
        showingInventory = true;

        SetInfoContentVisible(false);
        SetInventoryContentVisible(true);

        if (npcInventoryPanel != null &&
            currentTarget != null)
        {
            npcInventoryPanel.Show(currentTarget);
            npcInventoryPanel.SetContentVisible(true);
        }
    }

    void HideTabContents()
    {
        showingInventory = false;

        SetInfoContentVisible(false);
        SetInventoryContentVisible(false);

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.SetContentVisible(false);
        }
    }

    void BindTabButtons()
    {
        AutoFindTabReferences();

        if (infoButton != null)
        {
            infoButton.onClick.RemoveListener(ShowInfoTab);

            if (!HasPersistentListener(infoButton, nameof(ShowInfoTab)))
            {
                infoButton.onClick.AddListener(ShowInfoTab);
            }

            infoButton.interactable = true;
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveListener(ShowInventoryTab);

            if (!HasPersistentListener(inventoryButton, nameof(ShowInventoryTab)))
            {
                inventoryButton.onClick.AddListener(ShowInventoryTab);
            }

            inventoryButton.interactable = true;
        }
    }

    bool HasPersistentListener(Button button, string methodName)
    {
        if (button == null)
        {
            return false;
        }

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }

        return false;
    }

    void AutoFindTabReferences()
    {
        if (infoPanel == null)
        {
            return;
        }

        if (infoContentRoot == null)
        {
            Transform infoContent =
                FindChildByName(infoPanel.transform, "InfoContent");

            if (infoContent != null)
            {
                infoContentRoot = infoContent.gameObject;
            }
        }

        if (inventoryContentRoot == null)
        {
            Transform inventoryContent =
                FindChildByName(infoPanel.transform, "InventoryContent");

            if (inventoryContent != null)
            {
                inventoryContentRoot = inventoryContent.gameObject;
            }
        }

        if (infoText == null)
        {
            Transform infoContentText =
                infoContentRoot != null
                ? FindChildByName(infoContentRoot.transform, "InfoText")
                : null;

            if (infoContentText != null)
            {
                infoText = infoContentText.GetComponent<TMP_Text>();
            }
        }

        if (infoButton == null)
        {
            infoButton =
                FindButtonByName(
                    infoPanel.transform,
                    "InfoButton",
                    "Thong Tin",
                    "Thông Tin");
        }

        if (infoText == null &&
            infoButton != null)
        {
            Transform buttonText =
                FindChildByName(infoButton.transform, "InfoText");

            if (buttonText == null)
            {
                buttonText =
                    FindChildByName(infoButton.transform, "Thông Tin");
            }

            if (buttonText == null)
            {
                buttonText =
                    FindChildByName(infoButton.transform, "Thong Tin");
            }

            if (buttonText != null)
            {
                infoText = buttonText.GetComponent<TMP_Text>();
            }

            if (infoText == null)
            {
                infoText =
                    infoButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (inventoryButton == null)
        {
            inventoryButton =
                FindButtonByName(
                    infoPanel.transform,
                    "InventoryButton",
                    "Kho");
        }
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

        if (infoText != null)
        {
            infoText.gameObject.SetActive(visible);
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

    Transform GetSelectableTarget(Collider2D hit)
    {
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

    Transform GetClosestSelectableTarget(
        Collider2D[] hits,
        Vector2 worldPos)
    {
        Transform closestTarget = null;
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
                Vector2.Distance(
                    worldPos,
                    target.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    string GetTargetName(Transform target)
    {
        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.villagerName;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.monsterName;
        }

        return target.name;
    }

    string GetTargetRealm(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.GetRealmText() +
                " " +
                characterStats.realmStage;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.realm + " " + smartNpc.realmStage;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.GetRealmText();
        }

        return "Yeu Thu";
    }

    int GetTargetCurrentHP(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.currentHP;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.currentHP;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.currentHP;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.currentHP;
        }

        return 0;
    }

    int GetTargetMaxHP(Transform target)
    {
        CharacterStats characterStats =
            target.GetComponent<CharacterStats>();

        if (characterStats != null)
        {
            return characterStats.finalHP;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.maxHP;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.maxHP;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return monster.maxHP;
        }

        return 0;
    }

    string GetTargetAction(Transform target)
    {
        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.currentAction;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.currentAction;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            return "Sat thuong: " + monster.damage;
        }

        return "";
    }

    string BuildTargetInfo(Transform target)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine("Ten: " + GetTargetName(target));
        builder.AppendLine("Tuoi: " + GetTargetAge(target));
        builder.AppendLine("Tho Nguyen: " + GetTargetLifespan(target));
        builder.AppendLine("Nghe: " + GetTargetJob(target));
        builder.AppendLine("Tu Vi: " + GetTargetRealm(target));
        builder.AppendLine("Mau: " + BuildHealthText(target));

        string manuals =
            BuildManualStudyText(target);

        if (!string.IsNullOrEmpty(manuals))
        {
            builder.AppendLine("Cong Phap:");
            builder.Append(manuals);
        }

        builder.AppendLine("Hanh dong: " + GetTargetAction(target));

        return builder.ToString().TrimEnd();
    }

    string GetTargetAge(Transform target)
    {
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null &&
            villager.GetAge() > 0)
        {
            return villager.GetAge().ToString();
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null &&
            smartNpc.GetAge() > 0)
        {
            return smartNpc.GetAge().ToString();
        }

        return "-";
    }

    string GetTargetLifespan(Transform target)
    {
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.GetLifespan().ToString();
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.GetLifespan().ToString();
        }

        return "-";
    }

    string GetTargetJob(Transform target)
    {
        NpcSpecialProfession profession =
            target.GetComponent<NpcSpecialProfession>();

        if (profession != null &&
            !string.IsNullOrEmpty(profession.professionName))
        {
            return profession.professionName;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        return villager != null
            ? villager.job.ToString()
            : "-";
    }

    string BuildHealthText(Transform target)
    {
        int maxHP =
            Mathf.Max(1, GetTargetMaxHP(target));

        int currentHP =
            Mathf.Clamp(GetTargetCurrentHP(target), 0, maxHP);

        return BuildBar(currentHP, maxHP, 12) +
            " " +
            currentHP +
            " / " +
            maxHP;
    }

    string BuildBar(
        int current,
        int max,
        int width)
    {
        float percent =
            Mathf.Clamp01((float)current / Mathf.Max(1, max));

        int filled =
            Mathf.RoundToInt(width * percent);

        StringBuilder builder =
            new StringBuilder("[");

        for (int i = 0; i < width; i++)
        {
            builder.Append(i < filled ? "#" : "-");
        }

        builder.Append("]");
        return builder.ToString();
    }

    string BuildManualStudyText(Transform target)
    {
        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            return "";
        }

        StringBuilder builder =
            new StringBuilder();

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0 ||
                stack.item.itemType != ItemType.CongPhap)
            {
                continue;
            }

            builder.Append(" - ");
            builder.Append(stack.item.itemName);
            builder.Append(": ");
            builder.Append(GetManualMasteryText(stack.mastery));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    string GetManualMasteryText(CultivationManualMastery mastery)
    {
        switch (mastery)
        {
            case CultivationManualMastery.TieuThanh:
                return "Tieu Thanh";
            case CultivationManualMastery.TrungThanh:
                return "Trung Thanh";
            case CultivationManualMastery.DaiThanh:
                return "Dai Thanh";
            default:
                return "Chua hoc";
        }
    }

    int GetTargetMoney(Transform target)
    {
        VillagerAI villager =
            target.GetComponent<VillagerAI>();

        if (villager != null)
        {
            return villager.money;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();

        return smartNpc != null
            ? smartNpc.money
            : 0;
    }

    string GetInventorySummary(Transform target)
    {
        ItemInventory inventory =
            target.GetComponent<ItemInventory>();

        if (inventory == null)
        {
            return "Trong";
        }

        int itemKinds = 0;
        int totalAmount = 0;

        foreach (ItemStack stack in inventory.items)
        {
            if (stack == null ||
                stack.item == null ||
                stack.amount <= 0)
            {
                continue;
            }

            itemKinds++;
            totalAmount += stack.amount;
        }

        if (itemKinds <= 0)
        {
            return "Trong";
        }

        return itemKinds + " loai / " + totalAmount + " mon";
    }

    void HidePanel()
    {
        currentTarget = null;
        CurrentTarget = null;

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        if (npcInventoryPanel != null)
        {
            npcInventoryPanel.Hide();
        }
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
                screenPosition))
        {
            return true;
        }

        InventoryPanelUI[] inventoryPanels =
            FindObjectsOfType<InventoryPanelUI>(true);

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
            FindObjectsOfType<InventoryItemButtonUI>(true);

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

    void UpdatePanelPosition()
    {
        if (currentTarget == null)
        {
            return;
        }

        if (panelRect == null)
        {
            return;
        }

        if (infoText != null)
        {
            infoText.text =
                BuildTargetInfo(currentTarget);
        }

        if (npcInventoryPanel != null)
        {
            // Inventory UI rebuilds item buttons, so do not refresh it every frame.
            // It refreshes when opened and when the selected inventory changes.
        }

        Vector3 screenPos =
            cam.WorldToScreenPoint(
                currentTarget.position +
                panelOffset);

        panelRect.position =
            screenPos;
    }
}
