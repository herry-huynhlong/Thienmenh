using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FavoriteNpcListUI : MonoBehaviour
{
    [Header("List panel")]
    public GameObject panel;
    public Transform contentRoot;
    public FavoriteNpcRowUI rowPrefab;

    [Header("Main screen buttons")]
    public Button openButton;
    public Button closeButton;

    [Header("Info")]
    public TMP_Text countText;

    [Header("Camera")]
    public Camera targetCamera;

    private NpcFavoriteManager manager;

    private void Awake()
    {
        if (panel == null)
        {
            panel = gameObject;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(TogglePanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HidePanel);
        }

        manager = NpcFavoriteManager.EnsureInstance();

        if (manager != null)
        {
            manager.OnFavoritesChanged += RebuildList;
        }

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.OnFavoritesChanged -= RebuildList;
        }
    }

    public void TogglePanel()
    {
        if (panel == null)
        {
            return;
        }

        bool nextState = !panel.activeSelf;
        panel.SetActive(nextState);

        if (nextState)
        {
            RebuildList();
        }
    }

    public void HidePanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void RebuildList()
    {
        if (contentRoot == null || rowPrefab == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        manager = NpcFavoriteManager.EnsureInstance();

        if (manager == null)
        {
            return;
        }

        var list = manager.Favorites;

        for (int i = 0; i < list.Count; i++)
        {
            NpcFavorite npc = list[i];

            if (npc == null)
            {
                continue;
            }

            FavoriteNpcRowUI row = Instantiate(rowPrefab, contentRoot);
            row.Setup(npc, this);
        }

        if (countText != null)
        {
            countText.text = UiText.Format(
                "favorites",
                "countFormat",
                list.Count,
                manager.maxFavorites);
        }
    }

    public void FocusNpc(NpcFavorite npc)
    {
        if (npc == null || targetCamera == null)
        {
            return;
        }

        Vector3 pos = targetCamera.transform.position;
        pos.x = npc.transform.position.x;
        pos.y = npc.transform.position.y;
        targetCamera.transform.position = pos;

        HidePanel();
    }
}
