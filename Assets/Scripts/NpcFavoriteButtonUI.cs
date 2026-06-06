using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcFavoriteButtonUI : MonoBehaviour
{
    [Header("Star UI")]
    public Button starButton;
    public TMP_Text starText;

    private NpcFavorite currentNpc;
    private Transform lastSelectedTarget;

    private void Awake()
    {
        if (starButton != null)
        {
            starButton.onClick.RemoveAllListeners();
            starButton.onClick.AddListener(ToggleFavorite);
        }

        RefreshFromSelectedTarget();
        RefreshStarText();
    }

    private void OnEnable()
    {
        RefreshFromSelectedTarget();
        RefreshStarText();
    }

    private void Update()
    {
        if (TouchSelectTarget.CurrentTarget != lastSelectedTarget)
        {
            RefreshFromSelectedTarget();
            RefreshStarText();
        }
    }

    public void SetCurrentNpc(GameObject npcObject)
    {
        currentNpc = GetOrCreateFavorite(npcObject);
        RefreshStarText();
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
        RefreshStarText();
        FullGameSaveController.EnsureInstance().SaveFullGame();
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

    private void RefreshStarText()
    {
        if (starText == null)
        {
            return;
        }

        if (currentNpc == null)
        {
            starText.text = "+";
            return;
        }

        starText.text = currentNpc.IsFavorite ? "*" : "+";
    }
}
