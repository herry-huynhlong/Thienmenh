using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum TribulationTargetMotionMode
{
    MoveTargetToOpenArea,
    KeepTargetStill
}

public class HeavenlyTribulationSystem : MonoBehaviour
{
    public static HeavenlyTribulationSystem Instance { get; private set; }

    enum TribulationWitnessSpeechPhase
    {
        CloudGathering,
        LightningStrike,
        NearFailure,
        Success,
        Failure
    }

    enum TribulationWitnessRole
    {
        None,
        Commoner,
        Cultivator
    }

    struct PillProtectionState
    {
        public float expiresAtScaledSeconds;
        public float damageReduction;
    }

    static readonly Dictionary<int, PillProtectionState> pillProtectionUntil =
        new Dictionary<int, PillProtectionState>();

    static readonly HashSet<int> lockedTribulationTargets =
        new HashSet<int>();

    static readonly Dictionary<int, TribulationTargetLock> activeTargetLocks =
        new Dictionary<int, TribulationTargetLock>();

    static readonly Dictionary<int, float> nextWitnessSpeechTimes =
        new Dictionary<int, float>();

    static readonly string[] CultivatorCloudGatheringLines =
    {
        "Kiếp vân hội tụ... Có người sắp đột phá!",
        "Thiên địa dị tượng, chẳng lẽ có tiền bối đang độ kiếp?",
        "Uy áp thật mạnh... tu vi người này tuyệt đối không thấp.",
        "Mau lui lại! Thiên kiếp không phân biệt người ngoài cuộc.",
        "Lôi kiếp đã khóa chặt khí tức của hắn.",
        "Kiếp vân kéo dài trăm dặm... lần đột phá này không đơn giản.",
        "Thiên đạo đã giáng mắt nhìn xuống nơi này.",
        "Không biết người độ kiếp là bằng hữu hay kẻ địch.",
        "Khí tức này... chẳng lẽ sắp bước vào đại cảnh giới?",
        "Dám dẫn động thiên kiếp tại đây, người này thật quá liều lĩnh."
    };

    static readonly string[] CultivatorLightningStrikeLines =
    {
        "Đạo lôi thứ nhất đã giáng xuống!",
        "Lôi uy thật đáng sợ, mau vận công hộ thể!",
        "Hắn lại dùng thân thể chống đỡ thiên lôi?",
        "Một kích như vậy mà vẫn chưa ngã xuống...",
        "Thiên kiếp lần này mạnh hơn bình thường rất nhiều.",
        "Không ổn, kiếp vân vẫn đang tiếp tục tụ lại!",
        "Đây không phải tam trọng lôi kiếp... số lượng còn nhiều hơn!",
        "Khí tức của hắn đang suy yếu, e rằng khó vượt qua.",
        "Không được đến gần, nếu bị thiên kiếp xem là người trợ giúp thì tất cả đều phải chết!",
        "Lôi đình mang theo thiên uy, pháp bảo bình thường căn bản không chống nổi.",
        "Hắn đang mượn thiên lôi để luyện thể!",
        "Kẻ này thật điên cuồng, lại dám hấp thu sức mạnh của lôi kiếp.",
        "Kiếp lôi càng lúc càng mạnh... thiên đạo muốn diệt hắn sao?",
        "Còn có tâm ma kiếp! Đây mới là cửa ải nguy hiểm nhất."
    };

    static readonly string[] CultivatorNearFailureLines =
    {
        "Đạo tâm đã loạn, tình thế không ổn.",
        "Hộ thể linh quang sắp vỡ rồi!",
        "Pháp bảo của hắn đã bị thiên lôi đánh nát.",
        "Nếu không còn thủ đoạn cuối cùng, hôm nay e rằng thân tử đạo tiêu.",
        "Thiên kiếp vô tình, con đường tu hành vốn là nghịch thiên mà đi.",
        "Khí huyết đã cạn, hắn không chống được thêm bao lâu.",
        "Đáng tiếc một đời khổ tu, cuối cùng lại dừng bước tại đây.",
        "Mệnh số chưa đủ, cưỡng ép đột phá chỉ chuốc lấy diệt vong.",
        "Không ai có thể giúp hắn. Cửa ải này chỉ có thể tự mình vượt qua.",
        "Nguyên thần đang tan rã... thất bại đã thành định cục."
    };

    static readonly string[] CultivatorSuccessLines =
    {
        "Kiếp vân đã tan! Hắn thành công rồi!",
        "Thiên địa linh khí đang tụ về phía hắn.",
        "Sau kiếp nạn chính là tạo hóa.",
        "Khí tức đã hoàn toàn thay đổi... hắn đã bước vào cảnh giới mới.",
        "Chúc mừng đạo hữu vượt qua thiên kiếp!",
        "Từ hôm nay, thế gian lại có thêm một vị cường giả.",
        "Có thể vượt qua lôi kiếp như vậy, tiền đồ của người này không thể đo lường.",
        "Thiên lôi luyện thể, đạo vận nhập thân... thật khiến người khác ngưỡng mộ.",
        "Hắn đã được thiên đạo thừa nhận.",
        "Một bước vượt kiếp, từ đây tiên phàm cách biệt."
    };

