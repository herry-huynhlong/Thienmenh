using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanelUI : MonoBehaviour
{
    [Header("Data")]
    public SimpleItemShop shop;
    public PlayerWallet playerWallet;
    public ItemInventory playerInventory;

    [Header("Panel")]
    public GameObject panelRoot;
    public TMP_Text shopTitleText;
    public string shopTitle = "Linh Duoc Duong";

    [Header("Items")]
    public Transform itemGridParent;
    public ShopItemButtonUI itemButtonPrefab;

    [Header("Detail")]
    public GameObject detailPanel;
    public Image detailIcon;
    public TMP_Text detailNameText;
    public TMP_Text detailTypeText;
    public TMP_Text detailGradeText;
    public TMP_Text detailTargetsText;
    public TMP_Text detailPriceText;
    public TMP_Text detailDescriptionText;
    public TMP_Text detailStatsText;
    public Button buyButton;
    public TMP_Text buyButtonText;
    public TMP_Text moneyText;

    ItemType currentType = ItemType.DanDuoc;
    int selectedItemIndex = -1;
    ResponsivePanelFitter responsiveFitter;

    void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuySelectedItem);
        }

        responsiveFitter =
            GetComponent<ResponsivePanelFitter>();
    }

    void Start()
    {
        Close();
    }

    public void Open()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (responsiveFitter != null)
        {
            responsiveFitter.Apply();
        }

        ShowDanDuoc();
    }

    public void Close()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void Toggle()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (panelRoot.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void ShowDanDuoc()
    {
        ShowCategory(ItemType.DanDuoc, "Linh Duoc Duong");
    }

    public void ShowPhapBao()
    {
        ShowCategory(ItemType.PhapBao, "Than Binh Cac");
    }

    public void ShowCongPhap()
    {
        ShowCategory(ItemType.CongPhap, "Cong Phap Tang");
    }

    public void ShowVatLieu()
    {
        ShowCategory(ItemType.VatLieu, "Thien Tai Cac");
    }

    public void ShowCategory(
        ItemType itemType,
        string title)
    {
        currentType = itemType;
        shopTitle = title;
        selectedItemIndex = -1;

        if (shopTitleText != null)
        {
            shopTitleText.text = shopTitle;
        }

        RefreshMoney();
        ClearDetail();
        RebuildItemGrid();
    }

    public void SelectItem(int itemIndex)
    {
        selectedItemIndex = itemIndex;

        ShopItemSlot slot =
            shop.GetSlot(itemIndex);

        if (slot == null ||
            slot.item == null)
        {
            ClearDetail();
            return;
        }

        if (detailPanel != null)
        {
            detailPanel.SetActive(true);
        }

        if (detailIcon != null)
        {
            detailIcon.sprite = slot.item.icon;
            detailIcon.enabled = slot.item.icon != null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = slot.item.itemName;
        }

        if (detailTypeText != null)
        {
            detailTypeText.text = GetTypeText(slot.item.itemType);
        }

        if (detailGradeText != null)
        {
            detailGradeText.text = GetGradeText(slot.item.grade);
        }

        if (detailTargetsText != null)
        {
            detailTargetsText.text = GetTargetText(slot.item.validTargets);
        }

        if (detailPriceText != null)
        {
            detailPriceText.text = slot.item.price + " LT";
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = slot.item.description;
        }

        if (detailStatsText != null)
        {
            detailStatsText.text = BuildStatsText(slot.item);
        }

        RefreshBuyButton();
    }

    public void BuySelectedItem()
    {
        if (selectedItemIndex < 0 ||
            shop == null ||
            playerWallet == null ||
            playerInventory == null)
        {
            return;
        }

        bool bought =
            shop.BuyToInventory(
                selectedItemIndex,
                playerWallet,
                playerInventory);

        if (!bought)
        {
            RefreshBuyButton();
            return;
        }

        RefreshMoney();
        RebuildItemGrid();
        SelectItem(selectedItemIndex);
    }

    void RebuildItemGrid()
    {
        if (itemGridParent == null ||
            itemButtonPrefab == null ||
            shop == null)
        {
            return;
        }

        for (int i = itemGridParent.childCount - 1; i >= 0; i--)
        {
            Transform child =
                itemGridParent.GetChild(i);

            if (child == itemButtonPrefab.transform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        itemButtonPrefab.gameObject.SetActive(false);

        for (int i = 0; i < shop.ItemCount; i++)
        {
            ShopItemSlot slot =
                shop.GetSlot(i);

            if (slot == null ||
                slot.item == null ||
                slot.amount <= 0 ||
                slot.item.itemType != currentType)
            {
                continue;
            }

            ShopItemButtonUI button =
                Instantiate(
                    itemButtonPrefab,
                    itemGridParent);

            button.gameObject.SetActive(true);
            button.Setup(this, i, slot);
        }
    }

    void ClearDetail()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }

        if (buyButton != null)
        {
            buyButton.interactable = false;
        }
    }

    void RefreshMoney()
    {
        if (moneyText != null &&
            playerWallet != null)
        {
            moneyText.text =
                playerWallet.money + " LT";
        }
    }

    void RefreshBuyButton()
    {
        if (buyButton == null)
        {
            return;
        }

        ShopItemSlot slot =
            shop.GetSlot(selectedItemIndex);

        bool canBuy =
            slot != null &&
            slot.item != null &&
            slot.amount > 0 &&
            playerWallet != null &&
            playerWallet.CanPay(slot.item.price);

        buyButton.interactable = canBuy;

        if (buyButtonText != null)
        {
            buyButtonText.text =
                canBuy
                ? "Mua"
                : "Khong du LT";
        }
    }

    string BuildStatsText(StatItemData item)
    {
        StringBuilder builder =
            new StringBuilder();

        if (item.hpBonus != 0)
        {
            builder.AppendLine("Mau +" + item.hpBonus);
        }

        if (item.cultivationBonus != 0)
        {
            builder.AppendLine("Tu vi +" + item.cultivationBonus);
        }

        if (item.breakthroughRealm)
        {
            builder.AppendLine("Dot pha canh gioi");
        }

        if (item.damageBonus != 0)
        {
            builder.AppendLine("Dame +" + item.damageBonus);
        }

        if (item.armorBonus != 0)
        {
            builder.AppendLine("Giap +" + item.armorBonus);
        }

        if (item.effectResistanceBonus != 0)
        {
            builder.AppendLine("Khang hieu ung +" + item.effectResistanceBonus);
        }

        if (item.isTemporary)
        {
            builder.AppendLine("Thoi gian: " + item.duration + "s");
        }

        return builder.ToString();
    }

    string GetTypeText(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.DanDuoc:
                return "Dan Duoc";
            case ItemType.PhapBao:
                return "Phap Bao";
            case ItemType.CongPhap:
                return "Cong Phap";
            case ItemType.VatLieu:
                return "Vat Lieu";
            default:
                return itemType.ToString();
        }
    }

    string GetGradeText(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return "Ha";
            case ItemGrade.Trung:
                return "Trung";
            case ItemGrade.Thuong:
                return "Thuong";
            case ItemGrade.Tien:
                return "Tien";
            default:
                return grade.ToString();
        }
    }

    string GetTargetText(ItemTargetType targetType)
    {
        if (targetType == ItemTargetType.All)
        {
            return "Tat Ca";
        }

        return targetType.ToString();
    }
}
