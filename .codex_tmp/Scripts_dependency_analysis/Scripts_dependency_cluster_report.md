# Báo cáo rà soát Scripts(3).zip theo quan hệ gọi thật trong code

## Phạm vi kiểm tra

- Đã giải nén `Scripts(3).zip`.
- Tổng số file C#: **208**.
- Tổng số type/class/enum/struct/interface phát hiện: **334**.
- Cách lọc cụm: dựa trên `GetComponent<>`, `FindObject(s)ByType<>`, `AddComponent<>`, field/param type, `Class.Instance`, static call `Class.Method`, generic `List<Class>`, enum/static usage. Không gom chỉ theo tên file.
- Lưu ý: Unity Event/Inspector/prefab reference không thể thấy đầy đủ nếu chỉ có thư mục Scripts. Những file UI/Effect không có code gọi trực tiếp vẫn có thể đang được gắn trong Scene/Prefab.

---

## 1. Các trục lõi đang bị gọi nhiều nhất

Các trục này không nên xóa/đổi tên/tách folder tùy tiện:

| Trục | Vai trò | Dấu hiệu code gọi thật |
|---|---|---|
| `VillagerAI` | dân làng, lịch, nghề, giao thương, tài nguyên | bị rất nhiều class `GetComponent<VillagerAI>()`, và bản thân gọi `NpcScheduleController`, `NpcTaskProvider`, `HunterJob`, `HarvestJob`, `NpcResourceGatherer`, `ItemInventory` |
| `SmartNpcAI` | NPC tu sĩ/thông minh, lịch, chiến đấu, nhiệm vụ | gọi `NpcScheduleController`, `NpcTaskProvider`, `NpcMapNavigator`, `NpcCombatTechniqueSystem`, `ItemInventory`, `SmartNpcHelpRequestSystem` |
| `MonsterAI` | yêu thú/quái, săn mục tiêu, loot, tu luyện | gọi `VillagerAI`, `SmartNpcAI`, `CharacterStats`, `CombatStatCalculator`, `NpcCombatTechniqueSystem`, `WorldStatItemPickup`, `GameSaveSystem` |
| `WorldTimeSystem` | thời gian thế giới | bị gọi bởi lịch NPC, job, social, weather, save, UI đồng hồ |
| `StatItemData` + `ItemInventory` | lõi vật phẩm/balo | bị UI, shop, NPC trade, Heaven gift, save, pickup gọi |
| `NpcScheduleController` | bộ lịch NPC | được `VillagerAI`, `SmartNpcAI`, `NpcTaskProvider`, `NpcSocialSystem` gọi để quyết định làm việc/giao thương/social |
| `NpcRoleUtility` | utility chung cho actor | được dùng nhiều để lấy power, set action, kiểm tra trạng thái, phân loại NPC |
| `NpcText`, `ItemText`, `UiText` | text/localization | bị gọi khắp UI, NPC action, item display, world story |

---

## 2. Cụm AI dân làng / tu sĩ / lịch sinh hoạt

### File chính

- `VillagerAI.cs`
- `VillagerAI.Brain.cs`
- `VillagerAI.Work.cs`
- `VillagerAI.ProfessionWork.cs`
- `VillagerAI.Trade.cs`
- `VillagerAI.Status.cs`
- `VillagerAI.LegacyCultivation.cs`
- `VillagerAI.LegacyDailyTaskPlan.cs`
- `VillagerAI.LegacyTreasure.cs`
- `SmartNpcAI.cs`
- `SmartNpcAI.Brain.cs`
- `SmartNpcAI.Schedule.cs`
- `SmartNpcAI.Tasks.cs`
- `SmartNpcAI.Needs.cs`
- `SmartNpcAI.CombatSupport.cs`
- `NpcScheduleController.cs`
- `NpcDailyRoutineLibrary.cs`
- `NpcMapNavigator.cs`
- `NpcMapMover2D.cs`
- `NpcMapArea.cs`
- `NpcLocationArea.cs`
- `NpcTeleportGate.cs`

