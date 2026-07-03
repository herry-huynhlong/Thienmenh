using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NpcSocialSystem : MonoBehaviour
{
    [ContextMenu("Install Social System For Scene NPCs")]
    public void InstallForSceneNpcs()
    {
        NpcSocialWorldInstaller installer =
            NpcSocialWorldInstaller.Instance;

        if (installer == null)
        {
            GameObject installerObject =
                new GameObject("NpcSocialWorldInstaller");

            installer =
                installerObject.AddComponent<NpcSocialWorldInstaller>();
        }

        installer.InstallForSceneNpcs();
    }
}

public enum NpcMemoryType
{
    Greeting,
    Conversation,
    ResourceRumor,
    TradeOffer,
    TradeCompleted,
    TradeRejected,
    Theft,
    Attack,
    Help,
    Alliance,
    Warning
}

public enum NpcDecisionKind
{
    None,
    Work,
    Rest,
    Socialize,
    Trade,
    GatherResource,
    Study,
    Rob,
    Revenge,
    Ally,
    Flee
}

[Serializable]
public class NpcMemoryRecord
{
    public NpcMemoryType type;
    public string subjectId;
    public string targetId;
    public string topic;
    public string itemName;
    public string locationName;
    public int worldDay;
    public float worldHour;
    public float importance = 1f;
    public float confidence = 1f;
    public bool resolved;
    public int expiryDay = -1;
}

[Serializable]
public class NpcSocialRelationship
{
    public string targetId;
    [Range(-100, 100)] public int affection;
    [Range(-100, 100)] public int trust;
    [Range(0, 100)] public int fear;
    [Range(0, 100)] public int grudge;
    [Range(-100, 100)] public int debt;
    [Range(-100, 100)] public int respect;
    [Range(0, 100)] public int alliance;
    [Range(0, 100)] public int hostility;
    public int lastInteractionDay = -1;
    public string lastTopic;
}

public static class NpcSocialEventBus
{
    public static event Action<GameObject, GameObject, StatItemData, int> TradeCompleted;
    public static event Action<GameObject, GameObject, string> RumorShared;
    public static event Action<GameObject, GameObject, int> HostilityHappened;
    public static event Action<GameObject, GameObject, int, Vector3, string> HostilityDetailedHappened;
    public static event Action<MonsterAI, Vector3, string, int> MonsterDefeated;

    public static void PublishTradeCompleted(
        GameObject buyer,
        GameObject seller,
        StatItemData item,
        int price)
    {
        if (buyer == null || seller == null || item == null)
        {
            return;
        }

        if (NpcRoleUtility.IsDead(buyer) ||
            NpcRoleUtility.IsDead(seller))
        {
            return;
        }

        if (NpcRoleUtility.IsInCombat(buyer) ||
            NpcRoleUtility.IsInCombat(seller))
        {
            return;
        }

        TradeCompleted?.Invoke(buyer, seller, item, price);
    }

    public static void PublishRumorShared(
        GameObject speaker,
        GameObject listener,
        string topic)
    {
        if (speaker == null || listener == null || string.IsNullOrEmpty(topic))
        {
            return;
        }

        RumorShared?.Invoke(speaker, listener, topic);
    }

    public static void PublishHostility(
        GameObject actor,
        GameObject target,
        int severity)
    {
        if (actor == null || target == null)
        {
            return;
        }

        int clampedSeverity =
            Mathf.Clamp(severity, 1, 100);

        HostilityHappened?.Invoke(actor, target, clampedSeverity);
        HostilityDetailedHappened?.Invoke(
            actor,
            target,
            clampedSeverity,
            target.transform.position,
            NpcText.Dialogue("hostilityReasonFallback"));
    }

    public static void PublishHostility(
        GameObject actor,
        GameObject target,
        int severity,
        Vector3 position,
        string reason)
    {
        if (actor == null || target == null)
        {
            return;
        }

        int clampedSeverity =
            Mathf.Clamp(severity, 1, 100);

        HostilityHappened?.Invoke(actor, target, clampedSeverity);
        HostilityDetailedHappened?.Invoke(
            actor,
            target,
            clampedSeverity,
            position,
            string.IsNullOrEmpty(reason)
                ? NpcText.Dialogue("hostilityReasonFallback")
                : reason);
    }

    public static void PublishMonsterDefeated(MonsterAI monster)
    {
        if (monster == null)
        {
            return;
        }

        string monsterName = !string.IsNullOrWhiteSpace(monster.monsterName)
            ? monster.monsterName
            : NpcText.Get("entityTypes", "monster");

        MonsterDefeated?.Invoke(
            monster,
            monster.transform.position,
            monsterName,
            Mathf.Max(1, monster.beastLevel));
    }
}

public static class NpcMonsterCombatDialogue
{
    class CombatRecord
    {
        public readonly List<GameObject> attackers = new List<GameObject>();
        public float lastHitTime;
    }

    static readonly Dictionary<int, CombatRecord> records =
        new Dictionary<int, CombatRecord>();
    static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureSubscribed()
    {
        if (subscribed)
        {
            return;
        }

        subscribed = true;
        NpcSocialEventBus.HostilityDetailedHappened += HandleHostility;
        NpcSocialEventBus.MonsterDefeated += HandleMonsterDefeated;
    }

    static void HandleHostility(
        GameObject actor,
        GameObject target,
        int severity,
        Vector3 position,
        string reason)
    {
        if (actor == null || target == null || !IsNpc(actor))
        {
            return;
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        if (monster == null)
        {
            return;
        }

        int id = monster.GetInstanceID();
        if (!records.TryGetValue(id, out CombatRecord record))
        {
            record = new CombatRecord();
            records[id] = record;
        }

        if (!record.attackers.Contains(actor))
        {
            record.attackers.Add(actor);
        }

        record.lastHitTime = Time.time;
    }

    static void HandleMonsterDefeated(
        MonsterAI monster,
        Vector3 position,
        string monsterName,
        int monsterLevel)
    {
        if (monster == null)
        {
            return;
        }

        int id = monster.GetInstanceID();
        records.TryGetValue(id, out CombatRecord record);
        records.Remove(id);

        List<GameObject> fighters = CollectValidFighters(record, position);
        if (fighters.Count <= 0)
        {
            fighters = FindNearbyNpcs(position, 4.5f, null);
        }

        if (fighters.Count <= 0)
        {
            return;
        }

        GameObject first = fighters[0];
        GameObject second = fighters.Count > 1 ? fighters[1] : FindNearbyNpc(position, first);

        if (second != null)
        {
            ShowFormattedLine(
                first,
                "monsterVictoryAllyLines",
                4.5f,
                5,
                GetName(first),
                GetName(second),
                monsterName,
                monsterLevel);
            ShowFormattedLine(
                second,
                "monsterVictoryAllyReplies",
                4.5f,
                5,
                GetName(second),
                GetName(first),
                monsterName,
                monsterLevel);
            AddSharedRespect(first, second);
            return;
        }

        ShowFormattedLine(
            first,
            "monsterVictorySoloLines",
            4.5f,
            5,
            GetName(first),
            monsterName,
            monsterLevel);
    }

    static List<GameObject> CollectValidFighters(
        CombatRecord record,
        Vector3 position)
    {
        List<GameObject> fighters = new List<GameObject>();
        if (record == null || Time.time - record.lastHitTime > 20f)
        {
            return fighters;
        }

        for (int i = 0; i < record.attackers.Count; i++)
        {
            GameObject npc = record.attackers[i];
            if (npc == null ||
                NpcRoleUtility.IsDead(npc) ||
                Vector2.Distance(npc.transform.position, position) > 10f)
            {
                continue;
            }

            fighters.Add(npc);
        }

        return fighters;
    }

    static GameObject FindNearbyNpc(Vector3 position, GameObject exclude)
    {
        List<GameObject> nearby = FindNearbyNpcs(position, 4.5f, exclude);
        return nearby.Count > 0 ? nearby[0] : null;
    }

    static List<GameObject> FindNearbyNpcs(
        Vector3 position,
        float radius,
        GameObject exclude)
    {
        List<GameObject> result = new List<GameObject>();
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            GameObject npc = GetNpcRoot(hits[i]);
            if (npc == null ||
                npc == exclude ||
                result.Contains(npc) ||
                NpcRoleUtility.IsDead(npc))
            {
                continue;
            }

            result.Add(npc);
        }

        return result;
    }

    static GameObject GetNpcRoot(Collider2D hit)
    {
        VillagerAI villager = hit.GetComponentInParent<VillagerAI>();
        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc = hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        return null;
    }

    static bool IsNpc(GameObject actor)
    {
        return actor.GetComponent<VillagerAI>() != null ||
            actor.GetComponent<SmartNpcAI>() != null;
    }

