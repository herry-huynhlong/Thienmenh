using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcSpeechDisplayState
{
    None,
    Showing
}

[Serializable]
public class NpcDialogueDatabase
{
    public NpcDialogueRuleData[] rules;
}

[Serializable]
public class NpcDialogueRuleData
{
    public string id;
    public string category;
    public string channel;
    public float duration = 2f;
    public float cooldownSeconds = 20f;
    public int priority = 10;
    public string mode = "DisplayOnly";
    public string[] allowedContexts;
    public string[] blockedContexts;
    public string[] requiredSpeakerTags;
    public string[] blockedSpeakerTags;
    public string[] requiredTargetContexts;
    public string[] blockedTargetContexts;
    public string[] requiredTargetTags;
    public string[] blockedTargetTags;
    public string[] requiredKinships;
    public string[] blockedKinships;
    public string[] requiredRelationshipStates;
    public string[] blockedRelationshipStates;
    public string[] texts;
}

public readonly struct NpcDialogueToken
{
    public readonly string key;
    public readonly string value;

    public NpcDialogueToken(string key, string value)
    {
        this.key = key ?? "";
        this.value = value ?? "";
    }
}

public readonly struct NpcDialogueSelection
{
    public readonly string id;
    public readonly string category;
    public readonly string channel;
    public readonly string text;
    public readonly float duration;
    public readonly float cooldownSeconds;
    public readonly int priority;

    public NpcDialogueSelection(
        string id,
        string category,
        string channel,
        string text,
        float duration,
        float cooldownSeconds,
        int priority)
    {
        this.id = id ?? "";
        this.category = category ?? "";
        this.channel = channel ?? "";
        this.text = text ?? "";
        this.duration = duration;
        this.cooldownSeconds = cooldownSeconds;
        this.priority = priority;
    }
}

public readonly struct NpcDialogueContext
{
    public readonly string speakerId;
    public readonly string targetId;
    public readonly string speakerName;
    public readonly string targetName;
    public readonly string speakerRole;
    public readonly string targetRole;
    public readonly string kinship;
    public readonly string relationshipState;
    public readonly string[] speakerTags;
    public readonly string[] targetTags;

    public NpcDialogueContext(
        string speakerId,
        string targetId,
        string speakerName,
        string targetName,
        string speakerRole,
        string targetRole,
        string kinship,
        string relationshipState,
        string[] speakerTags,
        string[] targetTags)
    {
        this.speakerId = speakerId ?? "";
        this.targetId = targetId ?? "";
        this.speakerName = speakerName ?? "";
        this.targetName = targetName ?? "";
        this.speakerRole = speakerRole ?? "";
        this.targetRole = targetRole ?? "";
        this.kinship = kinship ?? "None";
        this.relationshipState = relationshipState ?? "None";
        this.speakerTags = speakerTags ?? Array.Empty<string>();
        this.targetTags = targetTags ?? Array.Empty<string>();
    }
}

public static class NpcDialogueCatalog
{
    const string ResourceName = "NpcDialogueDatabase";

    static NpcDialogueRuleData[] cachedRules;
    static string loadedLanguageCode;

    public static NpcDialogueRuleData[] GetRules()
    {
        EnsureLoaded();
        return cachedRules ?? Array.Empty<NpcDialogueRuleData>();
    }

