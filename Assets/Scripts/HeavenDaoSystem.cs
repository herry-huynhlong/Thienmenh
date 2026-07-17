using System;
using System.Collections.Generic;
using UnityEngine;

public enum HeavenDaoPower
{
    ObserveWorld,
    ViewBasicNpcInfo,
    DirectGiftItem,
    DropOpportunityArea,
    ViewLuck,
    GatherAreaSpiritualQi,
    OpenSpiritualVein,
    TriggerHeavenEarthOmen,
    InterveneTribulation,
    ChangeAreaSpiritualQi
}

[Serializable]
public class HeavenDaoUnlock
{
    public int controlPercent;
    public int requiredOrigin;
    public HeavenDaoPower power;
    public string displayName;
}

[Serializable]
public class HeavenDaoStoryReward
{
    public string keyword;
    public int originReward;
    public string displayLabel;
}

public class HeavenDaoSystem : MonoBehaviour
{
    public static HeavenDaoSystem Instance { get; private set; }

    internal const string OriginKey = "HeavenDao.Origin";
    internal const string KarmaKey = "HeavenDao.Karma";
    internal const string LifetimeOriginEarnedKey = "HeavenDao.LifetimeOriginEarned";
    internal const string MaxOriginReachedKey = "HeavenDao.MaxOriginReached";
    internal const string WorldSpiritQiKey = "HeavenDao.WorldSpiritQi";
    internal const string DropOpportunityUnlockedKey = "HeavenDao.DropOpportunityUnlocked";
    internal const string RegionalSpiritGatheringUnlockedKey = "HeavenDao.RegionalSpiritGatheringUnlocked";

    public const int DropOpportunityUnlockOrigin = 1000;
    public const int RegionalSpiritGatheringUnlockOrigin = 2000;
    public const int WorldSpiritQiBaseValue = 100;
    public const int WorldSpiritQiUpgradeCost = 1000;
    public const int WorldSpiritQiUpgradeStep = 50;

    [Header("State")]
    public int origin;
    public int karma;
    public int lifetimeOriginEarned;
    public int maxOriginReached;
    public int worldSpiritQi = WorldSpiritQiBaseValue;
    public bool isDropOpportunityUnlocked;
    public bool isRegionalSpiritGatheringUnlocked;
    public int maxRecentLogs = 5;

    [Header("Development")]
    [Tooltip("When enabled, Heaven Dao powers are treated as unlocked in development unless forced locked by code.")]
    public bool bypassUnlockRequirements = true;

    [Header("Unlocks")]
    public HeavenDaoUnlock[] unlocks =
    {
        new HeavenDaoUnlock
        {
            controlPercent = 5,
            requiredOrigin = 150,
            power = HeavenDaoPower.ViewBasicNpcInfo,
            displayName = "Xem th\u00F4ng tin Tu s\u0129"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 10,
            requiredOrigin = 500,
            power = HeavenDaoPower.DirectGiftItem,
            displayName = "Ban ph\u00E1t v\u1EADt ph\u1EA9m"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 20,
            requiredOrigin = DropOpportunityUnlockOrigin,
            power = HeavenDaoPower.DropOpportunityArea,
            displayName = "Th\u1EA3 c\u01A1 duy\u00EAn"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 40,
            requiredOrigin = RegionalSpiritGatheringUnlockOrigin,
            power = HeavenDaoPower.GatherAreaSpiritualQi,
            displayName = "C\u01B0\u1EDDng H\u00F3a Linh Kh\u00ED"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 50,
            requiredOrigin = 5000,
            power = HeavenDaoPower.OpenSpiritualVein,
            displayName = "Khai M\u1EDF Linh M\u1EA1ch"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 60,
            requiredOrigin = 20000,
            power = HeavenDaoPower.TriggerHeavenEarthOmen,
            displayName = "D\u1ECB T\u01B0\u1EE3ng Thi\u00EAn \u0110\u1ECBa"
        }
    };