    static void ShowFormattedLine(
        GameObject npc,
        string key,
        float duration,
        int priority,
        params object[] args)
    {
        if (npc == null)
        {
            return;
        }

        string template = NpcText.DialogueLine(key, "");
        if (string.IsNullOrEmpty(template))
        {
            return;
        }

        NpcOverheadDialogueUI overhead =
            npc.GetComponent<NpcOverheadDialogueUI>();

        if (overhead == null)
        {
            overhead = npc.AddComponent<NpcOverheadDialogueUI>();
        }

        overhead.ShowLine(NpcText.Format(template, args), duration, priority);
    }

    static void AddSharedRespect(GameObject first, GameObject second)
    {
        NpcRelationshipGraph firstGraph =
            first.GetComponent<NpcRelationshipGraph>();
        NpcRelationshipGraph secondGraph =
            second.GetComponent<NpcRelationshipGraph>();

        if (firstGraph != null)
        {
            firstGraph.AddSocial(second, 3, 4, NpcText.Dialogue("combatMonsterReason"));
        }

        if (secondGraph != null)
        {
            secondGraph.AddSocial(first, 3, 4, NpcText.Dialogue("combatMonsterReason"));
        }
    }

    static string GetName(GameObject npc)
    {
        return NpcRoleUtility.GetDisplayName(npc);
    }
}

public class NpcSocialIdentity : MonoBehaviour
{
    public string socialId;
    public string displayName;
    public string faction = "Dân cư";

    void Awake()
    {
        Refresh();
    }

    public void Refresh()
    {
        displayName = NpcRoleUtility.GetDisplayName(gameObject);

        if (string.IsNullOrEmpty(displayName))
        {
            displayName = name;
        }

        if (string.IsNullOrEmpty(socialId))
        {
            socialId = gameObject.scene.name + ":" + displayName + ":" + GetInstanceID();
        }
    }
}

public class NpcNeeds : MonoBehaviour
{
    [Range(0, 100)] public float hunger;
    [Range(0, 100)] public float fatigue;
    [Range(0, 100)] public float social;
    [Range(0, 100)] public float moneyNeed;
    [Range(0, 100)] public float cultivationNeed;
    [Range(0, 100)] public float safetyNeed;
    [Range(0, 100)] public float resourceNeed;
    public float updateInterval = 5f;

    float timer;

    void Awake()
    {
        SyncFromExistingAi();
        timer = UnityEngine.Random.Range(0f, updateInterval);
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer > 0f)
        {
            return;
        }

        timer = updateInterval;
        TickNeeds();
    }

    void SyncFromExistingAi()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            hunger = villager.hunger;
            fatigue = villager.fatigue;
            social = Mathf.Clamp(100f - villager.fun, 0f, 100f);
            moneyNeed = Mathf.Clamp(80f - villager.money, 0f, 100f);
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            hunger = smartNpc.hunger;
            fatigue = smartNpc.fatigue;
            moneyNeed = Mathf.Clamp(120f - smartNpc.money, 0f, 100f);
            cultivationNeed = smartNpc.canCultivate ? 55f : 0f;
        }
    }

    void TickNeeds()
    {
        hunger = Mathf.Clamp(hunger + UnityEngine.Random.Range(0.5f, 2f), 0f, 100f);
        fatigue = Mathf.Clamp(fatigue + UnityEngine.Random.Range(0.2f, 1.5f), 0f, 100f);
        social = Mathf.Clamp(social + UnityEngine.Random.Range(0.5f, 2.5f), 0f, 100f);
        moneyNeed = Mathf.Clamp(moneyNeed + UnityEngine.Random.Range(-1f, 1.5f), 0f, 100f);
        cultivationNeed = Mathf.Clamp(cultivationNeed + UnityEngine.Random.Range(0f, 1.2f), 0f, 100f);
        resourceNeed = Mathf.Clamp(resourceNeed + UnityEngine.Random.Range(-0.5f, 1.2f), 0f, 100f);
        safetyNeed = Mathf.Clamp(safetyNeed - 0.5f, 0f, 100f);
    }
}

public class NpcPersonality : MonoBehaviour
{
    [Range(0, 100)] public int sociability = 50;
    [Range(0, 100)] public int greed = 30;
    [Range(0, 100)] public int aggression = 20;
    [Range(0, 100)] public int caution = 40;
    [Range(0, 100)] public int loyalty = 40;
    [Range(0, 100)] public int curiosity = 45;

    void Awake()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null)
        {
            sociability = villager.sociability;
            greed = villager.greed;
            aggression = Mathf.Clamp(100 - villager.bravery, 0, 100);
            caution = Mathf.Clamp(100 - villager.bravery + 20, 0, 100);
            return;
        }

        SmartNpcAI smartNpc = GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            sociability = smartNpc.canMakeFriends ? 55 : 15;
            greed = smartNpc.greed;
            aggression = smartNpc.canKillOthers ? Mathf.Clamp(smartNpc.bravery, 0, 100) : 5;
            caution = Mathf.Clamp(100 - smartNpc.bravery, 0, 100);
            loyalty = smartNpc.kindness;
        }
    }
}

public class NpcRelationshipGraph : MonoBehaviour
{
    public List<NpcSocialRelationship> relationships =
        new List<NpcSocialRelationship>();

    public NpcSocialRelationship Get(GameObject target)
    {
        NpcSocialIdentity identity = target != null
            ? target.GetComponent<NpcSocialIdentity>()
            : null;

        return identity != null ? Get(identity.socialId) : null;
    }

    public NpcSocialRelationship Get(string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            return null;
        }

        for (int i = 0; i < relationships.Count; i++)
        {
            if (relationships[i].targetId == targetId)
            {
                return relationships[i];
            }
        }

        NpcSocialRelationship relationship =
            new NpcSocialRelationship
            {
                targetId = targetId
            };

        relationships.Add(relationship);
        return relationship;
    }

    public void AddSocial(
        GameObject target,
        int affection,
        int trust,
        string topic)
    {
        NpcSocialRelationship relationship = Get(target);
        if (relationship == null)
        {
            return;
        }

        relationship.affection = Mathf.Clamp(relationship.affection + affection, -100, 100);
        relationship.trust = Mathf.Clamp(relationship.trust + trust, -100, 100);
        relationship.lastInteractionDay = NpcSocialTime.Day;
        relationship.lastTopic = topic;
    }

    public void AddTrade(GameObject target, int trustDelta)
    {
        NpcSocialRelationship relationship = Get(target);
        if (relationship == null)
        {
            return;
        }

        relationship.trust = Mathf.Clamp(relationship.trust + trustDelta, -100, 100);
        relationship.respect = Mathf.Clamp(relationship.respect + Mathf.Max(0, trustDelta), -100, 100);
        relationship.lastInteractionDay = NpcSocialTime.Day;
        relationship.lastTopic = "buôn bán";
    }

    public void AddHostility(GameObject target, int amount)
    {
        NpcSocialRelationship relationship = Get(target);
        if (relationship == null)
        {
            return;
        }

        relationship.grudge = Mathf.Clamp(relationship.grudge + amount, 0, 100);
        relationship.hostility = Mathf.Clamp(relationship.hostility + amount, 0, 100);
        relationship.trust = Mathf.Clamp(relationship.trust - amount, -100, 100);
        relationship.affection = Mathf.Clamp(relationship.affection - amount, -100, 100);
        relationship.lastInteractionDay = NpcSocialTime.Day;
        relationship.lastTopic = NpcText.Dialogue("hostilityReasonFallback");
    }
}

public class NpcMemory : MonoBehaviour
{
    public int maxMemories = 50;
    public float combatLineCooldown = 4f;
    public List<NpcMemoryRecord> memories =
        new List<NpcMemoryRecord>();

    float nextCombatLineTime;

    void OnEnable()
    {
        NpcSocialEventBus.TradeCompleted += HandleTradeCompleted;
        NpcSocialEventBus.RumorShared += HandleRumorShared;
        NpcSocialEventBus.HostilityDetailedHappened += HandleHostility;
    }

    void OnDisable()
    {
        NpcSocialEventBus.TradeCompleted -= HandleTradeCompleted;
        NpcSocialEventBus.RumorShared -= HandleRumorShared;
        NpcSocialEventBus.HostilityDetailedHappened -= HandleHostility;
    }

