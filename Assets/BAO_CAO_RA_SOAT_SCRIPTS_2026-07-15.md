# Rà soát Scripts(3) — 15/07/2026

## Phạm vi và kết luận

Đã giải nén và đọc **243 file C#** (khoảng 100 nghìn dòng, chưa tính `.meta`). Đây là rà soát tĩnh toàn bộ mã nguồn: đọc class, trường dữ liệu, các vòng đời Unity, luồng gọi giữa các hệ và tìm điểm có thể lỗi. Không có project/scene/prefab và Console Unity kèm theo, nên không thể khẳng định lỗi runtime nào đã xảy ra; các mục dưới đây được xếp theo mức độ rủi ro từ mã hiện có.

Không sửa file nào. Các đề xuất đều là sửa cục bộ trong đúng file/hệ nêu ra, không đổi tên public field, public method, class, prefab reference hay format save hiện tại.

## Bản đồ hệ thống thực tế

| Vùng | Điểm vào chính | Các hàm/luồng đảm nhiệm | Phụ thuộc trực tiếp |
|---|---|---|---|
| Thời gian/thời tiết | `WorldTimeSystem.Update` | tăng giờ, chuẩn hóa ngày-tháng-năm, bắn event, autosave | weather/event/tài nguyên/NPC |
| NPC tu sĩ | `SmartNpcAI.Update` + partial | need → schedule → task/trade/gather/combat → movement/recovery | schedule, map mover, provider, monster |
| Dân làng | `VillagerAI.Update` + partial | daily work, nghề, pathfinding, kẹt đường, giao dịch | job, navigation |
| Nhiệm vụ/quán | `NpcTaskProvider.Update` + partial | offer, chọn NPC, di chuyển, harvest/hunt/escort, watchdog, turn-in | item, pickup, monster |
| Bí cảnh | `BicanhSessionManager` | snapshot, spawn, giới hạn hành vi, hoàn trả, save | map policy/social/full save |
| Combat | `DamageSystem` | dựng context, phòng thủ/kháng, damage/death | IDamageable, stats, AI |
| Vật phẩm/kinh tế | ItemInventory/GameSaveSystem/shop | stack/equip/use/stock/craft/refine | PlayerPrefs, ScriptableObject |
| Save/load | FullGameSaveController | snapshot player/NPC/camera/bí cảnh/weather/resources | scene lifecycle/ID actor |
| UI/input | panel/menu/TouchSelectTarget | list/bind/chọn mục tiêu/localization | EventSystem/UI/Resources |

## Các lỗi/rủi ro có bằng chứng cụ thể

### P0 — xử lý trước khi tăng số NPC hoặc thời gian chơi dài

1. **Mất độ chính xác giờ thế giới sau thời gian dài** — `WorldTimeSystem.cs:45-48`, `WorldEventSystem.cs:17-20,81-96`.

`WorldTimeSystem` tính đúng bằng `double CurrentWorldHourExact`, nhưng lại công khai `float CurrentWorldHour`; `WorldEventSystem.nextCheckWorldHour` cũng là `float`. Sau nhiều năm game float không còn giữ được từng giờ/phần giờ. Hậu quả: check event muộn hoặc dồn.

Sửa cục bộ: đổi riêng `WorldEventSystem.nextCheckWorldHour` và state save sang `double`, dùng `CurrentWorldHourExact`. Khi đọc save cũ float, gán qua double. Không cần đổi API float cũ của WorldTimeSystem.

2. **Full save và resource save ghi JSON lớn vào PlayerPrefs** — `FullGameSaveController.cs:227-285`, `GameSaveSystem.cs:381-615`, `WorldResourceField.cs:558-703`.

Autosave quét toàn scene bằng `FindObjectsByType`, serialise JSON rồi ghi PlayerPrefs; resource field còn có JSON riêng. Khi NPC/item/tài nguyên tăng, dữ liệu dễ nở lớn, nhất là Android; `PlayerPrefs.Save()` lúc pause/quit cũng có thể khựng hình.

Sửa an toàn: giữ API/key hiện tại nhưng chuyển payload lớn FullSave, backup và ResourceField sang JSON file ở `Application.persistentDataPath`, ghi atomic `.tmp` rồi replace. PlayerPrefs chỉ giữ cờ/version/scene. Đọc fallback PlayerPrefs cũ một lần để migrate. Không đổi `FullGameSaveData` nên không hỏng save cũ.

3. **Task có thể được trao thưởng khi provider bị disable** — `NpcTaskProvider.cs:1598-1667`.

`OnDisable()` gọi `CompleteInterruptedWork()`. Với gather/hunt, chỉ cần objective complete là hàm có thể consume item và `RewardNpc`, dù NPC chưa quay về turn-in. Disable do đổi scene/tắt object có thể biến task đang dở thành hoàn thành.

Sửa cục bộ: `OnDisable` chỉ cleanup/resume AI/reservation, tuyệt đối không reward. Chỉ reward tại stage `TurningIn`; nếu cần xuyên scene, save `RunningNpcTask` riêng.

4. **Navigation cấp phát mảng vật lý thường xuyên** — `SmartNpcAI.NavigationRecovery.cs`, `VillagerAI.NavigationRecovery.cs`, `NpcMapMover2D.cs`, `MonsterAI.TargetingMovement.cs`, `NpcTaskProvider.TravelWatchdog.cs`.