    static readonly string[] CultivatorFailureLines =
    {
        "Kiếp vân đã tan, nhưng khí tức của hắn cũng biến mất rồi.",
        "Thân tử đạo tiêu... cuối cùng vẫn không thể vượt qua.",
        "Tu hành nghìn năm, chỉ một lần độ kiếp liền hóa thành tro bụi.",
        "Thiên đạo vô tình, không phải ai cũng có thể nghịch mệnh thành công.",
        "Đạo cơ đã hủy, cho dù còn sống cũng khó tiếp tục tu hành.",
        "Đây chính là cái giá của việc cưỡng ép đột phá.",
        "Một vị cường giả nữa đã ngã xuống dưới thiên kiếp.",
        "Con đường trường sinh quả nhiên được xây bằng vô số xương trắng.",
        "Hồn phi phách tán, ngay cả cơ hội luân hồi cũng không còn.",
        "Đáng tiếc... chỉ thiếu một bước cuối cùng."
    };

    static readonly string[] CommonerCloudGatheringLines =
    {
        "Trời đang yên lành, sao tự nhiên tối sầm lại vậy?",
        "Mây đen kia đang xoáy thành một vòng tròn!",
        "Mau về nhà đi, có khi trời sắp nổi giông lớn!",
        "Đó có phải thần tiên đang thi triển phép thuật không?",
        "Chắc chắn có tiên nhân xuất hiện!",
        "Ông trời nổi giận rồi, mau đóng cửa lại!",
        "Ta sống từng này tuổi chưa từng thấy cảnh tượng như vậy.",
        "Mau gọi mọi người tránh xa ngọn núi đó!",
        "Gia súc đều đang hoảng loạn, chuyện này không bình thường.",
        "Chẳng lẽ có yêu quái đang làm loạn?"
    };

    static readonly string[] CommonerLightningStrikeLines =
    {
        "Trời ơi! Sét đánh thẳng xuống một chỗ!",
        "Mau chạy đi, đừng đứng ngoài đường!",
        "Tiếng sấm lớn quá, nhà cửa cũng đang rung chuyển!",
        "Có người đứng giữa sấm sét kìa!",
        "Người đó vẫn còn sống sao?",
        "Đúng là tiên nhân, người thường sao có thể chịu được sét đánh!",
        "Mau quỳ xuống, đừng chọc giận ông trời!",
        "Sét đánh liên tục như vậy, cả ngọn núi sẽ bị phá hủy mất!",
        "Đừng nhìn nữa, mau đưa trẻ con vào trong nhà!",
        "Ta còn tưởng tận thế đã đến.",
        "Ánh sáng chói quá, mắt ta không mở nổi!",
        "Ngay cả mặt đất cũng đang nứt ra!"
    };

    static readonly string[] CommonerSuccessLines =
    {
        "Mây đen tan rồi! Cuối cùng cũng kết thúc.",
        "Nhìn kìa, trên trời xuất hiện ánh sáng!",
        "Vị tiên nhân đó đã chiến thắng thiên lôi!",
        "Chúng ta vừa tận mắt nhìn thấy thần tiên sao?",
        "Mau tới bái kiến tiên nhân!",
        "Có tiên nhân xuất hiện gần làng, đây chắc chắn là điềm lành.",
        "Sau trận sét, cây cỏ quanh đó đều xanh tốt hơn.",
        "Từ nay nơi này chắc sẽ trở thành vùng đất linh thiêng.",
        "May quá, ông trời đã nguôi giận rồi.",
        "Chuyện hôm nay nhất định phải kể lại cho con cháu."
    };

    static readonly string[] CommonerFailureLines =
    {
        "Người kia... biến mất rồi sao?",
        "Chỉ còn lại một vùng đất cháy đen.",
        "Mây đã tan nhưng sao chẳng thấy vị tiên nhân đâu?",
        "Chẳng lẽ người đó đã bị ông trời trừng phạt?",
        "Đừng đến gần, nơi đó vẫn còn sét!",
        "Thật đáng sợ... ngay cả tiên nhân cũng không chống lại được trời.",
        "Mau lập bàn hương, cầu mong người đã khuất được yên nghỉ.",
        "Hôm nay chắc chắn là một ngày đại hung.",
        "Không được nhặt những thứ còn sót lại, kẻo rước họa vào thân.",
        "Từ nay không ai được tới gần ngọn núi đó nữa."
    };

    readonly List<Action> activeTribulationCancellations =
        new List<Action>();