    public void Remember(
        NpcMemoryType type,
        GameObject subject,
        GameObject target,
        string topic,
        float importance,
        float confidence,
        int durationDays)
    {
        NpcSocialIdentity subjectIdentity = subject != null
            ? subject.GetComponent<NpcSocialIdentity>()
            : null;
        NpcSocialIdentity targetIdentity = target != null
            ? target.GetComponent<NpcSocialIdentity>()
            : null;

        memories.Add(
            new NpcMemoryRecord
            {
                type = type,
                subjectId = subjectIdentity != null ? subjectIdentity.socialId : "",
                targetId = targetIdentity != null ? targetIdentity.socialId : "",
                topic = topic,
                worldDay = NpcSocialTime.Day,
                worldHour = NpcSocialTime.Hour,
                importance = Mathf.Max(0f, importance),
                confidence = Mathf.Clamp01(confidence),
                expiryDay = durationDays > 0 ? NpcSocialTime.Day + durationDays : -1
            });

        Prune();
    }

    public NpcMemoryRecord FindUnresolvedTopicWith(GameObject other)
    {
        NpcSocialIdentity identity = other != null
            ? other.GetComponent<NpcSocialIdentity>()
            : null;

        if (identity == null)
        {
            return null;
        }

        for (int i = memories.Count - 1; i >= 0; i--)
        {
            NpcMemoryRecord memory = memories[i];
            if (memory == null ||
                memory.resolved ||
                string.IsNullOrEmpty(memory.topic))
            {
                continue;
            }

            if (memory.subjectId == identity.socialId ||
                memory.targetId == identity.socialId)
            {
                return memory;
            }
        }

        return null;
    }

    public bool HasRecentTopic(string otherId, string topic, int withinDays)
    {
        if (string.IsNullOrEmpty(topic))
        {
            return false;
        }

        int minDay = NpcSocialTime.Day - Mathf.Max(0, withinDays);
        for (int i = memories.Count - 1; i >= 0; i--)
        {
            NpcMemoryRecord memory = memories[i];
            if (memory == null ||
                memory.worldDay < minDay)
            {
                continue;
            }

            if (memory.topic == topic &&
                (memory.subjectId == otherId || memory.targetId == otherId))
            {
                return true;
            }
        }

        return false;
    }

    public void Prune()
    {
        for (int i = memories.Count - 1; i >= 0; i--)
        {
            NpcMemoryRecord memory = memories[i];
            if (memory == null ||
                (memory.expiryDay >= 0 && memory.expiryDay < NpcSocialTime.Day))
            {
                memories.RemoveAt(i);
            }
        }

        memories.Sort(
            (a, b) =>
                b.importance.CompareTo(a.importance));

        while (memories.Count > maxMemories)
        {
            memories.RemoveAt(memories.Count - 1);
        }
    }

    void HandleTradeCompleted(
        GameObject buyer,
        GameObject seller,
        StatItemData item,
        int price)
    {
        if (buyer != gameObject && seller != gameObject)
        {
            return;
        }

        GameObject other = buyer == gameObject ? seller : buyer;
        string itemName = GetItemDisplayName(item);
        Remember(
            NpcMemoryType.TradeCompleted,
            other,
            gameObject,
            NpcText.DialogueFormat("tradeMemoryTopic", itemName, price),
            2f,
            1f,
            12);

        NpcRelationshipGraph relationshipGraph = GetComponent<NpcRelationshipGraph>();
        if (relationshipGraph != null)
        {
            relationshipGraph.AddTrade(other, 2);
        }

        ShowTradeOverhead(buyer, itemName, price);
    }

    void ShowTradeOverhead(GameObject buyer, string itemName, int price)
    {
        if (NpcRoleUtility.IsInCombat(gameObject))
        {
            return;
        }

        bool isBuyer = buyer == gameObject;
        string lineKey = isBuyer ? "tradeBuyerLines" : "tradeSellerLines";
        string fallbackKey = isBuyer ? "tradeBuyerFallback" : "tradeSellerFallback";
        string template = NpcText.DialogueLine(
            lineKey,
            NpcText.Dialogue(fallbackKey, ""));
        string line = NpcText.Format(template, itemName, price);

        ShowOverheadLine(line, 2.8f, 1);
    }

    void ShowOverheadLine(string line, float duration, int priority = 0)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        NpcOverheadDialogueUI overhead = GetComponent<NpcOverheadDialogueUI>();
        if (overhead == null)
        {
            overhead = gameObject.AddComponent<NpcOverheadDialogueUI>();
        }

        overhead.ShowLine(line, duration, priority);
    }

    string GetItemDisplayName(StatItemData item)
    {
        if (item != null)
        {
            return ItemText.Name(item);
        }

        return NpcText.Label("item");
    }

    void HandleRumorShared(GameObject speaker, GameObject listener, string topic)
    {
        if (speaker != gameObject && listener != gameObject)
        {
            return;
        }

        GameObject other = speaker == gameObject ? listener : speaker;
        Remember(
            NpcMemoryType.ResourceRumor,
            other,
            gameObject,
            topic,
            3f,
            0.75f,
            8);
    }

    void HandleHostility(
        GameObject actor,
        GameObject target,
        int severity,
        Vector3 position,
        string reason)
    {
        if (actor != gameObject && target != gameObject)
        {
            return;
        }

        GameObject other = actor == gameObject ? target : actor;
        Remember(
            NpcMemoryType.Attack,
            other,
            gameObject,
            string.IsNullOrEmpty(reason)
                ? NpcText.Dialogue("hostilityReasonFallback")
                : reason,
            severity * 0.1f,
            1f,
            60);

        ShowCombatOverhead(actor, target);
    }

    void ShowCombatOverhead(GameObject actor, GameObject target)
    {
        if (Time.time < nextCombatLineTime)
        {
            return;
        }

        bool isActor = actor == gameObject;
        bool lowHealth = !isActor && GetHealthRatio(gameObject) <= 0.35f;
        bool bulliedByStronger = !isActor && IsBulliedByStronger(actor, gameObject);
        string lineKey = bulliedByStronger
            ? "bulliedLowRealmLines"
            : lowHealth
            ? "combatLowHealthLines"
            : isActor
                ? "combatAttackLines"
                : "combatDefendLines";
        string fallbackKey = bulliedByStronger
            ? "combatDefendFallback"
            : lowHealth
            ? "combatLowHealthFallback"
            : isActor
                ? "combatAttackFallback"
                : "combatDefendFallback";
        string line = NpcText.DialogueLine(
            lineKey,
            NpcText.Dialogue(fallbackKey, ""));

        if (bulliedByStronger)
        {
            line = NpcText.Format(
                line,
                NpcRoleUtility.GetDisplayName(gameObject),
                NpcRoleUtility.GetDisplayName(actor));
        }

        ShowOverheadLine(line, 3f, 4);
        nextCombatLineTime = Time.time + Mathf.Max(0.5f, combatLineCooldown);
    }

    bool IsBulliedByStronger(GameObject actor, GameObject target)
    {
        if (actor == null || target == null)
        {
            return false;
        }

        int actorPower = NpcRoleUtility.GetRealmPower(actor);
        int targetPower = NpcRoleUtility.GetRealmPower(target);
        if (actorPower <= 0 || targetPower <= 0)
        {
            return false;
        }

        return actorPower >= targetPower + 2 ||
            actorPower >= targetPower * 2;
    }

    float GetHealthRatio(GameObject target)
    {
        if (target == null)
        {
            return 1f;
        }

        CharacterStats stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return stats.finalHP > 0
                ? Mathf.Clamp01(stats.currentHP / (float)stats.finalHP)
                : 0f;
        }

        VillagerAI villager = target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.maxHP > 0
                ? Mathf.Clamp01(villager.currentHP / (float)villager.maxHP)
                : 0f;
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.maxHP > 0
                ? Mathf.Clamp01(smartNpc.currentHP / (float)smartNpc.maxHP)
                : 0f;
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.maxHP > 0
                ? Mathf.Clamp01(monster.currentHP / (float)monster.maxHP)
                : 0f;
        }

        return 1f;
    }
}

