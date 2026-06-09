using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeavenlyTribulationSystem : MonoBehaviour
{
    public static HeavenlyTribulationSystem Instance { get; private set; }

    static readonly Dictionary<int, float> pillProtectionUntil =
        new Dictionary<int, float>();

    [Header("Tribulation")]
    public int lightningCount = 9;
    public float lightningInterval = 0.45f;
    public float strikeRadius = 1.8f;
    public float openAreaSearchRadius = 8f;
    public float openAreaClearRadius = 0.9f;
    public int damagePerStrike = 35;
    public int finalStrikeDamage = 70;
    [Range(0f, 1f)] public float breakthroughPillDamageReduction = 0.3f;
    public float pillProtectionDuration = 30f;

    [Header("Prefab Thiên Kiếp Mới")]
    public bool useStrikePrefab = true;
    public ThienKiepStrikePrefab strikePrefab;
    public bool useFallbackIfNoPrefab = true;

    [Header("Visual Fallback Cũ")]
    public float cloudHeight = 3.2f;
    public float boltLife = 0.22f;
    public float boltWidth = 0.1f;
    public Color boltCoreColor = Color.white;
    public Color boltOuterColor = new Color(0.25f, 0.85f, 1f, 1f);

    static Material cachedLineMaterial;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void Request(
        GameObject target,
        string displayName,
        CultivationRealm targetRealm,
        Action onPassed)
    {
        if (target == null)
        {
            return;
        }

        HeavenlyTribulationSystem system = Instance;

        if (system == null)
        {
            GameObject systemObject = new GameObject("Heavenly Tribulation System");
            system = systemObject.AddComponent<HeavenlyTribulationSystem>();
        }

        system.StartCoroutine(
            system.RunTribulation(
                target,
                displayName,
                targetRealm,
                onPassed));
    }

    public static void MarkPillProtectionIfEligible(
        GameObject target,
        StatItemData item)
    {
        if (target == null || item == null)
        {
            return;
        }

        bool isProtectionPill =
            item.itemType == ItemType.DanDuoc &&
            (item.pillKind == PillKind.Breakthrough ||
            item.pillKind == PillKind.TribulationProtection ||
            item.breakthroughRealm);

        if (!isProtectionPill)
        {
            return;
        }

        HeavenlyTribulationSystem system = Instance;

        float duration = system != null
            ? system.pillProtectionDuration
            : 30f;

        pillProtectionUntil[target.GetHashCode()] =
            Time.time + Mathf.Max(1f, duration);
    }

    IEnumerator RunTribulation(
        GameObject target,
        string displayName,
        CultivationRealm targetRealm,
        Action onPassed)
    {
        if (target == null)
        {
            yield break;
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();

        if (damageable == null || damageable.IsDead)
        {
            yield break;
        }

        TribulationRuntime runtime = BuildRuntime(target, targetRealm);

        Vector3 center = FindOpenArea(target.transform.position, target);

        MoveTargetToCenter(target, center);

        AddWorldLog(
            displayName + " dẫn động Thiên Kiếp, chuẩn bị đột phá " +
            NpcText.Realm(targetRealm) + ".",
            2);

        yield return PlayCloudGathering(center, runtime);

        yield return new WaitForSeconds(0.35f);

        HeavenSystem heaven = HeavenSystem.Instance;
        int count = Mathf.Max(1, runtime.lightningCount);

        for (int i = 0; i < count; i++)
        {
            if (target == null || damageable.IsDead)
            {
                yield break;
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle * strikeRadius;
            Vector3 strikePosition = center + (Vector3)offset;

            Strike(heaven, strikePosition, runtime.damagePerStrike);

            yield return new WaitForSeconds(Mathf.Max(0.05f, lightningInterval));
        }

        if (target == null || damageable.IsDead)
        {
            yield break;
        }

        Strike(heaven, target.transform.position, runtime.finalStrikeDamage);

        yield return new WaitForSeconds(0.1f);

        if (target == null || damageable.IsDead)
        {
            AddWorldLog(displayName + " thất bại dưới Thiên Kiếp.", 2);
            yield break;
        }

        onPassed?.Invoke();

        AddWorldLog(
            displayName + " vượt qua Thiên Kiếp, đột phá " +
            NpcText.Realm(targetRealm) + ".",
            1);
    }

    struct TribulationRuntime
    {
        public int lightningCount;
        public int damagePerStrike;
        public int finalStrikeDamage;
        public bool usedProtectionPill;
        public float talentFactor;
    }

    TribulationRuntime BuildRuntime(
        GameObject target,
        CultivationRealm targetRealm)
    {
        float talentFactor = GetTalentFactor(target);
        float realmFactor = 1f + Mathf.Max(0, (int)targetRealm) * 0.12f;

        bool protectedByPill = ConsumePillProtection(target);

        float pillMultiplier = protectedByPill
            ? 1f - Mathf.Clamp01(breakthroughPillDamageReduction)
            : 1f;

        return new TribulationRuntime
        {
            lightningCount = Mathf.Clamp(
                Mathf.RoundToInt(lightningCount * talentFactor * realmFactor),
                3,
                36),

            damagePerStrike = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    damagePerStrike *
                    (0.75f + talentFactor * 0.55f) *
                    realmFactor *
                    pillMultiplier)),

            finalStrikeDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    finalStrikeDamage *
                    (0.75f + talentFactor * 0.55f) *
                    realmFactor *
                    pillMultiplier)),

            usedProtectionPill = protectedByPill,
            talentFactor = talentFactor
        };
    }

    float GetTalentFactor(GameObject target)
    {
        float score = 20f;

        EntityProfile profile = target.GetComponent<EntityProfile>();

        if (profile != null && profile.talent != null)
        {
            score = Mathf.Max(
                score,
                profile.talent.comprehension +
                Mathf.Max(0f, profile.talent.cultivationSpeed - 1f) * 25f +
                Mathf.Max(0f, profile.talent.combatMultiplier - 1f) * 20f +
                Mathf.Max(0f, profile.talent.luck - 1f) * 15f);
        }

        SmartNpcAI smartNpc = target.GetComponent<SmartNpcAI>();

        if (smartNpc != null)
        {
            score = Mathf.Max(score, smartNpc.comprehension);
        }

        MonsterAI monster = target.GetComponent<MonsterAI>();

        if (monster != null)
        {
            score = Mathf.Max(
                score,
                monster.beastInstinct * 0.75f +
                monster.aggression * 0.25f);
        }

        return 1f + Mathf.Clamp(score, 0f, 100f) / 100f;
    }

    bool ConsumePillProtection(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        int key = target.GetHashCode();

        if (!pillProtectionUntil.TryGetValue(key, out float expiresAt))
        {
            return false;
        }

        pillProtectionUntil.Remove(key);

        return Time.time <= expiresAt;
    }

    IEnumerator PlayCloudGathering(
        Vector3 center,
        TribulationRuntime runtime)
    {
        GameObject cloudObject = new GameObject("Tribulation Cloud Ring");
        LineRenderer cloud = cloudObject.AddComponent<LineRenderer>();

        SetupLineRenderer(
            cloud,
            true,
            48,
            0.08f,
            0.08f,
            runtime.usedProtectionPill
                ? new Color(0.25f, 0.95f, 0.65f, 0.85f)
                : new Color(0.45f, 0.18f, 0.85f, 0.85f),
            runtime.usedProtectionPill
                ? new Color(0.25f, 0.95f, 0.65f, 0.85f)
                : new Color(0.45f, 0.18f, 0.85f, 0.85f),
            95);

        cloud.loop = true;

        float radius = strikeRadius * Mathf.Clamp(runtime.talentFactor, 1f, 2f);
        Vector3 cloudCenter = center + Vector3.up * cloudHeight;

        for (int i = 0; i < cloud.positionCount; i++)
        {
            float angle = i / (float)cloud.positionCount * Mathf.PI * 2f;
            float wobble = UnityEngine.Random.Range(-0.12f, 0.12f);

            cloud.SetPosition(
                i,
                cloudCenter +
                new Vector3(
                    Mathf.Cos(angle) * (radius + wobble),
                    Mathf.Sin(angle) * (radius * 0.28f + wobble),
                    0f));
        }

        yield return new WaitForSeconds(0.45f);

        if (cloudObject != null)
        {
            Destroy(cloudObject);
        }
    }

    void Strike(HeavenSystem heaven, Vector3 position, int damage)
    {
        float radius = heaven != null
            ? heaven.punishmentRadius
            : 1.2f;

        LayerMask damageLayers = heaven != null
            ? heaven.damageLayers
            : ~0;

        PlayStrikeVisual(position);

        ApplyStrikeDamage(position, radius, damageLayers, damage);
    }

    void PlayStrikeVisual(Vector3 position)
    {
        if (useStrikePrefab && strikePrefab != null)
        {
            ThienKiepStrikePrefab strike =
                Instantiate(strikePrefab, position, Quaternion.identity);

            LayerMask noDamageLayers = 0;

            strike.Play(0, noDamageLayers);

            return;
        }

        if (useFallbackIfNoPrefab)
        {
            StartCoroutine(PlayFallbackLightning(position));
        }
        else
        {
            Debug.LogWarning(
                "HeavenlyTribulationSystem chưa gán Strike Prefab và fallback đang tắt.",
                gameObject);
        }
    }

    void ApplyStrikeDamage(
        Vector3 position,
        float radius,
        LayerMask damageLayers,
        int damage)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            radius,
            damageLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();

            if (damageable == null || damageable.IsDead)
            {
                continue;
            }

            damageable.TakeDamage(Mathf.Max(1, damage));
        }
    }

    IEnumerator PlayFallbackLightning(Vector3 position)
    {
        GameObject lightningObject =
            new GameObject("Heavenly Tribulation Lightning");

        if (lightningObject == null)
        {
            yield break;
        }

        LineRenderer outer = lightningObject.AddComponent<LineRenderer>();
        LineRenderer core = lightningObject.AddComponent<LineRenderer>();

        if (outer == null || core == null)
        {
            if (lightningObject != null)
            {
                Destroy(lightningObject);
            }

            yield break;
        }

        int pointCount = 8;

        SetupLineRenderer(
            outer,
            true,
            pointCount,
            boltWidth * 1.8f,
            boltWidth * 0.45f,
            boltOuterColor,
            boltOuterColor,
            120);

        SetupLineRenderer(
            core,
            true,
            pointCount,
            boltWidth,
            boltWidth * 0.25f,
            boltCoreColor,
            boltCoreColor,
            121);

        Vector3 top = position + Vector3.up * cloudHeight;

        for (int i = 0; i < pointCount; i++)
        {
            float progress = i / Mathf.Max(1f, pointCount - 1f);
            Vector3 point = Vector3.Lerp(top, position, progress);

            point.x += UnityEngine.Random.Range(-0.26f, 0.26f) *
                Mathf.Lerp(1f, 0.2f, progress);

            outer.SetPosition(i, point);
            core.SetPosition(i, point);
        }

        for (int i = 2; i < pointCount - 2; i += 2)
        {
            if (outer != null && lightningObject != null)
            {
                PlayLightningBranch(lightningObject, outer.GetPosition(i));
            }
        }

        PlayImpactRing(position);

        yield return new WaitForSeconds(Mathf.Max(0.05f, boltLife));

        if (lightningObject != null)
        {
            Destroy(lightningObject);
        }
    }

    void PlayLightningBranch(GameObject parent, Vector3 start)
    {
        if (parent == null)
        {
            return;
        }

        GameObject branchObject = new GameObject("Lightning Branch");
        branchObject.transform.SetParent(parent.transform, true);

        LineRenderer branch = branchObject.AddComponent<LineRenderer>();

        if (branch == null)
        {
            Destroy(branchObject);
            return;
        }

        SetupLineRenderer(
            branch,
            true,
            3,
            boltWidth * 0.45f,
            boltWidth * 0.1f,
            boltOuterColor,
            boltCoreColor,
            119);

        Vector3 end = start + new Vector3(
            UnityEngine.Random.Range(-0.65f, 0.65f),
            UnityEngine.Random.Range(-0.35f, 0.15f),
            0f);

        branch.SetPosition(0, start);

        branch.SetPosition(
            1,
            Vector3.Lerp(start, end, 0.5f) +
            new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), 0f, 0f));

        branch.SetPosition(2, end);
    }

    void PlayImpactRing(Vector3 position)
    {
        GameObject ringObject = new GameObject("Lightning Impact Ring");

        if (ringObject == null)
        {
            return;
        }

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();

        if (ring == null)
        {
            Destroy(ringObject);
            return;
        }

        SetupLineRenderer(
            ring,
            true,
            28,
            0.04f,
            0.04f,
            boltOuterColor,
            Color.white,
            118);

        ring.loop = true;

        float radius = 0.32f;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;

            ring.SetPosition(
                i,
                position + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.35f,
                    0f));
        }

        Destroy(ringObject, boltLife);
    }

    void SetupLineRenderer(
        LineRenderer line,
        bool useWorldSpace,
        int positionCount,
        float startWidth,
        float endWidth,
        Color startColor,
        Color endColor,
        int sortingOrder)
    {
        if (line == null)
        {
            return;
        }

        line.useWorldSpace = useWorldSpace;
        line.positionCount = Mathf.Max(2, positionCount);
        line.startWidth = startWidth;
        line.endWidth = endWidth;
        line.startColor = startColor;
        line.endColor = endColor;
        line.sortingOrder = sortingOrder;

        Material material = GetLineMaterial();

        if (material != null)
        {
            line.material = material;
        }
    }

    Material GetLineMaterial()
    {
        if (cachedLineMaterial != null)
        {
            return cachedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            return null;
        }

        cachedLineMaterial = new Material(shader);
        cachedLineMaterial.name = "Runtime_Lightning_Line_Material";

        return cachedLineMaterial;
    }

    Vector3 FindOpenArea(Vector3 origin, GameObject target)
    {
        if (IsOpen(origin, target))
        {
            return origin;
        }

        float maxRadius = Mathf.Max(1f, openAreaSearchRadius);

        for (int i = 0; i < 36; i++)
        {
            float radius = Mathf.Lerp(1f, maxRadius, i / 35f);
            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude <= 0.001f)
            {
                randomDirection = Vector2.right;
            }

            Vector2 offset = randomDirection.normalized * radius;
            Vector3 candidate = origin + new Vector3(offset.x, offset.y, 0f);

            if (IsOpen(candidate, target))
            {
                return candidate;
            }
        }

        return origin;
    }

    bool IsOpen(Vector3 position, GameObject target)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            Mathf.Max(0.1f, openAreaClearRadius));

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            if (target != null && hit.transform.IsChildOf(target.transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    void MoveTargetToCenter(GameObject target, Vector3 center)
    {
        if (target == null)
        {
            return;
        }

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = center;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        target.transform.position = center;
    }

    void AddWorldLog(string content, int colorType)
    {
        if (WorldEventManager.Instance != null)
        {
            WorldEventManager.Instance.AddLog(content, colorType);
        }
    }
}