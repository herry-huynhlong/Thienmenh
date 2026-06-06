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

        NpcFavoriteManager existing = FindObjectOfType<NpcFavoriteManager>(true);

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
            Debug.Log("Da danh dau toi da " + maxFavorites + " NPC.");
            return false;
        }

        favorites.Add(npc);
        npc.SetFavoriteState(true);

        OnFavoritesChanged?.Invoke();
        return true;
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
}