public class NpcOverheadDialogueUI : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 1.25f, 0f);
    public float defaultDuration = 3f;
    public int sortingOrder = 50;
    public float fontSize = 3.4f;
    public Vector3 worldTextScale = Vector3.one;
    public Color textColor = Color.white;
    public Color outlineColor = Color.black;
    public float outlineWidth = 0.2f;

    TextMeshPro text;
    float hideAt;
    int activePriority = int.MinValue;

    void Awake()
    {
        EnsureText();
        Hide();
    }

    void LateUpdate()
    {
        if (text == null)
        {
            return;
        }

        text.transform.position = transform.position + offset;

        Camera camera = Camera.main;
        if (camera != null)
        {
            text.transform.rotation = camera.transform.rotation;
        }

        NormalizeTextTransform();

        if (text.gameObject.activeSelf && Time.time >= hideAt)
        {
            Hide();
        }
    }

    public void ShowLine(string line)
    {
        ShowLine(line, defaultDuration);
    }

    public void ShowLine(string line, float duration)
    {
        ShowLine(line, duration, 0);
    }

    public void ShowLine(string line, float duration, int priority)
    {
        line = NpcText.CleanDisplayText(line);

        if (string.IsNullOrEmpty(line))
        {
            if (CanReplace(priority))
            {
                Hide();
            }
            return;
        }

        if (!CanReplace(priority))
        {
            return;
        }

        EnsureText();
        ApplyTextStyle();
        text.text = line;
        text.gameObject.SetActive(true);
        hideAt = Time.time + Mathf.Max(0.2f, duration);
        activePriority = priority;
    }

    public void Hide()
    {
        if (text != null)
        {
            text.gameObject.SetActive(false);
        }

        activePriority = int.MinValue;
    }

    bool CanReplace(int priority)
    {
        return text == null ||
            !text.gameObject.activeSelf ||
            Time.time >= hideAt ||
            priority >= activePriority;
    }

    void EnsureText()
    {
        if (text != null)
        {
            return;
        }

        GameObject textObject = new GameObject("OverheadDialogue");
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(transform, false);
        text = textObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        ApplyTextStyle();
        text.textWrappingMode = TMPro.TextWrappingModes.Normal;
        text.rectTransform.sizeDelta = new Vector2(5.6f, 1.8f);
        NormalizeTextTransform();

        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerID = GetDialogueSortingLayerId();
            renderer.sortingOrder = GetDialogueSortingOrder();
        }
    }

    int GetDialogueSortingLayerId()
    {
        string[] preferredLayers =
        {
            "UI",
            "Foreground",
            "Characters"
        };

        foreach (string layerName in preferredLayers)
        {
            int layerId = SortingLayer.NameToID(layerName);
            if (layerId != 0 || layerName == "Default")
            {
                return layerId;
            }
        }

        return SortingLayer.NameToID("Default");
    }

    int GetDialogueSortingOrder()
    {
        int order = sortingOrder;
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                order = Mathf.Max(order, sprites[i].sortingOrder + 20);
            }
        }

        return order;
    }

    void ApplyTextStyle()
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = fontSize;
        text.color = textColor;
        text.outlineColor = outlineColor;
        text.outlineWidth = outlineWidth;
        NormalizeTextTransform();
    }

    void NormalizeTextTransform()
    {
        if (text == null)
        {
            return;
        }

        Vector3 parentScale = transform.lossyScale;
        text.transform.localScale = new Vector3(
            SafeInverse(parentScale.x) * worldTextScale.x,
            SafeInverse(parentScale.y) * worldTextScale.y,
            SafeInverse(parentScale.z) * worldTextScale.z);
    }

    float SafeInverse(float value)
    {
        return Mathf.Abs(value) <= 0.0001f
            ? 1f
            : 1f / value;
    }
}

public class NpcConversationSession
{
    public NpcConversationAgent first;
    public NpcConversationAgent second;
    public string topic;
    public float endTime;
    public float lockStrength;
    public bool canBecomeGroup;

    public bool Contains(NpcConversationAgent agent)
    {
        return agent != null && (agent == first || agent == second);
    }
}

public class NpcConversationAgent : MonoBehaviour
{
    static readonly List<NpcConversationSession> sessions =
        new List<NpcConversationSession>();
    public float scanRadius = 1.4f;
    public LayerMask npcLayers = ~0;
    public float scanInterval = 2f;
    public float conversationCooldown = 25f;
    public float conversationDuration = 2.5f;
    public float conversationBreakDistance = 2.1f;
    public bool allowFirstMeetingConversation = true;
    public bool requireFriendlyRelationship = true;
    public bool allowNeutralSmallTalk = false;
    public float minRelationshipToTalk = 8f;
    public float hostileConversationBlock = 60f;
    public float minConversationScore = 38f;
    [Range(0f, 1f)] public float firstMeetingTalkChance = 0.15f;
    [Range(0f, 1f)] public float weatherTalkChance = 0.35f;
    [Range(0f, 1f)] public float needsTalkChance = 0.30f;
    [Range(0f, 1f)] public float namedLongTalkChance = 0.12f;
    [Range(0f, 1f)] public float monsterHuntTalkChance = 0.22f;
    public float nearbyMonsterTalkRadius = 8f;
    public int strongMonsterTalkLevel = 2;
    public bool respectSocialContext = true;
    public bool allowNightConversation;
    public bool allowDangerZoneConversation;
    public float interruptThreshold = 80f;
    public float defaultLockStrength = 45f;
    public bool allowGroupConversation = true;

    NpcSocialIdentity identity;
    NpcMemory memory;
    NpcRelationshipGraph relationships;
    NpcOverheadDialogueUI overhead;
    NpcPersonality personality;
    float scanTimer;
    float nextConversationTime;
    NpcConversationSession activeSession;

    public bool IsBusyTalking =>
        activeSession != null &&
        Time.time < activeSession.endTime;

    void Awake()
    {
        EnsureReferences();
        DisableLegacyConversation();
        scanTimer = UnityEngine.Random.Range(0f, scanInterval);
    }

    void Update()
    {
        CleanupSessions();

        if (activeSession != null)
        {
            if (Time.time >= activeSession.endTime ||
                IsConversationBroken(activeSession))
            {
                EndConversation(activeSession);
            }
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f)
        {
            return;
        }

        scanTimer = scanInterval + UnityEngine.Random.Range(0f, 1.5f);
        TryFindConversation();
    }

    public bool TryStartConversation(NpcConversationAgent other, bool force)
    {
        EnsureReferences();

        if (other == null ||
            other == this ||
            NpcRoleUtility.IsDead(gameObject) ||
            NpcRoleUtility.IsDead(other.gameObject) ||
            NpcRoleUtility.IsInCombat(gameObject) ||
            NpcRoleUtility.IsInCombat(other.gameObject))
        {
            return false;
        }

        if (!force &&
            (!NpcScheduleController.AllowsSocial(gameObject) ||
            !NpcScheduleController.AllowsSocial(other.gameObject)))
        {
            return false;
        }

        other.EnsureReferences();

        bool firstMeeting = IsFirstMeetingWith(other);
        if (!force &&
            firstMeeting &&
            (!allowFirstMeetingConversation ||
                UnityEngine.Random.value > firstMeetingTalkChance))
        {
            return false;
        }

        if (!force &&
            (Time.time < nextConversationTime ||
            Time.time < other.nextConversationTime ||
            !IsSocialContextAllowed() ||
            !other.IsSocialContextAllowed() ||
            !CanSocializeWith(other) ||
            !other.CanSocializeWith(this)))
        {
            return false;
        }

        if (IsBusyTalking && !CanInterrupt(other))
        {
            return false;
        }

        if (other.IsBusyTalking && !other.CanInterrupt(this))
        {
            return false;
        }

        string topic = PickTopic(other);
        string myLine = BuildLineFor(other, topic, true);
        string otherLine = other.BuildLineFor(this, topic, false);

        NpcConversationSession session =
            new NpcConversationSession
            {
                first = this,
                second = other,
                topic = topic,
                endTime = Time.time + conversationDuration,
                lockStrength = defaultLockStrength,
                canBecomeGroup = allowGroupConversation && other.allowGroupConversation
            };

        activeSession = session;
        other.activeSession = session;
        sessions.Add(session);

        nextConversationTime = Time.time + conversationCooldown;
        other.nextConversationTime = Time.time + other.conversationCooldown;

        NpcRoleUtility.StopForConversation(gameObject);
        NpcRoleUtility.StopForConversation(other.gameObject);

        overhead.ShowLine(myLine, conversationDuration);
        other.overhead.ShowLine(otherLine, conversationDuration);

        relationships.AddSocial(other.gameObject, 1, 1, topic);
        other.relationships.AddSocial(gameObject, 1, 1, topic);

        memory.Remember(
            NpcMemoryType.Conversation,
            gameObject,
            other.gameObject,
            topic,
            1f,
            1f,
            5);

        other.memory.Remember(
            NpcMemoryType.Conversation,
            other.gameObject,
            gameObject,
            topic,
            1f,
            1f,
            5);

        if (ContainsJsonKeyword(topic, "spiritHerb") ||
            ContainsJsonKeyword(topic, "spiritMedicine") ||
            ContainsJsonKeyword(topic, "stream"))
        {
            NpcSocialEventBus.PublishRumorShared(gameObject, other.gameObject, topic);
        }

        NpcRoleUtility.SetAction(gameObject, NpcText.Action("talking"));
        NpcRoleUtility.SetAction(other.gameObject, NpcText.Action("talking"));
        return true;
    }

    bool IsFirstMeetingWith(NpcConversationAgent other)
    {
        if (other == null || relationships == null)
        {
            return false;
        }

        NpcSocialRelationship relation = relationships.Get(other.gameObject);
        return relation != null && relation.lastInteractionDay < 0;
    }

