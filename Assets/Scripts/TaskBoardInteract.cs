using UnityEngine;
using UnityEngine.EventSystems;

public class TaskBoardInteract : MonoBehaviour
{
    public NpcTaskProvider provider;
    public TaskBoardPanelUI panel;

    private Collider2D boardCollider;

    void Awake()
    {
        boardCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryOpenBoard(Input.mousePosition, -1);
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                TryOpenBoard(touch.position, touch.fingerId);
            }
        }
    }

    void TryOpenBoard(Vector2 screenPosition, int fingerId)
    {
        if (IsPointerOverUI(fingerId))
        {
            return;
        }

        if (panel == null)
        {
            Debug.LogWarning("Chưa gán TaskBoardPanelUI.");
            return;
        }

        if (provider == null)
        {
            Debug.LogWarning("Chưa gán NpcTaskProvider.");
            return;
        }

        Camera cam = Camera.main;

        if (cam == null)
        {
            cam = FindAnyObjectByType<Camera>();
        }

        if (cam == null)
        {
            Debug.LogWarning("Không tìm thấy Camera trong scene.");
            return;
        }

        if (boardCollider == null)
        {
            boardCollider = GetComponent<Collider2D>();
        }

        if (boardCollider == null)
        {
            Debug.LogWarning("Bảng nhiệm vụ chưa có Collider2D.");
            return;
        }

        Vector2 worldPosition = cam.ScreenToWorldPoint(screenPosition);

        if (!boardCollider.OverlapPoint(worldPosition))
        {
            return;
        }

        panel.Show(provider);
    }

    bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (fingerId >= 0)
        {
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}
