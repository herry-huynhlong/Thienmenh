using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FavoriteNpcListUI : MonoBehaviour
{
    [Header("Panel danh sách")]
    public GameObject panel;
    public Transform contentRoot;
    public FavoriteNpcRowUI rowPrefab;

    [Header("Nút ngoài màn hình")]
    public Button openButton;
    public Button closeButton;

    [Header("Thông tin")]
    public TMP_Text countText;

    [Header("Camera")]
    public Camera targetCamera;

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

        if (NpcFavoriteManager.Instance != null)
        {
            NpcFavoriteManager.Instance.OnFavoritesChanged += RebuildList;
        }

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (NpcFavoriteManager.Instance != null)
        {
            NpcFavoriteManager.Instance.OnFavoritesChanged -= RebuildList;
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

        if (NpcFavoriteManager.Instance == null)
        {
            return;
        }

        var list = NpcFavoriteManager.Instance.Favorites;

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
            countText.text = list.Count + "/" + NpcFavoriteManager.Instance.maxFavorites;
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