    bool IsConversationBroken(NpcConversationSession session)
    {
        if (session == null || session.first == null || session.second == null)
        {
            return true;
        }

        if (Vector2.Distance(session.first.transform.position, session.second.transform.position) >
            Mathf.Max(0.5f, conversationBreakDistance))
        {
            return true;
        }

        return NpcRoleUtility.IsDead(session.first.gameObject) ||
            NpcRoleUtility.IsDead(session.second.gameObject);
    }

    void EndConversation(NpcConversationSession session)
    {
        if (session == null)
        {
            activeSession = null;
            return;
        }

        session.endTime = Time.time;

        if (activeSession == session)
        {
            activeSession = null;
        }

        if (session.first != null && session.first.activeSession == session)
        {
            session.first.activeSession = null;
            NpcRoleUtility.SetAction(session.first.gameObject, NpcText.Action("idle"));
        }

        if (session.second != null && session.second.activeSession == session)
        {
            session.second.activeSession = null;
            NpcRoleUtility.SetAction(session.second.gameObject, NpcText.Action("idle"));
        }
    }

    public bool CanInterrupt(NpcConversationAgent interrupter)
    {
        if (interrupter == null || activeSession == null)
        {
            return true;
        }

        NpcSocialRelationship relation = relationships.Get(interrupter.gameObject);
        float relationshipUrgency = 0f;
        if (relation != null)
        {
            relationshipUrgency =
                Mathf.Max(relation.grudge, relation.hostility) +
                Mathf.Max(relation.affection, relation.alliance) * 0.8f;
        }

        NpcPersonality interrupterPersonality =
            interrupter.GetComponent<NpcPersonality>();

        float courage =
            interrupterPersonality != null
            ? interrupterPersonality.aggression * 0.4f
            : 10f;

        float score =
            relationshipUrgency +
            courage -
            activeSession.lockStrength;

        return score >= interruptThreshold;
    }

    bool CanSocializeWith(NpcConversationAgent other)
    {
        if (other == null)
        {
            return false;
        }

        NpcSocialRelationship relation = relationships.Get(other.gameObject);
        if (relation == null)
        {
            return false;
        }

        if (IsHostileBlocked(relation))
        {
            return false;
        }

        if (relation.lastInteractionDay < 0)
        {
            return allowFirstMeetingConversation;
        }

        if (!requireFriendlyRelationship)
        {
            return true;
        }

        if (Mathf.Max(relation.affection, relation.alliance) >=
            minRelationshipToTalk)
        {
            return true;
        }

        if (allowNeutralSmallTalk)
        {
            return relation.affection > -35 &&
                relation.trust > -35;
        }

        return relation.affection >= 0 &&
            relation.trust >= 0;
    }

    bool IsSocialContextAllowed()
    {
        if (!respectSocialContext)
        {
            return true;
        }

        if (!allowNightConversation && IsQuietTime())
        {
            return false;
        }

        if (!allowDangerZoneConversation && IsDangerZone())
        {
            return false;
        }

        return true;
    }