### Quan hệ code thật

- `VillagerAI` có `[RequireComponent(typeof(NpcScheduleController))]` tại `VillagerAI.cs:25`.
- `VillagerAI.Brain.cs` gọi `WorldTimeSystem.Instance` tại dòng 7-11 để reset plan theo ngày.
- `VillagerAI.Brain.cs:82-83` gọi `NpcScheduleController.GetSchedule(gameObject)`.
- `VillagerAI.Brain.cs:206` lấy `NpcResourceGatherer` bằng `GetComponent<NpcResourceGatherer>()`.
- `VillagerAI.Brain.cs:212` lấy `HarvestJob`, dòng 218 lấy `HunterJob`.
- `VillagerAI.Brain.cs:328-329` gọi `NpcTaskProvider.FindNearestProvider(transform.position)`.
- `SmartNpcAI.cs` có `[RequireComponent(typeof(NpcScheduleController))]` tại `SmartNpcAI.cs:21`.
- `SmartNpcAI.Brain.cs:253-254` lấy `NpcScheduleController` bằng `GetComponent<NpcScheduleController>()`.
- `SmartNpcAI.Schedule.cs:927-928` gọi `NpcTaskProvider.FindNearestProvider(...)`.
- `SmartNpcAI.cs:581-584` lấy/tạo `ItemInventory` cho NPC.
- `SmartNpcAI.cs:5836` gọi `NpcCombatTechniqueSystem.ModifyOutgoingDamage(...)`.
- `NpcScheduleController.cs:243-244` kiểm tra actor là `VillagerAI` hoặc `SmartNpcAI`.
- `NpcScheduleController.cs:344`, `437`, `448` gọi `WorldTimeSystem.Instance`.

### Kết luận cụm

Đây là cụm trung tâm của game. Nếu sửa AI hoặc lịch sinh hoạt, phải xem chung `VillagerAI`, `SmartNpcAI`, `NpcScheduleController`, `NpcTaskProvider`, `WorldTimeSystem`, `NpcMapNavigator`. Không nên chỉ sửa một file theo tên.

---

## 3. Cụm nghề nghiệp / tài nguyên / lao động

### File chính

- `HarvestJob.cs`
- `HunterJob.cs`
- `BuyerJob.cs`
- `SellerJob.cs`
- `GuardJob.cs`
- `HealerJob.cs`
- `NpcResourceGatherer.cs`
- `WorldResourceField.cs`
- `WorldResourceNode.cs`
- `ResourceNode.cs`
- `WorldTilemapManager.cs`
- `VillageStorage.cs`
- `VillagerJobDispatcher.cs`
- `VillageHomeManager.cs`

### Quan hệ code thật

- `VillagerAI.Brain.cs:206` gọi `NpcResourceGatherer`.
- `VillagerAI.Brain.cs:212` gọi `HarvestJob`.
- `VillagerAI.Brain.cs:218` gọi `HunterJob`.
- `VillagerAI.Work.cs:37-40` tự thêm `HunterJob` nếu thiếu bằng `gameObject.AddComponent<HunterJob>()`.
- `HarvestJob.cs` gọi `VillagerAI`, `NpcResourceGatherer`, `ItemInventory`, `VillageStorage.Instance`.
- `HunterJob.cs` gọi `WorldTimeSystem.Instance`, `WorldTilemapManager.Instance`, `TargetReservationSystem.Instance`.
- `WorldResourceField.cs:240-243` lấy/tạo `WorldResourceNode`.
- `WorldResourceField.cs:422`, `445`, `449`, `457`, `487` gọi `GameSaveSystem` để đăng ký/lưu/tải item tài nguyên.

### Kết luận cụm

Nghề nghiệp không độc lập. Nó phụ thuộc trực tiếp vào `VillagerAI`, `NpcScheduleController`, `WorldTimeSystem`, `ItemInventory`, `WorldResourceField/Node`, `VillageStorage`. Nếu muốn làm dân làng “chỉ đi làm đúng giờ”, cụm này là nơi cần rút gọn.

---

