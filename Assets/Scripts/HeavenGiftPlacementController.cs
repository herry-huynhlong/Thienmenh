using System.Collections;
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
    public float immortalLightningScale = 2.4f;

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
            camera.ScreenToWorldPoint(screenPosition);

        worldPosition.z = 0f;

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

        HeavenGiftEffectSession session =
            new HeavenGiftEffectSession();

        pickup.OnDepleted += session.Cancel;

        EnsurePickupVisual(pickup, item);
        LogHeavenGift(item);
        PlayStartEffect(item, targetPosition, session);

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
                if (IsNight())
                {
                    StartCoroutine(FlashNightSky(session));
                }

                StartCoroutine(
                    PlayMiddleGradeGlowWaves(
                        targetPosition,
                        session));
                break;

            case ItemGrade.Thuong:
                if (item.itemType == ItemType.PhapBao)
                {
                    StartCoroutine(
                        PlayRainbowAuraWaves(
                            targetPosition,
                            session));
                }
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
            item.grade != ItemGrade.Tien)
        {
            return;
        }

        if (item.itemType == ItemType.PhapBao)
        {
            StartCoroutine(
                PlayImmortalTreasureSequence(
                    targetPosition,
                    session));
        }
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
        HeavenGiftEffectSession session)
    {
        if (session == null ||
            !session.IsActive)
        {
            yield break;
        }

        int waves =
            Mathf.Max(1, rainbowWaveCount);

        for (int i = 0; i < waves && session.IsActive; i++)
        {
            yield return PlayRainbowAuraWave(center, session);

            if (rainbowWaveInterval > 0f &&
                i < waves - 1)
            {
                yield return new WaitForSeconds(rainbowWaveInterval);
            }
        }
    }

    IEnumerator PlayRainbowAuraWave(
        Vector3 center,
        HeavenGiftEffectSession session)
    {
        Color[] colors =
        {
            Color.red,
            new Color(1f, 0.5f, 0f),
            Color.yellow,
            Color.green,
            Color.cyan,
            Color.blue,
            new Color(0.65f, 0.25f, 1f)
        };

        GameObject auraRoot =
            new GameObject("Heaven Rainbow Aura");

        auraRoot.transform.position = center;
        session.Register(auraRoot);

        LineRenderer[] rings =
            new LineRenderer[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            GameObject ringObject =
                new GameObject("Rainbow Ring " + i);

            ringObject.transform.SetParent(auraRoot.transform, false);
            rings[i] = CreateRingRenderer(ringObject, colors[i], i);
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

                UpdateRing(rings[i], radius, colors[i], alpha);
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

    IEnumerator PlayImmortalTreasureSequence(
        Vector3 center,
        HeavenGiftEffectSession session)
    {
        StartCoroutine(
            PlayRainbowAuraWaves(center, session));

        yield return PlayImmortalLightning(center, session);
    }

    IEnumerator PlayImmortalLightning(
        Vector3 center,
        HeavenGiftEffectSession session)
    {
        if (session == null ||
            !session.IsActive)
        {
            yield break;
        }

        HeavenSystem heaven =
            HeavenSystem.Instance;

        int count =
            Mathf.Max(1, immortalLightningCount);

        float totalRainbowDuration =
            Mathf.Max(
                count * immortalLightningInterval,
                rainbowWaveCount *
                (rainbowWaveDuration + rainbowWaveInterval));

        float lightningInterval =
            Mathf.Max(0.12f, totalRainbowDuration / count);

        for (int i = 0; i < count && session.IsActive; i++)
        {
            float progress =
                i / Mathf.Max(1f, count - 1f);

            float strength =
                Mathf.Lerp(1f, 0.18f, progress);

            Vector2 offset =
                Random.insideUnitCircle *
                immortalLightningRadius *
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
                    immortalLightningScale * strength;

                session.Register(lightning);
            }
            else
            {
                StartCoroutine(
                    PlayFallbackLightning(
                        strikePosition,
                        immortalLightningScale * strength,
                        session));
            }

            yield return new WaitForSeconds(lightningInterval);
        }
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
        pickup.allowPlayerPickup = true;

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
            "Di tuong o " +
            locationName +
            ", troi giang " +
            treasureName +
            ".";

        int colorType =
            item.grade == ItemGrade.Tien
            ? 2
            : item.grade == ItemGrade.Thuong
                ? 1
                : 0;

        WorldEventManager.Instance.AddLog(message, colorType);
    }

    string GetCurrentLocationName()
    {
        Scene activeScene =
            SceneManager.GetActiveScene();

        if (!activeScene.IsValid() ||
            string.IsNullOrEmpty(activeScene.name))
        {
            return "khong gian";
        }

        switch (activeScene.name)
        {
            case "Lang":
                return "Lang";
            case "TuuQuan":
                return "Tuu Quan";
            case "BenTau":
                return "Ben Tau";
            case "khurung":
                return "Khu Rung";
            default:
                return activeScene.name;
        }
    }

    string GetHeavenGiftName(StatItemData item)
    {
        string gradeText =
            GetGradeText(item.grade);

        switch (item.itemType)
        {
            case ItemType.DanDuoc:
                return gradeText + " Bao Dan";
            case ItemType.PhapBao:
                return gradeText + " Phap Bao";
            case ItemType.CongPhap:
                return gradeText + " Cong Phap";
            case ItemType.VatLieu:
                return gradeText + " Linh Tai";
            case ItemType.ThucPham:
                return "Linh Thuc";
            default:
                return gradeText + " Bao Vat";
        }
    }

    string GetGradeText(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Trung:
                return "Trung pham";
            case ItemGrade.Thuong:
                return "Thuong pham";
            case ItemGrade.Tien:
                return "Tien pham";
            default:
                return "Ha pham";
        }
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