    bool IsQuietTime()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return false;
        }

        return timeSystem.CurrentPhase == WorldTimePhase.Night ||
            timeSystem.CurrentPhase == WorldTimePhase.Dawn;
    }

    bool IsDangerZone()
    {
        NpcMapArea area = NpcMapArea.FindArea(transform.position);
        return area != null && area.zone == NpcMapZone.MaThuSonMach;
    }

    public float GetConversationScore(NpcConversationAgent other)
    {
        if (other == null ||
            Time.time < nextConversationTime ||
            IsBusyTalking ||
            NpcRoleUtility.IsInCombat(gameObject) ||
            NpcRoleUtility.IsInCombat(other.gameObject) ||
            !NpcScheduleController.AllowsSocial(gameObject) ||
            !NpcScheduleController.AllowsSocial(other.gameObject) ||
            !IsSocialContextAllowed() ||
            !CanSocializeWith(other))
        {
            return 0f;
        }

        NpcSocialRelationship relation = relationships.Get(other.gameObject);
        float affection = relation != null ? relation.affection : 0f;
        float grudge = relation != null ? relation.grudge : 0f;
        float socialNeed = GetComponent<NpcNeeds>() != null
            ? GetComponent<NpcNeeds>().social
            : 40f;

        return socialNeed +
            personality.sociability * 0.5f +
            personality.curiosity * 0.15f +
            Mathf.Abs(affection) * 0.2f +
            grudge * 0.3f;
    }

    void TryFindConversation()
    {
        if (IsBusyTalking ||
            Time.time < nextConversationTime)
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(transform.position, scanRadius, npcLayers);

        NpcConversationAgent best = null;
        float bestScore = 0f;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.gameObject == gameObject)
            {
                continue;
            }

            NpcConversationAgent other =
                hit.GetComponentInParent<NpcConversationAgent>();

            if (other == null ||
                other == this)
            {
                continue;
            }

            float score = GetConversationScore(other);
            if (other.IsBusyTalking && !other.CanInterrupt(this))
            {
                score = 0f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        if (best != null && bestScore >= minConversationScore)
        {
            TryStartConversation(best, false);
        }
    }

    string PickTopic(NpcConversationAgent other)
    {
        NpcMemoryRecord unresolved = memory.FindUnresolvedTopicWith(other.gameObject);
        if (unresolved != null && !string.IsNullOrEmpty(unresolved.topic))
        {
            unresolved.resolved = true;
            return unresolved.topic;
        }

        NpcSocialRelationship relation = relationships.Get(other.gameObject);
        if (relation != null &&
            IsHostileBlocked(relation))
        {
            return Pick(GetHostileLines(), NpcText.DialogueLine("hostileFallback"));
        }

        string contextTopic = PickContextTopic(relation, other);
        if (!string.IsNullOrEmpty(contextTopic))
        {
            return contextTopic;
        }

        if (UnityEngine.Random.value < 0.35f)
        {
            return Pick(GetRumorLines(), NpcText.DialogueLine("rumorFallback"));
        }

        return Pick(GetGreetingLines(), NpcText.DialogueLine("genericGreeting"));
    }

    string BuildLineFor(
        NpcConversationAgent other,
        string topic,
        bool opener)
    {
        NpcMemoryRecord unresolved = memory.FindUnresolvedTopicWith(other.gameObject);
        if (unresolved != null &&
            !string.IsNullOrEmpty(unresolved.topic) &&
            NpcSocialTime.Day > unresolved.worldDay)
        {
            return NpcText.Format(NpcText.Dialogue("followUpTopic"), unresolved.topic);
        }

        if (opener)
        {
            return topic;
        }

        string contextReply = PickContextReply(other);
        if (!string.IsNullOrEmpty(contextReply))
        {
            return contextReply;
        }

        return Pick(GetReplyLines(), NpcText.DialogueLine("replyFallback"));
    }

    string PickContextTopic(
        NpcSocialRelationship relation,
        NpcConversationAgent other)
    {
        if (relation != null &&
            relation.lastInteractionDay < 0 &&
            UnityEngine.Random.value < firstMeetingTalkChance)
        {
            string namedFirstMeeting = PickNamedLine(
                "namedFirstMeetingLines",
                other,
                "");
            if (!string.IsNullOrEmpty(namedFirstMeeting))
            {
                return namedFirstMeeting;
            }

            return Pick(GetJsonLines("firstMeetingLines"), NpcText.DialogueLine("genericGreeting"));
        }

        string monsterTopic = PickMonsterHuntTopic(other);
        if (!string.IsNullOrEmpty(monsterTopic))
        {
            return monsterTopic;
        }

        if (UnityEngine.Random.value < namedLongTalkChance)
        {
            string longTopic = PickNamedLine("longPersonalLines", other, "");
            if (!string.IsNullOrEmpty(longTopic))
            {
                return longTopic;
            }
        }

        string weatherTopic = PickWeatherLine(false);
        if (!string.IsNullOrEmpty(weatherTopic))
        {
            return weatherTopic;
        }

        string needTopic = PickNeedsLine(false);
        if (!string.IsNullOrEmpty(needTopic))
        {
            return needTopic;
        }

        if (relation != null &&
            relation.affection >= 25 &&
            UnityEngine.Random.value < 0.35f)
        {
            string namedFollowUp = PickNamedLine(
                "namedFriendlyFollowUps",
                other,
                "");
            if (!string.IsNullOrEmpty(namedFollowUp))
            {
                return namedFollowUp;
            }

            return Pick(GetJsonLines("friendlyFollowUps"), NpcText.DialogueLine("genericGreeting"));
        }

        if (allowNeutralSmallTalk && UnityEngine.Random.value < 0.25f)
        {
            return Pick(GetJsonLines("smallTalkNeutralLines"), NpcText.DialogueLine("genericGreeting"));
        }

        return "";
    }

    string PickContextReply(NpcConversationAgent other)
    {
        NpcSocialRelationship relation = relationships.Get(other.gameObject);
        if (relation != null &&
            relation.lastInteractionDay < 0 &&
            UnityEngine.Random.value < 0.7f)
        {
            string namedFirstReply = PickNamedLine(
                "namedFirstMeetingReplies",
                other,
                "");
            if (!string.IsNullOrEmpty(namedFirstReply))
            {
                return namedFirstReply;
            }

            return Pick(GetJsonLines("firstMeetingReplies"), NpcText.DialogueLine("replyFallback"));
        }

        string monsterReply = PickMonsterHuntReply(other);
        if (!string.IsNullOrEmpty(monsterReply))
        {
            return monsterReply;
        }

        if (UnityEngine.Random.value < namedLongTalkChance)
        {
            string longReply = PickNamedLine("longPersonalReplies", other, "");
            if (!string.IsNullOrEmpty(longReply))
            {
                return longReply;
            }
        }

        string weatherReply = PickWeatherLine(true);
        if (!string.IsNullOrEmpty(weatherReply))
        {
            return weatherReply;
        }

        string needReply = PickNeedsLine(true);
        if (!string.IsNullOrEmpty(needReply))
        {
            return needReply;
        }

        return "";
    }

    string PickMonsterHuntTopic(NpcConversationAgent other)
    {
        if (UnityEngine.Random.value >= monsterHuntTalkChance)
        {
            return "";
        }

        MonsterAI monster = FindNearbyStrongMonster();
        if (monster == null)
        {
            return "";
        }

        return PickMonsterLine("monsterHuntInvites", other, monster);
    }

    string PickMonsterHuntReply(NpcConversationAgent other)
    {
        if (UnityEngine.Random.value >= monsterHuntTalkChance)
        {
            return "";
        }

        MonsterAI monster = FindNearbyStrongMonster();
        if (monster == null)
        {
            return "";
        }

        return PickMonsterLine("monsterHuntReplies", other, monster);
    }

    MonsterAI FindNearbyStrongMonster()
    {
        MonsterAI[] monsters =
            FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude);

        MonsterAI best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterAI monster = monsters[i];
            if (monster == null ||
                monster.currentHP <= 0 ||
                monster.beastLevel < strongMonsterTalkLevel)
            {
                continue;
            }

            float distance =
                Vector2.Distance(transform.position, monster.transform.position);

            if (distance > nearbyMonsterTalkRadius ||
                distance >= bestDistance)
            {
                continue;
            }

            best = monster;
            bestDistance = distance;
        }

        return best;
    }

    string PickMonsterLine(
        string key,
        NpcConversationAgent other,
        MonsterAI monster)
    {
        if (monster == null)
        {
            return "";
        }

        string template = Pick(GetJsonLines(key), "");
        if (string.IsNullOrEmpty(template))
        {
            return "";
        }

        string monsterName = !string.IsNullOrWhiteSpace(monster.monsterName)
            ? monster.monsterName
            : NpcText.Get("entityTypes", "monster");

        return NpcText.Format(
            template,
            GetSelfName(),
            GetOtherName(other),
            monsterName,
            Mathf.Max(1, monster.beastLevel));
    }

    string PickNamedLine(
        string key,
        NpcConversationAgent other,
        string fallback)
    {
        string template = Pick(GetJsonLines(key), fallback);
        if (string.IsNullOrEmpty(template))
        {
            return "";
        }

        return NpcText.Format(template, GetSelfName(), GetOtherName(other));
    }

    string PickWeatherLine(bool reply)
    {
        if (UnityEngine.Random.value >= weatherTalkChance)
        {
            return "";
        }

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather == null)
        {
            return "";
        }

        switch (weather.CurrentWeather)
        {
            case WorldWeather.Clear:
                if (IsQuietTime())
                {
                    return "";
                }
                return Pick(GetJsonLines(reply ? "weatherClearReplies" : "weatherClearLines"), "");
            case WorldWeather.Rain:
                return Pick(GetJsonLines(reply ? "weatherRainReplies" : "weatherRainLines"), "");
            case WorldWeather.Thunder:
                return Pick(GetJsonLines(reply ? "weatherThunderReplies" : "weatherThunderLines"), "");
            case WorldWeather.Snow:
                return Pick(GetJsonLines(reply ? "weatherSnowReplies" : "weatherSnowLines"), "");
            case WorldWeather.DenseSpiritualQi:
                return Pick(GetJsonLines(reply ? "weatherQiReplies" : "weatherQiLines"), "");
            default:
                return "";
        }
    }

    string PickNeedsLine(bool reply)
    {
        if (UnityEngine.Random.value >= needsTalkChance)
        {
            return "";
        }

        NpcNeeds needs = GetComponent<NpcNeeds>();
        if (needs == null)
        {
            return "";
        }

        if (needs.hunger >= 70f)
        {
            return Pick(GetJsonLines(reply ? "hungryReplies" : "hungryLines"), "");
        }

        if (needs.fatigue >= 70f)
        {
            return Pick(GetJsonLines(reply ? "tiredReplies" : "tiredLines"), "");
        }

        if (needs.resourceNeed >= 70f)
        {
            return Pick(GetJsonLines(reply ? "resourceNeedReplies" : "resourceNeedLines"), "");
        }

        if (needs.cultivationNeed >= 70f)
        {
            return Pick(GetJsonLines(reply ? "cultivationNeedReplies" : "cultivationNeedLines"), "");
        }

        return "";
    }

    bool IsHostileBlocked(NpcSocialRelationship relation)
    {
        return relation != null &&
            Mathf.Max(relation.grudge, relation.hostility) >= hostileConversationBlock;
    }
    string[] GetJsonLines(string key)
    {
        string[] lines = NpcText.Lines("dialogue", key);
        return lines != null && lines.Length > 0 ? lines : null;
    }

    bool ContainsJsonKeyword(string text, string key)
    {
        string keyword = NpcText.Get("dialogueKeywords", key, "");
        return !string.IsNullOrEmpty(text) &&
            !string.IsNullOrEmpty(keyword) &&
            text.Contains(keyword);
    }

    string[] GetGreetingLines()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null && villager.job == VillagerJob.Farmer)
        {
            string[] farmerLines = GetJsonLines("farmerGreetings");
            if (farmerLines != null)
            {
                return farmerLines;
            }
        }

        NpcTradeAgent trader = GetComponent<NpcTradeAgent>();
        if (trader != null && trader.isMarketTrader)
        {
            string[] traderLines = GetJsonLines("traderGreetings");
            if (traderLines != null)
            {
                return traderLines;
            }
        }

        return GetJsonLines("greetings");
    }

    string[] GetReplyLines()
    {
        VillagerAI villager = GetComponent<VillagerAI>();
        if (villager != null && villager.job == VillagerJob.Farmer)
        {
            string[] farmerLines = GetJsonLines("farmerReplies");
            if (farmerLines != null)
            {
                return farmerLines;
            }
        }

        NpcTradeAgent trader = GetComponent<NpcTradeAgent>();
        if (trader != null && trader.isMarketTrader)
        {
            string[] traderLines = GetJsonLines("traderReplies");
            if (traderLines != null)
            {
                return traderLines;
            }
        }

        return GetJsonLines("replies");
    }

    string[] GetRumorLines()
    {
        return GetJsonLines("resourceRumors");
    }

    string[] GetHostileLines()
    {
        return GetJsonLines("hostileLines");
    }

    string GetSelfName()
    {
        return NpcRoleUtility.GetDisplayName(gameObject);
    }

    string GetOtherName(NpcConversationAgent other)
    {
        return other != null
            ? NpcRoleUtility.GetDisplayName(other.gameObject)
            : NpcText.Get("entityTypes", "npc");
    }

    string Pick(string[] lines, string fallback)
    {
        if (lines == null || lines.Length == 0)
        {
            return fallback;
        }

        return lines[UnityEngine.Random.Range(0, lines.Length)];
    }

    void EnsureReferences()
    {
        identity = GetComponent<NpcSocialIdentity>();
        if (identity == null)
        {
            identity = gameObject.AddComponent<NpcSocialIdentity>();
        }

        identity.Refresh();

        memory = GetComponent<NpcMemory>();
        if (memory == null)
        {
            memory = gameObject.AddComponent<NpcMemory>();
        }

        relationships = GetComponent<NpcRelationshipGraph>();
        if (relationships == null)
        {
            relationships = gameObject.AddComponent<NpcRelationshipGraph>();
        }

        overhead = GetComponent<NpcOverheadDialogueUI>();
        if (overhead == null)
        {
            overhead = gameObject.AddComponent<NpcOverheadDialogueUI>();
        }

        personality = GetComponent<NpcPersonality>();
        if (personality == null)
        {
            personality = gameObject.AddComponent<NpcPersonality>();
        }
    }

    void DisableLegacyConversation()
    {
        DailyConversation legacy = GetComponent<DailyConversation>();
        if (legacy != null)
        {
            legacy.enabled = false;
        }
    }

    static void CleanupSessions()
    {
        for (int i = sessions.Count - 1; i >= 0; i--)
        {
            if (sessions[i] == null || Time.time >= sessions[i].endTime)
            {
                sessions.RemoveAt(i);
            }
        }
    }
}