## 4. Cụm yêu thú / combat / chỉ số / tu luyện

### File chính

- `MonsterAI.cs`
- `MonsterAI.TargetingMovement.cs`
- `MonsterAI.TreasureCombat.cs`
- `MonsterAI.ProfileStats.cs`
- `MonsterAI.NeedsCultivation.cs`
- `MonsterAI.DevourDeathLootRespawn.cs`
- `MonsterAI.Status.cs`
- `MonsterAttack.cs`
- `CharacterStats.cs`
- `CombatStatCalculator.cs`
- `CombatPowerUtility.cs`
- `CultivationProgression.cs`
- `NpcCombatTechniqueSystem.cs`
- `NpcCombatHitbox2D.cs`
- `NpcRangedSkill.cs`
- `Combat/RangedProjectile2D.cs`
- `PlayerHealth.cs`
- `IDamageable.cs`

### Quan hệ code thật

- `MonsterAI.TargetingMovement.cs:99` kiểm tra `VillagerAI` trên target.
- `MonsterAI.TargetingMovement.cs:104` kiểm tra `SmartNpcAI` trên target.
- `MonsterAI.TargetingMovement.cs:271` lấy `CharacterStats` từ target.
- `MonsterAI.ProfileStats.cs:20`, `84`, `88-90` gọi `CombatStatCalculator` để tính multiplier/chỉ số.
- `MonsterAI.TreasureCombat.cs:198` gọi `NpcCombatTechniqueSystem.ModifyOutgoingDamage(...)`.
- `MonsterAI.TreasureCombat.cs:270` gọi `CombatStatCalculator.CalculateFinalDamageInt(...)`.
- `MonsterAI.DevourDeathLootRespawn.cs:292-293` tạo loot bằng `WorldStatItemPickup`.
- `MonsterAI.DevourDeathLootRespawn.cs:306` gọi `GameSaveSystem.RegisterItem(loot)`.

### Kết luận cụm

Combat không chỉ nằm trong `MonsterAI`. Nó kéo theo `CharacterStats`, `CombatStatCalculator`, `CultivationProgression`, `NpcCombatTechniqueSystem`, `IDamageable`, `WorldStatItemPickup`, `GameSaveSystem`.

---

## 5. Cụm vật phẩm / balo / shop / kinh tế

### File chính

- `StatItemData.cs`
- `ItemInventory.cs`
- `InventoryPanelUI.cs`
- `InventoryItemButtonUI.cs`
- `InventoryToggleButton.cs`
- `WorldStatItemPickup.cs`
- `ItemPickup.cs`
- `ItemLifecycleSystem.cs`
- `ItemEffectSpawner.cs`
- `StatItemApplier.cs`
- `TimedStatItemBuff.cs`
- `SimpleItemShop.cs`
- `ShopPanelUI.cs`
- `ShopItemButtonUI.cs`
- `ShopItemAssetPostprocessor.cs`
- `OpenShopUi.cs`
- `NpcEconomy.cs`
- `NpcTradeAgent.cs`
- `NpcItemCollector.cs`
- `NpcCounterBroker.cs`
- `PlayerWallet.cs`
- `PlayerWalletTextUI.cs`

### Quan hệ code thật

- `ShopPanelUI.cs:10-12` có field trực tiếp tới `SimpleItemShop`, `PlayerWallet`, `ItemInventory`.
- `ShopPanelUI.cs:374` nhận `StatItemData` để tính giá.
- `ShopPanelUI.cs:378` gọi `NpcEconomy.GetTradePrice(item, NpcTradeContext.MarketBuy)`.
- `ShopPanelUI.cs:490` tìm `ItemInventory` bằng `FindAnyObjectByType<ItemInventory>()`.
- `ShopPanelUI.cs:497` tìm `PlayerWallet`.
- `SimpleItemShop.cs:35` có `ItemInventory sellerInventory`.
- `SimpleItemShop.cs:78`, `96`, `115` nhận `StatItemData` để mua/bán.
- `SimpleItemShop.cs:86`, `105`, `162`, `244`, `343` gọi `NpcEconomy`.
- `NpcTradeAgent.cs:5` giữ `ItemInventory inventory`.
- `NpcTradeAgent.cs:43-48` lấy/tự thêm `ItemInventory`.
- `NpcTradeAgent.cs:332` gọi `ItemLifecycleSystem.Notify(...)`.
- `InventoryPanelUI.cs:10`, `20`, `22`, `24` giữ `ItemInventory`, `StatItemData` và inventory NPC/player.
- `InventoryPanelUI.cs:895-901` lấy/tự thêm `ItemInventory` trên selected target.