    static void EnsureLoaded()
    {
        string currentLanguageCode = LocalizationSettings.CurrentLanguageCode;
        if (cachedRules != null &&
            string.Equals(
                loadedLanguageCode,
                currentLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        loadedLanguageCode = currentLanguageCode;
        cachedRules = Array.Empty<NpcDialogueRuleData>();

        TextAsset asset = LocalizationSettings.LoadTextAsset(ResourceName);
        if (asset == null)
        {
            Debug.LogWarning(
                "Missing Resources/" +
                LocalizationSettings.GetLocalizedResourcePath(ResourceName) +
                ".json");
            return;
        }

        NpcDialogueDatabase database =
            JsonUtility.FromJson<NpcDialogueDatabase>(asset.text);
        if (database == null || database.rules == null)
        {
            return;
        }

        for (int i = 0; i < database.rules.Length; i++)
        {
            CleanRule(database.rules[i]);
        }

        cachedRules = database.rules;
    }

    static void CleanRule(NpcDialogueRuleData rule)
    {
        if (rule == null)
        {
            return;
        }

        rule.id = Clean(rule.id);
        rule.category = Clean(rule.category);
        rule.channel = Clean(rule.channel);
        rule.mode = Clean(rule.mode);
        CleanArray(rule.allowedContexts);
        CleanArray(rule.blockedContexts);
        CleanArray(rule.requiredSpeakerTags);
        CleanArray(rule.blockedSpeakerTags);
        CleanArray(rule.requiredTargetContexts);
        CleanArray(rule.blockedTargetContexts);
        CleanArray(rule.requiredTargetTags);
        CleanArray(rule.blockedTargetTags);
        CleanArray(rule.requiredKinships);
        CleanArray(rule.blockedKinships);
        CleanArray(rule.requiredRelationshipStates);
        CleanArray(rule.blockedRelationshipStates);
        CleanArray(rule.texts);
    }

    static void CleanArray(string[] values)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Clean(values[i]);
        }
    }

    static string Clean(string value)
    {
        return NpcText.CleanDisplayText(value ?? "").Trim();
    }
}

public static class NpcDialogueSelector
{
    public static bool TrySelect(
        in NpcDialogueContext context,
        string category,
        NpcDialogueToken[] extraTokens,
        out NpcDialogueSelection selection)
    {
        selection = default;
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        NpcDialogueRuleData[] rules = NpcDialogueCatalog.GetRules();
        List<NpcDialogueRuleData> matches = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < rules.Length; i++)
        {
            NpcDialogueRuleData rule = rules[i];
            if (!Matches(rule, in context, category))
            {
                continue;
            }

            if (rule.priority > bestPriority)
            {
                bestPriority = rule.priority;
                if (matches == null)
                {
                    matches = new List<NpcDialogueRuleData>();
                }
                matches.Clear();
            }

            if (rule.priority == bestPriority)
            {
                if (matches == null)
                {
                    matches = new List<NpcDialogueRuleData>();
                }
                matches.Add(rule);
            }
        }

        if (matches == null || matches.Count <= 0)
        {
            return false;
        }

