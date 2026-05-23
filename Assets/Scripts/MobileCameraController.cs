using UnityEngine;

public class MobileCameraController : MonoBehaviour
{
    [Header("Follow")]
    public Transform followTarget;
    public float followSpeed = 5f;
    public bool smoothFollow = false;
    public Vector2 focusOffset =
        Vector2.zero;
    public Vector2 focusViewport =
        new Vector2(0.5f, 0.5f);
    public bool clampWhileFollowing = false;

    [Header("Drag")]
    public float dragThreshold = 10f;

    [Header("Zoom")]
    public float zoomSpeed = 0.01f;
    public float minZoom = 3f;
    public float maxZoom = 5f;
    public float mouseZoomSpeed = 0.5f;

    Camera cam;

    Vector3 dragStartScreenPos;
    Vector3 dragStartCameraPos;

    bool isDragging;
    bool dragStarted;

    BoxCollider2D mapBounds;

    float minX;
    float maxX;
    float minY;
    float maxY;

    void Start()
    {
        cam = Camera.main;
        FindMapBounds();
    }

    void Update()
    {
        if (mapBounds == null)
        {
            FindMapBounds();
        }

        HandleZoom();
        HandleDrag();
    }

    void LateUpdate()
    {
        if (!isDragging)
        {
            FollowTarget();
        }
    }

    void FindMapBounds()
    {
        GameObject boundsObject = GameObject.Find("MapBounds");

        if (boundsObject == null)
        {
            return;
        }

        mapBounds = boundsObject.GetComponent<BoxCollider2D>();
        SetupBounds();
    }

    void SetupBounds()
    {
        if (mapBounds == null || cam == null)
        {
            return;
        }

        Bounds bounds = mapBounds.bounds;

        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * cam.aspect;

        minX = bounds.min.x + camWidth / 2f;
        maxX = bounds.max.x - camWidth / 2f;
        minY = bounds.min.y + camHeight / 2f;
        maxY = bounds.max.y - camHeight / 2f;
    }

    void FollowTarget()
    {
        if (followTarget == null)
        {
            return;
        }

        Vector3 targetPos =
            GetFocusPosition(followTarget);

        if (smoothFollow)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                followSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = targetPos;
        }

        if (clampWhileFollowing)
        {
            ClampCamera();
        }
    }

    public void FollowImmediately(Transform target)
    {
        if (target == null)
        {
            return;
        }

        followTarget = target;
        isDragging = false;
        dragStarted = false;

        Vector3 targetPos =
            GetFocusPosition(target);

        transform.position = targetPos;

        if (clampWhileFollowing)
        {
            ClampCamera();
        }
    }

    Vector3 GetFocusPosition(Transform target)
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null)
        {
            Vector3 fallbackPos = target.position;
            fallbackPos.z = -10f;

            return fallbackPos;
        }

        Vector3 focusWorldPos = target.position;

        focusWorldPos.x += focusOffset.x;
        focusWorldPos.y += focusOffset.y;

        float focusDepth =
            Mathf.Abs(
                cam.transform.position.z -
                focusWorldPos.z);

        Vector3 currentViewportWorldPos =
            cam.ViewportToWorldPoint(
                new Vector3(
                    focusViewport.x,
                    focusViewport.y,
                    focusDepth));

        Vector3 cameraDelta =
            focusWorldPos -
            currentViewportWorldPos;

        Vector3 cameraPos =
            transform.position +
            cameraDelta;

        cameraPos.z = -10f;

        return cameraPos;
    }

    void HandleDrag()
    {
        if (Input.touchCount == 2)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragStartScreenPos = Input.mousePosition;
            dragStartCameraPos = transform.position;

            isDragging = false;
            dragStarted = true;
        }

        if (Input.GetMouseButton(0) && dragStarted)
        {
            Vector3 screenDelta = Input.mousePosition - dragStartScreenPos;

            if (!isDragging && screenDelta.magnitude > dragThreshold)
            {
                isDragging = true;
                followTarget = null;
            }

            if (isDragging)
            {
                Vector3 startWorld = cam.ScreenToWorldPoint(dragStartScreenPos);
                Vector3 currentWorld = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 worldDelta = startWorld - currentWorld;

                Vector3 targetPos = dragStartCameraPos + worldDelta;
                targetPos.z = -10f;

                transform.position = targetPos;
                ClampCamera();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            dragStarted = false;
        }
    }

    void HandleZoom()
    {
        if (cam == null)
        {
            return;
        }

        if (Input.touchCount == 2)
        {
            dragStarted = false;
            isDragging = false;

            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 prevTouch0 =
                touch0.position -
                touch0.deltaPosition;

            Vector2 prevTouch1 =
                touch1.position -
                touch1.deltaPosition;

            float prevMagnitude =
                (prevTouch0 - prevTouch1).magnitude;

            float currentMagnitude =
                (touch0.position - touch1.position).magnitude;

            float difference =
                currentMagnitude -
                prevMagnitude;

            ApplyZoom(
                -difference * zoomSpeed);

            return;
        }

        float mouseWheel =
            Input.mouseScrollDelta.y;

        if (Mathf.Abs(mouseWheel) > 0.01f)
        {
            ApplyZoom(
                -mouseWheel * mouseZoomSpeed);
        }
    }

    void ApplyZoom(float zoomDelta)
    {
        cam.orthographicSize =
            Mathf.Clamp(
                cam.orthographicSize + zoomDelta,
                minZoom,
                maxZoom);

        SetupBounds();

        if (followTarget != null)
        {
            transform.position =
                GetFocusPosition(followTarget);

            return;
        }

        ClampCamera();
    }

    void ClampCamera()
    {
        if (mapBounds == null)
        {
            return;
        }

        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }
}
