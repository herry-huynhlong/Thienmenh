using System.Collections.Generic;
using UnityEngine;

public class TreasureFrenzySystem : MonoBehaviour
{
    static TreasureFrenzySystem instance;

    [Header("Treasure Frenzy")]
    public int maxParticipants = 12;
    public int minParticipants = 8;
    public float upperGradeRadius = 18f;
    public float immortalGradeRadius = 36f;
    public float crossMapDistancePenalty = 0.15f;
    public float frenzyTickInterval = 1f;
    public float attackRange = 1.4f;
    public float cowardFearThreshold = 1.15f;
    public float holderPeaceGameHours = 1f;
    public int upperGradeDamage = 20;
    public int immortalGradeDamage = 80;
    public float immortalLightningDuration = 8f;
    public float immortalLightningDangerRadius = 5f;
    public float immortalLightningSafePadding = 2f;
    public float immortalLightningStrikeInterval = 1.1f;
    public int immortalLightningDamage = 35;
    public int outerSkirmishDamage = 8;
    public float lowPowerOuterSkirmishThreshold = 35f;

    readonly List<TreasureFrenzyEvent> activeEvents =
        new List<TreasureFrenzyEvent>();

    float tickTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeInstance()
    {
        EnsureInstance();
    }

    public static TreasureFrenzySystem EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        TreasureFrenzySystem existing =
            FindObjectOfType<TreasureFrenzySystem>(true);

        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return instance;
        }

        GameObject systemObject =
            new GameObject("Treasure Frenzy System");

        DontDestroyOnLoad(systemObject);
        instance = systemObject.AddComponent<TreasureFrenzySystem>();
        return instance;
    }

    public static void AnnounceTreasure(
        WorldStatItemPickup pickup,
        StatItemData item,
        Vector3 position)
    {
        if (item == null || pickup == null)
        {
            return;
        }

        EnsureInstance().StartFrenzy(pickup, item, position);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (activeEvents.Count == 0)
        {
            return;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer < frenzyTickInterval)
        {
            return;
        }

        tickTimer = 0f;

        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            UpdateEvent(activeEvents[i]);
        }
    }

    void StartFrenzy(
        WorldStatItemPickup pickup,
        StatItemData item,
        Vector3 position)
    {
        TreasureFrenzyEvent frenzyEvent = new TreasureFrenzyEvent
        {
            pickup = pickup,
            item = item,
            origin = position,
            radius = GetSignalRadius(item),
            slaughter = item.grade == ItemGrade.Tien,
            startHour = GetAbsoluteWorldHour(),
            holderStartHour = -1f,
            waitingForLightning = item.grade == ItemGrade.Tien,
            lightningStartTime = item.grade == ItemGrade.Tien
                ? Time.time + GetImmortalLightningStartDelay()
                : 0f,
            lightningEndTime = item.grade == ItemGrade.Tien
                ? Time.time + GetImmortalLightningStartDelay() + GetImmortalLightningDuration()
                : 0f,
            nextLightningStrikeTime = 0f
        };

        SelectParticipants(frenzyEvent);
        if (frenzyEvent.participants.Count == 0)
        {
            return;
        }

        activeEvents.Add(frenzyEvent);
        CommandParticipants(frenzyEvent);
        AnnounceWorld(frenzyEvent, false);
    }

    void SelectParticipants(TreasureFrenzyEvent frenzyEvent)
    {
        List<TreasureParticipant> candidates =
            new List<TreasureParticipant>();

        foreach (VillagerAI villager in FindObjectsByType<VillagerAI>(FindObjectsInactive.Exclude))
        {
            AddCandidate(candidates, villager.gameObject, ActorKind.Villager, frenzyEvent);
        }

        foreach (SmartNpcAI smartNpc in FindObjectsByType<SmartNpcAI>(FindObjectsInactive.Exclude))
        {
            AddCandidate(candidates, smartNpc.gameObject, ActorKind.SmartNpc, frenzyEvent);
        }

        foreach (MonsterAI monster in FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude))
        {
            AddCandidate(candidates, monster.gameObject, ActorKind.Monster, frenzyEvent);
        }

        candidates.Sort((a, b) => b.desire.CompareTo(a.desire));

        int minLimit = Mathf.Clamp(minParticipants, 1, Mathf.Max(1, maxParticipants));
        int maxLimit = Mathf.Clamp(maxParticipants, minLimit, 32);
        int limit = Random.Range(minLimit, maxLimit + 1);

        int npcQuota = Mathf.Max(1, Mathf.RoundToInt(limit * 0.35f));
        int monsterQuota = Mathf.Max(1, Mathf.RoundToInt(limit * 0.35f));
        AddTopCandidatesByKind(candidates, frenzyEvent.participants, ActorKind.Villager, npcQuota);
        AddTopCandidatesByKind(candidates, frenzyEvent.participants, ActorKind.SmartNpc, npcQuota);
        AddTopCandidatesByKind(candidates, frenzyEvent.participants, ActorKind.Monster, monsterQuota);

        for (int i = 0; i < candidates.Count && frenzyEvent.participants.Count < limit; i++)
        {
            if (candidates[i].desire <= 0f ||
                HasParticipant(frenzyEvent.participants, candidates[i].actor))
            {
                continue;
            }

            frenzyEvent.participants.Add(candidates[i]);
        }
    }

    void AddTopCandidatesByKind(
        List<TreasureParticipant> candidates,
        List<TreasureParticipant> participants,
        ActorKind kind,
        int maxCount)
    {
        if (maxCount <= 0)
        {
            return;
        }

        int added = 0;
        for (int i = 0; i < candidates.Count && added < maxCount; i++)
        {
            TreasureParticipant candidate = candidates[i];
            if (candidate.kind != kind ||
                candidate.desire <= 0f ||
                HasParticipant(participants, candidate.actor))
            {
                continue;
            }

            participants.Add(candidate);
            added++;
        }
    }

    bool HasParticipant(
        List<TreasureParticipant> participants,
        GameObject actor)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i].actor == actor)
            {
                return true;
            }
        }

        return false;
    }

    void AddCandidate(
        List<TreasureParticipant> candidates,
        GameObject actor,
        ActorKind kind,
        TreasureFrenzyEvent frenzyEvent)
    {
        if (actor == null || IsDead(actor, kind))
        {
            return;
        }

        float distance = Vector2.Distance(actor.transform.position, frenzyEvent.origin);
        float desire = GetDesire(actor, kind, frenzyEvent, distance);
        float fear = GetFear(actor, kind, frenzyEvent, distance);

        if (desire < fear && fear / Mathf.Max(1f, desire) >= cowardFearThreshold)
        {
            if (frenzyEvent.item.grade == ItemGrade.Tien && distance <= frenzyEvent.radius)
            {
                candidates.Add(new TreasureParticipant
                {
                    actor = actor,
                    kind = kind,
                    desire = desire * 0.25f,
                    fear = fear,
                    coward = true
                });
            }

            return;
        }

        if (distance > frenzyEvent.radius && desire < GetCrossMapThreshold(frenzyEvent.item))
        {
            return;
        }

        candidates.Add(new TreasureParticipant
        {
            actor = actor,
            kind = kind,
            desire = desire,
            fear = fear,
            coward = false
        });
    }

    float GetDesire(
        GameObject actor,
        ActorKind kind,
        TreasureFrenzyEvent frenzyEvent,
        float distance)
    {
        float gradeWeight = frenzyEvent.item.grade == ItemGrade.Tien ? 120f : 45f;
        float itemValue = Mathf.Log10(Mathf.Max(10, NpcEconomy.GetItemValue(frenzyEvent.item))) * 8f;
        float power = GetPower(actor, kind);
        float distanceCost = distance * (distance > frenzyEvent.radius ? crossMapDistancePenalty : 0.45f);

        if (kind == ActorKind.Monster)
        {
            MonsterAI monster = actor.GetComponent<MonsterAI>();
            return gradeWeight + itemValue + power * 0.7f + monster.aggression * 0.55f +
                monster.beastInstinct * 0.35f + monster.bloodlust * 0.45f + monster.hunger * 0.15f -
                monster.fear * 0.45f - distanceCost;
        }

        if (kind == ActorKind.SmartNpc)
        {
            SmartNpcAI npc = actor.GetComponent<SmartNpcAI>();
            return gradeWeight + itemValue + power * 1.2f + npc.greed * 0.8f +
                npc.bravery * 0.55f - (100f - npc.bravery) * 0.35f - distanceCost;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        return gradeWeight + itemValue + power * 1.1f + villager.greed * 0.8f +
            villager.bravery * 0.55f - (100f - villager.bravery) * 0.35f - distanceCost;
    }

    float GetFear(
        GameObject actor,
        ActorKind kind,
        TreasureFrenzyEvent frenzyEvent,
        float distance)
    {
        float gradeFear = frenzyEvent.item.grade == ItemGrade.Tien ? 70f : 25f;
        float power = GetPower(actor, kind);

        if (kind == ActorKind.Monster)
        {
            MonsterAI monster = actor.GetComponent<MonsterAI>();
            return gradeFear + monster.fear * 0.9f + monster.survivalInstinct * 0.25f -
                monster.aggression * 0.35f - power * 0.45f + distance * 0.08f;
        }

        if (kind == ActorKind.SmartNpc)
        {
            SmartNpcAI npc = actor.GetComponent<SmartNpcAI>();
            return gradeFear + (100f - npc.bravery) * 0.9f - npc.greed * 0.3f -
                power * 0.5f + distance * 0.08f;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        return gradeFear + (100f - villager.bravery) * 0.9f - villager.greed * 0.3f -
            power * 0.5f + distance * 0.08f;
    }

    void CommandParticipants(TreasureFrenzyEvent frenzyEvent)
    {
        if (frenzyEvent.waitingForLightning)
        {
            CommandLightningWait(frenzyEvent);
            return;
        }

        Transform target = GetTreasureTarget(frenzyEvent);
        foreach (TreasureParticipant participant in frenzyEvent.participants)
        {
            if (participant.actor == null)
            {
                continue;
            }

            if (participant.coward)
            {
                MarkCoward(participant, frenzyEvent);
                continue;
            }

            ForceHunt(participant, target, frenzyEvent.item);
        }
    }

    void CommandLightningWait(TreasureFrenzyEvent frenzyEvent)
    {
        float safeRadius =
            Mathf.Max(0.5f, immortalLightningDangerRadius + immortalLightningSafePadding);

        foreach (TreasureParticipant participant in frenzyEvent.participants)
        {
            if (participant.actor == null)
            {
                continue;
            }

            if (participant.coward)
            {
                MarkCoward(participant, frenzyEvent);
                continue;
            }

            bool lowPower =
                GetPower(participant.actor, participant.kind) <= lowPowerOuterSkirmishThreshold;

            ForceWaitOutsideLightning(participant, frenzyEvent.origin, safeRadius, frenzyEvent.item, lowPower);
        }
    }

    void ForceWaitOutsideLightning(
        TreasureParticipant participant,
        Vector3 origin,
        float safeRadius,
        StatItemData item,
        bool lowPowerSkirmish)
    {
        if (participant.actor == null || item == null)
        {
            return;
        }

        if (participant.kind == ActorKind.Villager)
        {
            participant.actor.GetComponent<VillagerAI>()?.ForceTreasureWait(origin, safeRadius, item, lowPowerSkirmish);
            return;
        }

        if (participant.kind == ActorKind.SmartNpc)
        {
            participant.actor.GetComponent<SmartNpcAI>()?.ForceTreasureWait(origin, safeRadius, item, lowPowerSkirmish);
            return;
        }

        participant.actor.GetComponent<MonsterAI>()?.ForceTreasureWait(origin, safeRadius, item, lowPowerSkirmish);
    }

    void UpdateEvent(TreasureFrenzyEvent frenzyEvent)
    {
        CleanupParticipants(frenzyEvent);

        if (frenzyEvent.participants.Count == 0)
        {
            EndEvent(frenzyEvent);
            return;
        }

        if (frenzyEvent.waitingForLightning)
        {
            UpdateLightningPhase(frenzyEvent);
            return;
        }

        GameObject holder = FindHolder(frenzyEvent);
        if (holder != null)
        {
            if (frenzyEvent.holder != holder)
            {
                frenzyEvent.holder = holder;
                frenzyEvent.holderStartHour = GetAbsoluteWorldHour();
                AnnounceWorld(frenzyEvent, true);
            }

            if (GetAbsoluteWorldHour() - frenzyEvent.holderStartHour >= holderPeaceGameHours)
            {
                EndEvent(frenzyEvent);
                return;
            }
        }
        else
        {
            frenzyEvent.holder = null;
            frenzyEvent.holderStartHour = -1f;
        }

        Transform target = holder != null ? holder.transform : GetTreasureTarget(frenzyEvent);
        foreach (TreasureParticipant participant in frenzyEvent.participants)
        {
            if (participant.actor == null || participant.coward)
            {
                continue;
            }

            if (holder != null && participant.actor == holder)
            {
                continue;
            }

            ForceHunt(participant, target, frenzyEvent.item);
        }

        if (frenzyEvent.slaughter)
        {
            ResolveFrenzyCombat(frenzyEvent, holder);
        }
    }


    float GetImmortalLightningStartDelay()
    {
        return Mathf.Max(0f, HeavenGiftPlacementController.GetImmortalLightningStartDelay());
    }

    float GetImmortalLightningDuration()
    {
        return Mathf.Max(
            Mathf.Max(0f, immortalLightningDuration),
            HeavenGiftPlacementController.GetImmortalLightningTotalDuration());
    }
    void UpdateLightningPhase(TreasureFrenzyEvent frenzyEvent)
    {
        CommandLightningWait(frenzyEvent);

        if (Time.time >= frenzyEvent.lightningStartTime &&
            Time.time >= frenzyEvent.nextLightningStrikeTime)
        {
            frenzyEvent.nextLightningStrikeTime =
                Time.time + Mathf.Max(0.1f, immortalLightningStrikeInterval);
            StrikeLightningZone(frenzyEvent);
        }

        ResolveOuterSkirmish(frenzyEvent);

        if (Time.time < frenzyEvent.lightningEndTime)
        {
            return;
        }

        frenzyEvent.waitingForLightning = false;
        if (WorldEventManager.Instance != null && frenzyEvent.item != null)
        {
            WorldEventManager.Instance.AddLog(
                "Thien loi tan, " + frenzyEvent.item.itemName + " co the tranh doat.",
                2);
        }

        CommandParticipants(frenzyEvent);
    }

    void StrikeLightningZone(TreasureFrenzyEvent frenzyEvent)
    {
        for (int i = 0; i < frenzyEvent.participants.Count; i++)
        {
            TreasureParticipant participant = frenzyEvent.participants[i];
            if (participant.actor == null || IsDead(participant.actor, participant.kind))
            {
                continue;
            }

            float distance = Vector2.Distance(
                participant.actor.transform.position,
                frenzyEvent.origin);

            if (distance > immortalLightningDangerRadius)
            {
                continue;
            }

            NpcRoleUtility.Damage(
                null,
                participant.actor,
                immortalLightningDamage,
                "thien loi");
        }
    }

    void ResolveOuterSkirmish(TreasureFrenzyEvent frenzyEvent)
    {
        for (int i = 0; i < frenzyEvent.participants.Count; i++)
        {
            TreasureParticipant attacker = frenzyEvent.participants[i];
            if (attacker.actor == null ||
                attacker.coward ||
                IsDead(attacker.actor, attacker.kind) ||
                GetPower(attacker.actor, attacker.kind) > lowPowerOuterSkirmishThreshold)
            {
                continue;
            }

            TreasureParticipant victim = FindNearestOuterSkirmishTarget(attacker, frenzyEvent);
            if (victim == null || victim.actor == null)
            {
                continue;
            }

            if (Vector2.Distance(attacker.actor.transform.position, victim.actor.transform.position) > attackRange)
            {
                continue;
            }

            NpcRoleUtility.Damage(
                attacker.actor,
                victim.actor,
                outerSkirmishDamage,
                "tranh doat vong ngoai");
        }
    }

    TreasureParticipant FindNearestOuterSkirmishTarget(
        TreasureParticipant attacker,
        TreasureFrenzyEvent frenzyEvent)
    {
        TreasureParticipant best = null;
        float bestDistance = float.MaxValue;

        bool attackerIsMonster = attacker.kind == ActorKind.Monster;
        for (int i = 0; i < frenzyEvent.participants.Count; i++)
        {
            TreasureParticipant candidate = frenzyEvent.participants[i];
            if (candidate.actor == null ||
                candidate.actor == attacker.actor ||
                candidate.coward ||
                IsDead(candidate.actor, candidate.kind) ||
                GetPower(candidate.actor, candidate.kind) > lowPowerOuterSkirmishThreshold)
            {
                continue;
            }

            bool candidateIsMonster = candidate.kind == ActorKind.Monster;
            if (attackerIsMonster == candidateIsMonster)
            {
                continue;
            }

            float distance = Vector2.Distance(
                attacker.actor.transform.position,
                candidate.actor.transform.position);

            if (distance >= bestDistance)
            {
                continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }
    void ResolveFrenzyCombat(TreasureFrenzyEvent frenzyEvent, GameObject holder)
    {
        int damage = frenzyEvent.item.grade == ItemGrade.Tien
            ? immortalGradeDamage
            : upperGradeDamage;

        foreach (TreasureParticipant attacker in frenzyEvent.participants)
        {
            if (attacker.actor == null || attacker.coward || IsDead(attacker.actor, attacker.kind))
            {
                continue;
            }

            GameObject victim = holder != null && holder != attacker.actor
                ? holder
                : FindNearestRival(attacker, frenzyEvent);

            if (victim == null)
            {
                continue;
            }

            float distance = Vector2.Distance(attacker.actor.transform.position, victim.transform.position);
            if (distance > attackRange)
            {
                continue;
            }

            IDamageable damageable = victim.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                int modifiedDamage =
                    NpcCombatTechniqueSystem.ModifyOutgoingDamage(
                        attacker.actor,
                        victim,
                        damage);

                damageable.TakeDamage(modifiedDamage);
            }
        }
    }

    GameObject FindNearestRival(
        TreasureParticipant attacker,
        TreasureFrenzyEvent frenzyEvent)
    {
        GameObject best = null;
        float bestDistance = float.MaxValue;

        foreach (TreasureParticipant participant in frenzyEvent.participants)
        {
            if (participant.actor == null || participant.actor == attacker.actor || participant.coward)
            {
                continue;
            }

            if (IsDead(participant.actor, participant.kind))
            {
                continue;
            }

            float distance = Vector2.Distance(
                attacker.actor.transform.position,
                participant.actor.transform.position);

            if (distance >= bestDistance)
            {
                continue;
            }

            best = participant.actor;
            bestDistance = distance;
        }

        return best;
    }

    GameObject FindHolder(TreasureFrenzyEvent frenzyEvent)
    {
        foreach (TreasureParticipant participant in frenzyEvent.participants)
        {
            if (participant.actor == null)
            {
                continue;
            }

            ItemInventory inventory = participant.actor.GetComponent<ItemInventory>();
            if (inventory != null && inventory.GetAmount(frenzyEvent.item) > 0)
            {
                return participant.actor;
            }
        }

        return null;
    }

    void CleanupParticipants(TreasureFrenzyEvent frenzyEvent)
    {
        for (int i = frenzyEvent.participants.Count - 1; i >= 0; i--)
        {
            TreasureParticipant participant = frenzyEvent.participants[i];
            if (participant.actor == null || IsDead(participant.actor, participant.kind))
            {
                frenzyEvent.participants.RemoveAt(i);
            }
        }
    }

    void EndEvent(TreasureFrenzyEvent frenzyEvent)
    {
        foreach (TreasureParticipant participant in frenzyEvent.participants)
        {
            ClearFrenzy(participant);
        }

        activeEvents.Remove(frenzyEvent);

        if (WorldEventManager.Instance != null && frenzyEvent.item != null)
        {
            WorldEventManager.Instance.AddLog(
                frenzyEvent.item.itemName + " tranh doat da lang xuong.",
                frenzyEvent.item.grade == ItemGrade.Tien ? 2 : 1);
        }
    }

    Transform GetTreasureTarget(TreasureFrenzyEvent frenzyEvent)
    {
        return frenzyEvent.pickup != null
            ? frenzyEvent.pickup.transform
            : null;
    }

    void ForceHunt(
        TreasureParticipant participant,
        Transform target,
        StatItemData item)
    {
        if (target == null || item == null || participant.actor == null)
        {
            return;
        }

        if (participant.kind == ActorKind.Villager)
        {
            participant.actor.GetComponent<VillagerAI>()?.ForceTreasureHunt(target, item);
            return;
        }

        if (participant.kind == ActorKind.SmartNpc)
        {
            participant.actor.GetComponent<SmartNpcAI>()?.ForceTreasureHunt(target, item);
            return;
        }

        participant.actor.GetComponent<MonsterAI>()?.ForceTreasureHunt(target, item);
    }

    void ClearFrenzy(TreasureParticipant participant)
    {
        if (participant.actor == null)
        {
            return;
        }

        if (participant.kind == ActorKind.Monster)
        {
            participant.actor.GetComponent<MonsterAI>()?.ClearTreasureHunt();
            return;
        }

        if (participant.kind == ActorKind.Villager)
        {
            participant.actor.GetComponent<VillagerAI>()?.ClearTreasureHunt();
            return;
        }

        if (participant.kind == ActorKind.SmartNpc)
        {
            participant.actor.GetComponent<SmartNpcAI>()?.ClearTreasureHunt();
            return;
        }

        NpcRoleUtility.SetAction(participant.actor, "Binh tinh tro lai");
    }

    void MarkCoward(TreasureParticipant participant, TreasureFrenzyEvent frenzyEvent)
    {
        if (participant.actor == null)
        {
            return;
        }

        NpcRoleUtility.SetAction(participant.actor, "Hoang so tranh xa " + frenzyEvent.item.itemName);
    }

    void AnnounceWorld(TreasureFrenzyEvent frenzyEvent, bool holderChanged)
    {
        if (WorldEventManager.Instance == null || frenzyEvent.item == null)
        {
            return;
        }

        if (holderChanged && frenzyEvent.holder != null)
        {
            WorldEventManager.Instance.AddLog(
                GetActorName(frenzyEvent.holder) + " cuop duoc " + frenzyEvent.item.itemName +
                ", neu giu duoc 1 gio se thoat khoi tranh doat.",
                frenzyEvent.item.grade == ItemGrade.Tien ? 2 : 1);
            return;
        }

        WorldEventManager.Instance.AddLog(
            frenzyEvent.item.itemName + " xuat the, tin tuc truyen khap cac map. " +
            frenzyEvent.participants.Count + " ke bi hap dan lao vao tranh doat.",
            frenzyEvent.item.grade == ItemGrade.Tien ? 2 : 1);
    }

    float GetSignalRadius(StatItemData item)
    {
        switch (item.grade)
        {
            case ItemGrade.Tien:
                return immortalGradeRadius;

            case ItemGrade.Thuong:
                return upperGradeRadius;

            case ItemGrade.Trung:
                return 12f;

            default:
                return 8f;
        }
    }

    float GetCrossMapThreshold(StatItemData item)
    {
        if (item.grade == ItemGrade.Tien)
        {
            return 55f;
        }

        if (item.grade == ItemGrade.Thuong)
        {
            return 85f;
        }

        return float.MaxValue;
    }

    float GetPower(GameObject actor, ActorKind kind)
    {
        if (kind == ActorKind.Monster)
        {
            MonsterAI monster = actor.GetComponent<MonsterAI>();
            return monster != null ? monster.GetRealmPower() : 1f;
        }

        if (kind == ActorKind.SmartNpc)
        {
            SmartNpcAI npc = actor.GetComponent<SmartNpcAI>();
            return npc != null ? npc.GetRealmPower() : 1f;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager == null)
        {
            return 1f;
        }

        return ((int)villager.realm * CultivationProgression.MaxStage) +
            Mathf.Clamp(villager.realmStage, 1, CultivationProgression.MaxStage) +
            villager.attack + villager.defense;
    }

    bool IsDead(GameObject actor, ActorKind kind)
    {
        if (actor == null)
        {
            return true;
        }

        if (kind == ActorKind.Monster)
        {
            MonsterAI monster = actor.GetComponent<MonsterAI>();
            return monster == null || monster.IsDead;
        }

        if (kind == ActorKind.SmartNpc)
        {
            SmartNpcAI npc = actor.GetComponent<SmartNpcAI>();
            return npc == null || npc.IsDead;
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        return villager == null || villager.IsDead;
    }

    string GetActorName(GameObject actor)
    {
        if (actor == null)
        {
            return "Vo danh";
        }

        VillagerAI villager = actor.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.villagerName;
        }

        SmartNpcAI smartNpc = actor.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.npcName;
        }

        MonsterAI monster = actor.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.monsterName;
        }

        return actor.name;
    }

    float GetAbsoluteWorldHour()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            return Time.time / 60f;
        }

        int year = Mathf.Max(1, timeSystem.currentYear);
        int month = Mathf.Max(1, timeSystem.currentMonth);
        int day = Mathf.Max(1, timeSystem.currentDay);
        int absoluteDay = (year - 1) * 360 + (month - 1) * 30 + (day - 1);
        return absoluteDay * 24f + Mathf.Max(0f, timeSystem.currentHour);
    }

    enum ActorKind
    {
        Villager,
        SmartNpc,
        Monster
    }

    class TreasureParticipant
    {
        public GameObject actor;
        public ActorKind kind;
        public float desire;
        public float fear;
        public bool coward;
    }

    class TreasureFrenzyEvent
    {
        public WorldStatItemPickup pickup;
        public StatItemData item;
        public Vector3 origin;
        public float radius;
        public bool slaughter;
        public float startHour;
        public GameObject holder;
        public float holderStartHour;
        public bool waitingForLightning;
        public float lightningStartTime;
        public float lightningEndTime;
        public float nextLightningStrikeTime;
        public readonly List<TreasureParticipant> participants =
            new List<TreasureParticipant>();
    }
}