public class NpcDecisionBrain : MonoBehaviour
{
    public bool enabledDecisionBrain = true;
    public float thinkIntervalMin = 3f;
    public float thinkIntervalMax = 10f;
    public float nearbyRadius = 4f;
    public LayerMask npcLayers = ~0;
    public bool allowViolence;
    public bool allowRobbery;
    public NpcDecisionKind currentDecision;

    NpcNeeds needs;
    NpcPersonality personality;
    NpcRelationshipGraph relationships;
    NpcConversationAgent conversation;
    float nextThinkTime;

    void Awake()
    {
        needs = GetComponent<NpcNeeds>();
        if (needs == null)
        {
            needs = gameObject.AddComponent<NpcNeeds>();
        }

        personality = GetComponent<NpcPersonality>();
        if (personality == null)
        {
            personality = gameObject.AddComponent<NpcPersonality>();
        }

        relationships = GetComponent<NpcRelationshipGraph>();
        if (relationships == null)
        {
            relationships = gameObject.AddComponent<NpcRelationshipGraph>();
        }

        conversation = GetComponent<NpcConversationAgent>();
        if (conversation == null)
        {
            conversation = gameObject.AddComponent<NpcConversationAgent>();
        }

        ScheduleNextThink();
    }

    void Update()
    {
        if (!enabledDecisionBrain ||
            HasAuthoritativeSchedule() ||
            Time.time < nextThinkTime ||
            NpcRoleUtility.IsDead(gameObject))
        {
            return;
        }

        ScheduleNextThink();
        Think();
    }

    void Think()
    {
        NpcDecisionKind decision = PickBestDecision();
        currentDecision = decision;

        if (decision == NpcDecisionKind.Socialize)
        {
            TryPromptConversation();
            return;
        }

        if (decision == NpcDecisionKind.Revenge && allowViolence)
        {
            TryHostileAction(false);
            return;
        }

        if (decision == NpcDecisionKind.Rob && allowRobbery)
        {
            TryHostileAction(true);
            return;
        }

        ApplyActionText(decision);
    }

    bool HasAuthoritativeSchedule()
    {
        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(gameObject);

        return schedule != null &&
            schedule.enforceSchedule &&
            (GetComponent<VillagerAI>() != null ||
            GetComponent<SmartNpcAI>() != null);
    }

    NpcDecisionKind PickBestDecision()
    {
        float socialScore = needs.social + personality.sociability * 0.4f;
        float tradeScore = needs.moneyNeed + personality.greed * 0.35f;
        float resourceScore = needs.resourceNeed + personality.curiosity * 0.25f;
        float restScore = needs.fatigue;
        float revengeScore = GetHighestGrudgeNearby() + personality.aggression * 0.4f;
        float robberyScore = needs.moneyNeed + personality.greed * 0.6f - personality.caution * 0.5f;

        NpcDecisionKind best = NpcDecisionKind.Work;
        float bestScore = 35f;

        if (NpcScheduleController.AllowsSocial(gameObject))
        {
            Consider(NpcDecisionKind.Socialize, socialScore, ref best, ref bestScore);
        }

        if (NpcScheduleController.AllowsTrade(gameObject))
        {
            Consider(NpcDecisionKind.Trade, tradeScore, ref best, ref bestScore);
        }

        if (NpcScheduleController.AllowsGather(gameObject))
        {
            Consider(NpcDecisionKind.GatherResource, resourceScore, ref best, ref bestScore);
        }

        if (NpcScheduleController.AllowsActivity(gameObject, NpcScheduleActivity.Sleep) ||
            NpcScheduleController.AllowsActivity(gameObject, NpcScheduleActivity.ReturnHome))
        {
            Consider(NpcDecisionKind.Rest, restScore, ref best, ref bestScore);
        }

        Consider(NpcDecisionKind.Revenge, revengeScore, ref best, ref bestScore);
        Consider(NpcDecisionKind.Rob, robberyScore, ref best, ref bestScore);

        return best;
    }

    void Consider(
        NpcDecisionKind decision,
        float score,
        ref NpcDecisionKind best,
        ref float bestScore)
    {
        if (score > bestScore)
        {
            best = decision;
            bestScore = score;
        }
    }

    void TryPromptConversation()
    {
        NpcConversationAgent target = FindBestConversationTarget();
        if (target != null)
        {
            conversation.TryStartConversation(target, false);
        }
    }

    NpcConversationAgent FindBestConversationTarget()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(transform.position, nearbyRadius, npcLayers);

        NpcConversationAgent best = null;
        float bestScore = 0f;

        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.gameObject == gameObject)
            {
                continue;
            }

            NpcConversationAgent candidate =
                hit.GetComponentInParent<NpcConversationAgent>();

            if (candidate == null || candidate == conversation)
            {
                continue;
            }

            float score = conversation.GetConversationScore(candidate);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    float GetHighestGrudgeNearby()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(transform.position, nearbyRadius, npcLayers);

        float best = 0f;
        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                hit.gameObject == gameObject)
            {
                continue;
            }

            NpcSocialIdentity targetIdentity =
                hit.GetComponentInParent<NpcSocialIdentity>();

            if (targetIdentity == null)
            {
                continue;
            }

            NpcSocialRelationship relationship =
                relationships.Get(targetIdentity.gameObject);

            if (relationship != null)
            {
                best = Mathf.Max(best, relationship.grudge, relationship.hostility);
            }
        }

        return best;
    }

    void TryHostileAction(bool robbery)
    {
        NpcConversationAgent target = FindBestConversationTarget();
        if (target == null)
        {
            return;
        }

        relationships.AddHostility(target.gameObject, robbery ? 8 : 18);
        NpcSocialEventBus.PublishHostility(
            gameObject,
            target.gameObject,
            robbery ? 8 : 18,
            target.transform.position,
            robbery ? NpcText.Dialogue("robberyReason") : NpcText.Dialogue("revengeReason"));
        NpcRoleUtility.SetAction(gameObject, robbery ? NpcText.Action("rob") : NpcText.Action("revenge"));

        NpcOverheadDialogueUI overhead = GetComponent<NpcOverheadDialogueUI>();
        if (overhead != null)
        {
            overhead.ShowLine(
                robbery ? NpcText.DialogueLine("robberyThreat") : NpcText.DialogueLine("revengeThreat"),
                3f,
                4);
        }

        if (!robbery)
        {
            NpcRoleUtility.Damage(
                gameObject,
                target.gameObject,
                Mathf.Max(1, NpcRoleUtility.GetAttack(gameObject) / 2),
                NpcText.Dialogue("revengeReason"));
        }
    }

    void ApplyActionText(NpcDecisionKind decision)
    {
        switch (decision)
        {
            case NpcDecisionKind.Rest:
                NpcRoleUtility.SetAction(gameObject, NpcText.Action("rest"));
                break;
            case NpcDecisionKind.Trade:
                NpcRoleUtility.SetAction(gameObject, NpcText.Action("tradeSeek"));
                break;
            case NpcDecisionKind.GatherResource:
                NpcRoleUtility.SetAction(gameObject, NpcText.Action("gatherResource"));
                break;
            default:
                break;
        }
    }

    void ScheduleNextThink()
    {
        nextThinkTime =
            Time.time +
            UnityEngine.Random.Range(thinkIntervalMin, thinkIntervalMax);
    }
}

public class NpcNegotiationAgent : MonoBehaviour
{
    public int generousDiscountPercent = 10;
    public int hostileMarkupPercent = 20;

    NpcRelationshipGraph relationships;
    NpcMemory memory;

    void Awake()
    {
        relationships = GetComponent<NpcRelationshipGraph>();
        if (relationships == null)
        {
            relationships = gameObject.AddComponent<NpcRelationshipGraph>();
        }

        memory = GetComponent<NpcMemory>();
        if (memory == null)
        {
            memory = gameObject.AddComponent<NpcMemory>();
        }
    }