    [Header("Tribulation")]
    public TribulationTargetMotionMode targetMotionMode = TribulationTargetMotionMode.KeepTargetStill;
    public int baseLightningCount = 2;
    public int lightningCountPerMajorRealm = 1;
    [FormerlySerializedAs("lightningInterval")]
    public float lightningIntervalScaledSeconds = 0.45f;
    [Min(0f)] public float postTribulationRecoveryScaledSeconds = 2f;
    public float strikeRadius = 1.8f;
    public float openAreaSearchRadius = 8f;
    public float openAreaClearRadius = 0.9f;
    public int damagePerStrike = 35;
    public int finalStrikeDamage = 70;
    [FormerlySerializedAs("pillProtectionDuration")]
    public float pillProtectionDurationScaledSeconds = 30f;
    public float damageGrowthPerMajorRealm = 0.35f;
    public float finalStrikeGrowthPerMajorRealm = 0.45f;

    [Header("Prefab Thiên Kiếp Mới")]
    public bool useStrikePrefab = true;
    public ThienKiepStrikePrefab strikePrefab;
    public bool useFallbackIfNoPrefab = true;
    public Transform debugTestPoint;
    public GameObject debugTestTarget;
    public bool debugUseGameCameraCenterWhenNoPoint = true;
    public bool debugAttachPreviewToGameCamera = true;
    [Min(0.05f)] public float debugTestHoldScaledSeconds = 0.9f;
    public bool debugPlayStrikeOnTest = true;

    [Header("Witness Speech")]
    [Min(2f)] public float witnessSpeechRadius = 6f;
    [Min(1)] public int maxWitnessSpeakersPerPhase = 3;
    [Min(0.25f)] public float witnessSpeechCooldownScaledSeconds = 2.25f;
    [Min(0.5f)] public float witnessSpeechDurationScaledSeconds = 3.2f;
    [Range(0.05f, 0.95f)] public float witnessDangerHealthRatio = 0.35f;

    [Header("Visual Fallback Cũ")]
    public float cloudHeight = 3.2f;
    [FormerlySerializedAs("boltLife")]
    public float boltLifetimeScaledSeconds = 0.22f;
    public float boltWidth = 0.1f;
    public Color boltCoreColor = Color.white;
    public Color boltOuterColor = new Color(0.25f, 0.85f, 1f, 1f);

    static Material cachedLineMaterial;

    public static bool HasActiveTribulation =>
        Instance != null &&
        Instance.activeTribulationCancellations.Count > 0;

    public static bool IsTargetLocked(GameObject target)
    {
        return target != null &&
            lockedTribulationTargets.Contains(
                UnityObjectIdUtility.GetRuntimeId(target));
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureRuntimeReferences();
        DontDestroyOnLoad(gameObject);
    }

    void OnDisable()
    {
        CancelActiveTribulations();
    }