Các hàm dùng `OverlapCircleAll`, `CircleCastAll`, `RaycastAll` trong luồng movement. Mỗi lần tạo array mới; nhiều NPC sẽ GC spike và AI có thể tưởng là kẹt vì tụt frame.

Sửa cục bộ: thay dần bằng bản NonAlloc, buffer riêng theo instance và giữ thuật toán chọn mục tiêu. Làm theo thứ tự MapMover → Smart/Villager recovery → Monster; đo profiler sau từng file.

### P1 — sai trạng thái/khó tái hiện

5. **Snapshot bí cảnh không tự bao phủ hành vi mới** — `BicanhSessionManager.cs:19-102`, `NpcMapBehaviorPolicy.cs:6-240`.

Session lưu nhiều bool/action/HP và enabled Behaviour theo type name. Component/flag mới thêm vào AI không tự được snapshot; kết thúc bí cảnh NPC có thể giữ action/flag bí cảnh.

Giải pháp: thêm interface cục bộ `IBicanhParticipantState` với Capture/Restore; giữ field cũ để đọc save tương thích. Không tiếp tục mở rộng danh sách bool khắp manager.

6. **WorldEvent không catch-up có giới hạn khi time nhảy** — `WorldEventSystem.cs:81-96`.

Mỗi frame chỉ check một lần, dù thời gian có thể vượt rất nhiều mốc khi load, time scale cao, hoặc debug set time. Check bị dồn qua nhiều frame.

Sửa: advance mốc theo interval trong vòng lặp giới hạn 3 lần/frame; sau load clamp về now + interval nếu không muốn mô phỏng event cũ.

7. **Ownership singleton chưa thống nhất** — `WorldTimeSystem.Awake:64-95`, `WeatherVisualSystem.Awake:62-84`, `DontDestroy.Awake:12-43`, `EventSystemGuard.Awake:41-55`.

Mỗi hệ chọn instance runtime/scene khác nhau. Quay menu có thể config mới ghi đè hoặc không tùy hệ. EventSystemGuard quét EventSystem mỗi 0.25 giây.

Sửa: không refactor hàng loạt. Quy ước cho hệ mới: scene config ưu tiên, runtime chỉ fallback. EventSystemGuard chỉ enforce sceneLoaded hoặc khi EventSystem.current null.

8. **Villager có legacy partial song song hệ mới** — `VillagerAI.LegacyCultivation.cs`, `VillagerAI.LegacyDailyTaskPlan.cs`, `VillagerAI.LegacyTreasure.cs` cùng `.Brain/.Work/.Movement`.

Vì là partial, legacy có thể vẫn đặt destination/action cùng hệ mới, gây NPC kéo qua lại/tự quay việc cũ. Thêm feature flag `useLegacy...` mặc định false, giữ method để không vỡ prefab; sau release test mới bỏ.

### P2 — hiệu năng/bảo trì

9. **FindObjectsByType ở UI/logic** — InventoryPanelUI:802; ShopPanelUI:1151; BottomMenuButtonRouter:88/304/498/573; TouchSelectTarget:2004/4168/4197; PopulationWidgetUI:107/132/156; NpcPerformanceOverlay:146/150. Cache sau scene load, refresh theo event; luôn null-check cache bị destroy.

10. **Dependency ẩn theo tên/path** — HeavenDaoPanelUI:51/423; MainMenuManager:288; MainMenuBackgroundVideoController:86; TouchSelectTarget:2344-2870; weather/item-frame libraries. Đổi tên object/resource không báo compile. Dùng SerializeField cho scene/prefab và constants + log cấu hình một lần cho Resources; không đổi path hiện có.

11. **Mega-class** — TouchSelectTarget 4,405 dòng; SmartNpcAI >14k qua partial; VillagerAI >10k; NpcTaskProvider >7k; NpcSocialSystem 2,928; FixedBlacksmith 3,102. Không refactor lớn khi gameplay đang chạy; feature mới nên là component/service riêng với API hẹp.

## Bóc tách cụm AI/hàm quan trọng

### SmartNpcAI

- `SmartNpcAI.cs`: stats, needs, ability, reference; Awake/Start/Update điều phối; damage/death/cultivation/state.
- `.Brain`: chọn priority/task; phải kiểm policy bí cảnh trước schedule/free activity.
- `.Schedule`: time slot tu luyện/trade/social/task/idle.
- `.Tasks`: nhận và đi task provider.
- `.Movement` + `.NavigationRecovery`: move target, crowd/obstacle, stuck/escape/retry; cần profiler đầu tiên.
- `.MonsterCombat` + `.CombatSupport`: target/attack/support; phải qua map policy.
- `.DirectedActions`: hiệu ứng/action cưỡng bức; không để hai nhánh cùng set currentTarget.
- `.Needs`: hunger/fatigue.

### VillagerAI

- Core: state, nhu cầu, HP, home/job và lifecycle.
- Brain/Work/ProfessionWork/Trade: phân công lao động.
- Pathfinding/Movement/NavigationRecovery: đường đi và unstuck.
- Legacy*: chỉ giữ tương thích, cần feature flag trước khi thêm luồng mới.

### Nhiệm vụ và nghề

