using UnityEngine;

public class MobileCameraController : MonoBehaviour
{
    [Header("Follow")]
    public Transform followTarget;

    public float followSpeed = 5f;

    [Header("Drag")]
    public float dragSpeed = 1f;

    public float dragThreshold = 0.3f;

    Vector3 dragOrigin;

    bool isDragging = false;

    Camera cam;

    TouchSelectTarget selector;

    void Start()
    {
        cam = Camera.main;

        selector =
            FindFirstObjectByType<TouchSelectTarget>();
    }

    void Update()
    {
        FollowTarget();

        HandleDrag();
    }

    void FollowTarget()
    {
        if (followTarget == null)
        {
            return;
        }

        Vector3 targetPos =
            followTarget.position;

        targetPos.z = -10f;

        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPos,
                followSpeed *
                Time.deltaTime);
    }

    void HandleDrag()
    {
        if (cam == null)
        {
            return;
        }

        // Bắt đầu chạm
        if (Input.GetMouseButtonDown(0))
        {
            dragOrigin =
                cam.ScreenToWorldPoint(
                    Input.mousePosition);

            isDragging = false;
        }

        // Đang giữ
        if (Input.GetMouseButton(0))
        {
            Vector3 currentPos =
                cam.ScreenToWorldPoint(
                    Input.mousePosition);

            Vector3 difference =
                dragOrigin -
                currentPos;

            // Chỉ tính drag nếu kéo đủ xa
            if (difference.magnitude >
                dragThreshold)
            {
                isDragging = true;

                // Thoát follow
                followTarget = null;

                // Kéo camera
                transform.position +=
                    difference *
                    dragSpeed;

                // Ẩn panel
                if (selector != null)
                {
                    if (selector.infoPanel != null)
                    {
                        selector
                            .infoPanel
                            .SetActive(false);
                    }
                }
            }
        }

        // Thả tay
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }
}