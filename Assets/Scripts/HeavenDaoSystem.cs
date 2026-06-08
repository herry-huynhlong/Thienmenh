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

    const string OriginKey = "HeavenDao.Origin";
    const string KarmaKey = "HeavenDao.Karma";

    [Header("State")]
    public int origin;
    public int karma;
    public int maxRecentLogs = 5;

    [Header("Unlocks")]
    public HeavenDaoUnlock[] unlocks =
    {
        new HeavenDaoUnlock
        {
            controlPercent = 5,
            requiredOrigin = 150,
            power = HeavenDaoPower.ViewBasicNpcInfo,
            displayName = "Xem thông tin Tu sĩ"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 10,
            requiredOrigin = 500,
            power = HeavenDaoPower.DirectGiftItem,
            displayName = "Ban phát vật phẩm"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 20,
            requiredOrigin = 2000,
            power = HeavenDaoPower.DropOpportunityArea,
            displayName = "Thả cơ duyên"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 40,
            requiredOrigin = 5000,
            power = HeavenDaoPower.GatherAreaSpiritualQi,
            displayName = "Tụ Linh Khu Vực"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 50,
            requiredOrigin = 10000,
            power = HeavenDaoPower.OpenSpiritualVein,
            displayName = "Khai Mở Linh Mạch"
        },
        new HeavenDaoUnlock
        {
            controlPercent = 60,
            requiredOrigin = 20000,
            power = HeavenDaoPower.TriggerHeavenEarthOmen,
            displayName = "Dị Tượng Thiên Địa"
        }
    };

    [Header("Story Rewards")]
    public HeavenDaoStoryReward[] storyRewards =
    {
        new HeavenDaoStoryReward { keyword = "đột phá", originReward = 3, displayLabel = "đột phá" },
        new HeavenDaoStoryReward { keyword = "dot pha", originReward = 3, displayLabel = "đột phá" },
        new HeavenDaoStoryReward { keyword = "tiến hóa", originReward = 25, displayLabel = "tiến hóa" },
        new HeavenDaoStoryReward { keyword = "tien hoa", originReward = 25, displayLabel = "tiến hóa" },
        new HeavenDaoStoryReward { keyword = "thăng cấp", originReward = 5, displayLabel = "thăng cấp" },
        new HeavenDaoStoryReward { keyword = "thang cap", originReward = 5, displayLabel = "thăng cấp" },
        new HeavenDaoStoryReward { keyword = "sinh con", originReward = 10, displayLabel = "sinh con" },
        new HeavenDaoStoryReward { keyword = "thiên tài", originReward = 50, displayLabel = "thiên tài xuất hiện" },
        new HeavenDaoStoryReward { keyword = "thien tai", originReward = 50, displayLabel = "thiên tài xuất hiện" },
        new HeavenDaoStoryReward { keyword = "đại chiến", originReward = 80, displayLabel = "đại chiến" },
        new HeavenDaoStoryReward { keyword = "dai chien", originReward = 80, displayLabel = "đại chiến" }
    };

    readonly List<string> recentLogs = new List<string>();
    readonly HashSet<string> processedStoryLogs = new HashSet<string>();

    public event Action OnChanged;

    public int ControlPercent => GetControlPercent();
    public IReadOnlyList<string> RecentLogs => recentLogs;

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
        if (power == HeavenDaoPower.ObserveWorld)
        {
            return true;
        }

        for (int i = 0; i < unlocks.Length; i++)
        {
            HeavenDaoUnlock unlock = unlocks[i];
            if (unlock != null &&
                unlock.power == power &&
                origin >= unlock.requiredOrigin)
            {
                return true;
            }
        }

        return false;
    }

    public int GetNextRequiredOrigin()
    {
        int next = 0;
        for (int i = 0; i < unlocks.Length; i++)
        {
            HeavenDaoUnlock unlock = unlocks[i];
            if (unlock == null || unlock.requiredOrigin <= origin)
            {
                continue;
            }

            if (next <= 0 || unlock.requiredOrigin < next)
            {
                next = unlock.requiredOrigin;
            }
        }

        return next > 0 ? next : Mathf.Max(origin, 1);
    }

    public HeavenDaoUnlock GetNextUnlock()
    {
        HeavenDaoUnlock next = null;
        for (int i = 0; i < unlocks.Length; i++)
        {
            HeavenDaoUnlock unlock = unlocks[i];
            if (unlock == null || unlock.requiredOrigin <= origin)
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

        origin = Mathf.Max(0, origin + amount);
        AddRecentLog("+" + amount + " " + reason);
        SaveState();
        OnChanged?.Invoke();
    }

    public void AddKarma(int amount, string reason)
    {
        if (amount <= 0)
        {
            return;
        }

        karma = Mathf.Max(0, karma + amount);
        AddRecentLog("Nhân Quả +" + amount + " " + reason);
        SaveState();
        OnChanged?.Invoke();
    }

    void HandleStoryLogAdded(LogEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.content))
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

        origin = Mathf.Max(0, origin + reward.originReward);
        AddRecentLog(BuildStoryRewardLine(entry.content, reward.originReward));
        SaveState();
        OnChanged?.Invoke();
    }

    void SyncExistingStoryLogs()
    {
        if (WorldEventManager.Instance == null)
        {
            return;
        }

        List<LogEntry> logs = WorldEventManager.Instance.GetLogs();
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
            if (unlock != null && origin >= unlock.requiredOrigin)
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
    }

    void SaveState()
    {
        PlayerPrefs.SetInt(OriginKey, origin);
        PlayerPrefs.SetInt(KarmaKey, karma);
        PlayerPrefs.Save();
    }
}