- `NpcTaskProvider.cs`: enum/offer/runtime; assign timer, meal, catalog reset.
- OfferEvaluation: điều kiện/reward; WorkPositions: điểm đứng; TaskExecution: stage; GatherProgress/Hunt: progress; TravelWatchdog: không tới; TurnIn: consume/reward; EscortRuntime: hộ tống.
- NpcResourceGatherer, NpcItemCollector, HarvestJob, HunterJob có phần chồng; resource mới phải chọn đúng một owner progress để không cộng item hai lần.
- Forge/Alchemy agent là hệ động; fixed controller là quầy cố định. Không gắn cả hai cho cùng NPC nếu chưa có ownership flag.

### Save/load

- GameSaveSystem: ID item, inventory/shop PlayerPrefs, dynamic key, world time, clear.
- FullGameSaveController: backup trước overwrite; apply: time → world simulation → player/camera → NPC → bí cảnh → favorite. Thứ tự này hợp lý; hệ mới phụ thuộc NPC restore sau NPC.
- WorldResourceField/GrowingHerbNode/WorldResourceNode/WeatherAccumulation/WorldEvent: mỗi hệ state riêng; khi đổi field thời gian phải migration version.

## Điểm làm đúng

- Damage đi qua DamageContext/DamageSystem/IDamageable.
- Item có ItemId và fallback name/itemName để cứu save cũ.
- Weather visual/accumulation/event đã tách logic/visual và capture/restore state.
- Nhiều UI unsubscribe event OnDisable.
- Partial giúp định vị domain, dù shared state vẫn cần kỷ luật owner.

## Lộ trình ít đụng chạm

1. Test scene 20 NPC, 10 monster, 2 provider; bật CPU/GC profiler.
2. Sửa P0.3, test disable/re-enable provider giữa task.
3. WorldEvent double + migration; test năm 1/100/10,000.
4. Chuyển full/resource save sang file có fallback PlayerPrefs.
5. NonAlloc theo profiler, từng file và test route/unstuck.
6. Flag legacy Villager false, log action/destination một ngày game.

## Phụ lục: chỉ mục toàn bộ file/class

Mỗi dòng: `file | số dòng | kiểu/class khai báo`. Partial là một class chung, không phải class trùng.

