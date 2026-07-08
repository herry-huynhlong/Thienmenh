using UnityEngine;

public class NpcFavoriteClickBridge : MonoBehaviour
{
    [Header("Legacy click bridge")]
    public bool enableBridge = false;

    [Header("Camera")]
    public Camera targetCamera;

    [Header("Star button in TargetInfoPanel")]
    public NpcFavoriteButtonUI favoriteButtonUI;

    [Header("Raycast")]
    public LayerMask clickableLayers = ~0;

    [Header("Debug")]
    public bool debugLog = false;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (!enableBridge)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TrySelectNpc(Input.mousePosition);
        }

        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TrySelectNpc(Input.GetTouch(0).position);
        }
    }

    private void TrySelectNpc(Vector2 screenPosition)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            if (debugLog)
            {
                Debug.LogWarning("NpcFavoriteClickBridge: Khong tim thay camera.");
            }

            return;
        }

        Vector3 worldPosition =
            CameraWorldPlaneUtility.ScreenToWorldOnPlane(
                targetCamera,
                screenPosition);
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D[] hits = Physics2D.OverlapPointAll(point, clickableLayers);

        if (hits == null || hits.Length == 0)
        {
            if (debugLog)
            {
                Debug.Log("NpcFavoriteClickBridge: Click khong trung Collider2D nao.");
            }

            return;
        }

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            GameObject npcObject = FindNpcObject(hit.gameObject);

            if (npcObject == null)
            {
                continue;
            }

            if (favoriteButtonUI != null)
            {
                favoriteButtonUI.SetCurrentNpc(npcObject);

                if (debugLog)
                {
                    Debug.Log("Da gan Tu si cho nut sao: " + npcObject.name);
                }
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log("NpcFavoriteClickBridge: Co collider nhung khong tim thay Tu si tren object duoc click.");
        }
    }

    private GameObject FindNpcObject(GameObject clickedObject)
    {
        if (clickedObject == null)
        {
            return null;
        }

        VillagerAI villager = clickedObject.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc = clickedObject.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        NpcData npcData = clickedObject.GetComponentInParent<NpcData>();

        if (npcData != null)
        {
            return npcData.gameObject;
        }

        NpcFavorite favorite = clickedObject.GetComponentInParent<NpcFavorite>();

        if (favorite != null)
        {
            return favorite.gameObject;
        }

        return null;
    }
}
