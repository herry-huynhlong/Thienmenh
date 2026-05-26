using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class TouchSelectTarget : MonoBehaviour
{
    public static Transform CurrentTarget { get; private set; }

    [Header("Camera")]
    public MobileCameraController cameraController;

    [Header("UI")]
    public GameObject infoPanel;

    public TMP_Text infoText;

    public NpcInventoryPanelUI npcInventoryPanel;

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

            if (!pointerMoved &&
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

        if (infoText != null)
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
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(
                Input.GetTouch(0).fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
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

        Vector3 screenPos =
            cam.WorldToScreenPoint(
                currentTarget.position +
                panelOffset);

        panelRect.position =
            screenPos;
    }
}
