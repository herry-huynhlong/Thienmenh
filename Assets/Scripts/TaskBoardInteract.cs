using UnityEngine;
using UnityEngine.EventSystems;

public class TaskBoardInteract : MonoBehaviour
{
    public NpcTaskProvider provider;
    public TaskBoardPanelUI panel;

    Collider2D boardCollider;
    bool warnedMissingProvider;

    void Awake()
    {
        boardCollider = GetComponent<Collider2D>();
        ResolveProvider();
    }

    void OnEnable()
    {
        ResolveProvider();
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
        if (panel != null && panel.gameObject.activeInHierarchy)
        {
            return;
        }

        if (IsPointerOverUI(fingerId))
        {
            return;
        }

        if (panel == null)
        {
            Debug.LogWarning("Missing TaskBoardPanelUI.");
            return;
        }

        if (provider == null && !ResolveProvider())
        {
            WarnMissingProviderOnce();
            return;
        }

        Camera cam = Camera.main;

        if (cam == null)
        {
            cam = FindAnyObjectByType<Camera>();
        }

        if (cam == null)
        {
            Debug.LogWarning("Camera not found in scene.");
            return;
        }

        if (boardCollider == null)
        {
            boardCollider = GetComponent<Collider2D>();
        }

        if (boardCollider == null)
        {
            Debug.LogWarning("Task board is missing a Collider2D.");
            return;
        }

        Vector2 worldPosition = cam.ScreenToWorldPoint(screenPosition);

        if (!boardCollider.OverlapPoint(worldPosition))
        {
            return;
        }

        panel.Show(provider);
    }

    bool ResolveProvider()
    {
        if (provider != null)
        {
            return true;
        }

        provider = FindAnyObjectByType<NpcTaskProvider>();
        return provider != null;
    }

    void WarnMissingProviderOnce()
    {
        if (warnedMissingProvider)
        {
            return;
        }

        warnedMissingProvider = true;
        Debug.LogWarning("Missing NpcTaskProvider.");
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
