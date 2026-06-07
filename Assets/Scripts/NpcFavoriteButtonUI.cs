using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcFavoriteButtonUI : MonoBehaviour
{
    [Header("Star UI")]
    public Button starButton;
    public Image starImage;
    public TMP_Text starText;

    [Header("Star Sprite")]
    public Sprite normalStarSprite; // sao trắng / sao rỗng
    public Sprite markedStarSprite; // sao vàng / đã đánh dấu
    public bool hideTextWhenUsingImage = true;

    private NpcFavorite currentNpc;
    private Transform lastSelectedTarget;

    private void Awake()
    {
        if (starButton == null)
        {
            starButton = GetComponent<Button>();
        }

        if (starImage == null)
        {
            starImage = GetComponent<Image>();
        }

        if (starButton != null)
        {
            starButton.onClick.RemoveAllListeners();
            starButton.onClick.AddListener(ToggleFavorite);
        }

        RefreshFromSelectedTarget();
        RefreshStarVisual();
    }

    private void OnEnable()
    {
        RefreshFromSelectedTarget();
        RefreshStarVisual();
    }

    private void Update()
    {
        if (TouchSelectTarget.CurrentTarget != lastSelectedTarget)
        {
            RefreshFromSelectedTarget();
            RefreshStarVisual();
        }
    }

    public void SetCurrentNpc(GameObject npcObject)
    {
        currentNpc = GetOrCreateFavorite(npcObject);
        RefreshStarVisual();
    }

    private void ToggleFavorite()
    {
        RefreshFromSelectedTarget();

        if (currentNpc == null)
        {
            TryGetNpcFromNpcInventoryPanel();
        }

        if (currentNpc == null)
        {
            Debug.Log("Chua chon NPC de danh dau.");
            return;
        }

        NpcFavoriteManager manager = NpcFavoriteManager.EnsureInstance();

        if (manager == null)
        {
            Debug.LogWarning("Khong tao duoc NpcFavoriteManager.");
            return;
        }

        manager.ToggleFavorite(currentNpc);
        RefreshStarVisual();

        FullGameSaveController saveController =
            FullGameSaveController.EnsureInstance();

        if (saveController != null)
        {
            saveController.SaveFullGame();
        }
    }

    private void RefreshFromSelectedTarget()
    {
        Transform selectedTarget = TouchSelectTarget.CurrentTarget;
        lastSelectedTarget = selectedTarget;

        if (selectedTarget == null)
        {
            currentNpc = null;
            return;
        }

        currentNpc = GetOrCreateFavorite(selectedTarget.gameObject);
    }

    private NpcFavorite GetOrCreateFavorite(GameObject targetObject)
    {
        GameObject npcObject = FindNpcObject(targetObject);

        if (npcObject == null)
        {
            return null;
        }

        NpcFavorite favorite = npcObject.GetComponent<NpcFavorite>();

        if (favorite == null)
        {
            favorite = npcObject.AddComponent<NpcFavorite>();
        }

        return favorite;
    }

    private GameObject FindNpcObject(GameObject clickedObject)
    {
        if (clickedObject == null)
        {
            return null;
        }

        if (clickedObject.GetComponentInParent<MonsterAI>() != null)
        {
            return null;
        }

        if (clickedObject.GetComponentInParent<WorldStatItemPickup>() != null)
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

    private void TryGetNpcFromNpcInventoryPanel()
    {
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            if (script == null)
            {
                continue;
            }

            FieldInfo field = script.GetType().GetField(
                "currentNpc",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (field == null)
            {
                continue;
            }

            Transform npcTransform = field.GetValue(script) as Transform;

            if (npcTransform == null)
            {
                continue;
            }

            currentNpc = GetOrCreateFavorite(npcTransform.gameObject);

            if (currentNpc != null)
            {
                Debug.Log("Da tu lay NPC cho nut sao: " + npcTransform.name);
                return;
            }
        }
    }

    private void RefreshStarVisual()
    {
        bool isMarked =
            currentNpc != null &&
            currentNpc.IsFavorite;

        if (starImage != null)
        {
            if (isMarked)
            {
                if (markedStarSprite != null)
                {
                    starImage.sprite = markedStarSprite;
                }
            }
            else
            {
                if (normalStarSprite != null)
                {
                    starImage.sprite = normalStarSprite;
                }
            }
        }

        if (starText != null)
        {
            if (hideTextWhenUsingImage && starImage != null)
            {
                starText.text = "";
            }
            else
            {
                starText.text = isMarked ? "*" : "+";
            }
        }
    }
}
