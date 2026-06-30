using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FavoriteNpcRowUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text realmText;
    public Button focusButton;
    public Button removeButton;

    private NpcFavorite npc;
    private FavoriteNpcListUI owner;

    public void Setup(NpcFavorite targetNpc, FavoriteNpcListUI listOwner)
    {
        npc = targetNpc;
        owner = listOwner;

        if (nameText != null)
        {
            nameText.text = npc.GetDisplayName();
        }

        if (realmText != null)
        {
            realmText.text = npc.GetRealmText();
        }

        if (focusButton != null)
        {
            focusButton.onClick.RemoveAllListeners();
            focusButton.onClick.AddListener(FocusNpc);
            SetButtonLabel(
                focusButton,
                UiText.Get("favorites", "focusButton"));
        }

        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(RemoveNpc);
            SetButtonLabel(
                removeButton,
                UiText.Get("favorites", "removeButton"));
        }
    }

    private void FocusNpc()
    {
        if (owner != null)
        {
            owner.FocusNpc(npc);
        }
    }

    private void RemoveNpc()
    {
        if (NpcFavoriteManager.Instance != null)
        {
            NpcFavoriteManager.Instance.RemoveFavorite(npc);
        }
    }

    void SetButtonLabel(Button button, string value)
    {
        if (button == null || string.IsNullOrEmpty(value))
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = value;
        }
    }
}