### Kết luận cụm

`StatItemData` và `ItemInventory` là lõi. Shop, NPC trade, Heaven gift, pickup, save đều phụ thuộc nó. Không nên đổi field trong `StatItemData` nếu chưa kiểm tra `InventoryPanelUI`, `ShopPanelUI`, `SimpleItemShop`, `NpcTradeAgent`, `WorldStatItemPickup`, `GameSaveSystem`.

---

## 6. Cụm Thiên Đạo / ban tặng / bồi dưỡng / thiên kiếp

### File chính

- `HeavenSystem.cs`
- `HeavenDaoSystem.cs`
- `HeavenDaoPanelUI.cs`
- `HeavenGiftPlacementController.cs`
- `HeavenlyTribulationSystem.cs`
- `ThienKiepStrikePrefab.cs`
- `HeavenNurturePanelUI.cs`
- `HeavenNurtureListItemUI.cs`
- `HeavenNurtureTargetData.cs`
- `HeavenNurtureToggleButtonUI.cs`
- `TouchSelectTarget.cs`
- `GoldenEnergyEffect.cs`
- `SkyDropToPosition.cs`

### Quan hệ code thật

- `HeavenDaoPanelUI.cs:69` lấy `HeavenDaoSystem.Instance`.
- `HeavenDaoSystem.cs:153` đăng ký event `WorldEventManager.LogAdded`.
- `HeavenDaoSystem.cs:393-398` gọi `WorldEventManager.Instance.GetStoryLogs()`.
- `HeavenGiftPlacementController.cs:179-180` lấy `HeavenSystem.Instance`.
- `HeavenGiftPlacementController.cs:310-331` gọi `WorldTimeSystem.Instance` và `DayNightLightingSystem.Instance`.
- `HeavenGiftPlacementController.cs:919-922` xác định target bằng `SmartNpcAI`, `VillagerAI`, `MonsterAI`, `CharacterStats`.
- `HeavenGiftPlacementController.cs:934-956` lấy `CharacterStats`, `SmartNpcAI`, `VillagerAI`, `MonsterAI` để apply hiệu ứng.
- `HeavenNurturePanelUI.cs:1321-1322` lấy `MonsterAI`.
- `HeavenNurturePanelUI.cs:1402`, `1414`, `1422` lấy `CharacterStats`, `SmartNpcAI`, `VillagerAI`.
- `HeavenlyTribulationSystem.cs:188` gọi `HeavenSystem.Instance`.
- `HeavenlyTribulationSystem.cs:307`, `314` lấy `SmartNpcAI` và `MonsterAI`.

### Kết luận cụm

Cụm Thiên Đạo là layer player can thiệp lên NPC/monster/item. Nó phụ thuộc cực mạnh vào `StatItemData`, `ItemInventory`, `WorldTimeSystem`, `WorldEventManager`, `VillagerAI`, `SmartNpcAI`, `MonsterAI`, `CharacterStats`.

---

## 7. Cụm nhiệm vụ / task board / quán rượu / giao hàng

### File chính

- `NpcTaskProvider.cs`
- `NpcTaskProvider.OfferEvaluation.cs`
- `TaskBoardInteract.cs`
- `TaskBoardPanelUI.cs`
- `TaskBoardRowUI.cs`
- `NpcEscortTaskForceStarter.cs`
- `NpcTavernEntrance.cs`
- `NpcAlchemyAgent.cs`
- `NpcForgeAgent.cs`
- `NpcAlchemyRole.cs`
- `NpcForgeRole.cs`
- `NpcMerchantRole.cs`