    void OnDestroy()
    {
        CancelActiveTribulations();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void Request(
        GameObject target,
        string displayName,
        CultivationRealm targetRealm,
        Action onPassed)
    {
        Request(target, displayName, targetRealm, onPassed, null);
    }

    public static void Request(
        GameObject target,
        string displayName,
        CultivationRealm targetRealm,
        Action onPassed,
        Action<bool> onCompleted)
    {
        if (target == null)
        {
            onCompleted?.Invoke(false);
            return;
        }

        HeavenlyTribulationSystem system = ResolveRuntimeSystem();

        if (system == null)
        {
            GameObject systemObject = new GameObject("Heavenly Tribulation System");
            system = systemObject.AddComponent<HeavenlyTribulationSystem>();
            system.EnsureRuntimeReferences();
        }

        Action cancelSession = null;
        bool completed = false;
        Action<bool> guardedCompleted = passed =>
        {
            if (completed)
            {
                return;
            }

            completed = true;
            if (cancelSession != null)
            {
                system.activeTribulationCancellations.Remove(cancelSession);
            }

            onCompleted?.Invoke(passed);
        };

        cancelSession = () => guardedCompleted(false);
        system.activeTribulationCancellations.Add(cancelSession);

        system.StartCoroutine(
            system.RunTribulation(
                target,
                displayName,
                targetRealm,
                onPassed,
                guardedCompleted));
    }

    static HeavenlyTribulationSystem ResolveRuntimeSystem()
    {
        if (Instance != null)
        {
            Instance.EnsureRuntimeReferences();
            return Instance;
        }

        HeavenlyTribulationSystem sceneSystem =
            FindAnyObjectByType<HeavenlyTribulationSystem>(
                FindObjectsInactive.Include);
        if (sceneSystem != null)
        {
            Instance = sceneSystem;
            sceneSystem.EnsureRuntimeReferences();
            return sceneSystem;
        }

        return null;
    }

    void EnsureRuntimeReferences()
    {
        if (strikePrefab != null)
        {
            return;
        }

        ThienKiepStrikePrefab resolvedPrefab =
            LoadDefaultStrikePrefab();
        if (resolvedPrefab == null)
        {
            return;
        }

        strikePrefab = resolvedPrefab;
    }

    ThienKiepStrikePrefab LoadDefaultStrikePrefab()
    {
        const string prefabName = "PF_ThienKiepStrike";
        const string assetPath = "Assets/Prefabs/PF_ThienKiepStrike.prefab";

        ThienKiepStrikePrefab fromResources =
            Resources.Load<ThienKiepStrikePrefab>(prefabName);
        if (fromResources != null)
        {
            return fromResources;
        }

        GameObject resourceObject =
            Resources.Load<GameObject>(prefabName);
        if (resourceObject != null)
        {
            ThienKiepStrikePrefab resourcePrefab =
                resourceObject.GetComponent<ThienKiepStrikePrefab>();
            if (resourcePrefab != null)
            {
                return resourcePrefab;
            }
        }

#if UNITY_EDITOR
        GameObject assetObject =
            UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (assetObject != null)
        {
            ThienKiepStrikePrefab assetPrefab =
                assetObject.GetComponent<ThienKiepStrikePrefab>();
            if (assetPrefab != null)
            {
                return assetPrefab;
            }
        }

        ThienKiepStrikePrefab[] loadedPrefabs =
            Resources.FindObjectsOfTypeAll<ThienKiepStrikePrefab>();
        for (int i = 0; i < loadedPrefabs.Length; i++)
        {
            ThienKiepStrikePrefab candidate = loadedPrefabs[i];
            if (candidate == null ||
                !UnityEditor.EditorUtility.IsPersistent(candidate))
            {
                continue;
            }

            string candidatePath =
                UnityEditor.AssetDatabase.GetAssetPath(candidate);
            if (string.Equals(
                    candidatePath,
                    assetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }
#endif

        return null;
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

        float durationScaledSeconds = system != null
            ? system.pillProtectionDurationScaledSeconds
            : 30f;

        pillProtectionUntil[
            UnityObjectIdUtility.GetRuntimeId(target)] =
            new PillProtectionState
            {
                expiresAtScaledSeconds =
                    GameTime.ScaledNowSeconds +
                    Mathf.Max(1f, durationScaledSeconds),
                damageReduction = GetPillDamageReduction(item)
            };
    }

    static float GetPillDamageReduction(StatItemData item)
    {
        if (item == null)
        {
            return 0f;
        }

        if (item.grade == ItemGrade.Ha)
        {
            return 0.1f;
        }

        if (item.grade == ItemGrade.Trung)
        {
            return 0.2f;
        }

        return 0.3f;
    }

    IEnumerator RunTribulation(
        GameObject target,
        string displayName,
        CultivationRealm targetRealm,
        Action onPassed,
        Action<bool> onCompleted)
    {
        if (target == null)
        {
            CompleteTribulation(onCompleted, false);
            yield break;
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();

        if (damageable == null || damageable.IsDead)
        {
            CompleteTribulation(onCompleted, false);
            yield break;
        }

        TribulationTargetLock targetLock = null;
        TribulationRuntime runtime = BuildRuntime(target, targetRealm);

        Vector3 originalPosition = target.transform.position;
        Vector3 center = targetMotionMode == TribulationTargetMotionMode.MoveTargetToOpenArea
            ? FindOpenArea(originalPosition, target)
            : originalPosition;

        if (targetMotionMode == TribulationTargetMotionMode.MoveTargetToOpenArea)
        {
            MoveTargetToCenter(target, center);
        }

        targetLock = BeginTribulationLock(target);

        Vector3 strikeCenter = GetTribulationStrikeCenter(target, center);
        Vector3 visualCenter = GetTribulationVisualCenter(target, strikeCenter);
        ThienKiepStrikePrefab activeStrikeVisual =
            CreatePersistentStrikeVisual(
                visualCenter,
                strikeCenter);
        ShowTribulationSpeech(target);
        ShowNearbyTribulationWitnessSpeech(target);

        AddWorldLog(
            displayName + " dẫn động Thiên Kiếp, chuẩn bị đột phá " +
            NpcText.Realm(targetRealm) + ".",
            2);

        if (activeStrikeVisual != null)
        {
            yield return activeStrikeVisual.BeginLoiKiep(
                visualCenter,
                strikeCenter);
        }
        else
        {
            yield return PlayCloudGathering(center, runtime);
            yield return GameTime.WaitForScaledSeconds(0.35f);
        }

        HeavenSystem heaven = HeavenSystem.Instance;
        int count = Mathf.Max(1, runtime.lightningCount);

        for (int i = 0; i < count; i++)
        {
            if (target == null || damageable.IsDead)
            {
                if (activeStrikeVisual != null)
                {
                    yield return activeStrikeVisual.EndLoiKiep();
                }

                ReleaseTribulationLock(targetLock);
                CompleteTribulation(onCompleted, false);
                yield break;
            }

            if (activeStrikeVisual != null)
            {
                bool damageApplied = false;
                yield return activeStrikeVisual.PlayStrikeFlash(
                    () =>
                    {
                        if (damageApplied)
                        {
                            return;
                        }

                        damageApplied = true;
                        ApplyStrikeDamage(
                            strikeCenter,
                            heaven != null ? heaven.punishmentRadius : 1.2f,
                            heaven != null ? heaven.damageLayers : (LayerMask)~0,
                            runtime.damagePerStrike);
                    });
            }
            else
            {
                Strike(
                    heaven,
                    visualCenter,
                    strikeCenter,
                    runtime.damagePerStrike);
            }

            yield return GameTime.WaitForScaledSeconds(
                Mathf.Max(0.05f, lightningIntervalScaledSeconds));

        }

        if (target == null || damageable.IsDead)
        {
            if (activeStrikeVisual != null)
            {
                yield return activeStrikeVisual.EndLoiKiep();
            }

            ReleaseTribulationLock(targetLock);
            CompleteTribulation(onCompleted, false);
            yield break;
        }

        if (activeStrikeVisual != null)
        {
            bool finalDamageApplied = false;
            yield return activeStrikeVisual.PlayStrikeFlash(
                () =>
                {
                    if (finalDamageApplied)
                    {
                        return;
                    }

                    finalDamageApplied = true;
                    ApplyStrikeDamage(
                        strikeCenter,
                        heaven != null ? heaven.punishmentRadius : 1.2f,
                        heaven != null ? heaven.damageLayers : (LayerMask)~0,
                        runtime.finalStrikeDamage);
                });
            yield return activeStrikeVisual.EndLoiKiep();
        }
        else
        {
            Strike(
                heaven,
                visualCenter,
                strikeCenter,
                runtime.finalStrikeDamage);

            yield return GameTime.WaitForScaledSeconds(0.1f);
        }

        if (target == null || damageable.IsDead)
        {
            AddWorldLog(displayName + " thất bại dưới Thiên Kiếp.", 2);
            ReleaseTribulationLock(targetLock);
            CompleteTribulation(onCompleted, false);
            yield break;
        }

        if (postTribulationRecoveryScaledSeconds > 0f)
        {
            yield return GameTime.WaitForScaledSeconds(
                postTribulationRecoveryScaledSeconds);
        }

        if (target == null || damageable.IsDead)
        {
            AddWorldLog(displayName + " tháº¥t báº¡i dÆ°á»›i ThiÃªn Kiáº¿p.", 2);
            ReleaseTribulationLock(targetLock);
            CompleteTribulation(onCompleted, false);
            yield break;
        }

        onPassed?.Invoke();
        ReleaseTribulationLock(targetLock);
        CompleteTribulation(onCompleted, true);

        AddWorldLog(
            displayName + " vượt qua Thiên Kiếp, đột phá " +
            NpcText.Realm(targetRealm) + ".",
            1);
    }

    void CompleteTribulation(Action<bool> onCompleted, bool passed)
    {
        onCompleted?.Invoke(passed);
    }

    ThienKiepStrikePrefab CreatePersistentStrikeVisual(
        Vector3 gatherPosition,
        Vector3 strikePosition)
    {
        if (!useStrikePrefab ||
            strikePrefab == null)
        {
            return null;
        }

        ThienKiepStrikePrefab strike =
            Instantiate(
                strikePrefab,
                gatherPosition,
                Quaternion.identity);
        strike.PrepareAt(
            gatherPosition,
            strikePosition);
        return strike;
    }

    [ContextMenu("Debug/Test Loi Kiep Visual")]
    public void DebugPlayLoiKiepVisual()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Chi co the test loi kiep khi game dang Play.",
                this);
            return;
        }

        Vector3 fallbackCenter =
            debugTestPoint != null
                ? debugTestPoint.position
                : debugTestTarget != null
                    ? debugTestTarget.transform.position
                    : ResolveDebugFallbackCenter();

        GameObject resolvedTarget = debugTestTarget;
        if (resolvedTarget == null &&
            debugTestPoint != null)
        {
            resolvedTarget = debugTestPoint.gameObject;
        }

        Vector3 strikeCenter =
            GetTribulationStrikeCenter(
                resolvedTarget,
                fallbackCenter);
        Vector3 visualCenter =
            GetTribulationVisualCenter(
                resolvedTarget,
                strikeCenter);
        Camera debugCamera =
            resolvedTarget == null &&
            debugTestPoint == null &&
            debugUseGameCameraCenterWhenNoPoint
                ? ResolveDebugCamera()
                : null;

        StartCoroutine(
            DebugPlayLoiKiepVisualRoutine(
                visualCenter,
                strikeCenter,
                debugCamera));
    }

    IEnumerator DebugPlayLoiKiepVisualRoutine(
        Vector3 visualCenter,
        Vector3 strikeCenter,
        Camera debugCamera)
    {
        ThienKiepStrikePrefab visual =
            CreatePersistentStrikeVisual(
                visualCenter,
                strikeCenter);

        if (visual == null)
        {
            if (useFallbackIfNoPrefab)
            {
                yield return PlayFallbackLightning(
                    visualCenter,
                    strikeCenter);
            }
            else
            {
                Debug.LogWarning(
                    "Khong the test loi kiep vi strikePrefab chua duoc gan.",
                    this);
            }

            yield break;
        }

        if (debugCamera != null &&
            debugAttachPreviewToGameCamera)
        {
            AttachDebugVisualToCamera(
                visual,
                debugCamera);
            visualCenter = visual.transform.position;
            strikeCenter = visualCenter;
        }

        yield return visual.BeginLoiKiep(
            visualCenter,
            strikeCenter);
        yield return GameTime.WaitForScaledSeconds(
            Mathf.Max(0.05f, debugTestHoldScaledSeconds));

        if (debugPlayStrikeOnTest)
        {
            yield return visual.PlayStrikeFlash();
        }

        yield return visual.EndLoiKiep();
    }

    Vector3 ResolveDebugFallbackCenter()
    {
        if (debugUseGameCameraCenterWhenNoPoint)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return CameraWorldPlaneUtility.ScreenToWorldOnPlane(
                    mainCamera,
                    new Vector2(
                        mainCamera.pixelWidth * 0.5f,
                        mainCamera.pixelHeight * 0.5f),
                    0f);
            }
        }

