using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcFavoriteManager : MonoBehaviour
{
    public static NpcFavoriteManager Instance;

    [Header("Favorite limit")]
    public int maxFavorites = 5;

    private readonly List<NpcFavorite> favorites = new List<NpcFavorite>();

    public IReadOnlyList<NpcFavorite> Favorites => favorites;

    public event Action OnFavoritesChanged;

    public static NpcFavoriteManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        NpcFavoriteManager existing =
            FindAnyObjectByType<NpcFavoriteManager>(FindObjectsInactive.Include);

        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject managerObject = new GameObject("NpcFavoriteManager");
        Instance = managerObject.AddComponent<NpcFavoriteManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool ToggleFavorite(NpcFavorite npc)
    {
        if (npc == null)
        {
            return false;
        }

        if (favorites.Contains(npc))
        {
            RemoveFavorite(npc);
            return true;
        }

        return AddFavorite(npc);
    }

    public bool AddFavorite(NpcFavorite npc)
    {
        if (npc == null)
        {
            return false;
        }

        if (favorites.Contains(npc))
        {
            npc.SetFavoriteState(true);
            OnFavoritesChanged?.Invoke();
            return true;
        }

        if (favorites.Count >= maxFavorites)
        {
            Debug.Log("Da danh dau toi da " + maxFavorites + " Tu si.");
            return false;
        }

        favorites.Add(npc);
        npc.SetFavoriteState(true);

        OnFavoritesChanged?.Invoke();
        return true;
    }

    public bool AddFavorite(GameObject target)
    {
        NpcFavorite favorite =
            GetOrCreateFavorite(target);

        return AddFavorite(favorite);
    }

    public void RemoveFavorite(NpcFavorite npc)
    {
        if (npc == null)
        {
            return;
        }

        if (favorites.Remove(npc))
        {
            npc.SetFavoriteState(false);
            OnFavoritesChanged?.Invoke();
        }
    }

    public void ClearFavorites(bool clearNpcState = true)
    {
        if (clearNpcState)
        {
            for (int i = 0; i < favorites.Count; i++)
            {
                if (favorites[i] != null)
                {
                    favorites[i].SetFavoriteState(false);
                }
            }
        }

        favorites.Clear();
        OnFavoritesChanged?.Invoke();
    }

    public NpcFavorite GetOrCreateFavorite(GameObject target)
    {
        GameObject favoriteTarget =
            ResolveFavoriteTarget(target);

        if (favoriteTarget == null)
        {
            return null;
        }

        NpcFavorite favorite =
            favoriteTarget.GetComponent<NpcFavorite>();

        if (favorite == null)
        {
            favorite =
                favoriteTarget.AddComponent<NpcFavorite>();
        }

        return favorite;
    }

    public GameObject ResolveFavoriteTarget(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        if (target.GetComponentInParent<WorldStatItemPickup>() != null)
        {
            return null;
        }

        MonsterAI monster =
            target.GetComponentInParent<MonsterAI>();
        if (monster != null)
        {
            return monster.gameObject;
        }

        VillagerAI villager =
            target.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc =
            target.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        NpcData npcData =
            target.GetComponentInParent<NpcData>();
        if (npcData != null)
        {
            return npcData.gameObject;
        }

        NpcFavorite favorite =
            target.GetComponentInParent<NpcFavorite>();
        if (favorite != null)
        {
            return favorite.gameObject;
        }

        return null;
    }
}