    [Header("Story Rewards")]
    public HeavenDaoStoryReward[] storyRewards =
    {
        new HeavenDaoStoryReward { keyword = "\u0111\u1ED9t ph\u00E1", originReward = 3, displayLabel = "\u0111\u1ED9t ph\u00E1" },
        new HeavenDaoStoryReward { keyword = "dot pha", originReward = 3, displayLabel = "dot pha" },
        new HeavenDaoStoryReward { keyword = "th\u0103ng c\u1EA5p", originReward = 5, displayLabel = "th\u0103ng c\u1EA5p" },
        new HeavenDaoStoryReward { keyword = "thang cap", originReward = 5, displayLabel = "thang cap" },
        new HeavenDaoStoryReward { keyword = "sinh con", originReward = 10, displayLabel = "sinh con" },
        new HeavenDaoStoryReward { keyword = "ti\u1EBFn h\u00F3a", originReward = 25, displayLabel = "ti\u1EBFn h\u00F3a" },
        new HeavenDaoStoryReward { keyword = "tien hoa", originReward = 25, displayLabel = "tien hoa" },
        new HeavenDaoStoryReward { keyword = "thi\u00EAn t\u00E0i", originReward = 50, displayLabel = "thi\u00EAn t\u00E0i xu\u1EA5t hi\u1EC7n" },
        new HeavenDaoStoryReward { keyword = "thien tai", originReward = 50, displayLabel = "thien tai xuat hien" },
        new HeavenDaoStoryReward { keyword = "\u0111\u1EA1i chi\u1EBFn", originReward = 80, displayLabel = "\u0111\u1EA1i chi\u1EBFn" },
        new HeavenDaoStoryReward { keyword = "dai chien", originReward = 80, displayLabel = "dai chien" }
    };

    readonly List<string> recentLogs = new List<string>();
    readonly HashSet<string> processedStoryLogs = new HashSet<string>();
    readonly HashSet<HeavenDaoPower> forcedLockedPowers = new HashSet<HeavenDaoPower>();

    public event Action OnChanged;
    public event Action<int> WorldSpiritQiChanged;