### Quan hệ code thật

- `NpcTaskProvider.cs:160-172` duy trì danh sách provider và tìm provider gần nhất.
- `VillagerAI.Brain.cs:328-329` gọi `NpcTaskProvider.FindNearestProvider(...)`.
- `SmartNpcAI.Schedule.cs:927-928` gọi `NpcTaskProvider.FindNearestProvider(...)`.
- `TaskBoardInteract.cs:106` tìm `NpcTaskProvider` bằng `FindAnyObjectByType<NpcTaskProvider>()`.
- `TaskBoardPanelUI.cs:34` tìm `NpcTaskProvider`.
- `NpcTaskProvider.cs:1016`, `1025`, `1046` gọi `NpcScheduleController` để quyết định NPC có được task/eat hay không.
- `NpcTaskProvider.cs:3695-3696` gọi `WorldTimeSystem.Instance.CurrentDay`.
- `NpcTaskProvider.cs:3897-3899` ghi log qua `WorldEventManager.Instance.AddLog(...)`.
- `NpcTaskProvider.cs:4802` lấy `NpcTradeAgent` trên NPC.

### Kết luận cụm

Task provider là cầu nối giữa NPC AI, lịch sinh hoạt, trade, world event và UI task board. Nếu sửa nhiệm vụ, phải xem đồng thời `NpcTaskProvider`, `VillagerAI`, `SmartNpcAI`, `NpcScheduleController`, `NpcTradeAgent`, `TaskBoardPanelUI`.

---

## 8. Cụm social / quan hệ / yêu thích / chọn target

### File chính

- `NpcSocialSystem.cs`
- `DailyConversation.cs`
- `VillagerRelationship.cs`
- `VillagerRelationshipManager.cs`
- `NpcFavorite.cs`
- `NpcFavoriteManager.cs`
- `NpcFavoriteButtonUI.cs`
- `NpcFavoriteClickBridge.cs`
- `FavoriteNpcListUI.cs`
- `FavoriteNpcRowUI.cs`
- `NpcInteractionPoint.cs`
- `TouchSelectTarget.cs`
- `NpcInventoryPanelUI.cs`

### Quan hệ code thật

- `NpcSocialSystem.cs:389`, `395` lấy `VillagerAI` / `SmartNpcAI` từ collider target.
- `NpcSocialSystem.cs:524`, `534` lấy `VillagerAI` / `SmartNpcAI` trên chính actor.
- `NpcSocialSystem.cs:1332-1333` gọi `NpcScheduleController.AllowsSocial(...)`.
- `NpcSocialSystem.cs:1585`, `2906`, `2915` gọi `WorldTimeSystem.Instance`.
- `NpcSocialSystem.cs:1972` gọi `WeatherSystem.Instance`.
- `NpcSocialSystem.cs:2066`, `2091` lấy `NpcTradeAgent`.
- `TouchSelectTarget.cs` lặp lại rất nhiều lần `GetComponent<SmartNpcAI>()`, `GetComponent<VillagerAI>()`, `GetComponent<MonsterAI>()`, `GetComponent<CharacterStats>()` để nhận diện target và hiện UI.
- `NpcFavoriteManager` bị `FavoriteNpcListUI`, `FavoriteNpcRowUI`, `HeavenNurturePanelUI`, `InventoryPanelUI`, `FullGameSaveController` gọi.

### Kết luận cụm

Social và chọn target đang dính với hầu hết actor. `TouchSelectTarget` là file UI/select lớn, nếu sửa chọn NPC hoặc bảng thông tin NPC thì phải xem chung `NpcInventoryPanelUI`, `NpcFavorite*`, `HeavenNurturePanelUI`, `NpcSocialSystem`.

---

## 9. Cụm thế giới / thời gian / thời tiết / map / scene

### File chính

