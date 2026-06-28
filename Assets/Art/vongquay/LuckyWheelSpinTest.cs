using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LuckyWheelSpinTest : MonoBehaviour
{
    private static readonly ItemGrade[] GradeOrder =
    {
        ItemGrade.Ha,
        ItemGrade.Trung,
        ItemGrade.Thuong,
        ItemGrade.Tien
    };

    [Header("References")]
    public RectTransform wheelDisk;
    public Button spinButton;
    public WheelBulbBlinkOnly bulbBlink;
    public WheelSlotAutoLayout slotLayout;
    public RectTransform slotsRoot;
    public LuckyWheelWinEffect winEffect;

    [Header("Auto Fill")]
    public bool autoLoadItemsFromAssets = true;
    public string[] itemFolders = { "Assets/Item" };

    [Header("Grade Chance")]
    [Range(0f, 100f)] public float haChance = 75f;
    [Range(0f, 100f)] public float trungChance = 20f;
    [Range(0f, 100f)] public float thuongChance = 4.5f;
    [Range(0f, 100f)] public float tienChance = 0.5f;

    [Header("Spin Settings")]
    public float spinDuration = 4f;
    public int minRounds = 5;
    public int maxRounds = 8;
    public float pointerAngle = 90f;
    public float stopAngleOffset = 0f;

    [SerializeField]
    private List<StatItemData> wheelItems = new List<StatItemData>();

    public StatItemData CurrentReward { get; private set; }
    public int CurrentRewardSlotIndex { get; private set; } = -1;

    class SlotBinding
    {
        public RectTransform root;
        public Image iconImage;
        public TMP_Text nameText;
        public Image frameImage;
        public StatItemData item;
    }

    readonly List<SlotBinding> slotBindings = new List<SlotBinding>();
    bool isSpinning;

#if UNITY_EDITOR
    bool isRefreshingEditorData;
#endif

    void Awake()
    {
        InitializeWheel();
    }

    void OnEnable()
    {
        if (spinButton != null)
        {
            spinButton.onClick.AddListener(Spin);
        }

        InitializeWheel();
    }

    void Start()
    {
        InitializeWheel();
    }

    void OnDisable()
    {
        isSpinning = false;

        if (spinButton != null)
        {
            spinButton.interactable = true;
            spinButton.onClick.RemoveListener(Spin);
        }

        if (bulbBlink != null)
        {
            bulbBlink.SetSpinning(false);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (isRefreshingEditorData)
        {
            return;
        }

        isRefreshingEditorData = true;
        try
        {
            if (autoLoadItemsFromAssets)
            {
                RefreshWheelItemsFromAssets();
            }

            InitializeWheel();
        }
        finally
        {
            isRefreshingEditorData = false;
        }
    }
#endif

    [ContextMenu("Refresh Wheel Visuals")]
    public void RefreshWheelVisuals()
    {
        InitializeWheel();
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh Wheel Items From Assets")]
    public void RefreshWheelItemsFromAssets()
    {
        if (!autoLoadItemsFromAssets)
        {
            return;
        }

        CacheReferences();

        int slotCount = GetSlotCountFromHierarchy();
        if (slotCount <= 0)
        {
            if (wheelItems == null)
            {
                wheelItems = new List<StatItemData>();
            }

            wheelItems.Clear();
            BuildSlotBindings();
            ApplyWheelItemsToSlots();
            return;
        }

        if (itemFolders == null || itemFolders.Length == 0)
        {
            if (wheelItems == null)
            {
                wheelItems = new List<StatItemData>();
            }

            wheelItems.Clear();
            BuildSlotBindings();
            ApplyWheelItemsToSlots();
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:StatItemData", itemFolders);
        List<StatItemData> loadedItems = new List<StatItemData>();
        HashSet<StatItemData> seen = new HashSet<StatItemData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StatItemData item = AssetDatabase.LoadAssetAtPath<StatItemData>(path);

            if (item == null || seen.Contains(item))
            {
                continue;
            }

            seen.Add(item);
            loadedItems.Add(item);
        }

        wheelItems = BuildWheelItemsForSlots(loadedItems, slotCount);
        BuildSlotBindings();
        ApplyWheelItemsToSlots();
        EditorUtility.SetDirty(this);
    }
#endif

    public void Spin()
    {
        if (isSpinning)
        {
            return;
        }

        InitializeWheel();
        CurrentReward = null;
        CurrentRewardSlotIndex = -1;

        if (wheelDisk == null ||
            slotBindings.Count == 0 ||
            wheelItems == null ||
            wheelItems.Count == 0)
        {
            Debug.LogWarning("LuckyWheelSpinTest: wheel is not ready.");
            return;
        }

        StartCoroutine(SpinRoutine());
    }

    IEnumerator SpinRoutine()
    {
        if (!TryRollReward(out StatItemData reward, out int slotIndex))
        {
            Debug.LogWarning("LuckyWheelSpinTest: no reward item could be selected.");
            yield break;
        }

        isSpinning = true;
        CurrentReward = reward;
        CurrentRewardSlotIndex = slotIndex;

        if (spinButton != null)
        {
            spinButton.interactable = false;
        }

        if (bulbBlink != null)
        {
            bulbBlink.SetSpinning(true);
        }

        float startZ = wheelDisk.localEulerAngles.z;
        float targetZ = GetTargetWheelRotation(slotIndex);

        int lowRounds = Mathf.Min(minRounds, maxRounds);
        int highRounds = Mathf.Max(minRounds, maxRounds);
        int rounds = UnityEngine.Random.Range(lowRounds, highRounds + 1);
        float endZ = targetZ - rounds * 360f;

        float duration = Mathf.Max(0.01f, spinDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            float z = Mathf.Lerp(startZ, endZ, eased);
            wheelDisk.localRotation = Quaternion.Euler(0f, 0f, z);

            yield return null;
        }

        wheelDisk.localRotation = Quaternion.Euler(0f, 0f, endZ);

        if (bulbBlink != null)
    {
        bulbBlink.SetSpinning(false);
    }

    yield return new WaitForSeconds(0.15f);

    if (winEffect != null &&
        slotIndex >= 0 &&
        slotIndex < slotBindings.Count &&
        slotBindings[slotIndex] != null &&
        slotBindings[slotIndex].root != null)
    {
        winEffect.PlayFromSlot(slotBindings[slotIndex].root);
    }

    GiveRewardToPlayer(reward);

    if (spinButton != null)
    {
        spinButton.interactable = true;
    }

    isSpinning = false;
    }

    void InitializeWheel()
    {
        CacheReferences();

#if UNITY_EDITOR
        if (!isRefreshingEditorData &&
            autoLoadItemsFromAssets &&
            (wheelItems == null || wheelItems.Count == 0))
        {
            RefreshWheelItemsFromAssets();
        }
#endif

        if (slotLayout != null)
        {
            slotLayout.ApplyLayout();
        }

        BuildSlotBindings();
        ApplyWheelItemsToSlots();
    }

    void CacheReferences()
    {
        if (slotLayout == null && wheelDisk != null)
        {
            slotLayout = wheelDisk.GetComponentInChildren<WheelSlotAutoLayout>(true);
        }

        if (slotsRoot == null && slotLayout != null)
        {
            slotsRoot = slotLayout.transform as RectTransform;
        }

        if (slotsRoot == null && wheelDisk != null)
        {
            Transform found = wheelDisk.Find("SlotsRoot");
            if (found != null)
            {
                slotsRoot = found as RectTransform;
                if (slotLayout == null)
                {
                    slotLayout = found.GetComponent<WheelSlotAutoLayout>();
                }
            }
        }

        if (wheelDisk == null)
        {
            if (slotsRoot != null && slotsRoot.parent is RectTransform parentRect)
            {
                wheelDisk = parentRect;
            }
            else if (slotLayout != null &&
                slotLayout.transform.parent is RectTransform layoutParent)
            {
                wheelDisk = layoutParent;
            }
        }

        if (slotLayout == null && slotsRoot != null)
        {
            slotLayout = slotsRoot.GetComponent<WheelSlotAutoLayout>();
        }
    }

    void BuildSlotBindings()
    {
        slotBindings.Clear();

        Transform root = GetSlotsRootTransform();
        if (root == null)
        {
            return;
        }

        string tierFrameName = GetTierFrameName();
        string itemIconName = GetItemIconName();
        string itemTextName = GetItemTextName();

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null ||
                !child.gameObject.activeSelf ||
                !child.name.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RectTransform slot = child as RectTransform;
            if (slot == null)
            {
                continue;
            }

            SlotBinding binding = new SlotBinding
            {
                root = slot,
                frameImage = FindComponentByName<Image>(slot, tierFrameName),
                iconImage = FindComponentByName<Image>(slot, itemIconName),
                nameText = FindComponentByName<TMP_Text>(slot, itemTextName)
            };

            slotBindings.Add(binding);
        }
    }

    void ApplyWheelItemsToSlots()
    {
        if (wheelItems == null)
        {
            wheelItems = new List<StatItemData>();
        }

        int count = slotBindings.Count;
        for (int i = 0; i < count; i++)
        {
            StatItemData item = i < wheelItems.Count ? wheelItems[i] : null;
            ApplyItemToSlot(slotBindings[i], item);
        }
    }

    void ApplyItemToSlot(SlotBinding slot, StatItemData item)
    {
        if (slot == null)
        {
            return;
        }

        slot.item = item;

        if (slot.iconImage != null)
        {
            slot.iconImage.sprite = item != null ? item.icon : null;
            slot.iconImage.enabled = item != null && item.icon != null;
        }

        if (slot.nameText != null)
        {
            slot.nameText.text = item != null ? ItemText.Name(item) : "";
        }

        if (slot.frameImage != null)
        {
            slot.frameImage.enabled = true;
        }
    }

    bool TryRollReward(out StatItemData reward, out int slotIndex)
    {
        reward = null;
        slotIndex = -1;

        Dictionary<ItemGrade, List<int>> buckets = CreateSlotBuckets();
        float totalWeight = 0f;

        foreach (ItemGrade grade in GradeOrder)
        {
            if (buckets[grade].Count > 0)
            {
                totalWeight += GetGradeWeight(grade);
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = UnityEngine.Random.value * totalWeight;

        foreach (ItemGrade grade in GradeOrder)
        {
            List<int> bucket = buckets[grade];
            if (bucket.Count == 0)
            {
                continue;
            }

            roll -= GetGradeWeight(grade);
            if (roll > 0f)
            {
                continue;
            }

            slotIndex = bucket[UnityEngine.Random.Range(0, bucket.Count)];
            reward = slotBindings[slotIndex].item;
            return reward != null;
        }

        foreach (ItemGrade grade in GradeOrder)
        {
            List<int> bucket = buckets[grade];
            if (bucket.Count == 0)
            {
                continue;
            }

            slotIndex = bucket[0];
            reward = slotBindings[slotIndex].item;
            return reward != null;
        }

        return false;
    }

    Dictionary<ItemGrade, List<int>> CreateSlotBuckets()
    {
        Dictionary<ItemGrade, List<int>> buckets = CreateEmptySlotBucketMap();

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.item == null)
            {
                continue;
            }

            buckets[binding.item.grade].Add(i);
        }

        return buckets;
    }

    List<StatItemData> BuildWheelItemsForSlots(List<StatItemData> sourceItems, int slotCount)
    {
        List<StatItemData> result = new List<StatItemData>();
        if (sourceItems == null ||
            sourceItems.Count == 0 ||
            slotCount <= 0)
        {
            return result;
        }

        Dictionary<ItemGrade, List<StatItemData>> buckets =
            CreateItemBuckets(sourceItems);

        foreach (ItemGrade grade in GradeOrder)
        {
            buckets[grade].Sort(CompareItemsByName);
        }

        int availableCount = 0;
        foreach (ItemGrade grade in GradeOrder)
        {
            availableCount += buckets[grade].Count;
        }

        int targetCount = Mathf.Min(slotCount, availableCount);
        if (targetCount <= 0)
        {
            return result;
        }

        Dictionary<ItemGrade, int> targetCounts =
            CalculateTargetCounts(buckets, targetCount);

        Dictionary<ItemGrade, int> placedCounts =
            CreateEmptyCountMap();

        while (result.Count < targetCount)
        {
            ItemGrade? grade =
                ChooseGradeToPlaceNext(
                    targetCounts,
                    placedCounts,
                    buckets);

            if (!grade.HasValue)
            {
                break;
            }

            ItemGrade chosenGrade = grade.Value;
            int index = placedCounts[chosenGrade];

            if (index >= buckets[chosenGrade].Count)
            {
                break;
            }

            result.Add(buckets[chosenGrade][index]);
            placedCounts[chosenGrade] = index + 1;
        }

        return result;
    }

    Dictionary<ItemGrade, int> CalculateTargetCounts(
        Dictionary<ItemGrade, List<StatItemData>> buckets,
        int slotCount)
    {
        Dictionary<ItemGrade, int> counts = CreateEmptyCountMap();
        if (slotCount <= 0)
        {
            return counts;
        }

        List<ItemGrade> eligibleGrades = new List<ItemGrade>();
        foreach (ItemGrade grade in GradeOrder)
        {
            if (buckets[grade].Count > 0 &&
                GetGradeWeight(grade) > 0f)
            {
                eligibleGrades.Add(grade);
            }
        }

        if (eligibleGrades.Count == 0)
        {
            return counts;
        }

        if (slotCount >= eligibleGrades.Count)
        {
            foreach (ItemGrade grade in eligibleGrades)
            {
                counts[grade] = 1;
            }

            slotCount -= eligibleGrades.Count;
        }

        while (slotCount > 0)
        {
            ItemGrade? grade =
                ChooseGradeForAllocation(counts, buckets);

            if (!grade.HasValue)
            {
                break;
            }

            counts[grade.Value]++;
            slotCount--;
        }

        return counts;
    }

    ItemGrade? ChooseGradeForAllocation(
        Dictionary<ItemGrade, int> counts,
        Dictionary<ItemGrade, List<StatItemData>> buckets)
    {
        ItemGrade? chosen = null;
        float bestScore = float.MaxValue;
        float bestWeight = -1f;

        foreach (ItemGrade grade in GradeOrder)
        {
            float weight = GetGradeWeight(grade);
            int capacity = buckets[grade].Count - counts[grade];

            if (weight <= 0f || capacity <= 0)
            {
                continue;
            }

            float score = counts[grade] / weight;
            if (!chosen.HasValue ||
                score < bestScore - 0.0001f ||
                (Mathf.Abs(score - bestScore) <= 0.0001f &&
                weight > bestWeight))
            {
                chosen = grade;
                bestScore = score;
                bestWeight = weight;
            }
        }

        return chosen;
    }

    ItemGrade? ChooseGradeToPlaceNext(
        Dictionary<ItemGrade, int> targetCounts,
        Dictionary<ItemGrade, int> placedCounts,
        Dictionary<ItemGrade, List<StatItemData>> buckets)
    {
        ItemGrade? chosen = null;
        float bestRatio = float.MaxValue;
        int bestTarget = -1;
        float bestWeight = -1f;

        foreach (ItemGrade grade in GradeOrder)
        {
            int target = targetCounts[grade];
            int placed = placedCounts[grade];

            if (target <= 0 ||
                placed >= target ||
                placed >= buckets[grade].Count)
            {
                continue;
            }

            float ratio = placed / (float)target;
            float weight = GetGradeWeight(grade);

            if (!chosen.HasValue ||
                ratio < bestRatio - 0.0001f ||
                (Mathf.Abs(ratio - bestRatio) <= 0.0001f &&
                (target > bestTarget ||
                (target == bestTarget && weight > bestWeight))))
            {
                chosen = grade;
                bestRatio = ratio;
                bestTarget = target;
                bestWeight = weight;
            }
        }

        return chosen;
    }

    Dictionary<ItemGrade, List<StatItemData>> CreateItemBuckets(
        List<StatItemData> sourceItems)
    {
        Dictionary<ItemGrade, List<StatItemData>> buckets =
            CreateEmptyItemBucketMap();

        for (int i = 0; i < sourceItems.Count; i++)
        {
            StatItemData item = sourceItems[i];
            if (item == null)
            {
                continue;
            }

            buckets[item.grade].Add(item);
        }

        return buckets;
    }

    Dictionary<ItemGrade, List<int>> CreateEmptySlotBucketMap()
    {
        Dictionary<ItemGrade, List<int>> map =
            new Dictionary<ItemGrade, List<int>>();

        foreach (ItemGrade grade in GradeOrder)
        {
            map[grade] = new List<int>();
        }

        return map;
    }

    Dictionary<ItemGrade, List<StatItemData>> CreateEmptyItemBucketMap()
    {
        Dictionary<ItemGrade, List<StatItemData>> map =
            new Dictionary<ItemGrade, List<StatItemData>>();

        foreach (ItemGrade grade in GradeOrder)
        {
            map[grade] = new List<StatItemData>();
        }

        return map;
    }

    Dictionary<ItemGrade, int> CreateEmptyCountMap()
    {
        Dictionary<ItemGrade, int> map =
            new Dictionary<ItemGrade, int>();

        foreach (ItemGrade grade in GradeOrder)
        {
            map[grade] = 0;
        }

        return map;
    }

    float GetGradeWeight(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Ha:
                return Mathf.Max(0f, haChance);
            case ItemGrade.Trung:
                return Mathf.Max(0f, trungChance);
            case ItemGrade.Thuong:
                return Mathf.Max(0f, thuongChance);
            case ItemGrade.Tien:
                return Mathf.Max(0f, tienChance);
            default:
                return Mathf.Max(0f, haChance);
        }
    }

    float GetTargetWheelRotation(int slotIndex)
    {
        if (slotIndex < 0 ||
            slotIndex >= slotBindings.Count ||
            slotBindings[slotIndex] == null ||
            slotBindings[slotIndex].root == null)
        {
            return 0f;
        }

        Vector2 slotPosition = slotBindings[slotIndex].root.anchoredPosition;
        float slotAngle = Mathf.Atan2(slotPosition.y, slotPosition.x) * Mathf.Rad2Deg;
        return NormalizeAngle(pointerAngle - slotAngle + stopAngleOffset);
    }

    Transform GetSlotsRootTransform()
    {
        CacheReferences();
        return slotsRoot != null ? slotsRoot : null;
    }

    int GetSlotCountFromHierarchy()
    {
        Transform root = GetSlotsRootTransform();
        if (root == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null &&
                child.gameObject.activeSelf &&
                child.name.StartsWith("Slot_", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    string GetTierFrameName()
    {
        if (slotLayout != null &&
            !string.IsNullOrEmpty(slotLayout.tierFrameName))
        {
            return slotLayout.tierFrameName;
        }

        return "TierFrame";
    }

    string GetItemIconName()
    {
        if (slotLayout != null &&
            !string.IsNullOrEmpty(slotLayout.itemIconName))
        {
            return slotLayout.itemIconName;
        }

        return "ItemIcon";
    }

    string GetItemTextName()
    {
        if (slotLayout != null &&
            !string.IsNullOrEmpty(slotLayout.itemTextName))
        {
            return slotLayout.itemTextName;
        }

        return "ItemName";
    }

    static string GetSortName(StatItemData item)
    {
        if (item == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            return item.itemName.Trim();
        }

        return item.name;
    }

    static int CompareItemsByName(StatItemData a, StatItemData b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int nameCompare = string.Compare(
            GetSortName(a),
            GetSortName(b),
            StringComparison.OrdinalIgnoreCase);

        if (nameCompare != 0)
        {
            return nameCompare;
        }

        return string.Compare(
            a.name,
            b.name,
            StringComparison.OrdinalIgnoreCase);
    }

    static T FindComponentByName<T>(Transform parent, string targetName)
        where T : Component
    {
        if (parent == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name == targetName)
            {
                return child.GetComponent<T>();
            }

            T found = FindComponentByName<T>(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static float NormalizeAngle(float angle)
    {
        float value = angle % 360f;
        if (value < 0f)
        {
            value += 360f;
        }

        return value;
    }

    void GiveRewardToPlayer(StatItemData reward)
    {
        if (reward == null)
        {
            return;
        }

        ItemInventory playerInventory = GetPlayerInventory();
        if (playerInventory == null)
        {
            Debug.LogWarning(
                "LuckyWheelSpinTest: khong tim thay ItemInventory cua player de nhan thuong.");
            return;
        }

        ItemEffectSpawner.PlayPickupEffect(reward, playerInventory.transform);
        playerInventory.AddItem(reward, 1);
    }

    ItemInventory GetPlayerInventory()
    {
        InventoryPanelUI inventoryPanel = FindBestInventoryPanel();
        if (inventoryPanel != null)
        {
            if (inventoryPanel.inventory != null)
            {
                return inventoryPanel.inventory;
            }

            if (inventoryPanel.playerInventory != null)
            {
                return inventoryPanel.playerInventory;
            }
        }

        InventoryToggleButton inventoryToggle =
            FindAnyObjectByType<InventoryToggleButton>(
                FindObjectsInactive.Include);
        if (inventoryToggle != null &&
            inventoryToggle.inventoryPanel != null)
        {
            ItemInventory inventory = inventoryToggle.inventoryPanel.inventory;
            if (inventory != null)
            {
                return inventory;
            }

            inventory = inventoryToggle.inventoryPanel.playerInventory;
            if (inventory != null)
            {
                return inventory;
            }

            inventory = inventoryToggle.inventoryPanel.GetComponent<ItemInventory>();
            if (inventory != null)
            {
                return inventory;
            }
        }

        PlayerHealth playerHealth =
            FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (playerHealth != null)
        {
            ItemInventory inventory = playerHealth.GetComponent<ItemInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = playerHealth.GetComponentInParent<ItemInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = playerHealth.GetComponentInChildren<ItemInventory>(true);
            if (inventory != null)
            {
                return inventory;
            }
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            ItemInventory inventory = taggedPlayer.GetComponent<ItemInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = taggedPlayer.GetComponentInChildren<ItemInventory>(true);
            if (inventory != null)
            {
                return inventory;
            }
        }

        return null;
    }

    InventoryPanelUI FindBestInventoryPanel()
    {
        InventoryPanelUI[] panels =
            FindObjectsByType<InventoryPanelUI>(FindObjectsInactive.Include);

        InventoryPanelUI exactBaloPanel = null;
        InventoryPanelUI exactBalo = null;
        InventoryPanelUI nameMatch = null;
        InventoryPanelUI fallback = null;

        foreach (InventoryPanelUI panel in panels)
        {
            if (panel == null ||
                !panel.enabled ||
                HasAncestorNamed(panel.transform, "menupanel"))
            {
                continue;
            }

            string nameKey = panel.name.ToLowerInvariant();
            if (nameKey == "balopanel")
            {
                exactBaloPanel = panel;
            }
            else if (nameKey == "balo")
            {
                exactBalo = panel;
            }
            else if ((nameKey.Contains("balo") || nameKey.Contains("inventory")) &&
                nameMatch == null)
            {
                nameMatch = panel;
            }

            if (fallback == null && !panel.readOnly)
            {
                fallback = panel;
            }
        }

        if (exactBaloPanel != null)
        {
            return exactBaloPanel;
        }

        if (exactBalo != null)
        {
            return exactBalo;
        }

        if (nameMatch != null)
        {
            return nameMatch;
        }

        return fallback;
    }

    bool HasAncestorNamed(Transform current, string normalizedName)
    {
        while (current != null)
        {
            string key =
                current.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (key == normalizedName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