        NpcDialogueRuleData chosen =
            matches[UnityEngine.Random.Range(0, matches.Count)];
        string template = PickTemplate(chosen.texts);
        string text = Render(template, in context, extraTokens);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        selection = new NpcDialogueSelection(
            chosen.id,
            chosen.category,
            chosen.channel,
            text,
            Mathf.Max(0.2f, chosen.duration),
            Mathf.Max(0f, chosen.cooldownSeconds),
            chosen.priority);
        return true;
    }

    static bool Matches(
        NpcDialogueRuleData rule,
        in NpcDialogueContext context,
        string category)
    {
        if (rule == null ||
            !string.Equals(
                rule.mode,
                "DisplayOnly",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                rule.category,
                category,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return MatchesAny(rule.allowedContexts, context.speakerTags) &&
            !ContainsAny(rule.blockedContexts, context.speakerTags) &&
            ContainsAll(rule.requiredSpeakerTags, context.speakerTags) &&
            !ContainsAny(rule.blockedSpeakerTags, context.speakerTags) &&
            MatchesAny(rule.requiredTargetContexts, context.targetTags) &&
            !ContainsAny(rule.blockedTargetContexts, context.targetTags) &&
            ContainsAll(rule.requiredTargetTags, context.targetTags) &&
            !ContainsAny(rule.blockedTargetTags, context.targetTags) &&
            MatchesValue(rule.requiredKinships, context.kinship) &&
            !ContainsValue(rule.blockedKinships, context.kinship) &&
            MatchesValue(rule.requiredRelationshipStates, context.relationshipState) &&
            !ContainsValue(rule.blockedRelationshipStates, context.relationshipState);
    }

    static string PickTemplate(string[] texts)
    {
        if (texts == null || texts.Length <= 0)
        {
            return "";
        }

        return texts[UnityEngine.Random.Range(0, texts.Length)];
    }

    static string Render(
        string template,
        in NpcDialogueContext context,
        NpcDialogueToken[] extraTokens)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return "";
        }

        string text = template;
        text = text.Replace("{speaker}", context.speakerName);
        text = text.Replace("{target}", context.targetName);
        text = text.Replace("{speakerRole}", context.speakerRole);
        text = text.Replace("{targetRole}", context.targetRole);

        if (extraTokens != null)
        {
            for (int i = 0; i < extraTokens.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(extraTokens[i].key))
                {
                    continue;
                }

                text = text.Replace(
                    "{" + extraTokens[i].key + "}",
                    extraTokens[i].value ?? "");
            }
        }

        return NpcText.CleanDisplayText(text).Trim();
    }

    static bool MatchesAny(string[] filters, string[] tags)
    {
        if (filters == null || filters.Length <= 0)
        {
            return true;
        }

        return ContainsAny(filters, tags);
    }

    static bool MatchesValue(string[] filters, string value)
    {
        if (filters == null || filters.Length <= 0)
        {
            return true;
        }

        return Contains(filters, value);
    }

    static bool ContainsValue(string[] filters, string value)
    {
        return filters != null &&
            filters.Length > 0 &&
            Contains(filters, value);
    }

    static bool ContainsAll(string[] filters, string[] tags)
    {
        if (filters == null || filters.Length <= 0)
        {
            return true;
        }

        for (int i = 0; i < filters.Length; i++)
        {
            if (!Contains(tags, filters[i]))
            {
                return false;
            }
        }

        return true;
    }

    static bool ContainsAny(string[] filters, string[] tags)
    {
        if (filters == null || filters.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < filters.Length; i++)
        {
            if (Contains(tags, filters[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool Contains(string[] values, string expected)
    {
        if (values == null ||
            values.Length <= 0 ||
            string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(
                    values[i],
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

[DisallowMultipleComponent]
public class NpcSpeechController : MonoBehaviour
{
    readonly Dictionary<string, float> nextCategoryTimes =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, float> nextRuleTimes =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    NpcOverheadDialogueUI overhead;

    public static bool TryShowSpeech(
        GameObject speaker,
        GameObject target,
        string category)
    {
        return TryShowSpeech(
            speaker,
            target,
            category,
            out _,
            null);
    }

    public static bool TryShowSpeech(
        GameObject speaker,
        GameObject target,
        string category,
        NpcDialogueToken[] extraTokens)
    {
        return TryShowSpeech(
            speaker,
            target,
            category,
            out _,
            extraTokens);
    }

    public static bool TryShowSpeech(
        GameObject speaker,
        GameObject target,
        string category,
        out NpcDialogueSelection selection,
        NpcDialogueToken[] extraTokens = null)
    {
        selection = default;
        if (speaker == null)
        {
            return false;
        }

        NpcSpeechController controller =
            speaker.GetComponent<NpcSpeechController>();
        if (controller == null)
        {
            controller = speaker.AddComponent<NpcSpeechController>();
        }

        return controller.TryShowSpeechInternal(
            target,
            category,
            out selection,
            extraTokens);
    }

    void Awake()
    {
        EnsureOverhead();
    }

    bool TryShowSpeechInternal(
        GameObject target,
        string category,
        out NpcDialogueSelection selection,
        NpcDialogueToken[] extraTokens)
    {
        selection = default;
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        float now = Time.time;
        if (nextCategoryTimes.TryGetValue(category, out float nextCategoryTime) &&
            now < nextCategoryTime)
        {
            return false;
        }

        NpcDialogueContext context =
            NpcDialogueContextBuilder.Build(gameObject, target);
        if (!NpcDialogueSelector.TrySelect(
                in context,
                category,
                extraTokens,
                out selection))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(selection.id) &&
            nextRuleTimes.TryGetValue(selection.id, out float nextRuleTime) &&
            now < nextRuleTime)
        {
            return false;
        }

        EnsureOverhead();
        if (overhead == null ||
            !overhead.TryShowLine(
                selection.text,
                selection.duration,
                selection.priority))
        {
            return false;
        }

        nextCategoryTimes[category] = now + selection.cooldownSeconds;
        if (!string.IsNullOrEmpty(selection.id))
        {
            nextRuleTimes[selection.id] = now + selection.cooldownSeconds;
        }

        return true;
    }

    void EnsureOverhead()
    {
        if (overhead == null)
        {
            overhead = GetComponent<NpcOverheadDialogueUI>();
        }

        if (overhead == null)
        {
            overhead = gameObject.AddComponent<NpcOverheadDialogueUI>();
        }
    }
}

static class NpcDialogueContextBuilder
{
    static readonly string[] MovementActionKeys =
    {
        "walkingRoad",
        "goWork",
        "goFarmWork",
        "goPatrol",
        "goHeal",
        "goFish",
        "goHunt",
        "goMarketTrade",
        "bringGoodsToCounter",
        "goHomeRest",
        "eatAtShop",
        "goPlay",
        "goTaskProviderDaily",
        "goHomeCultivate",
        "goCultivatePoint",
        "goGatherNamed",
        "teleportGateTo",
        "goTavern",
        "buyPill",
        "moveToTask"
    };

    static readonly string[] WorkingActionKeys =
    {
        "working",
        "workingFarm",
        "fishing",
        "hunting",
        "alchemy",
        "forging",
        "trading",
        "taskWorking",
        "workingTask",
        "gatherResource",
        "healing",
        "patrolling"
    };

    static readonly string[] CultivationActionKeys =
    {
        "cultivate",
        "cultivateAbsorbQi",
        "breakthrough",
        "breakthroughTo",
        "waitTribulation"
    };

    public static NpcDialogueContext Build(
        GameObject speaker,
        GameObject target)
    {
        NPCIdentity speakerIdentity = speaker != null
            ? speaker.GetComponent<NPCIdentity>()
            : null;
        NPCIdentity targetIdentity = target != null
            ? target.GetComponent<NPCIdentity>()
            : null;

        string kinship = ResolveKinship(speakerIdentity, targetIdentity);
        string relationshipState =
            ResolveRelationshipState(speaker, target, speakerIdentity, targetIdentity);

        NpcSocialRelationship socialRelationship =
            ResolveSocialRelationship(speaker, target);

        HashSet<string> speakerTags = BuildTags(
            speaker,
            target,
            speakerIdentity,
            targetIdentity,
            socialRelationship,
            relationshipState,
            kinship);
        HashSet<string> targetTags = BuildTags(
            target,
            speaker,
            targetIdentity,
            speakerIdentity,
            ResolveSocialRelationship(target, speaker),
            ResolveRelationshipState(target, speaker, targetIdentity, speakerIdentity),
            ReverseKinship(kinship));

        return new NpcDialogueContext(
            speakerIdentity != null ? speakerIdentity.npcId : "",
            targetIdentity != null ? targetIdentity.npcId : "",
            NpcRoleUtility.GetDisplayName(speaker),
            NpcRoleUtility.GetDisplayName(target),
            NpcRoleUtility.GetRoleLabel(speaker),
            NpcRoleUtility.GetRoleLabel(target),
            kinship,
            relationshipState,
            ToArray(speakerTags),
            ToArray(targetTags));
    }

    static HashSet<string> BuildTags(
        GameObject npc,
        GameObject other,
        NPCIdentity identity,
        NPCIdentity otherIdentity,
        NpcSocialRelationship relationship,
        string relationshipState,
        string kinship)
    {
        HashSet<string> tags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (npc == null)
        {
            return tags;
        }

        AddPrimaryStateTags(tags, npc);
        AddIdentityTags(tags, npc, identity);
        AddSocialTags(tags, relationship, relationshipState, kinship, identity, otherIdentity);
        AddWorldTags(tags);
        AddComparisonTags(tags, npc, other);
        return tags;
    }

    static void AddPrimaryStateTags(HashSet<string> tags, GameObject npc)
    {
        if (NpcRoleUtility.IsDead(npc))
        {
            tags.Add("Dead");
            return;
        }

        if (HeavenlyTribulationSystem.IsTargetLocked(npc) ||
            HasAction(npc, CultivationActionKeys[4]))
        {
            tags.Add("InTribulation");
        }

        if (NpcRoleUtility.IsInCombat(npc))
        {
            tags.Add("InCombat");
        }

        if (IsCultivating(npc))
        {
            tags.Add("Cultivating");
        }

        if (IsWorking(npc))
        {
            tags.Add("Working");
        }

        if (IsGathering(npc))
        {
            tags.Add("Gathering");
        }

        if (IsMoving(npc))
        {
            tags.Add("Moving");
        }

        if (HasAction(npc, "panicBurned"))
        {
            tags.Add("Fleeing");
        }

        if (GetHealthRatio(npc) <= 0.35f)
        {
            tags.Add("LowHealth");
        }

        if (!tags.Contains("Moving") &&
            !tags.Contains("Working") &&
            !tags.Contains("Cultivating") &&
            !tags.Contains("InCombat") &&
            !tags.Contains("InTribulation"))
        {
            tags.Add("Idle");
        }
    }

    static void AddIdentityTags(
        HashSet<string> tags,
        GameObject npc,
        NPCIdentity identity)
    {
        if (NpcRoleUtility.IsCommoner(npc))
        {
            tags.Add("Commoner");
        }

        if (NpcRoleUtility.IsCultivator(npc))
        {
            tags.Add("Cultivator");
        }

        if (npc != null && npc.GetComponent<MonsterAI>() != null)
        {
            tags.Add("Monster");
        }

        if (identity == null)
        {
            return;
        }

        tags.Add(identity.gender == Gender.Female ? "Female" : "Male");
        tags.Add(identity.lifeStage.ToString());

        if (identity.lifeStage == LifeStage.Baby ||
            identity.lifeStage == LifeStage.Child)
        {
            tags.Add("Child");
        }

        if (identity.lifeStage == LifeStage.Old)
        {
            tags.Add("Elder");
        }
    }

    static void AddSocialTags(
        HashSet<string> tags,
        NpcSocialRelationship relationship,
        string relationshipState,
        string kinship,
        NPCIdentity identity,
        NPCIdentity otherIdentity)
    {
        if (!string.IsNullOrWhiteSpace(kinship) &&
            !string.Equals(kinship, "None", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add(kinship);
            tags.Add("SameFamily");
        }

        if (!string.IsNullOrWhiteSpace(relationshipState) &&
            !string.Equals(relationshipState, "None", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add(relationshipState);
        }

        if (string.Equals(kinship, "Spouse", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relationshipState, "Married", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relationshipState, "Dating", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("Friendly");
            return;
        }

        if (relationship == null)
        {
            tags.Add(identity != null && otherIdentity != null ? "Stranger" : "Neutral");
            return;
        }

        if (Mathf.Max(relationship.grudge, relationship.hostility) >= 50 ||
            relationship.trust <= -40 ||
            relationship.affection <= -40)
        {
            tags.Add("Hostile");
            return;
        }

        if (Mathf.Max(relationship.affection, relationship.alliance) >= 20 ||
            relationship.trust >= 20 ||
            relationship.respect >= 20)
        {
            tags.Add("Friendly");
            return;
        }

        if (relationship.lastInteractionDay < 0)
        {
            tags.Add("Stranger");
            return;
        }

        tags.Add("Neutral");
    }

    static void AddWorldTags(HashSet<string> tags)
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            tags.Add(timeSystem.CurrentPhase.ToString());
        }

        WeatherSystem weather = WeatherSystem.Instance;
        if (weather != null)
        {
            tags.Add(weather.CurrentWeather.ToString());
        }
    }

    static void AddComparisonTags(
        HashSet<string> tags,
        GameObject npc,
        GameObject other)
    {
        if (npc == null || other == null)
        {
            return;
        }

        int selfPower = NpcRoleUtility.GetRealmPower(npc);
        int otherPower = NpcRoleUtility.GetRealmPower(other);
        if (selfPower > otherPower)
        {
            tags.Add("SeniorToTarget");
        }
        else if (selfPower < otherPower)
        {
            tags.Add("JuniorToTarget");
        }
        else
        {
            tags.Add("EqualRealm");
        }
    }

    static string ResolveRelationshipState(
        GameObject speaker,
        GameObject target,
        NPCIdentity speakerIdentity,
        NPCIdentity targetIdentity)
    {
        if (speaker == null || target == null)
        {
            return "None";
        }

        VillagerRelationship relationship =
            speaker.GetComponent<VillagerRelationship>();
        if (relationship != null &&
            targetIdentity != null &&
            string.Equals(
                relationship.partnerId,
                targetIdentity.npcId,
                StringComparison.OrdinalIgnoreCase))
        {
            return relationship.status.ToString();
        }

        if (speakerIdentity != null &&
            targetIdentity != null &&
            string.Equals(
                speakerIdentity.spouseId,
                targetIdentity.npcId,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Married";
        }

        return "None";
    }

    static string ResolveKinship(
        NPCIdentity speaker,
        NPCIdentity target)
    {
        if (speaker == null || target == null)
        {
            return "None";
        }

        if (string.Equals(
                speaker.spouseId,
                target.npcId,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                target.spouseId,
                speaker.npcId,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Spouse";
        }

        if (string.Equals(
                target.fatherId,
                speaker.npcId,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                target.motherId,
                speaker.npcId,
                StringComparison.OrdinalIgnoreCase))
        {
            return "ParentOfTarget";
        }

        if (string.Equals(
                speaker.fatherId,
                target.npcId,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                speaker.motherId,
                target.npcId,
                StringComparison.OrdinalIgnoreCase))
        {
            return "ChildOfTarget";
        }

        bool sameFather =
            !string.IsNullOrWhiteSpace(speaker.fatherId) &&
            string.Equals(
                speaker.fatherId,
                target.fatherId,
                StringComparison.OrdinalIgnoreCase);
        bool sameMother =
            !string.IsNullOrWhiteSpace(speaker.motherId) &&
            string.Equals(
                speaker.motherId,
                target.motherId,
                StringComparison.OrdinalIgnoreCase);

        if (sameFather || sameMother)
        {
            return "Sibling";
        }

        return "None";
    }

    static string ReverseKinship(string kinship)
    {
        if (string.Equals(kinship, "ParentOfTarget", StringComparison.OrdinalIgnoreCase))
        {
            return "ChildOfTarget";
        }

        if (string.Equals(kinship, "ChildOfTarget", StringComparison.OrdinalIgnoreCase))
        {
            return "ParentOfTarget";
        }

        return kinship ?? "None";
    }

    static NpcSocialRelationship ResolveSocialRelationship(
        GameObject speaker,
        GameObject target)
    {
        if (speaker == null || target == null)
        {
            return null;
        }

        NpcRelationshipGraph graph =
            speaker.GetComponent<NpcRelationshipGraph>();
        return graph != null
            ? graph.Find(target)
            : null;
    }

    static bool IsMoving(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        if (NpcRoleUtility.TryGetCurrentMovement(npc, out NpcMovementResult movement))
        {
            if (movement.status == NpcMovementStatus.Moving ||
                movement.status == NpcMovementStatus.Pending ||
                movement.status == NpcMovementStatus.Paused)
            {
                return true;
            }
        }

        Rigidbody2D rb = npc.GetComponent<Rigidbody2D>();
        if (rb != null &&
            rb.simulated &&
            rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            return true;
        }

        for (int i = 0; i < MovementActionKeys.Length; i++)
        {
            if (HasAction(npc, MovementActionKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsWorking(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(npc);
        if (schedule != null)
        {
            switch (schedule.CurrentActivity)
            {
                case NpcScheduleActivity.Work:
                case NpcScheduleActivity.Gather:
                case NpcScheduleActivity.Hunt:
                case NpcScheduleActivity.Alchemy:
                case NpcScheduleActivity.Forge:
                case NpcScheduleActivity.SellGoods:
                case NpcScheduleActivity.BuyGoods:
                case NpcScheduleActivity.TakeTask:
                case NpcScheduleActivity.DoMission:
                case NpcScheduleActivity.FreeHuntAndGather:
                case NpcScheduleActivity.TradeBuySell:
                    return true;
            }
        }

        for (int i = 0; i < WorkingActionKeys.Length; i++)
        {
            if (HasAction(npc, WorkingActionKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsGathering(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        NpcScheduleController schedule =
            NpcScheduleController.GetSchedule(npc);
        if (schedule != null)
        {
            switch (schedule.CurrentActivity)
            {
                case NpcScheduleActivity.Gather:
                case NpcScheduleActivity.FreeHuntAndGather:
                    return true;
            }
        }

        return HasAction(npc, "gatherResource");
    }

    static bool IsCultivating(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        for (int i = 0; i < CultivationActionKeys.Length; i++)
        {
            if (HasAction(npc, CultivationActionKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasAction(GameObject npc, string key)
    {
        string action = GetCurrentAction(npc);
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        string template = NpcText.Action(key);
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        int placeholderIndex = template.IndexOf('{');
        string prefix = placeholderIndex >= 0
            ? template.Substring(0, placeholderIndex).Trim()
            : template.Trim();

        return string.Equals(
                action,
                template,
                StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(prefix) &&
            action.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase));
    }

    static string GetCurrentAction(GameObject npc)
    {
        if (npc == null)
        {
            return "";
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null &&
            !string.IsNullOrWhiteSpace(villager.currentAction))
        {
            return villager.currentAction;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null &&
            !string.IsNullOrWhiteSpace(smartNpc.currentAction))
        {
            return smartNpc.currentAction;
        }

        MonsterAI monster = npc.GetComponent<MonsterAI>();
        if (monster != null &&
            !string.IsNullOrWhiteSpace(monster.currentAction))
        {
            return monster.currentAction;
        }

        NpcMapMover2D mover = npc.GetComponent<NpcMapMover2D>();
        if (mover != null &&
            !string.IsNullOrWhiteSpace(mover.currentAction))
        {
            return mover.currentAction;
        }

        NpcData npcData = npc.GetComponent<NpcData>();
        return npcData != null ? npcData.currentAction : "";
    }

    static float GetHealthRatio(GameObject npc)
    {
        if (npc == null)
        {
            return 1f;
        }

        CharacterStats stats = npc.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return stats.finalHP > 0
                ? Mathf.Clamp01(stats.currentHP / (float)stats.finalHP)
                : 0f;
        }

        VillagerAI villager = npc.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.maxHP > 0
                ? Mathf.Clamp01(villager.currentHP / (float)villager.maxHP)
                : 0f;
        }

        SmartNpcAI smartNpc = npc.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.maxHP > 0
                ? Mathf.Clamp01(smartNpc.currentHP / (float)smartNpc.maxHP)
                : 0f;
        }

        MonsterAI monster = npc.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.maxHP > 0
                ? Mathf.Clamp01(monster.currentHP / (float)monster.maxHP)
                : 0f;
        }

        return 1f;
    }

    static string[] ToArray(HashSet<string> values)
    {
        if (values == null || values.Count <= 0)
        {
            return Array.Empty<string>();
        }

        string[] result = new string[values.Count];
        values.CopyTo(result);
        return result;
    }
}