- `WorldTimeSystem.cs`
- `WorldClockTextUI.cs`
- `WorldSimulationBootstrap.cs`
- `WorldEventSystem.cs`
- `WorldEventManager.cs`
- `WorldScreenNotificationHub.cs`
- `WeatherSystem.cs`
- `WeatherVisualSystem.cs`
- `DayNightLightingSystem.cs`
- `WorldTilemapManager.cs`
- `SpawnRegion.cs`
- `CameraBounds.cs`
- `MobileCameraController.cs`
- `DoorTeleport.cs`
- `DoorTeleportSameScene.cs`
- `SceneLoader.cs`
- `OpenMap.cs`
- `CloseMap.cs`
- `MapToggleUI.cs`

### Quan hệ code thật

- `WorldTimeSystem.cs:56-76` có `EnsureInstance()` và tự tạo object nếu thiếu.
- `WorldTimeSystem.cs:106` gọi `GameSaveSystem.TryLoadWorldTime(...)`.
- `WorldTimeSystem.cs:209` gọi `GameSaveSystem.SaveWorldTime(...)`.
- `WorldTimeSystem.cs:221-243` gọi `UiText` để format ngày giờ.
- `WorldSimulationBootstrap.cs:8-17` tự tạo `DayNightLightingSystem` và `WeatherSystem` nếu thiếu.
- `WorldEventSystem.cs:40`, `180` gọi `WorldTimeSystem.Instance`.
- `WorldEventManager.cs:44-46` dùng `UiText` để tạo intro log.
- `DayNightLightingSystem.cs` gọi `WorldTimeSystem.Instance`.

### Kết luận cụm

`WorldTimeSystem` là dependency ngang toàn project. Lịch NPC, job, social, weather, save, clock UI đều phụ thuộc nó.

---

## 10. Cụm save / localization / text database

### File chính

- `GameSaveSystem.cs`
- `FullGameSaveController.cs`
- `SpawnedWorldActor.cs`
- `LocalizationSettings.cs`
- `ItemText.cs`
- `NpcText.cs`
- `UiText.cs`
- `SceneLocalizedTextBootstrap.cs`
- `MainMenuManager.cs`
- `NewGameIntroPanel.cs`

### Quan hệ code thật

- `FullGameSaveController.cs:57-75` có `EnsureInstance()` tự tạo object save controller.
- `FullGameSaveController.cs:134`, `161-173` gọi `GameSaveSystem` để load/save scene.
- `FullGameSaveController.cs:407-422` lưu/tải `WorldTimeSystem` qua `GameSaveSystem`.
- `ItemText.cs:175-200` load text theo `LocalizationSettings` và parse `ItemTextDatabase`.
- `NpcText.cs:213-239` load text theo `LocalizationSettings` và parse `NpcTextDatabase`.
- `UiText.cs:105-132` load text theo `LocalizationSettings` và parse `UiTextDatabase`.
- `SceneLocalizedTextBootstrap.cs:146-153` subscribe/unsubscribe `LocalizationSettings.LanguageChanged`.

### Kết luận cụm

Text không phải file phụ. `NpcText`, `ItemText`, `UiText` đang được gọi rất rộng trong AI/action/UI. Nếu đổi cấu trúc JSON text database, phải kiểm tra cả ba file text và các UI gọi chúng.

---

## 11. Cụm bí cảnh / bone monster / world event đặc biệt

### File chính

- `BicanhSessionManager.cs`
- `BicanhBoneMonsterAI.cs`
- `BicanhDungeonResident.cs`
- `BoneSpiritAmbush.cs`

### Quan hệ code thật

- `BicanhSessionManager.cs:312` quét `SmartNpcAI` bằng `FindObjectsByType<SmartNpcAI>()`.
- `BicanhSessionManager.cs:327` quét `VillagerAI`.
- `BicanhSessionManager.cs:342` quét `MonsterAI`.
- `BicanhSessionManager.cs:361` kiểm tra `BicanhDungeonResident`.
- `BicanhSessionManager.cs:391`, `397`, `406` lấy `SmartNpcAI`, `VillagerAI`, `MonsterAI` bằng `GetComponent`.
- `BicanhSessionManager.cs:208-209` gọi `HeavenDaoSystem.Instance.HasPower(...)`.
- `BicanhSessionManager.cs:189-194` gọi `WorldEventSystem.Instance`.

