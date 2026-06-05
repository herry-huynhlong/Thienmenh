using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcFavoriteManager : MonoBehaviour
{
    public static NpcFavoriteManager Instance;

    [Header("Giới hạn đánh dấu")]
    public int maxFavorites = 5;

    private readonly List<NpcFavorite> favorites = new List<NpcFavorite>();

    public IReadOnlyList<NpcFavorite> Favorites => favorites;

    public event Action OnFavoritesChanged;

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

        if (favorites.Count >= maxFavorites)
        {
            Debug.Log("Đã đánh dấu tối đa " + maxFavorites + " NPC.");
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
}