        return transform.position;
    }

    Camera ResolveDebugCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null &&
            mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras =
            FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null &&
                camera.isActiveAndEnabled &&
                camera.targetDisplay == 0)
            {
                return camera;
            }
        }

        return null;
    }

    void AttachDebugVisualToCamera(
        ThienKiepStrikePrefab visual,
        Camera debugCamera)
    {
        if (visual == null ||
            debugCamera == null)
        {
            return;
        }

        float localZ =
            Mathf.Abs(
                0f - debugCamera.transform.position.z);
        if (localZ < 0.5f)
        {
            localZ = 10f;
        }

        visual.transform.SetParent(
            debugCamera.transform,
            false);
        visual.transform.localPosition =
            new Vector3(0f, 0f, localZ);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
    }

    void CancelActiveTribulations()
    {
        if (activeTribulationCancellations.Count <= 0)
        {
            return;
        }

        Action[] cancellations = activeTribulationCancellations.ToArray();
        activeTribulationCancellations.Clear();

        for (int i = 0; i < cancellations.Length; i++)
        {
            cancellations[i]?.Invoke();
        }

        ReleaseAllTribulationLocks();
    }

    struct TribulationRuntime
    {
        public int lightningCount;
        public int damagePerStrike;
        public int finalStrikeDamage;
        public bool usedProtectionPill;
        public float talentFactor;
    }

    class TribulationTargetLock
    {
        public GameObject target;
        public int targetId;
        public Rigidbody2D rb;
        public RigidbodyType2D bodyType;
        public bool simulated;
        public float gravityScale;
        public RigidbodyConstraints2D constraints;
        public bool released;
    }

    int GetMajorRealmTier(CultivationRealm targetRealm)
    {
        return Mathf.Clamp(
            (int)targetRealm - (int)CultivationRealm.Foundation,
            0,
            4);
    }

    TribulationTargetLock BeginTribulationLock(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        int targetId =
            UnityObjectIdUtility.GetRuntimeId(target);
        if (activeTargetLocks.TryGetValue(
                targetId,
                out TribulationTargetLock existing))
        {
            return existing;
        }

        TribulationTargetLock targetLock = new TribulationTargetLock
        {
            target = target,
            targetId = targetId,
            rb = target.GetComponent<Rigidbody2D>()
        };

        if (targetLock.rb != null)
        {
            targetLock.bodyType = targetLock.rb.bodyType;
            targetLock.simulated = targetLock.rb.simulated;
            targetLock.gravityScale = targetLock.rb.gravityScale;
            targetLock.constraints = targetLock.rb.constraints;
            targetLock.rb.linearVelocity = Vector2.zero;
            targetLock.rb.angularVelocity = 0f;
            targetLock.rb.gravityScale = 0f;
            targetLock.rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        lockedTribulationTargets.Add(targetId);
        activeTargetLocks[targetId] = targetLock;
        ApplyTribulationWaitPose(target);
        target.SendMessage(
            "OnHeavenlyTribulationLockChanged",
            true,
            SendMessageOptions.DontRequireReceiver);
        return targetLock;
    }

    void ReleaseTribulationLock(TribulationTargetLock targetLock)
    {
        if (targetLock == null || targetLock.released)
        {
            return;
        }

        targetLock.released = true;
        lockedTribulationTargets.Remove(targetLock.targetId);
        activeTargetLocks.Remove(targetLock.targetId);

        if (targetLock.rb != null)
        {
            targetLock.rb.bodyType = targetLock.bodyType;
            targetLock.rb.simulated = targetLock.simulated;
            targetLock.rb.gravityScale = targetLock.gravityScale;
            targetLock.rb.constraints = targetLock.constraints;
            targetLock.rb.linearVelocity = Vector2.zero;
            targetLock.rb.angularVelocity = 0f;
        }

        if (targetLock.target != null)
        {
            targetLock.target.SendMessage(
                "OnHeavenlyTribulationLockChanged",
                false,
                SendMessageOptions.DontRequireReceiver);
        }
    }

    void ReleaseAllTribulationLocks()
    {
        if (activeTargetLocks.Count <= 0)
        {
            return;
        }

        TribulationTargetLock[] locks =
            new List<TribulationTargetLock>(activeTargetLocks.Values).ToArray();

        for (int i = 0; i < locks.Length; i++)
        {
            ReleaseTribulationLock(locks[i]);
        }
    }

    void ApplyTribulationWaitPose(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        string waitAction =
            NpcText.Action("waitTribulation");
        NpcActionState waitState =
            NpcActionState.FromKey("waitTribulation");

        NpcRoleUtility.SetAction(target, waitAction);

        SmartNpcAI smartNpc =
            target.GetComponent<SmartNpcAI>();
        if (smartNpc != null)
        {
            smartNpc.ForceSetCurrentAction(waitAction, 0.25f);
            smartNpc.SetCurrentActionState(waitState);
        }

        VillagerAI villager =
            target.GetComponent<VillagerAI>();
        if (villager != null)
        {
            villager.currentAction = waitAction;
            villager.SetCurrentActionState(waitState);
        }

        MonsterAI monster =
            target.GetComponent<MonsterAI>();
        if (monster != null)
        {
            monster.SetCurrentActionState(waitState);
        }
    }

    TribulationRuntime BuildRuntime(
        GameObject target,
        CultivationRealm targetRealm)
    {
        float talentFactor = GetTalentFactor(target);
        int majorTier = GetMajorRealmTier(targetRealm);

        bool protectedByPill =
            ConsumePillProtection(target, out float pillReduction);

        float pillMultiplier = protectedByPill
            ? 1f - Mathf.Clamp01(pillReduction)
            : 1f;

        return new TribulationRuntime
        {
            lightningCount = Mathf.Clamp(
                baseLightningCount + majorTier * lightningCountPerMajorRealm,
                2,
                6),

            damagePerStrike = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    damagePerStrike *
                    (1f + majorTier * damageGrowthPerMajorRealm) *
                    pillMultiplier)),

            finalStrikeDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    finalStrikeDamage *
                    (1f + majorTier * finalStrikeGrowthPerMajorRealm) *
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

    Vector3 GetTribulationStrikeCenter(
        GameObject target,
        Vector3 fallbackCenter)
    {
        if (target == null)
        {
            return fallbackCenter;
        }

        Bounds? bounds =
            GetTargetVisualBounds(target, true);
        if (bounds.HasValue)
        {
            Bounds value = bounds.Value;
            float anchorX = GetTribulationAnchorX(target, value.center.x);
            float groundOffset =
                Mathf.Clamp(
                    value.size.y * 0.08f,
                    0.05f,
                    0.16f);
            float strikeY =
                value.min.y + groundOffset;
            return new Vector3(
                anchorX,
                strikeY,
                fallbackCenter.z);
        }

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            return new Vector3(
                rb.position.x,
                rb.position.y + 0.5f,
                fallbackCenter.z);
        }

        Vector3 position = target.transform.position;
        return new Vector3(
            position.x,
            position.y + 0.5f,
            fallbackCenter.z);
    }

    Vector3 GetTribulationVisualCenter(
        GameObject target,
        Vector3 strikeCenter)
    {
        Bounds? bounds =
            GetTargetVisualBounds(target, true);
        if (bounds.HasValue)
        {
            Bounds value = bounds.Value;
            float anchorX = GetTribulationAnchorX(target, value.center.x);
            return new Vector3(
                anchorX,
                value.max.y + 1.9f,
                strikeCenter.z);
        }

        if (target == null)
        {
            return strikeCenter + Vector3.up * 2.6f;
        }

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            return new Vector3(
                rb.position.x,
                rb.position.y + 2.6f,
                strikeCenter.z);
        }

        Vector3 position = target.transform.position;
        return new Vector3(
            position.x,
            position.y + 2.6f,
            strikeCenter.z);
    }

    float GetTribulationAnchorX(
        GameObject target,
        float fallbackX)
    {
        if (target == null)
        {
            return fallbackX;
        }

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            return rb.position.x;
        }

        return target.transform.position.x;
    }

    Bounds? GetTargetVisualBounds(
        GameObject target,
        bool includeColliders)
    {
        if (target == null)
        {
            return null;
        }

        Bounds? bounds = null;
        SpriteRenderer[] spriteRenderers =
            target.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null ||
                !renderer.enabled ||
                renderer.sprite == null)
            {
                continue;
            }

            bounds = bounds.HasValue
                ? Encapsulate(bounds.Value, renderer.bounds)
                : renderer.bounds;
        }

        if (bounds.HasValue || !includeColliders)
        {
            return bounds;
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            bounds = bounds.HasValue
                ? Encapsulate(bounds.Value, collider.bounds)
                : collider.bounds;
        }

        return bounds;
    }

    void ShowTribulationSpeech(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        NpcSpeechController.TryShowSpeech(
            target,
            null,
            "tribulation_self");
    }

    void ShowNearbyTribulationWitnessSpeech(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        float radius = Mathf.Max(4f, strikeRadius * 3f);
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(target.transform.position, radius);
        HashSet<int> shownNpcIds = new HashSet<int>();

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject npc = ResolveNearbyNpcRoot(hits[i], target);
            if (npc == null ||
                !shownNpcIds.Add(
                    UnityObjectIdUtility.GetRuntimeId(npc)) ||
                NpcRoleUtility.IsDead(npc))
            {
                continue;
            }

            NpcSpeechController.TryShowSpeech(
                npc,
                target,
                "tribulation_witness");
        }
    }

    GameObject ResolveNearbyNpcRoot(Collider2D hit, GameObject excluded)
    {
        if (hit == null)
        {
            return null;
        }

        VillagerAI villager = hit.GetComponentInParent<VillagerAI>();
        if (villager != null &&
            villager.gameObject != excluded)
        {
            return villager.gameObject;
        }

        SmartNpcAI smartNpc = hit.GetComponentInParent<SmartNpcAI>();
        if (smartNpc != null &&
            smartNpc.gameObject != excluded)
        {
            return smartNpc.gameObject;
        }

        MonsterAI monster = hit.GetComponentInParent<MonsterAI>();
        if (monster != null &&
            monster.gameObject != excluded)
        {
            return monster.gameObject;
        }

        return null;
    }

    Bounds Encapsulate(Bounds first, Bounds second)
    {
        first.Encapsulate(second.min);
        first.Encapsulate(second.max);
        return first;
    }

    bool ConsumePillProtection(GameObject target, out float damageReduction)
    {
        damageReduction = 0f;

        if (target == null)
        {
            return false;
        }

        int key =
            UnityObjectIdUtility.GetRuntimeId(target);

        if (!pillProtectionUntil.TryGetValue(
                key,
                out PillProtectionState state))
        {
            return false;
        }

        if (GameTime.ScaledNowSeconds > state.expiresAtScaledSeconds)
        {
            pillProtectionUntil.Remove(key);
            return false;
        }

        damageReduction = Mathf.Clamp01(state.damageReduction);
        pillProtectionUntil.Remove(key);
        return true;
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

        yield return GameTime.WaitForScaledSeconds(0.45f);

        if (cloudObject != null)
        {
            Destroy(cloudObject);
        }
    }

    void Strike(
        HeavenSystem heaven,
        Vector3 visualPosition,
        Vector3 impactPosition,
        int damage)
    {
        float radius = heaven != null
            ? heaven.punishmentRadius
            : 1.2f;

        LayerMask damageLayers = heaven != null
            ? heaven.damageLayers
            : (LayerMask)~0;

        PlayStrikeVisual(visualPosition, impactPosition);

        ApplyStrikeDamage(impactPosition, radius, damageLayers, damage);
    }

    void PlayStrikeVisual(Vector3 visualPosition, Vector3 impactPosition)
    {
        if (useStrikePrefab && strikePrefab != null)
        {
            ThienKiepStrikePrefab strike =
                Instantiate(strikePrefab, visualPosition, Quaternion.identity);

            LayerMask noDamageLayers = 0;

            strike.Play(0, noDamageLayers, impactPosition);

            return;
        }

        if (useFallbackIfNoPrefab)
        {
            StartCoroutine(PlayFallbackLightning(visualPosition, impactPosition));
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

        HashSet<IDamageable> damagedTargets =
            new HashSet<IDamageable>();
        HashSet<Transform> damagedTransforms =
            new HashSet<Transform>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();

            if (damageable == null ||
                damageable.IsDead ||
                !damagedTargets.Add(damageable))
            {
                continue;
            }

            Transform damageTransform = damageable.DamageTransform;
            if (damageTransform != null &&
                !damagedTransforms.Add(damageTransform))
            {
                continue;
            }

            DamageContext context = DamageContext.Environment(
                Mathf.Max(1, damage),
                this,
                DamageType.HeavenlyTribulation,
                "heavenly_tribulation_strike",
                position);
            DamageSystem.Apply(damageable, context);
        }
    }

    IEnumerator PlayFallbackLightning(
        Vector3 visualPosition,
        Vector3 impactPosition)
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

        Vector3 top = visualPosition + Vector3.up * cloudHeight;

        for (int i = 0; i < pointCount; i++)
        {
            float progress = i / Mathf.Max(1f, pointCount - 1f);
            Vector3 point = Vector3.Lerp(top, impactPosition, progress);

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

        PlayImpactRing(impactPosition);

        yield return GameTime.WaitForScaledSeconds(
            Mathf.Max(0.05f, boltLifetimeScaledSeconds));

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

        Destroy(ringObject, boltLifetimeScaledSeconds);
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
            WorldEventManager.Instance.AddLog(content, colorType, true);
        }
    }
}