    public int AdjustPriceFor(GameObject other, int basePrice)
    {
        NpcSocialRelationship relationship = relationships.Get(other);
        if (relationship == null)
        {
            return basePrice;
        }

        int price = basePrice;

        if (relationship.affection >= 70 || relationship.trust >= 70)
        {
            price -= Mathf.RoundToInt(price * generousDiscountPercent / 100f);
        }

        if (relationship.grudge >= 60 || relationship.hostility >= 60)
        {
            price += Mathf.RoundToInt(price * hostileMarkupPercent / 100f);
        }

        return Mathf.Max(1, price);
    }

    public void RecordRejectedOffer(GameObject other, StatItemData item, int offeredPrice)
    {
        string itemName = item != null
            ? ItemText.Name(item)
            : NpcText.Label("item");
        memory.Remember(
            NpcMemoryType.TradeRejected,
            other,
            gameObject,
            NpcText.DialogueFormat("tradeRejectedMemoryTopic", itemName, offeredPrice),
            2f,
            1f,
            12);

        relationships.AddTrade(other, -2);

        string line = NpcText.Format(
            NpcText.DialogueLine(
                "tradeRejectLines",
                NpcText.Dialogue("tradeRejectFallback", "")),
            itemName,
            offeredPrice);

        NpcOverheadDialogueUI overhead = GetComponent<NpcOverheadDialogueUI>();
        if (overhead == null)
        {
            overhead = gameObject.AddComponent<NpcOverheadDialogueUI>();
        }

        if (!NpcRoleUtility.IsInCombat(gameObject))
        {
            overhead.ShowLine(line, 2.8f, 1);
        }
    }
}

[RequireComponent(typeof(Collider2D))]
public abstract class NpcLawZoneInternal : MonoBehaviour
{
    [Header("Luật")]
    public string zoneName = "Làng";
    public bool forbidKilling = true;
    public bool forbidAttacking = true;
    public int hatredPenalty = 65;
    public int trustPenalty = 35;
    public int witnessMemoryDays = 60;

    [Header("Phản Ứng")]
    public LayerMask npcLayers = ~0;
    public float witnessRadius = 8f;
    public bool callNearbyNpcsToAttack = true;
    public bool includeVictimAsWitness = true;
    public string witnessLine = "Dám ra tay trong làng!";

    Collider2D zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;
    }

    void OnEnable()
    {
        NpcSocialEventBus.HostilityDetailedHappened += HandleHostility;
    }

    void OnDisable()
    {
        NpcSocialEventBus.HostilityDetailedHappened -= HandleHostility;
    }

    void HandleHostility(
        GameObject actor,
        GameObject target,
        int severity,
        Vector3 position,
        string reason)
    {
        if (actor == null ||
            target == null ||
            zoneCollider == null ||
            (!forbidAttacking && !forbidKilling))
        {
            return;
        }

        bool inside =
            zoneCollider.OverlapPoint(actor.transform.position) ||
            zoneCollider.OverlapPoint(target.transform.position) ||
            zoneCollider.OverlapPoint(position);

        if (!inside)
        {
            return;
        }

        PunishViolation(actor, target, severity, reason);
    }

    void PunishViolation(
        GameObject violator,
        GameObject victim,
        int severity,
        string reason)
    {
        Collider2D[] witnesses =
            Physics2D.OverlapCircleAll(
                violator.transform.position,
                witnessRadius,
                npcLayers);

        for (int i = 0; i < witnesses.Length; i++)
        {
            Collider2D witnessCollider = witnesses[i];
            if (witnessCollider == null)
            {
                continue;
            }

            GameObject witness =
                GetNpcRoot(witnessCollider);

            if (witness == null ||
                witness == violator ||
                (!includeVictimAsWitness && witness == victim))
            {
                continue;
            }

            if (!zoneCollider.OverlapPoint(witness.transform.position))
            {
                continue;
            }

            MarkWitnessHostile(
                witness,
                violator,
                victim,
                Mathf.Max(severity, hatredPenalty),
                reason);
        }
    }

    void MarkWitnessHostile(
        GameObject witness,
        GameObject violator,
        GameObject victim,
        int severity,
        string reason)
    {
        NpcRelationshipGraph relationships =
            witness.GetComponent<NpcRelationshipGraph>();

        if (relationships == null)
        {
            relationships =
                witness.AddComponent<NpcRelationshipGraph>();
        }

        relationships.AddHostility(violator, severity);

        NpcSocialRelationship relation =
            relationships.Get(violator);

        if (relation != null)
        {
            relation.trust =
                Mathf.Clamp(relation.trust - trustPenalty, -100, 100);
            relation.lastTopic =
                "vi phạm luật " + zoneName;
        }

        NpcMemory memory =
            witness.GetComponent<NpcMemory>();

        if (memory == null)
        {
            memory =
                witness.AddComponent<NpcMemory>();
        }

        memory.Remember(
            NpcMemoryType.Attack,
            violator,
            victim,
            "vi phạm luật " + zoneName + ": " + reason,
            8f,
            1f,
            witnessMemoryDays);

        NpcOverheadDialogueUI overhead =
            witness.GetComponent<NpcOverheadDialogueUI>();

        if (overhead == null)
        {
            overhead =
                witness.AddComponent<NpcOverheadDialogueUI>();
        }

        overhead.ShowLine(witnessLine, 3f, 3);
        NpcRoleUtility.SetAction(
            witness,
            NpcText.ActionFormat("lawAttack", zoneName));

        if (callNearbyNpcsToAttack)
        {
            NpcDecisionBrain brain =
                witness.GetComponent<NpcDecisionBrain>();

            if (brain != null)
            {
                brain.allowViolence = true;
                brain.currentDecision = NpcDecisionKind.Revenge;
            }
        }
    }

    GameObject GetNpcRoot(Collider2D npcCollider)
    {
        VillagerAI villager =
            npcCollider.GetComponentInParent<VillagerAI>();

        if (villager != null)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc =
            npcCollider.GetComponentInParent<SmartNpcAI>();

        if (smartNpc != null)
        {
            return smartNpc.gameObject;
        }

        CharacterStats stats =
            npcCollider.GetComponentInParent<CharacterStats>();

        return stats != null ? stats.gameObject : null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.35f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}

public class NpcSocialWorldInstaller : MonoBehaviour
{
    public static NpcSocialWorldInstaller Instance { get; private set; }

    public bool autoInstall = true;
    public float installInterval = 3f;
    public bool disableLegacyDailyConversation = true;

    readonly HashSet<int> installedNpcIds = new HashSet<int>();
    float timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateInstaller()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject installer = new GameObject("NpcSocialWorldInstaller");
        DontDestroyOnLoad(installer);
        installer.AddComponent<NpcSocialWorldInstaller>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        if (!autoInstall)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }

        timer = installInterval;
        InstallForSceneNpcs();
    }

    [ContextMenu("Install For Scene NPCs")]
    public void InstallForSceneNpcs()
    {
        VillagerAI[] villagers = FindObjectsByType<VillagerAI>(
            FindObjectsInactive.Include);
        for (int i = 0; i < villagers.Length; i++)
        {
            Install(villagers[i].gameObject);
        }

        SmartNpcAI[] smartNpcs = FindObjectsByType<SmartNpcAI>(
            FindObjectsInactive.Include);
        for (int i = 0; i < smartNpcs.Length; i++)
        {
            Install(smartNpcs[i].gameObject);
        }
    }

    void Install(GameObject npc)
    {
        if (npc == null)
        {
            return;
        }

        int instanceId = npc.GetInstanceID();
        if (installedNpcIds.Contains(instanceId))
        {
            return;
        }

        installedNpcIds.Add(instanceId);

        Ensure<NpcSocialIdentity>(npc);
        Ensure<NpcNeeds>(npc);
        Ensure<NpcPersonality>(npc);
        Ensure<NpcRelationshipGraph>(npc);
        Ensure<NpcMemory>(npc);
        Ensure<NpcOverheadDialogueUI>(npc);
        Ensure<NpcConversationAgent>(npc);

        NpcDecisionBrain brain = Ensure<NpcDecisionBrain>(npc);
        if (npc.GetComponent<VillagerAI>() != null ||
            npc.GetComponent<SmartNpcAI>() != null)
        {
            brain.enabledDecisionBrain = false;
        }

        Ensure<NpcNegotiationAgent>(npc);

        if (disableLegacyDailyConversation)
        {
            DailyConversation legacy = npc.GetComponent<DailyConversation>();
            if (legacy != null)
            {
                legacy.enabled = false;
            }
        }
    }

    T Ensure<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }
}

public static class NpcSocialTime
{
    public static int Day
    {
        get
        {
            WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
            return timeSystem != null ? timeSystem.CurrentDay : 0;
        }
    }

    public static float Hour
    {
        get
        {
            WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
            return timeSystem != null ? timeSystem.CurrentHour : 0f;
        }
    }
}
