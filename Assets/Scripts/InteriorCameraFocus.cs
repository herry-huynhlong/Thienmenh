using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class InteriorCameraFocus : MonoBehaviour
{
    [Header("Camera Targets")]
    public Transform interiorFocusPoint;
    public Transform outsideFocusPoint;
    public float interiorOrthographicSize = 2.5f;
    public float outsideOrthographicSize = 4.5f;
    public bool changeZoom = true;

    [Header("Toggle")]
    public bool isInteriorFocused = false;

    [Header("Tap")]
    public float tapThreshold = 12f;
    public bool ignoreWhenPointerOverUi = true;
    public LayerMask clickableLayers = ~0;
    public float hitRadius = 0.05f;

    [Header("Debug")]
    public bool logFocusEvents;

    Camera targetCamera;
    MobileCameraController cameraController;
    Collider2D[] ownColliders;
    Coroutine focusRoutine;

    Vector3 pointerDownPosition;
    bool pointerStartedOverUi;
    bool pointerStartedOnThis;

    void Awake()
    {
        ownColliders = GetComponentsInChildren<Collider2D>();
    }

    void OnValidate()
    {
        hitRadius = Mathf.Max(0f, hitRadius);
        tapThreshold = Mathf.Max(0f, tapThreshold);
        interiorOrthographicSize = Mathf.Max(0.5f, interiorOrthographicSize);
        outsideOrthographicSize = Mathf.Max(0.5f, outsideOrthographicSize);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            pointerDownPosition = Input.mousePosition;
            pointerStartedOverUi = IsPointerOverUi();
            pointerStartedOnThis = IsPointerOverThis(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            HandlePointerUp(Input.mousePosition);
        }
    }

    public void FocusInterior()
    {
        isInteriorFocused = true;
        QueueFocus(interiorFocusPoint, interiorOrthographicSize);
    }

    public void FocusOutside()
    {
        isInteriorFocused = false;
        QueueFocus(outsideFocusPoint != null ? outsideFocusPoint : transform, outsideOrthographicSize);
    }

    public void ToggleFocus()
    {
        if (isInteriorFocused)
        {
            FocusOutside();
        }
        else
        {
            FocusInterior();
        }
    }

    void HandlePointerUp(Vector3 pointerPosition)
    {
        if (ignoreWhenPointerOverUi && (pointerStartedOverUi || IsPointerOverUi()))
        {
            return;
        }

        if (!pointerStartedOnThis || !IsPointerOverThis(pointerPosition))
        {
            return;
        }

        if ((pointerPosition - pointerDownPosition).magnitude > tapThreshold)
        {
            return;
        }

        FocusInterior();
    }

    void QueueFocus(Transform target, float orthographicSize)
    {
        if (focusRoutine != null)
        {
            StopCoroutine(focusRoutine);
        }

        focusRoutine = StartCoroutine(FocusAtEndOfFrame(target, orthographicSize));
    }

    IEnumerator FocusAtEndOfFrame(Transform target, float orthographicSize)
    {
        yield return new WaitForEndOfFrame();
        Focus(target, orthographicSize);
        focusRoutine = null;
    }

    void Focus(Transform target, float orthographicSize)
    {
        if (target == null)
        {
            if (logFocusEvents)
            {
                Debug.LogWarning($"{name}: Focus point is not assigned.", this);
            }

            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            if (logFocusEvents)
            {
                Debug.LogWarning($"{name}: No Camera.main found.", this);
            }

            return;
        }

        if (cameraController == null)
        {
            cameraController = targetCamera.GetComponent<MobileCameraController>();
        }

        if (cameraController == null)
        {
            cameraController = FindAnyObjectByType<MobileCameraController>(FindObjectsInactive.Include);
        }

        if (cameraController != null)
        {
            cameraController.followTarget = null;
        }

        if (changeZoom)
        {
            targetCamera.orthographicSize = Mathf.Max(0.5f, orthographicSize);
        }

        Vector3 cameraPosition = target.position;
        cameraPosition.z = targetCamera.transform.position.z;
        targetCamera.transform.position = cameraPosition;

        if (logFocusEvents)
        {
            Debug.Log($"{name}: Camera focused on {target.name} at {cameraPosition}.", this);
        }
    }

    bool IsPointerOverThis(Vector3 screenPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector2 worldPosition =
            CameraWorldPlaneUtility.ScreenToWorldOnPlane(
                cam,
                screenPosition);
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPosition, hitRadius, clickableLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform) || IsOwnCollider(hit))
            {
                return true;
            }
        }

        return false;
    }

    bool IsOwnCollider(Collider2D hit)
    {
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == hit)
            {
                return true;
            }
        }

        return false;
    }

    bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}