### Kết luận cụm

Bí cảnh không chỉ là quái riêng. Nó snapshot/tác động lên NPC thường, tu sĩ, monster, inventory, social, task, HeavenDao và WorldEvent.

---

## 12. Cụm UI routing / mobile / ads / hiệu ứng rời

### File liên quan

- `BottomMenuButtonRouter.cs`
- `InventoryToggleButton.cs`
- `ShopToggleUI.cs`
- `OpenShopUi.cs`
- `OpenMap.cs`
- `CloseMap.cs`
- `PopulationWidgetUI.cs`
- `ResponsiveCanvasScaler.cs`
- `ResponsivePanelFitter.cs`
- `MobileSafeAreaFitter.cs`
- `MobileCameraController.cs`
- `AdsInitializer.cs`
- `RewardedLTAdsButton.cs`
- `AutoDestroy.cs`
- `Combat/AutoDestroyEffect.cs`
- `GoldenEnergyEffect.cs`
- `UIBorderSparkEffect.cs`
- `LightnighHitBurst.cs`

### Quan hệ code thật

- `BottomMenuButtonRouter.cs` gọi `InventoryPanelUI`, `OpenMap`, `OpenShopUI`, `PlayerWallet`, `ShopPanelUI`, `UIWorldStoryManager`.
- `RewardedLTAdsButton.cs` tìm `PlayerWallet` bằng `FindAnyObjectByType<PlayerWallet>()`.
- Nhiều file hiệu ứng không có incoming code trực tiếp, có thể chỉ được gắn prefab/Inspector.

### Kết luận cụm

Đừng xóa các script không thấy code gọi trực tiếp nếu nó là UI Button/Event/Prefab. Cần kiểm tra Scene/Prefab Unity trước.

---

## 13. Các file có dấu hiệu “không được code khác gọi trực tiếp” nhưng có thể được Inspector gọi

Nhóm này không nên kết luận là rác ngay:

- `BottomMenuButtonRouter.cs`
- `HeavenDaoPanelUI.cs`
- `HeavenNurturePanelUI.cs`
- `HeavenNurtureToggleButtonUI.cs`
- `TaskBoardInteract.cs`
- `PopulationWidgetUI.cs`
- `RewardedLTAdsButton.cs`
- `MapToggleUI.cs`
- `CloseMap.cs`
- `DoorTeleport.cs`
- `SceneLoader.cs`
- `WorldClockTextUI.cs`
- `VillageHomeManager.cs`
- `VillageNpcSetupTool.cs`
- `NpcCombatHitbox2D.cs`
- `NpcRangedSkill.cs`
- `MonsterAttack.cs`
- `AutoDestroy.cs`
- `Combat/AutoDestroyEffect.cs`
- `GoldenEnergyEffect.cs`
- `UIBorderSparkEffect.cs`
- `LightnighHitBurst.cs`

Lý do: những file dạng UI, trigger, button, effect, scene component thường được Unity gọi qua `Awake/Start/Update`, Collider event, Button OnClick, hoặc prefab reference, nên graph code không thấy incoming.

---

## 14. Điểm cần sửa/cẩn thận khi refactor

### 14.1. File/class name lệch hoặc nhiều MonoBehaviour trong một file

Các trường hợp đáng chú ý:

- `LightnighHitBurst.cs` khai báo `LightningHitBurst` → tên file bị thiếu chữ `t` trong `Lightning`. Nên đổi file thành `LightningHitBurst.cs` nếu prefab không bị mất reference.
- `OpenShopUi.cs` khai báo `OpenShopUI` → lệch chữ hoa/thường `Ui`/`UI`.
- `menumap.cs` khai báo `Menumap` → lệch hoa/thường, nên thống nhất.
- `EntityCore.cs` chứa `EntityProfile : MonoBehaviour` trong file tên khác.
- `NpcSocialSystem.cs` chứa nhiều MonoBehaviour khác: `NpcIdentity`, `NpcNeeds`, `NpcPersonality`, `NpcRelationshipGraph`, `NpcMemory`, `NpcOverheadDialogueUI`, `NpcConversationAgent`, `NpcDecisionBrain`, `NpcNegotiationAgent`, `NpcSocialWorldInstaller`.

