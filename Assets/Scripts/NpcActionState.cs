using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcActionId
{
    Unknown,
    Idle,
    WalkingRoad,
    Rest,
    Eating,
    Work,
    GatherResource,
    Trade,
    Cultivate,
    Breakthrough,
    WaitTribulation,
    Hunt,
    Combat,
    Flee,
    Injured,
    Dead,
    OldAgeDeath,
    Conversation,
    Teleport,
    Task,
    Treasure,
    Social,
    Travel
}

[Serializable]
public struct NpcActionState
{
    public NpcActionId id;
    public string key;
    public string displayText;

    public bool IsKnown => id != NpcActionId.Unknown ||
        !string.IsNullOrWhiteSpace(key);

    public static NpcActionState FromDisplayText(string displayText)
    {
        return NpcActionStateCatalog.Resolve(displayText);
    }

    public static NpcActionState FromKey(string key)
    {
        return NpcActionStateCatalog.FromKey(key);
    }
}

public interface INpcActionStateOwner
{
    NpcActionState CurrentActionState { get; }
    void SetCurrentActionState(NpcActionState state);
}

public static class NpcActionStateCatalog
{
    static readonly Dictionary<string, NpcActionId> keyToId =
        new Dictionary<string, NpcActionId>(StringComparer.OrdinalIgnoreCase);

    static readonly Dictionary<string, string> normalizedDisplayToKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    static bool initialized;

    public static NpcActionState FromKey(string key)
    {
        EnsureInitialized();

        string safeKey = key ?? "";
        NpcActionId id = ResolveIdFromKey(safeKey);
        return new NpcActionState
        {
            id = id,
            key = safeKey,
            displayText = string.IsNullOrWhiteSpace(safeKey)
                ? ""
                : NpcText.Action(safeKey)
        };
    }

    public static NpcActionState Resolve(string displayText)
    {
        EnsureInitialized();

        string safeText = displayText ?? "";
        string normalized = Normalize(safeText);
        string key = "";
        NpcActionId id = NpcActionId.Unknown;

        if (!string.IsNullOrEmpty(normalized) &&
            normalizedDisplayToKey.TryGetValue(normalized, out key))
        {
            id = ResolveIdFromKey(key);
        }
        else
        {
            id = InferIdFromDisplayText(safeText);
        }

        return new NpcActionState
        {
            id = id,
            key = key,
            displayText = safeText
        };
    }

    public static bool Is(
        string displayText,
        NpcActionId expectedId)
    {
        return Resolve(displayText).id == expectedId;
    }

    static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        RegisterCoreKeys();
        RebuildDisplayLookup();
    }

    static void RegisterCoreKeys()
    {
        Register(NpcActionId.Idle, "idle");
        Register(NpcActionId.WalkingRoad, "walkingRoad");
        Register(NpcActionId.Rest, "rest", "restNearHome", "restVillageNoon", "stayNearHome", "goHomeRest");
        Register(NpcActionId.Eating, "eating", "eatAtShop");
        Register(NpcActionId.Work, "goWork", "goFarmWork", "working", "fishing", "goFish", "alchemy", "goAlchemy", "forging", "goForge", "goPatrol", "patrolling", "goHeal", "healing", "fixedAlchemistRefining", "fixedBlacksmithForging", "fixedBlacksmithForgingProgress");
        Register(NpcActionId.GatherResource, "gatherResource", "gatherVillageResource", "harvestResource", "pickItem", "pickHuntEvidence");
        Register(NpcActionId.Trade, "goMarketTrade", "tradeSeek", "trading", "goTavern", "buyPill", "goVanBaoLauBroker", "goVanBaoLauTask", "checkedVanBaoLau", "goBuyGoods", "goSellGoods", "sellGoods", "boughtGoods", "soldGoods", "waitTraderBuyGoods", "bringGoodsToCounter");
        Register(NpcActionId.Cultivate, "cultivate", "cultivateAbsorbQi", "goCultivatePoint", "goHomeCultivate");
        Register(NpcActionId.Breakthrough, "breakthrough", "breakthroughTo");
        Register(NpcActionId.WaitTribulation, "waitTribulation", "waitLightning", "waitLightningNamed");
        Register(NpcActionId.Hunt, "goHunt", "treasureHuntNamed");
        Register(NpcActionId.Combat, "detectIntruder", "chaseIntruder", "attackIntruder", "attack", "attackMonster", "attackMonsterNamed", "fight", "fightBlockingMonster", "guardSpiritHerbMonster", "clearHarvestMonster", "outerSkirmishNamed");
        Register(NpcActionId.Flee, "flee", "retreat", "fleeMonsterArea", "panicBurned");
        Register(NpcActionId.Injured, "injured");
        Register(NpcActionId.Dead, "dead");
        Register(NpcActionId.OldAgeDeath, "oldAgeDeath");
        Register(NpcActionId.Conversation, "talking");
        Register(
            NpcActionId.Task,
            "goTaskProviderDaily",
            "visitedTaskProvider",
            "askProviderFindTask",
            "showTaskBoard",
            "chooseTask",
            "returnProviderReceiveTask",
            "goWorkTask",
            "workingTask",
            "taskCompleted",
            "moveToTask",
            "receiveTask");
        Register(NpcActionId.Social, "makeFriend", "createSect", "goPlay", "playWithFriends", "wanderVillage", "eveningWalkVillage");
        Register(NpcActionId.Travel, "returnTerritory", "restTerritory", "choosePatrolPoint");
    }

    static void Register(NpcActionId id, params string[] keys)
    {
        if (keys == null)
        {
            return;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            if (!string.IsNullOrWhiteSpace(key))
            {
                keyToId[key] = id;
            }
        }
    }

    static void RebuildDisplayLookup()
    {
        normalizedDisplayToKey.Clear();

        foreach (KeyValuePair<string, NpcActionId> pair in keyToId)
        {
            string displayText = NpcText.Action(pair.Key);
            string normalized = Normalize(displayText);
            if (!string.IsNullOrEmpty(normalized) &&
                !normalizedDisplayToKey.ContainsKey(normalized))
            {
                normalizedDisplayToKey.Add(normalized, pair.Key);
            }
        }
    }

    static NpcActionId ResolveIdFromKey(string key)
    {
        if (!string.IsNullOrWhiteSpace(key) &&
            keyToId.TryGetValue(key, out NpcActionId id))
        {
            return id;
        }

        if (!string.IsNullOrWhiteSpace(key) &&
            key.StartsWith("teleportGateTo", StringComparison.OrdinalIgnoreCase))
        {
            return NpcActionId.Teleport;
        }

        return NpcActionId.Unknown;
    }

    static NpcActionId InferIdFromDisplayText(string text)
    {
        string normalized = Normalize(text);
        if (string.IsNullOrEmpty(normalized))
        {
            return NpcActionId.Unknown;
        }

        if (normalized.Contains("teleport") ||
            normalized.Contains("congdichchuyen"))
        {
            return NpcActionId.Teleport;
        }

        if (normalized.Contains("treasure") ||
            normalized.Contains("khobau"))
        {
            return NpcActionId.Treasure;
        }

        return NpcActionId.Unknown;
    }

    static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        char[] buffer = new char[value.Length];
        int length = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(c))
            {
                buffer[length] = c;
                length++;
            }
        }

        return new string(buffer, 0, length);
    }
}