```text
AdsInitializer.cs | 82 | public class AdsInitializer : MonoBehaviour, IUnityAdsInitializationListener 
+AutoDestroy.cs | 10 | public class AutoDestroy : MonoBehaviour 
+BicanhBoneMonsterAI.cs | 220 | public class BicanhBoneMonsterAI : MonoBehaviour 
+BicanhDungeonResident.cs | 8 | public class BicanhDungeonResident : MonoBehaviour 
+BicanhSessionManager.cs | 2198 | public class BicanhSessionSaveData;public class BicanhParticipantSaveData;public class BicanhBehaviourSaveData;public class BicanhSessionManager : MonoBehaviour;    class BehaviourState;    class ParticipantSnapshot 
+BlockingTilemap2D.cs | 82 | public class BlockingTilemap2D : MonoBehaviour 
+BoneSpiritAmbush.cs | 315 | public class BoneSpiritAmbush : MonoBehaviour 
+BottomMenuButtonRouter.cs | 647 | public class BottomMenuButtonRouter : MonoBehaviour 
+BuyerJob.cs | 37 | public class BuyerJob : MonoBehaviour 
+CameraBounds.cs | 124 | public class CameraBounds : MonoBehaviour 
+CameraWorldPlaneUtility.cs | 57 | public static class CameraWorldPlaneUtility 
+CharacterStats.cs | 525 | public class CharacterStats : MonoBehaviour, IDamageable 
+CloseMap.cs | 63 | public class CloseMap : 
+Combat/AutoDestroyEffect.cs | 10 | public class AutoDestroyEffect : MonoBehaviour 
+Combat/CharacterMovementAnimator.cs | 631 | public class CharacterMovementAnimator : MonoBehaviour 
+Combat/NpcRangedSkill.cs | 117 | public class NpcRangedSkill : MonoBehaviour 
+Combat/RangedProjectile2D.cs | 202 | public class RangedProjectile2D : MonoBehaviour 
+CombatPowerUtility.cs | 197 | public static class CombatPowerUtility 
+CombatStatCalculator.cs | 131 | public static class CombatStatCalculator 
+CultivationProgression.cs | 207 | public static class CultivationProgression 
+DailyConversation.cs | 152 | public class DailyConversation : MonoBehaviour 
+DamageContext.cs | 199 | public enum DamageType;public enum DamageSourceCategory;public enum DamageElement;public enum DamageBlockReason;public struct DamageContext;public struct DamageResult 
+DamageSystem.cs | 362 | public static class DamageSystem 
+DayNightLightingSystem.cs | 265 | public class DayNightLightingSystem : MonoBehaviour 
+DontDestroy.cs | 72 | public class DontDestroy : MonoBehaviour 
+DoorTeleport.cs | 389 | public class DoorTeleport : MonoBehaviour 
+DoorTeleportSameScene.cs | 200 | public class DoorTeleportSameScene : MonoBehaviour 
+EntityCore.cs | 542 | public enum EntityKind;public enum EntityGender;public enum TalentGrade;public enum EntityMood;public enum EntityGoal;public class EntityIdentity;public class EntityStats;public class EntityTalent;public class EntityPersonality;public class EntityEmotion;public class EntityNeeds;public class EntityMemory;public class EntityRelationship;public class EntityProfile : MonoBehaviour;public static class EntityGenerator 
+EventSystemGuard.cs | 246 | public class EventSystemGuard : MonoBehaviour 
+FavoriteNpcListUI.cs | 148 | public class FavoriteNpcListUI : MonoBehaviour 
+FavoriteNpcRowUI.cs | 78 | public class FavoriteNpcRowUI : MonoBehaviour 
+Fireball.cs | 152 | public class Fireball : MonoBehaviour 
+FullGameSaveController.cs | 2230 | public class FullGameSaveData;public class SavedFavoriteNpcData;public class SavedNpcStateData;public class FullGameSaveController : MonoBehaviour 
+GamePerformanceSettings.cs | 58 | public class GamePerformanceSettings : MonoBehaviour 
+GameSaveSystem.cs | 632 | public class SavedItemStack;public class SavedInventoryData;public class SavedShopStock;public class SavedShopData;public static class GameSaveSystem 
+GameTime.cs | 75 | public static class GameTime 
+GiveItemToNpcButton.cs | 16 | public class GiveItemToNpcButton : MonoBehaviour 
+GoldenEnergyEffect.cs | 74 | public class GoldenEnergyEffect : MonoBehaviour 
+GrowingHerbField.cs | 122 | public class GrowingHerbField : MonoBehaviour 
+GrowingHerbNode.cs | 477 | public interface IWorldResourcePersistentState;public class GrowingHerbNodeState;public class GrowingHerbNode : MonoBehaviour, IWorldResourcePersistentState 
+GuardJob.cs | 121 | public class GuardJob : MonoBehaviour 
+HarvestJob.cs | 588 | public class HarvestJob : MonoBehaviour 
+HealerJob.cs | 66 | public class HealerJob : MonoBehaviour 
+HeavenDaoPanelUI.cs | 469 | public class HeavenDaoPanelUI : MonoBehaviour 
+HeavenDaoSystem.cs | 515 | public enum HeavenDaoPower;public class HeavenDaoUnlock;public class HeavenDaoStoryReward;public class HeavenDaoSystem : MonoBehaviour 
+HeavenGiftPlacementController.cs | 1298 | public class HeavenGiftPlacementController : MonoBehaviour;    class HeavenGiftEffectSession 
+HeavenNurtureListItemUI.cs | 166 | public class HeavenNurtureListItemUI : MonoBehaviour 
+HeavenNurturePanelUI.cs | 1775 | public class HeavenNurturePanelUI : MonoBehaviour 
+HeavenNurtureTargetData.cs | 36 | public enum HeavenTargetType;public class HeavenNurtureTargetData;public class HeavenNurtureTargetDatabase 
+HeavenNurtureToggleButtonUI.cs | 137 | public class HeavenNurtureToggleButtonUI : MonoBehaviour 
+HeavenSystem.cs | 179 | public class HeavenSystem : MonoBehaviour 
+HeavenlyTribulationSystem.cs | 1175 | public enum TribulationTargetMotionMode;public class HeavenlyTribulationSystem : MonoBehaviour;    struct PillProtectionState;    struct TribulationRuntime;    class TribulationTargetLock 
+HunterJob.cs | 963 | public class HunterJob : MonoBehaviour 
+IDamageable.cs | 12 | public interface IDamageable 
+InGameMenuController.cs | 660 | public sealed class InGameMenuController : MonoBehaviour 
+InteriorCameraFocus.cs | 245 | public class InteriorCameraFocus : MonoBehaviour 
+InventoryItemButtonUI.cs | 644 | public class InventoryItemButtonUI : 
+InventoryPanelUI.cs | 2812 | public class InventoryPanelUI : MonoBehaviour;    enum InventoryCategoryFilter 
+InventoryToggleButton.cs | 153 | public class InventoryToggleButton : MonoBehaviour, IPointerDownHandler 
+ItemEffectSpawner.cs | 73 | public static class ItemEffectSpawner 
+ItemGradeFrameLibrary.cs | 37 | public static class ItemGradeFrameLibrary 
+ItemInventory.cs | 817 | public class ItemStack;public class ItemInventory : MonoBehaviour 
+ItemLifecycleSystem.cs | 104 | public enum ItemLifecycleEventType;public static class ItemLifecycleSystem 
+ItemPickup.cs | 38 | public class ItemPickup : MonoBehaviour 
+ItemStatBalanceUtility.cs | 438 | public static class ItemStatBalanceUtility;    struct IntRange 
+ItemText.cs | 258 | public class ItemTextDatabase;public class ItemTextCategory;public class ItemTextEntry;public static class ItemText 
+LightnighHitBurst.cs | 48 | public class LightningHitBurst : MonoBehaviour 
+LocalizationSettings.cs | 112 | public static class LocalizationSettings 
+MainMenuBackgroundVideoController.cs | 218 | public class MainMenuBackgroundVideoController : MonoBehaviour 
+MainMenuManager.cs | 1393 | public class MainMenuManager : MonoBehaviour 
+MapToggleUI.cs | 23 | public class MapToggleUI : MonoBehaviour 
+MobileCameraController.cs | 597 | public class MobileCameraController : MonoBehaviour 
+MobileSafeAreaFitter.cs | 16 | public class MobileSafeAreaFitter : MonoBehaviour 
+MonsterAI.AnimationDebug.cs | 96 | public partial class MonsterAI 
+MonsterAI.DevourDeathLootRespawn.cs | 327 | public partial class MonsterAI 
+MonsterAI.ItemEffects.cs | 98 | public partial class MonsterAI 
+MonsterAI.NeedsCultivation.cs | 254 | public partial class MonsterAI 
+MonsterAI.ProfileStats.cs | 305 | public partial class MonsterAI 
+MonsterAI.Status.cs | 27 | public partial class MonsterAI 
+MonsterAI.TargetingMovement.cs | 776 | public partial class MonsterAI 
+MonsterAI.TreasureCombat.cs | 360 | public partial class MonsterAI 
+MonsterAI.cs | 789 | public enum HuntTargetType;public partial class MonsterAI : MonoBehaviour, IDamageable, INpcActionStateOwner 
+MonsterAttack.cs | 163 | public class MonsterAttack : MonoBehaviour 
+MonsterDirectionalAnimator.cs | 351 | public class MonsterDirectionalAnimator : MonoBehaviour 
+NPCIdentity.cs | 216 | public enum Gender;public enum LifeStage;public static class NpcAgeUtility;public class NPCIdentity : MonoBehaviour 
+NPCLifecycle.cs | 239 | public class NPCLifecycle : MonoBehaviour 
+NPCVisualAnimation.cs | 1469 | public class NPCVisualAnimation : MonoBehaviour;    enum ActionCategory 
+NPCVisualProfile.cs | 43 | public class NPCVisualProfile : ScriptableObject 
+NPCVisualResolver.cs | 143 | public class NPCVisualResolver : MonoBehaviour 
+NewGameIntroPanel.cs | 1740 | public class NewGameIntroPanel : MonoBehaviour 
+NpcActionState.cs | 249 | public enum NpcActionId;public struct NpcActionState;public interface INpcActionStateOwner;public static class NpcActionStateCatalog 
+NpcAlchemyAgent.cs | 1460 | public class AlchemyIngredient;public class AlchemyRecipe;public class NpcAlchemyAgent : MonoBehaviour;    class AlchemyCost 
+NpcAlchemyRole.cs | 123 | public class NpcAlchemyRole : MonoBehaviour 
+NpcAreaUtility.cs | 50 | public static class NpcAreaUtility 
+NpcCollisionRegistry.cs | 246 | public static class NpcCollisionRegistry 
+NpcCombatHitbox2D.cs | 176 | public class NpcCombatHitbox2D : MonoBehaviour 
+NpcCombatTechniqueSystem.cs | 606 | public static class NpcCombatTechniqueSystem;public sealed class NpcCombatTechniqueRuntime : MonoBehaviour;public sealed class NpcCombatTechniqueBuff : MonoBehaviour 
+NpcCounterBroker.cs | 1496 | public class NpcCounterBroker : MonoBehaviour 
+NpcCultivationAwakeningUtility.cs | 283 | public static class NpcCultivationAwakeningUtility 
+NpcDailyRoutineLibrary.cs | 567 | public static class NpcDailyRoutineLibrary 
+NpcData.cs | 101 | public class NpcData : MonoBehaviour 
+NpcDialogueRuntime.cs | 1284 | public enum NpcSpeechDisplayState;public class NpcDialogueDatabase;public class NpcDialogueRuleData;public static class NpcDialogueCatalog;public static class NpcDialogueSelector;public class NpcSpeechController : MonoBehaviour;static class NpcDialogueContextBuilder 
+NpcEconomy.cs | 501 | public enum NpcTradeContext;public static class NpcEconomy 
+NpcEscortTaskForceStarter.cs | 236 | public class NpcEscortTaskForceStarter : MonoBehaviour;    struct NpcEligibilitySnapshot 
+NpcFavorite.cs | 308 | public class NpcFavorite : MonoBehaviour 
+NpcFavoriteButtonUI.cs | 253 | public class NpcFavoriteButtonUI : MonoBehaviour 
+NpcFavoriteClickBridge.cs | 151 | public class NpcFavoriteClickBridge : MonoBehaviour 
+NpcFavoriteManager.cs | 231 | public class NpcFavoriteManager : MonoBehaviour 
+NpcFixedAlchemistController.cs | 1102 | public class FixedAlchemistMaterialRequirement;public class NpcFixedAlchemistController : MonoBehaviour 
+NpcFixedBlacksmithController.cs | 3102 | public class FixedBlacksmithMaterialRequirement;public class NpcFixedBlacksmithController : MonoBehaviour 
+NpcForgeAgent.cs | 2374 | public class ForgeIngredient;public class ForgeRecipe;public class ForgeCustomerOrder;public class NpcForgeAgent : MonoBehaviour;    class ForgeCost 
+NpcForgeRole.cs | 175 | public class NpcForgeRole : MonoBehaviour 
+NpcHideCultivationInfo.cs | 7 | public class NpcHideCultivationInfo : MonoBehaviour 
+NpcInteractionPoint.cs | 440 | public class NpcInteractionPoint : MonoBehaviour;    class StandReservation 
+NpcInventoryDropper.cs | 89 | public static class NpcInventoryDropper 
+NpcInventoryPanelUI.cs | 1299 | public class NpcInventoryPanelUI : MonoBehaviour 
+NpcItemCollector.cs | 967 | public class NpcItemCollector : MonoBehaviour 
+NpcJobState.cs | 8 | public enum NpcJobState 
+NpcLawZone.cs | 6 | public class NpcLawZone : NpcLawZoneInternal 
+NpcLocationArea.cs | 574 | public enum NpcLocationPurpose;public enum NpcDangerTier;public class NpcLocationArea : MonoBehaviour 
+NpcMapArea.cs | 253 | public enum NpcMapZone;public class NpcMapArea : MonoBehaviour 
+NpcMapBehaviorPolicy.cs | 240 | public static class NpcMapBehaviorPolicy 
+NpcMapBoundaryClamp.cs | 232 | public class NpcMapBoundaryClamp : MonoBehaviour 
+NpcMapDestination.cs | 5 | public class NpcMapDestination : MonoBehaviour 
+NpcMapMover2D.cs | 1818 | public class NpcMapMover2D : MonoBehaviour, INpcMovementResultProvider 
+NpcMapNavigator.cs | 576 | public enum NpcRouteStatus;public static class NpcMapNavigator;    struct ZoneLockState 
+NpcMerchantRole.cs | 107 | public class NpcMerchantRole : MonoBehaviour 
+NpcMovementState.cs | 59 | public enum NpcMovementStatus;public enum NpcMovementFailureReason;public struct NpcMovementResult;public interface INpcMovementResultProvider 
+NpcPathMemorySystem.cs | 192 | public static class NpcPathMemorySystem;    class RememberedPath 
+NpcPerformanceOverlay.cs | 225 | public class NpcPerformanceOverlay : MonoBehaviour 
+NpcPetCompanion.cs | 56 | public class NpcPetCompanion : MonoBehaviour 
+NpcPortraitIcon.cs | 6 | public class NpcPortraitIcon : MonoBehaviour 
+NpcRageNearbyAttack.cs | 412 | public class NpcRageNearbyAttack : MonoBehaviour;    struct NearbyTarget 
+NpcResourceGatherer.cs | 1655 | public class NpcResourceGatherer : MonoBehaviour 
+NpcRoleUtility.cs | 671 | public static class NpcRoleUtility 
+NpcScheduleController.cs | 502 | public enum NpcLifePath;public enum NpcScheduleActivity;public enum NpcSocialChannel;public class NpcScheduleSlot;public class NpcScheduleController : MonoBehaviour 
+NpcSharedInventoryLink.cs | 66 | public class NpcSharedInventoryLink : MonoBehaviour 
+NpcShopStockRefill.cs | 514 | public class NpcShopStockRefill : MonoBehaviour 
+NpcSocialSystem.cs | 2928 | public class NpcSocialSystem : MonoBehaviour;public enum NpcMemoryType;public enum NpcDecisionKind;public class NpcMemoryRecord;public class NpcSocialRelationship;public static class NpcSocialEventBus;public static class NpcMonsterCombatDialogue;    class CombatRecord;public class NpcSocialIdentity : MonoBehaviour;public class NpcNeeds : MonoBehaviour;public class NpcPersonality : MonoBehaviour;public class NpcRelationshipGraph : MonoBehaviour;public class NpcMemory : MonoBehaviour;public class NpcOverheadDialogueUI : MonoBehaviour;public class NpcConversationSession;public class NpcConversationAgent : MonoBehaviour;public class NpcDecisionBrain : MonoBehaviour;public class NpcNegotiationAgent : MonoBehaviour;public abstract class NpcLawZoneInternal : MonoBehaviour;public class NpcSocialWorldInstaller : MonoBehaviour;public static class NpcSocialTime 
+NpcSpecialProfession.cs | 28 | public class NpcSpecialProfession : MonoBehaviour 
+NpcTaskProvider.EscortRuntime.cs | 484 | public partial class NpcTaskProvider 
+NpcTaskProvider.GatherProgress.cs | 710 | public partial class NpcTaskProvider 
+NpcTaskProvider.Hunt.cs | 486 | public partial class NpcTaskProvider 
+NpcTaskProvider.OfferEvaluation.cs | 530 | public partial class NpcTaskProvider 
+NpcTaskProvider.Runtime.cs | 375 | public partial class NpcTaskProvider 
+NpcTaskProvider.TaskExecution.cs | 784 | public partial class NpcTaskProvider 
+NpcTaskProvider.TravelWatchdog.cs | 692 | public partial class NpcTaskProvider 
+NpcTaskProvider.TurnIn.cs | 176 | public partial class NpcTaskProvider 
+NpcTaskProvider.WorkPositions.cs | 424 | public partial class NpcTaskProvider 
+NpcTaskProvider.cs | 3464 | public enum NpcTaskType;public enum NpcTaskRank;public enum TavernTaskStage;public enum TavernMealStage;public class NpcTaskOffer;class RunningNpcTask;class RunningTavernMeal;class PendingTaskGoods;class HuntOfferSeed;public partial class NpcTaskProvider : MonoBehaviour 
+NpcTavernEntrance.cs | 327 | public class NpcTavernEntrance : MonoBehaviour;class NpcTavernVisit 
+NpcTeleportGate.cs | 816 | public class NpcTeleportGate : MonoBehaviour 
+NpcText.cs | 576 | public class NpcTextDatabase;public class NpcTextCategory;public class NpcTextEntry;public class NpcTextList;public static class NpcText 
+NpcTradeAgent.cs | 709 | public class NpcTradeAgent : MonoBehaviour 
+NpcWorkArea.cs | 206 | public class NpcWorkArea : MonoBehaviour 
+OpenMap.cs | 85 | public class OpenMap : 
+OpenShopUi.cs | 10 | public class OpenShopUI : MonoBehaviour 
+PickupVisualUtility.cs | 49 | public static class PickupVisualUtility 
+PlayerHealth.cs | 164 | public class PlayerHealth : MonoBehaviour, IDamageable 
+PlayerWallet.cs | 175 | public enum WalletGrantSource;public class PlayerWallet : MonoBehaviour 
+PlayerWalletGrantButton.cs | 47 | public class PlayerWalletGrantButton : MonoBehaviour 
+PlayerWalletTextUI.cs | 114 | public class PlayerWalletTextUI : MonoBehaviour 
+PopulationWidgetUI.cs | 351 | public class PopulationWidgetUI : MonoBehaviour 
+ResourceNode.cs | 263 | public enum HarvestResourceKind;public class ResourceNode : MonoBehaviour 
+ResponsiveCanvasScaler.cs | 17 | public class ResponsiveCanvasScaler : MonoBehaviour 
+ResponsivePanelFitter.cs | 21 | public class ResponsivePanelFitter : MonoBehaviour 
+RewardedLTAdsButton.cs | 266 | public class RewardedLTAdsButton : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener 
+SceneLoader.cs | 93 | public class SceneLoader : MonoBehaviour 
+SceneLocalizedTextBootstrap.cs | 400 | public class SceneLocalizedTextBootstrap : MonoBehaviour;    struct SceneTextEntry 
+SceneTransitionOverlay.cs | 169 | public class SceneTransitionOverlay : MonoBehaviour 
+SellerJob.cs | 49 | public class SellerJob : MonoBehaviour 
+ShopCardHoverEffect.cs | 61 | public class ShopCardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler 
+ShopItemAssetPostprocessor.cs | 50 | public class ShopItemAssetPostprocessor : AssetPostprocessor 
+ShopItemButtonUI.cs | 973 | public class ShopItemButtonUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler 
+ShopPanelUI.cs | 2629 | public class ShopPanelUI : MonoBehaviour 
+ShopToggleUI.cs | 23 | public class ShopToggleUI : MonoBehaviour 
+SimpleItemShop.cs | 763 | public class ShopItemSlot;public class SimpleItemShop : MonoBehaviour 
+SkyDropToPosition.cs | 95 | public class SkyDropToPosition : MonoBehaviour 
+SmartAITask.cs | 53 | public enum SmartAITaskGoal;public enum SmartAITaskPriority;public class SmartAITask 
+SmartNpcAI.Brain.cs | 329 | public partial class SmartNpcAI 
+SmartNpcAI.CombatSupport.cs | 674 | public partial class SmartNpcAI 
+SmartNpcAI.DirectedActions.cs | 720 | public partial class SmartNpcAI 
+SmartNpcAI.MonsterCombat.cs | 694 | public partial class SmartNpcAI 
+SmartNpcAI.Movement.cs | 1968 | public partial class SmartNpcAI 
+SmartNpcAI.NavigationRecovery.cs | 1503 | public partial class SmartNpcAI 
+SmartNpcAI.Needs.cs | 64 | public partial class SmartNpcAI 
+SmartNpcAI.Schedule.cs | 1668 | public partial class SmartNpcAI 
+SmartNpcAI.Tasks.cs | 855 | public partial class SmartNpcAI 
+SmartNpcAI.cs | 3789 | public enum CultivationRealm;public enum PhysiqueType;public partial class SmartNpcAI : MonoBehaviour, IDamageable, INpcActionStateOwner 
+SmartNpcHelpRequestSystem.cs | 310 | public class SmartNpcHelpRequestSystem : MonoBehaviour 
+SpawnRegion.cs | 27 | public class SpawnRegion : MonoBehaviour 
+SpawnedWorldActor.cs | 186 | public class SpawnedWorldActor : MonoBehaviour 
+StatItemApplier.cs | 22 | public class StatItemApplier : MonoBehaviour 
+StatItemData.cs | 943 | public enum ItemType;public enum ItemGrade;public enum CultivationManualMastery;public enum RawUsePolicy;public enum NpcItemIntent;public enum ItemUseStyle;public enum EquipmentSlot;public enum ArtifactKind;public enum PillKind;public enum ManualKind;public enum MaterialKind;public enum FoodKind;public enum ItemConversionType;public enum ItemTargetType;public enum StatType;public class StatModifier;public class StatItemData : ScriptableObject 
+TargetReservationSystem.cs | 301 | public class TargetReservationSystem : MonoBehaviour;    class TargetReservation 
+TaskBoardInteract.cs | 138 | public class TaskBoardInteract : MonoBehaviour 
+TaskBoardPanelUI.cs | 193 | public class TaskBoardPanelUI : MonoBehaviour 
+TaskBoardRowUI.cs | 173 | public class TaskBoardRowUI : MonoBehaviour 
+ThienKiepStrikePrefab.cs | 449 | public class ThienKiepStrikePrefab : MonoBehaviour 
+TilemapObstacle2D.cs | 76 | public class TilemapObstacle2D : MonoBehaviour 
+TimedStatItemBuff.cs | 44 | public class TimedStatItemBuff : MonoBehaviour 
+TouchSelectTarget.cs | 4405 | public class TouchSelectTarget : MonoBehaviour 
+TreasureFrenzySystem.cs | 1060 | public class TreasureFrenzySystem : MonoBehaviour;    enum ActorKind;    class TreasureParticipant;    class TreasureFrenzyEvent 
+TreasureHeatSystem.cs | 707 | public class TreasureHeatSystem : MonoBehaviour;    struct TreasureThreat 
+UIBorderSparkEffect.cs | 485 | public class UIBorderSparkEffect : MonoBehaviour 
+UIWorldStoryManager.cs | 238 | public class UIWorldStoryManager : MonoBehaviour 
+UiText.cs | 177 | public class UiTextDatabase;public class UiTextCategory;public class UiTextEntry;public class UiTextList;public static class UiText 
+VillageHomeManager.cs | 119 | public class VillageHomeManager : MonoBehaviour 
+VillageNpcSetupTool.cs | 94 | public class VillageNpcSetupTool : MonoBehaviour 
+VillageStorage.cs | 119 | public class VillageStorage : MonoBehaviour 
+VillagerAI.Brain.cs | 800 | public partial class VillagerAI 
+VillagerAI.LegacyCultivation.cs | 231 | public partial class VillagerAI 
+VillagerAI.LegacyDailyTaskPlan.cs | 317 | public partial class VillagerAI 
+VillagerAI.LegacyTreasure.cs | 86 | public partial class VillagerAI 
+VillagerAI.Movement.cs | 1676 | public partial class VillagerAI 
+VillagerAI.NavigationRecovery.cs | 1626 | public partial class VillagerAI 
+VillagerAI.Pathfinding.cs | 938 | public partial class VillagerAI;    class PathNode 
+VillagerAI.ProfessionWork.cs | 124 | public partial class VillagerAI 
+VillagerAI.Status.cs | 64 | public partial class VillagerAI 
+VillagerAI.Trade.cs | 14 | public partial class VillagerAI 
+VillagerAI.Work.cs | 318 | public partial class VillagerAI 
+VillagerAI.cs | 3656 | public enum VillagerAgeGroup;public enum VillagerJob;public partial class VillagerAI : MonoBehaviour, IDamageable, INpcActionStateOwner;    class DailyTaskNeed 
+VillagerBirthManager.cs | 495 | public class VillagerBirthManager : MonoBehaviour 
+VillagerJobDispatcher.cs | 199 | public class VillagerJobDispatcher : MonoBehaviour 
+VillagerRelationship.cs | 199 | public enum VillagerRelationshipStatus;public class VillagerRelationship : MonoBehaviour 
+VillagerRelationshipManager.cs | 737 | public class VillagerRelationshipManager : MonoBehaviour 
+WeatherAccumulationSystem.cs | 781 | public class WeatherAccumulationPersistentState;public class WeatherAccumulationSystem : MonoBehaviour, ISerializationCallbackReceiver;    class PuddleEntry;    class SnowEntry 
+WeatherControlPanelUI.cs | 250 | public class WeatherControlPanelUI : MonoBehaviour 
+WeatherSystem.cs | 288 | public enum WorldWeather;public class WeatherPersistentState;public class WeatherSystem : MonoBehaviour 
+WeatherVisualSystem.cs | 610 | public class WeatherVisualSystem : MonoBehaviour 
+WorldClockTextUI.cs | 413 | public class WorldClockTextUI : MonoBehaviour 
+WorldEventManager.cs | 221 | public class LogEntry;public class WorldEventManager : MonoBehaviour 
+WorldEventSystem.cs | 246 | public enum WorldEventType;public class WorldEventPersistentState;public class WorldEventSystem : MonoBehaviour 
+WorldItemInfoPanelUI.cs | 1635 | public class WorldItemInfoPanelUI : MonoBehaviour 
+WorldResourceField.cs | 1036 | public class ResourceFieldItemEntry;public class SavedWorldResourceNode;public class SavedWorldResourceField;public class WorldResourceField : MonoBehaviour, ISerializationCallbackReceiver;    sealed class ResolvedSavedResourceNode 
+WorldResourceNode.cs | 396 | public enum ResourceRespawnMode;public class WorldResourceNode : MonoBehaviour, ISerializationCallbackReceiver 
+WorldScreenNotificationHub.cs | 735 | public class WorldScreenNotificationHub : MonoBehaviour;    struct NoticeRequest;    enum NoticeKind;    struct NoticeView 
+WorldSimulationBootstrap.cs | 58 | public static class WorldSimulationBootstrap 
+WorldStatItemPickup.cs | 495 | public class WorldStatItemPickup : MonoBehaviour 
+WorldTilemapManager.cs | 489 | public class WorldTilemapManager : MonoBehaviour 
+WorldTimeSystem.cs | 572 | public enum WorldTimePhase;public class WorldTimeSystem : MonoBehaviour 
+mau.cs | 168 | public class mau : MonoBehaviour 
+menumap.cs | 133 | public class Menumap : MonoBehaviour 
+```