    public int ControlPercent => GetControlPercent();
    public IReadOnlyList<string> RecentLogs => recentLogs;
    public bool HasForcedLocks => forcedLockedPowers.Count > 0;
    public int CurrentOrigin => Mathf.Max(0, origin);
    public int LifetimeOriginTotal => Mathf.Max(0, lifetimeOriginEarned);
    public int MaxOriginProgress => Mathf.Max(Mathf.Max(0, maxOriginReached), CurrentOrigin);
    public int CurrentWorldSpiritQi => Mathf.Max(WorldSpiritQiBaseValue, worldSpiritQi);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject systemObject = new GameObject("HeavenDaoSystem");
        DontDestroyOnLoad(systemObject);
        systemObject.AddComponent<HeavenDaoSystem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
    }

    void OnEnable()
    {
        WorldEventManager.LogAdded += HandleStoryLogAdded;
        SyncExistingStoryLogs();
    }

    void OnDisable()
    {
        WorldEventManager.LogAdded -= HandleStoryLogAdded;
    }

    public bool HasPower(HeavenDaoPower power)
    {
        if (forcedLockedPowers.Contains(power))
        {
            return false;
        }

        if (bypassUnlockRequirements)
        {
            return true;
        }

        return HasPowerStrict(power);
    }

    public bool HasPowerStrict(HeavenDaoPower power)
    {
        if (power == HeavenDaoPower.ObserveWorld)
        {
            return true;
        }

        if (power == HeavenDaoPower.DropOpportunityArea)
        {
            return isDropOpportunityUnlocked;
        }

        if (power == HeavenDaoPower.GatherAreaSpiritualQi)
        {
            return isRegionalSpiritGatheringUnlocked;
        }

        for (int i = 0; i < unlocks.Length; i++)
        {
            HeavenDaoUnlock unlock = unlocks[i];
            if (unlock != null &&
                unlock.power == power &&
                MaxOriginProgress >= unlock.requiredOrigin)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPowerForcedLocked(HeavenDaoPower power)
    {
        return forcedLockedPowers.Contains(power);
    }

    public void SetPowerLocked(HeavenDaoPower power, bool locked)
    {
        if (power == HeavenDaoPower.ObserveWorld)
        {
            return;
        }

        bool changed = locked
            ? forcedLockedPowers.Add(power)
            : forcedLockedPowers.Remove(power);

        if (!changed)
        {
            return;
        }

        OnChanged?.Invoke();
    }

    public void ClearForcedPowerLocks()
    {
        if (forcedLockedPowers.Count == 0)
        {
            return;
        }

        forcedLockedPowers.Clear();
        OnChanged?.Invoke();
    }

    public void SetBypassUnlockRequirements(bool bypass)
    {
        if (bypassUnlockRequirements == bypass)
        {
            return;
        }

        bypassUnlockRequirements = bypass;
        OnChanged?.Invoke();
    }

    public int GetNextRequiredOrigin()
    {
        if (bypassUnlockRequirements && forcedLockedPowers.Count == 0)
        {
            return Mathf.Max(CurrentOrigin, 1);
        }

        int next = 0;
        for (int i = 0; i < unlocks.Length; i++)
        {
            HeavenDaoUnlock unlock = unlocks[i];
            if (unlock == null)
            {
                continue;
            }

            bool forcedLocked = forcedLockedPowers.Contains(unlock.power);
            if (!forcedLocked && bypassUnlockRequirements)
            {
                continue;
            }

            if (!forcedLocked && HasPowerStrict(unlock.power))
            {
                continue;
            }

            if (next <= 0 || unlock.requiredOrigin < next)
            {
                next = unlock.requiredOrigin;
            }
        }

        return next > 0 ? next : Mathf.Max(CurrentOrigin, 1);
    }

    public HeavenDaoUnlock GetNextUnlock()
    {
        if (bypassUnlockRequirements && forcedLockedPowers.Count == 0)
        {
            return null;
        }

        HeavenDaoUnlock next = null;
        for (int i = 0; i < unlocks.Length; i++)
        {
            HeavenDaoUnlock unlock = unlocks[i];
            if (unlock == null)
            {
                continue;
            }

            bool forcedLocked = forcedLockedPowers.Contains(unlock.power);
            if (!forcedLocked && bypassUnlockRequirements)
            {
                continue;
            }

            if (!forcedLocked && HasPowerStrict(unlock.power))
            {
                continue;
            }

            if (next == null || unlock.requiredOrigin < next.requiredOrigin)
            {
                next = unlock;
            }
        }

        return next;
    }

    public void AddOrigin(int amount, string reason)
    {
        if (amount <= 0)
        {
            return;
        }

        ApplyOriginGain(amount);
        AddRecentLog("+" + amount + " " + reason);
        if (WorldScreenNotificationHub.Instance != null)
        {
            WorldScreenNotificationHub.ShowOrigin(
                string.IsNullOrWhiteSpace(reason) ? "Thien Dao" : reason,
                amount);
        }

        NotifyStateChanged();
    }

    public bool TrySpendOrigin(int amount, string reason)
    {
        return TrySpendOriginInternal(amount, reason, true);
    }

    bool TrySpendOriginInternal(int amount, string reason, bool notifyStateChange)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (CurrentOrigin < amount)
        {
            return false;
        }

        origin = Mathf.Max(0, origin - amount);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            AddRecentLog("-" + amount + " " + reason);
        }

        if (notifyStateChange)
        {
            NotifyStateChanged();
        }

        return true;
    }

    public bool TryUpgradeWorldSpiritQi()
    {
        if (!HasPower(HeavenDaoPower.GatherAreaSpiritualQi))
        {
            return false;
        }

        if (!TrySpendOriginInternal(WorldSpiritQiUpgradeCost, "Nang linh khi", false))
        {
            return false;
        }

        worldSpiritQi = Mathf.Max(
            WorldSpiritQiBaseValue,
            CurrentWorldSpiritQi + WorldSpiritQiUpgradeStep);

        SaveState();
        WorldSpiritQiChanged?.Invoke(CurrentWorldSpiritQi);
        OnChanged?.Invoke();
        return true;
    }

    public float GetWorldSpiritQiMultiplier()
    {
        return Mathf.Max(0.1f, CurrentWorldSpiritQi / 100f);
    }

    public void AddKarma(int amount, string reason)
    {
        if (amount <= 0)
        {
            return;
        }

        karma = Mathf.Max(0, karma + amount);
        AddRecentLog("Nhan Qua +" + amount + " " + reason);
        if (WorldScreenNotificationHub.Instance != null)
        {
            WorldScreenNotificationHub.ShowNormal(
                string.IsNullOrWhiteSpace(reason)
                    ? "Nhan Qua +" + amount
                    : "Nhan Qua +" + amount + " " + reason);
        }

        NotifyStateChanged();
    }

    public static void ClearSavedState()
    {
        PlayerPrefs.DeleteKey(OriginKey);
        PlayerPrefs.DeleteKey(KarmaKey);
        PlayerPrefs.DeleteKey(LifetimeOriginEarnedKey);
        PlayerPrefs.DeleteKey(MaxOriginReachedKey);
        PlayerPrefs.DeleteKey(WorldSpiritQiKey);
        PlayerPrefs.DeleteKey(DropOpportunityUnlockedKey);
        PlayerPrefs.DeleteKey(RegionalSpiritGatheringUnlockedKey);

        if (Instance == null)
        {
            return;
        }

        Instance.origin = 0;
        Instance.karma = 0;
        Instance.lifetimeOriginEarned = 0;
        Instance.maxOriginReached = 0;
        Instance.worldSpiritQi = WorldSpiritQiBaseValue;
        Instance.isDropOpportunityUnlocked = false;
        Instance.isRegionalSpiritGatheringUnlocked = false;
        Instance.recentLogs.Clear();
        Instance.processedStoryLogs.Clear();
        Instance.forcedLockedPowers.Clear();
        Instance.WorldSpiritQiChanged?.Invoke(Instance.CurrentWorldSpiritQi);
        Instance.OnChanged?.Invoke();
    }

    public bool TryGetStoryReward(
        string content,
        out HeavenDaoStoryReward reward)
    {
        reward = FindStoryReward(content);
        return reward != null;
    }

    void HandleStoryLogAdded(LogEntry entry)
    {
        if (entry == null ||
            string.IsNullOrEmpty(entry.content) ||
            !entry.isStoryLog)
        {
            return;
        }

        string uniqueKey = entry.timestamp + entry.content;
        if (!processedStoryLogs.Add(uniqueKey))
        {
            return;
        }

        HeavenDaoStoryReward reward = FindStoryReward(entry.content);
        if (reward == null)
        {
            return;
        }

        ApplyOriginGain(reward.originReward);
        AddRecentLog(BuildStoryRewardLine(entry.content, reward.originReward));
        NotifyStateChanged();
    }

    void SyncExistingStoryLogs()
    {
        if (WorldEventManager.Instance == null)
        {
            return;
        }

        List<LogEntry> logs = WorldEventManager.Instance.GetStoryLogs();
        if (logs == null)
        {
            return;
        }

        for (int i = logs.Count - 1; i >= 0; i--)
        {
            HandleStoryLogAdded(logs[i]);
        }
    }

    HeavenDaoStoryReward FindStoryReward(string content)
    {
        string normalized = Normalize(content);
        HeavenDaoStoryReward best = null;

        for (int i = 0; i < storyRewards.Length; i++)
        {
            HeavenDaoStoryReward reward = storyRewards[i];
            if (reward == null || string.IsNullOrEmpty(reward.keyword))
            {
                continue;
            }

            if (!normalized.Contains(Normalize(reward.keyword)))
            {
                continue;
            }

            if (best == null || reward.originReward > best.originReward)
            {
                best = reward;
            }
        }

        return best;
    }

    string BuildStoryRewardLine(string content, int originReward)
    {
        string compact = content.Trim();
        if (compact.Length > 42)
        {
            compact = compact.Substring(0, 42) + "...";
        }

        return compact + " +" + originReward;
    }

    string Normalize(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.ToLowerInvariant();
    }

    int GetControlPercent()
    {
        int percent = 0;
        for (int i = 0; i < unlocks.Length; i++)
        {
            HeavenDaoUnlock unlock = unlocks[i];
            if (unlock != null && HasPowerStrict(unlock.power))
            {
                percent = Mathf.Max(percent, unlock.controlPercent);
            }
        }

        return percent;
    }

    void AddRecentLog(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        recentLogs.Insert(0, line);
        int limit = Mathf.Clamp(maxRecentLogs, 1, 5);
        while (recentLogs.Count > limit)
        {
            recentLogs.RemoveAt(recentLogs.Count - 1);
        }
    }

    void LoadState()
    {
        origin = Mathf.Max(0, PlayerPrefs.GetInt(OriginKey, origin));
        karma = Mathf.Max(0, PlayerPrefs.GetInt(KarmaKey, karma));
        lifetimeOriginEarned =
            Mathf.Max(PlayerPrefs.GetInt(LifetimeOriginEarnedKey, lifetimeOriginEarned), origin);
        maxOriginReached =
            Mathf.Max(PlayerPrefs.GetInt(MaxOriginReachedKey, maxOriginReached), origin);
        worldSpiritQi =
            Mathf.Max(WorldSpiritQiBaseValue, PlayerPrefs.GetInt(WorldSpiritQiKey, worldSpiritQi));
        isDropOpportunityUnlocked =
            PlayerPrefs.GetInt(DropOpportunityUnlockedKey, isDropOpportunityUnlocked ? 1 : 0) == 1;
        isRegionalSpiritGatheringUnlocked =
            PlayerPrefs.GetInt(
                RegionalSpiritGatheringUnlockedKey,
                isRegionalSpiritGatheringUnlocked ? 1 : 0) == 1;

        EvaluatePermanentUnlocks();
        SaveState();
    }

    void SaveState()
    {
        PlayerPrefs.SetInt(OriginKey, origin);
        PlayerPrefs.SetInt(KarmaKey, karma);
        PlayerPrefs.SetInt(LifetimeOriginEarnedKey, lifetimeOriginEarned);
        PlayerPrefs.SetInt(MaxOriginReachedKey, maxOriginReached);
        PlayerPrefs.SetInt(WorldSpiritQiKey, CurrentWorldSpiritQi);
        PlayerPrefs.SetInt(DropOpportunityUnlockedKey, isDropOpportunityUnlocked ? 1 : 0);
        PlayerPrefs.SetInt(
            RegionalSpiritGatheringUnlockedKey,
            isRegionalSpiritGatheringUnlocked ? 1 : 0);
        PlayerPrefs.Save();
    }

    void ApplyOriginGain(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        origin = Mathf.Max(0, origin + amount);
        lifetimeOriginEarned = Mathf.Max(0, lifetimeOriginEarned + amount);
        maxOriginReached = Mathf.Max(maxOriginReached, origin);
        EvaluatePermanentUnlocks();
    }

    void EvaluatePermanentUnlocks()
    {
        int progressOrigin = MaxOriginProgress;

        if (!isDropOpportunityUnlocked &&
            progressOrigin >= DropOpportunityUnlockOrigin)
        {
            isDropOpportunityUnlocked = true;
        }

        if (!isRegionalSpiritGatheringUnlocked &&
            progressOrigin >= RegionalSpiritGatheringUnlockOrigin)
        {
            isRegionalSpiritGatheringUnlocked = true;
        }
    }

    void NotifyStateChanged()
    {
        SaveState();
        OnChanged?.Invoke();
    }
}
