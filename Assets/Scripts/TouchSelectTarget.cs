using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class TouchSelectTarget : MonoBehaviour
{
    [Header("Camera")]
    public MobileCameraController cameraController;

    [Header("UI")]
    public GameObject infoPanel;

    public TMP_Text infoText;

    [Header("Panel Follow")]
    public Vector3 panelOffset =
        new Vector3(0, 2f, 0);

    Camera cam;

    RectTransform panelRect;

    Transform currentTarget;

    void Start()
    {
        cam = Camera.main;

        panelRect =
            infoPanel.GetComponent<RectTransform>();

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SelectTarget();
        }

        UpdatePanelPosition();
    }

    void SelectTarget()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
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

        Collider2D hit =
            Physics2D.OverlapCircle(
                worldPos,
                0.5f);

        if (hit == null)
        {
            return;
        }

        NpcData npc =
            hit.GetComponent<NpcData>();

        if (npc == null)
        {
            return;
        }

        currentTarget =
            npc.transform;

        // Camera follow
        if (cameraController != null)
        {
            cameraController.followTarget =
                npc.transform;
        }

        // Hiện panel
        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }

        // Update text
        if (infoText != null)
        {
            infoText.text =
                "Tên: " +
                npc.npcName +

                "\nTu Vi: " +
                npc.realm +

                "\nMáu: " +
                npc.hp +
                " / " +
                npc.maxHp +

                "\nHành động: " +
                npc.currentAction;
        }
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