Khuyến nghị: nếu đang refactor lớn, nên tách mỗi MonoBehaviour public ra một file cùng tên class. Với prefab đang dùng sẵn thì rename/tách phải test lại reference.

### 14.2. `VillagerAI.cs` có block code cũ bị tắt

Trong `VillagerAI.cs` có một đoạn lớn `#if false` từ khoảng dòng 1177 đến 1967. Đoạn này là code cũ/disabled, không compile. Khi rà soát, không nên tính nó là logic đang chạy. Logic active đã được tách sang `VillagerAI.Brain.cs`, `VillagerAI.Work.cs`, `VillagerAI.ProfessionWork.cs`...

### 14.3. `VillagerAI`, `SmartNpcAI`, `MonsterAI` là cụm vòng tròn

- `MonsterAI` tìm/đánh `VillagerAI`, `SmartNpcAI`.
- `SmartNpcAI` xử lý help/combat với `MonsterAI`.
- `VillagerAI` tương tác với `SmartNpcAI`, trade/task/combat.

Không nên cố tách 3 cụm này mà không tạo interface chung như `IActor`, `ICombatActor`, `INpcBrain`, `IInventoryOwner`.

### 14.4. `WorldTimeSystem` là singleton nền

Nhiều class gọi `WorldTimeSystem.Instance`. Nếu scene thiếu `WorldTimeSystem`, nhiều hệ thống sẽ fallback/null hoặc hoạt động sai nhịp. Cần bảo đảm `WorldTimeSystem.EnsureInstance()` hoặc object tồn tại trước NPC AI/job/social.

### 14.5. `StatItemData` là dữ liệu lõi

Item đang đi qua nhiều luồng: shop, inventory, NPC trade, Heaven gift, pickup, save, text display. Nếu đổi trường dữ liệu item, phải test toàn bộ cụm item/economy/heaven/save.

---

## 15. Thứ tự nên refactor an toàn

1. **Không đụng data model trước**: giữ `StatItemData`, `ItemInventory`, `ItemStack`, `CharacterStats`, `CultivationRealm` ổn định.
2. **Tách actor interface trước**: tạo lớp/utility chung cho actor để giảm `GetComponent<VillagerAI/SmartNpcAI/MonsterAI>()` lặp lại ở `TouchSelectTarget`, `HeavenGiftPlacementController`, `NpcSocialSystem`.
3. **Rút gọn VillagerAI sau**: mục tiêu của dân làng nên chỉ còn lịch + nghề + ăn/ngủ + về nhà. Các phần tu luyện/treasure legacy nên đưa hẳn sang `SmartNpcAI` hoặc tắt bằng setting rõ ràng.
4. **Tách UI khỏi logic**: `TouchSelectTarget`, `InventoryPanelUI`, `HeavenNurturePanelUI` đang chứa nhiều logic nhận diện actor. Nên đưa nhận diện actor vào utility/service.
5. **Tách MonoBehaviour mỗi file**: đặc biệt `NpcSocialSystem.cs`, `EntityCore.cs`, `LightnighHitBurst.cs`, `OpenShopUi.cs`.
6. **Test theo cụm**: test NPC schedule → job → item pickup → shop/trade → combat → Heaven gift → save/load.

---

## 16. Kết luận nhanh

Cấu trúc hiện tại không phải rời rạc. Nó là một hệ simulation lớn xoay quanh 6 lõi: **Actor AI**, **World Time**, **Item/Inventory**, **Combat/Stats**, **Task/Trade**, **Heaven System**. Muốn nâng cấp ổn định thì phải refactor theo cụm gọi thật, không theo tên file.
