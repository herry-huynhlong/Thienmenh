using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class HeavenGiftPlacementController : MonoBehaviour
{
    static HeavenGiftPlacementController instance;
    static Material effectMaterial;

    public float skyDropHeight = 6f;
    public float fallSpeed = 8f;
    public float pickupVisualSize = 0.45f;
    public float middleGradeLightDuration = 1.4f;
    public float middleGradeLightBoost = 0.35f;
    public int middleGradeGlowWaveCount = 4;
    public float middleGradeGlowWaveDuration = 2f;
    public float middleGradeGlowRadius = 0.9f;
    public int rainbowWaveCount = 10;
    public float rainbowWaveDuration = 5f;
    public float rainbowWaveInterval = 0.15f;
    public float rainbowRadius = 1.4f;
    public int immortalLightningCount = 49;
    public float immortalLightningInterval = 0.35f;
    public float immortalLightningRadius = 2.2f;
    public int immortalLightningDamage = 65;
    public float immortalLightningScale = 2.4f;
    [Header("Summon")]
    public GameObject[] upperGradeSummonPrefabs;
    public GameObject[] immortalGradeSummonPrefabs;
    [Range(0f, 1f)] public float upperGradeSummonChance = 0.5f;
    [Range(0f, 1f)] public float immortalGradeSummonChance = 0.9f;
    public float upperGradeSummonRadius = 3.5f;
    public float immortalGradeSummonRadius = 5f;
    public int upperGradeLightningCount = 19;
    public int upperGradeLightningDamage = 45;
    public float upperGradeLightningRadius = 1.9f;
    public float upperGradeLightningInterval = 0.25f;
    public float upperGradeLightningScale = 1.65f;
    ItemInventory sourceInventory;
    StatItemData pendingItem;
    bool waitingForPlacement;

    public static void BeginGift(ItemInventory inventory, StatItemData item)
    {
        if (inventory == null ||
            item == null)
        {
            return;
        }

        EnsureInstance().Begin(inventory, item);
    }

    static HeavenGiftPlacementController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject controllerObject =
            new GameObject("HeavenGiftPlacementController");

        DontDestroyOnLoad(controllerObject);
        instance =
            controllerObject.AddComponent<HeavenGiftPlacementController>();

        return instance;
    }

    public static float GetImmortalLightningStartDelay()
    {
        HeavenGiftPlacementController controller = EnsureInstance();
        return controller.skyDropHeight / Mathf.Max(0.1f, controller.fallSpeed);
    }

    public static float GetImmortalLightningTotalDuration()
    {
        HeavenGiftPlacementController controller = EnsureInstance();
        int count = Mathf.Max(1, controller.immortalLightningCount);
        float rainbowDuration =
            controller.rainbowWaveCount *
            (controller.rainbowWaveDuration + controller.rainbowWaveInterval);

        return Mathf.Max(
            count * controller.immortalLightningInterval,
            rainbowDuration);
    }
    void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Begin(ItemInventory inventory, StatItemData item)
    {
        sourceInventory = inventory;
        pendingItem = item;
        waitingForPlacement = true;
    }

    void Update()
    {
        if (!waitingForPlacement)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceAtScreenPosition(Input.mousePosition);
            return;
        }

        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TryPlaceAtScreenPosition(Input.GetTouch(0).position);
        }
    }

    void TryPlaceAtScreenPosition(Vector2 screenPosition)
    {
        if (IsPointerOverUI())
        {
            return;
        }

        if (sourceInventory == null ||
            pendingItem == null ||
            sourceInventory.GetAmount(pendingItem) <= 0)
        {
            CancelPlacement();
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 worldPosition =
            CameraWorldPlaneUtility.ScreenToWorldOnPlane(
                camera,
                screenPosition);

        if (!sourceInventory.RemoveItem(pendingItem, 1))
        {
            CancelPlacement();
            return;
        }

        DropGift(worldPosition, pendingItem);
        CancelPlacement();
    }

    void DropGift(Vector3 targetPosition, StatItemData item)
    {
        Vector3 spawnPosition =
            targetPosition + Vector3.up * skyDropHeight;
        GameObject sourceActor =
            ResolveLightningSourceActor(
                sourceInventory != null
                    ? sourceInventory.gameObject
                    : null);

        HeavenSystem heavenSystem =
            HeavenSystem.Instance;

        WorldStatItemPickup pickup =
            heavenSystem != null
            ? heavenSystem.DropItemAt(spawnPosition, item, 1)
            : CreateFallbackPickup(spawnPosition, item);

        if (pickup == null)
        {
            return;
        }

        pickup.trackReceiverInHeavenNurture = true;

        HeavenGiftEffectSession session =
            new HeavenGiftEffectSession();
        session.sourceActor = sourceActor;

        pickup.OnDepleted += session.Cancel;

        EnsurePickupVisual(pickup, item);
        LogHeavenGift(item);
        PlayStartEffect(item, targetPosition, session);
        TreasureFrenzySystem.AnnounceTreasure(pickup, item, targetPosition);

        SkyDropToPosition dropMovement =
            pickup.GetComponent<SkyDropToPosition>();

        if (dropMovement == null)
        {
            dropMovement =
                pickup.gameObject.AddComponent<SkyDropToPosition>();
        }

        dropMovement.Setup(
            targetPosition,
            fallSpeed,
            () => PlayLandedEffect(item, targetPosition, session));
    }

    void PlayStartEffect(
        StatItemData item,
        Vector3 targetPosition,
        HeavenGiftEffectSession session)
    {
        if (item == null ||
            session == null ||
            !session.IsActive)
        {
            return;
        }

        switch (item.grade)
        {
            case ItemGrade.Trung:
                StartCoroutine(
                    PlayRainbowAuraWaves(
                        targetPosition,
                        session,
                        GetRainbowPalette(7)));
                break;

            case ItemGrade.Thuong:
                StartCoroutine(
                    PlayRainbowAuraWaves(
                        targetPosition,
                        session,
                        GetRainbowPalette(5)));
                break;

            case ItemGrade.Tien:
                StartCoroutine(
                    PlayRainbowAuraWaves(
                        targetPosition,
                        session,
                        GetRainbowPalette(7)));
                break;
        }
    }

    void PlayLandedEffect(
        StatItemData item,
        Vector3 targetPosition,
        HeavenGiftEffectSession session)
    {
        if (item == null ||
            session == null ||
            !session.IsActive ||
            (item.grade != ItemGrade.Thuong &&
            item.grade != ItemGrade.Tien))
        {
            return;
        }

        if (item.grade == ItemGrade.Thuong)
        {
            StartCoroutine(
                PlayLightningSummonSequence(
                    targetPosition,
                    session,
                    upperGradeLightningCount,
                    upperGradeLightningDamage,
                    upperGradeLightningRadius,
                    upperGradeLightningScale,
                    item.grade,
                    upperGradeSummonChance,
                    upperGradeSummonPrefabs,
                    CultivationRealm.NascentSoul,
                    upperGradeSummonRadius,
                    "Nguyen Anh"));
            return;
        }

        StartCoroutine(
            PlayLightningSummonSequence(
                    targetPosition,
                    session,
                    immortalLightningCount,
                    immortalLightningDamage,
                    immortalLightningRadius,
                    immortalLightningScale,
                    item.grade,
                    immortalGradeSummonChance,
                    immortalGradeSummonPrefabs,
                    CultivationRealm.SoulFormation,
                    immortalGradeSummonRadius,
                    "duoi Hoa Than"));
    }

    bool IsNight()
    {
        WorldTimeSystem timeSystem =
            WorldTimeSystem.Instance;

        if (timeSystem == null)
        {
            return false;
        }

        float hour = timeSystem.CurrentHour;
        return hour >= 19f || hour < 6f;
    }

    IEnumerator FlashNightSky(HeavenGiftEffectSession session)
    {
        if (session == null ||
            !session.IsActive)
        {
            yield break;
        }

        DayNightLightingSystem lighting =
            DayNightLightingSystem.Instance;

        if (lighting == null)
        {
            yield break;
        }

        Light2D globalLight =
            lighting.globalLight;

        if (globalLight == null)
        {
            yield break;
        }

        float startIntensity =
            globalLight.intensity;

        Color startColor =
            globalLight.color;

        float peakIntensity =
            Mathf.Min(1.2f, startIntensity + middleGradeLightBoost);

        float halfDuration =
            Mathf.Max(0.05f, middleGradeLightDuration * 0.5f);

        for (float t = 0f; t < halfDuration && session.IsActive; t += Time.deltaTime)
        {
            float progress = t / halfDuration;
            globalLight.intensity =
                Mathf.Lerp(startIntensity, peakIntensity, progress);
            globalLight.color =
                Color.Lerp(startColor, Color.white, progress);
            yield return null;
        }

        for (float t = 0f; t < halfDuration && session.IsActive; t += Time.deltaTime)
        {
            float progress = t / halfDuration;
            globalLight.intensity =
                Mathf.Lerp(peakIntensity, startIntensity, progress);
            globalLight.color =
                Color.Lerp(Color.white, startColor, progress);
            yield return null;
        }

        globalLight.intensity = startIntensity;
        globalLight.color = startColor;
    }

    IEnumerator PlayMiddleGradeGlowWaves(
        Vector3 center,
        HeavenGiftEffectSession session)
    {
        if (session == null ||
            !session.IsActive)
        {
            yield break;
        }

        int waves =
            Mathf.Max(1, middleGradeGlowWaveCount);

        for (int i = 0; i < waves && session.IsActive; i++)
        {
            yield return PlayMiddleGradeGlowWave(center, session);

            if (i < waves - 1)
            {
                yield return new WaitForSeconds(0.12f);
            }
        }
    }

    IEnumerator PlayMiddleGradeGlowWave(
        Vector3 center,
        HeavenGiftEffectSession session)
    {
        GameObject glowObject =
            new GameObject("Heaven Middle Grade Glow");

        glowObject.transform.position = center;
        session.Register(glowObject);

        LineRenderer ring =
            CreateRingRenderer(
                glowObject,
                new Color(1f, 0.82f, 0.22f, 1f),
                0);

        ring.startWidth = 0.035f;
        ring.endWidth = 0.035f;

        float duration =
            Mathf.Max(0.1f, middleGradeGlowWaveDuration);

        for (float t = 0f; t < duration && session.IsActive; t += Time.deltaTime)
        {
            float progress =
                Mathf.Clamp01(t / duration);

            float radius =
                middleGradeGlowRadius *
                Mathf.Lerp(0.25f, 1.25f, progress);

            float alpha =
                Mathf.Sin(progress * Mathf.PI) * 0.7f;

            UpdateRing(
                ring,
                radius,
                new Color(1f, 0.82f, 0.22f, 1f),
                alpha);

            glowObject.transform.Rotate(0f, 0f, 35f * Time.deltaTime);
            yield return null;
        }

        session.DestroyEffect(glowObject);
    }

    IEnumerator PlayRainbowAuraWaves(
        Vector3 center,
        HeavenGiftEffectSession session,
        Color[] colors)
    {
        if (session == null ||
            !session.IsActive)
        {
            yield break;
        }

        Color[] palette = GetValidPalette(colors);
        int waves =
            Mathf.Max(1, rainbowWaveCount);

        for (int i = 0; i < waves && session.IsActive; i++)
        {
            yield return PlayRainbowAuraWave(center, session, palette);

            if (rainbowWaveInterval > 0f &&
                i < waves - 1)
            {
                yield return new WaitForSeconds(rainbowWaveInterval);
            }
        }
    }

    IEnumerator PlayRainbowAuraWave(
        Vector3 center,
        HeavenGiftEffectSession session,
        Color[] colors)
    {
        Color[] palette = GetValidPalette(colors);

        GameObject auraRoot =
            new GameObject("Heaven Rainbow Aura");

        auraRoot.transform.position = center;
        session.Register(auraRoot);

        LineRenderer[] rings =
            new LineRenderer[palette.Length];

        for (int i = 0; i < palette.Length; i++)
        {
            GameObject ringObject =
                new GameObject("Rainbow Ring " + i);

            ringObject.transform.SetParent(auraRoot.transform, false);
            rings[i] = CreateRingRenderer(ringObject, palette[i], i);
        }

        float duration =
            Mathf.Max(0.1f, rainbowWaveDuration);

        for (float t = 0f; t < duration && session.IsActive; t += Time.deltaTime)
        {
            float progress =
                Mathf.Clamp01(t / duration);

            for (int i = 0; i < rings.Length; i++)
            {
                float radius =
                    rainbowRadius *
                    Mathf.Lerp(0.2f, 1.75f, progress) +
                    i * 0.08f;

                float alpha =
                    Mathf.Sin(progress * Mathf.PI);

                UpdateRing(rings[i], radius, palette[i], alpha);
            }

            auraRoot.transform.Rotate(0f, 0f, 60f * Time.deltaTime);
            yield return null;
        }

        session.DestroyEffect(auraRoot);
    }

    LineRenderer CreateRingRenderer(
        GameObject owner,
        Color color,
        int index)
    {
        LineRenderer renderer =
            owner.AddComponent<LineRenderer>();

        renderer.useWorldSpace = false;
        renderer.loop = true;
        renderer.positionCount = 64;
        renderer.startWidth = 0.045f;
        renderer.endWidth = 0.045f;
        renderer.sortingOrder = 40 + index;
        renderer.material = GetEffectMaterial();

        UpdateRing(renderer, rainbowRadius, color, 1f);
        return renderer;
    }

    void UpdateRing(
        LineRenderer renderer,
        float radius,
        Color color,
        float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color resolvedColor = color;
        resolvedColor.a = alpha;
        renderer.startColor = resolvedColor;
        renderer.endColor = resolvedColor;

        for (int i = 0; i < renderer.positionCount; i++)
        {
            float angle =
                i / (float)renderer.positionCount *
                Mathf.PI *
                2f;

            renderer.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f));
        }
    }

    Color[] GetRainbowPalette(int colorCount)
    {
        Color[] fullPalette =
        {
            Color.red,
            new Color(1f, 0.5f, 0f),
            Color.yellow,
            Color.green,
            Color.cyan,
            Color.blue,
            new Color(0.65f, 0.25f, 1f)
        };

        int count = Mathf.Clamp(colorCount, 1, fullPalette.Length);
        Color[] palette = new Color[count];
        for (int i = 0; i < count; i++)
        {
            palette[i] = fullPalette[i];
        }

        return palette;
    }

    Color[] GetValidPalette(Color[] colors)
    {
        if (colors == null || colors.Length == 0)
        {
            return GetRainbowPalette(7);
        }

        return colors;
    }

    IEnumerator PlayLightningSummonSequence(
        Vector3 center,
        HeavenGiftEffectSession session,
        int strikeCount,
        int strikeDamage,
        float strikeRadius,
        float strikeScale,
        ItemGrade grade,
        float summonChance,
        GameObject[] summonPrefabs,
        CultivationRealm summonRealm,
        float summonRadius,
        string summonLabel)
    {
        if (session == null ||
            !session.IsActive)
        {
            yield break;
        }

        TrySummonTreasureBeast(
            center,
            summonChance,
            summonPrefabs,
            summonRealm,
            summonRadius,
            summonLabel);

        HeavenSystem heaven =
            HeavenSystem.Instance;

        int count =
            Mathf.Max(1, strikeCount);

        float interval =
            grade == ItemGrade.Tien
            ? Mathf.Max(0.12f, immortalLightningInterval)
            : Mathf.Max(0.12f, upperGradeLightningInterval);

        for (int i = 0; i < count && session.IsActive; i++)
        {
            float progress =
                i / Mathf.Max(1f, count - 1f);

            float strength =
                Mathf.Lerp(1f, 0.18f, progress);

            Vector2 offset =
                Random.insideUnitCircle *
                Mathf.Max(0.5f, strikeRadius) *
                Mathf.Lerp(1.1f, 0.55f, progress);

            Vector3 strikePosition =
                center + (Vector3)offset;

            if (heaven != null &&
                heaven.lightningEffectPrefab != null)
            {
                GameObject lightning =
                    Instantiate(
                        heaven.lightningEffectPrefab,
                        strikePosition,
                        Quaternion.identity);

                lightning.transform.localScale *=
                    strikeScale * strength;

                session.Register(lightning);
            }

            StartCoroutine(
                PlayFallbackLightning(
                    strikePosition,
                    strikeScale * strength,
                    session));

            StrikeLightningDamage(
                strikePosition,
                Mathf.Max(0.5f, strikeRadius),
                strikeDamage,
                session.sourceActor);

            yield return new WaitForSeconds(interval);
        }
    }

    void TrySummonTreasureBeast(
        Vector3 center,
        float summonChance,
        GameObject[] summonPrefabs,
        CultivationRealm summonRealm,
        float summonRadius,
        string summonLabel)
    {
        if (summonChance <= 0f ||
            Random.value > summonChance)
        {
            return;
        }

        Vector2 offset =
            Random.insideUnitCircle;

        if (offset.sqrMagnitude <= 0.0001f)
        {
            offset = Vector2.right;
        }

        offset.Normalize();
        offset *= Mathf.Max(0.75f, summonRadius) * Random.Range(0.45f, 0.9f);

        Vector3 spawnPosition =
            center + (Vector3)offset;

        GameObject beastObject =
            SpawnSummonedMonsterObject(
                summonPrefabs,
                spawnPosition);

        MonsterAI monster = null;
        if (beastObject != null)
        {
            monster = beastObject.GetComponent<MonsterAI>();
            if (monster == null)
            {
                monster = beastObject.AddComponent<MonsterAI>();
            }
        }

        if (monster == null)
        {
            return;
        }

        ConfigureSummonedMonster(
            monster,
            summonRealm,
            summonLabel);
    }

    GameObject SpawnSummonedMonsterObject(
        GameObject[] summonPrefabs,
        Vector3 spawnPosition)
    {
        GameObject prefab =
            PickSummonPrefab(summonPrefabs);

        if (prefab != null)
        {
            return Instantiate(prefab, spawnPosition, Quaternion.identity);
        }

        GameObject fallback =
            new GameObject("Summoned Beast");

        fallback.transform.position = spawnPosition;
        fallback.AddComponent<Rigidbody2D>();
        fallback.AddComponent<CapsuleCollider2D>().isTrigger = false;
        fallback.AddComponent<SpriteRenderer>();
        return fallback;
    }

    GameObject PickSummonPrefab(GameObject[] summonPrefabs)
    {
        if (summonPrefabs == null ||
            summonPrefabs.Length == 0)
        {
            return null;
        }

        List<GameObject> validPrefabs = new List<GameObject>();
        for (int i = 0; i < summonPrefabs.Length; i++)
        {
            if (summonPrefabs[i] != null)
            {
                validPrefabs.Add(summonPrefabs[i]);
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    void ConfigureSummonedMonster(
        MonsterAI monster,
        CultivationRealm summonRealm,
        string summonLabel)
    {
        if (monster == null)
        {
            return;
        }

        monster.generateFromEntityProfile = true;
        monster.autoStatsFromRealm = true;
        monster.syncBeastLevelFromRealm = true;
        monster.guardTerritory = true;
        monster.attackPlayer = true;
        monster.attackVillagers = true;
        monster.attackSmartNpcs = true;
        monster.attackOtherMonsters = false;
        monster.huntTargetType = HuntTargetType.Any;
        monster.realm = summonRealm;
        monster.realmStage = Random.Range(1, CultivationProgression.MaxStage + 1);
        monster.currentAction = "Diem linh " + summonLabel;

        if (string.IsNullOrEmpty(monster.monsterName))
        {
            monster.monsterName = "Yeu thu linh giang";
        }

        monster.name = monster.monsterName;

        EntityProfile profile =
            monster.GetComponent<EntityProfile>();

        if (profile == null)
        {
            profile =
                monster.gameObject.AddComponent<EntityProfile>();
        }

        profile.kind = EntityKind.Beast;
        if (!profile.lockGeneratedValues)
        {
            EntityGenerator.FillProfile(profile, EntityKind.Beast);
        }

        profile.lockGeneratedValues = true;
        profile.stats.realm = summonRealm;
        profile.stats.realmStage = monster.realmStage;
        profile.stats.currentHP = Mathf.Max(1, profile.stats.maxHP);
    }

    void StrikeLightningDamage(
        Vector3 position,
        float radius,
        int damage,
        GameObject attacker)
    {
        if (damage <= 0 ||
            radius <= 0f)
        {
            return;
        }

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(position, radius);
        HashSet<GameObject> damagedTargets =
            new HashSet<GameObject>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            GameObject targetRoot =
                ResolveLightningDamageTarget(hit);
            if (targetRoot == null ||
                damagedTargets.Contains(targetRoot) ||
                NpcRoleUtility.IsDead(targetRoot))
            {
                continue;
            }

            damagedTargets.Add(targetRoot);

            int hpBefore = ReadCurrentHp(targetRoot);
            DamageContext context = DamageContext.Environment(
                damage,
                attacker,
                this,
                DamageType.HeavenlyTribulation,
                "heaven_gift_lightning",
                position);
            DamageResult result = DamageSystem.Apply(
                targetRoot,
                context);
            int hpAfter = ReadCurrentHp(targetRoot);

            Debug.LogWarning(
                "[HeavenGiftLightning] target=" +
                NpcRoleUtility.GetDisplayName(targetRoot) +
                " damage=" +
                result.finalDamage +
                " hp=" +
                hpBefore +
                "->" +
                hpAfter +
                " pos=" +
                position);
        }
    }

    GameObject ResolveLightningDamageTarget(Collider2D hit)
    {
        if (hit == null)
        {
            return null;
        }

        Transform root =
            hit.GetComponentInParent<SmartNpcAI>()?.transform ??
            hit.GetComponentInParent<VillagerAI>()?.transform ??
            hit.GetComponentInParent<MonsterAI>()?.transform ??
            hit.GetComponentInParent<CharacterStats>()?.transform;

        return root != null ? root.gameObject : null;
    }

    GameObject ResolveLightningSourceActor(GameObject candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        Transform root =
            candidate.GetComponentInParent<SmartNpcAI>()?.transform ??
            candidate.GetComponentInParent<VillagerAI>()?.transform ??
            candidate.GetComponentInParent<MonsterAI>()?.transform ??
            candidate.GetComponentInParent<PlayerHealth>()?.transform ??
            candidate.GetComponentInParent<CharacterStats>()?.transform;

        return root != null
            ? root.gameObject
            : candidate;
    }

    int ReadCurrentHp(GameObject target)
    {
        if (target == null)
        {
            return -1;
        }

        CharacterStats stats =
            target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            return stats.currentHP;
        }

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            return smartNpc.currentHP;
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            return villager.currentHP;
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();
        if (monster != null)
        {
            return monster.currentHP;
        }

        return -1;
    }

    IEnumerator PlayFallbackLightning(
        Vector3 position,
        float scale,
        HeavenGiftEffectSession session)
    {
        if (session == null ||
            !session.IsActive)
        {
            yield break;
        }

        scale =
            Mathf.Max(0.5f, scale);

        GameObject lightningObject =
            new GameObject("Immortal Lightning");

        session.Register(lightningObject);

        LineRenderer renderer =
            lightningObject.AddComponent<LineRenderer>();

        renderer.useWorldSpace = true;
        renderer.positionCount = 8;
        renderer.startWidth = 0.08f * scale;
        renderer.endWidth = 0.02f * scale;
        renderer.startColor = Color.white;
        renderer.endColor = new Color(0.55f, 0.8f, 1f, 0.9f);
        renderer.sortingOrder = 80;
        renderer.material = GetEffectMaterial();

        Vector3 top =
            position + Vector3.up * 2.2f * scale;

        for (int i = 0; i < renderer.positionCount; i++)
        {
            float progress =
                i / (float)(renderer.positionCount - 1);

            Vector3 point =
                Vector3.Lerp(top, position, progress);

            if (i > 0 &&
                i < renderer.positionCount - 1)
            {
                point +=
                    new Vector3(
                        Random.Range(-0.22f, 0.22f) * scale,
                        0f,
                        0f);
            }

            renderer.SetPosition(i, point);
        }

        float duration =
            Mathf.Lerp(0.22f, 0.1f, Mathf.InverseLerp(0.5f, immortalLightningScale, scale));

        for (float t = 0f; t < duration && session.IsActive; t += Time.deltaTime)
        {
            float alpha =
                1f - t / duration;

            renderer.startColor =
                new Color(1f, 1f, 1f, alpha);

            renderer.endColor =
                new Color(0.55f, 0.8f, 1f, alpha * 0.8f);

            yield return null;
        }

        session.DestroyEffect(lightningObject);
    }

    Material GetEffectMaterial()
    {
        if (effectMaterial != null)
        {
            return effectMaterial;
        }

        Shader shader =
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader =
                Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader =
                Shader.Find("Unlit/Color");
        }

        effectMaterial =
            shader != null
            ? new Material(shader)
            : null;

        return effectMaterial;
    }

    WorldStatItemPickup CreateFallbackPickup(Vector3 position, StatItemData item)
    {
        GameObject itemObject =
            new GameObject("Heaven Gift - " + item.itemName);

        itemObject.transform.position = position;

        WorldStatItemPickup pickup =
            itemObject.AddComponent<WorldStatItemPickup>();

        pickup.item = item;
        pickup.amount = 1;
        pickup.allowNpcPickup = true;
        pickup.allowPlayerPickup = false;
        pickup.ConfigureAsDroppedWorldItem();

        return pickup;
    }

    void EnsurePickupVisual(WorldStatItemPickup pickup, StatItemData item)
    {
        if (pickup == null)
        {
            return;
        }

        CircleCollider2D collider =
            pickup.GetComponent<CircleCollider2D>();

        if (collider == null)
        {
            collider =
                pickup.gameObject.AddComponent<CircleCollider2D>();
        }

        collider.isTrigger = true;
        collider.radius = 0.25f;

        if (item == null ||
            item.icon == null)
        {
            return;
        }

        SpriteRenderer renderer =
            pickup.GetComponentInChildren<SpriteRenderer>();

        if (renderer == null)
        {
            renderer =
                pickup.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = item.icon;
        renderer.sortingOrder = 20;
        NormalizeRendererSize(renderer);
    }

    void NormalizeRendererSize(SpriteRenderer renderer)
    {
        if (renderer == null ||
            renderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize =
            renderer.sprite.bounds.size;

        float largestSide =
            Mathf.Max(spriteSize.x, spriteSize.y);

        if (largestSide <= 0f)
        {
            return;
        }

        float scale =
            Mathf.Max(0.05f, pickupVisualSize) /
            largestSide;

        renderer.transform.localScale =
            Vector3.one * scale;
    }

    void CancelPlacement()
    {
        waitingForPlacement = false;
        sourceInventory = null;
        pendingItem = null;
    }

    void LogHeavenGift(StatItemData item)
    {
        if (item == null ||
            WorldEventManager.Instance == null)
        {
            return;
        }

        string locationName =
            GetCurrentLocationName();

        string treasureName =
            GetHeavenGiftName(item);

        string message =
            "Dị tượng ở " +
            locationName +
            ", trời giáng " +
            treasureName +
            ".";

        int colorType =
            item.grade == ItemGrade.Tien
            ? 2
            : item.grade == ItemGrade.Thuong
                ? 1
                : 0;

        WorldEventManager.Instance.AddLog(message, colorType, true);
    }

    string GetCurrentLocationName()
    {
        Scene activeScene =
            SceneManager.GetActiveScene();

        if (!activeScene.IsValid() ||
            string.IsNullOrEmpty(activeScene.name))
        {
            return "không gian";
        }

        switch (activeScene.name)
        {
            case "Lang":
                return "Làng";
            case "TuuQuan":
                return "Tửu Quán";
            case "BenTau":
                return "Bến Tàu";
            case "khurung":
                return "Khu Rung";
            default:
                return activeScene.name;
        }
    }

    string GetHeavenGiftName(StatItemData item)
    {
        return ItemText.HeavenGiftName(item);
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(
                Input.GetTouch(0).fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    class HeavenGiftEffectSession
    {
        readonly System.Collections.Generic.List<GameObject> effects =
            new System.Collections.Generic.List<GameObject>();

        public GameObject sourceActor;
        public bool IsActive { get; private set; } = true;

        public void Register(GameObject effect)
        {
            if (effect == null)
            {
                return;
            }

            if (!IsActive)
            {
                UnityEngine.Object.Destroy(effect);
                return;
            }

            effects.Add(effect);
        }

        public void DestroyEffect(GameObject effect)
        {
            if (effect == null)
            {
                return;
            }

            effects.Remove(effect);
            UnityEngine.Object.Destroy(effect);
        }

        public void Cancel()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (effects[i] != null)
                {
                    UnityEngine.Object.Destroy(effects[i]);
                }
            }

            effects.Clear();
        }
    }
}
