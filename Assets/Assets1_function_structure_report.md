# Báo cáo cấu trúc tổng quan chức năng - Assets(1).zip

## 1. Thống kê nhanh

- ZIP đã đọc: `Assets(1).zip`
- Tổng số file trong ZIP: khoảng **5.981 file**
- Tổng số script C#: **204 script**
- Scene chính tìm thấy:
  - `Assets/Lang.unity`
  - `Assets/MainMenu.unity`
  - `Assets/PersistentScene.unity`
  - `Assets/Settings/Scenes/URP2DSceneTemplate.unity`
  - `Assets/omiStudio/Enemy Cow/Scenes/Main.unity`
  - `Assets/_Recovery/...` chứa nhiều bản scene phục hồi / backup.

Lưu ý kỹ thuật: khi giải nén có nhiều cảnh báo tên file tiếng Việt bị mã hóa dạng `#U...` hoặc lệch local/central filename. Đây là dấu hiệu nên hạn chế đổi tên thủ công ngoài Unity, vì có thể làm gãy GUID/meta nếu xử lý sai.

---

## 2. Cấu trúc thư mục cấp cao

| Thư mục | Vai trò chính |
|---|---|
| `Assets/Scripts` | Toàn bộ logic gameplay chính: NPC, tu sĩ, yêu thú, thiên đạo, item, nhiệm vụ, shop, save/load, thời gian, thời tiết, UI, combat, map. |
| `Assets/Art` | Sprite nhân vật, animation clip/controller, ảnh UI menu, vòng quay, yêu thú, skill, prefab hiệu ứng nhỏ. Có một số script UI/effect đặt trong đây. |
| `Assets/Item` | Dữ liệu item dạng ScriptableObject và icon: đan dược, pháp bảo, công pháp, linh dược, linh thạch, thực phẩm, vật phẩm yêu thú. |
| `Assets/Prefabs` | Prefab nhân vật, yêu thú, pháp bảo/skill effect, fireball, item spawn area, health bar, task/log row. |
| `Assets/BanVe` | Tile/palette/map art. Số lượng rất lớn, chủ yếu asset tilemap. |
| `Assets/photo` | Icon/UI/sprite phụ: rank cảnh giới, icon NPC/monster, ảnh giao diện. |
| `Assets/Resources` | Dữ liệu text/json/xlsx: `ItemTextDatabase.json`, `NpcTextDatabase.json`, BillingMode, icon rank, icon npc/monster. |
| `Assets/Effects` | Script và prefab hiệu ứng skill/aura. |
| `Assets/Audio` | BGM/audio. |
| `Assets/Settings` | URP/Renderer/Build profile. |
| `Assets/Editor` | Tool editor cân bằng item và chạy test NPC. |
| `Assets/Tests` | PlayMode test cho luồng NPC. |
| `Assets/_Recovery` | Bản phục hồi scene, không nên coi là source chính nếu không cần khôi phục. |
| `Assets/BluBlu Games`, `Assets/omiStudio` | Asset ngoài/plugin mẫu: skeleton, enemy cow. |
| `Assets/TextMesh Pro`, `Assets/Plugins`, `MobileDependencyResolver` | Thư viện/SDK/phụ trợ Unity. |

---

## 3. Nhánh hệ thống gameplay chính

### A. Nhánh khởi động, scene, save/load

**Mục tiêu:** điều phối menu, tạo game mới, tiếp tục game, lưu toàn bộ trạng thái thế giới.

Script liên quan:

- `MainMenuManager.cs`: điều khiển menu chính, New Game, Continue, Settings/About, mở/tắt setting, xóa save khi cần.
- `NewGameIntroPanel.cs`: panel giới thiệu game mới, chuyển trang, hiệu ứng type text.
- `SceneLoader.cs`, `DoorTeleport.cs`, `DoorTeleportSameScene.cs`: chuyển scene / dịch chuyển cùng scene, spawn point, cooldown, camera follow sau teleport.
- `DontDestroy.cs`: giữ object qua scene.
- `WorldSimulationBootstrap.cs`: đảm bảo các hệ thống mô phỏng thế giới được tạo.
- `GameSaveSystem.cs`: registry item, lưu inventory/shop stock, cờ new game/continue, PlayerPrefs key.
- `FullGameSaveController.cs`: save/load full game, auto-save, save khi pause/quit, gom dữ liệu favorite NPC và các hệ thống khác.
- `SpawnedWorldActor.cs`, `NpcData.cs`: ID/persistentId đơn giản cho actor/NPC.

**Nhận xét:** có 2 lớp save chính (`GameSaveSystem`, `FullGameSaveController`) nên khi refactor cần xác định rõ: `GameSaveSystem` là lớp dữ liệu/registry thấp hơn, `FullGameSaveController` là orchestrator lưu toàn cục.

---

### B. Nhánh thời gian, thời tiết, sự kiện thế giới

**Mục tiêu:** làm thế giới chạy theo năm-tháng-ngày-giờ, ảnh hưởng lịch NPC, ngày đêm, thời tiết, log sự kiện.

Script liên quan:

- `WorldTimeSystem.cs`: singleton thời gian thế giới, continent name, năm/tháng/ngày/giờ, phase ngày, save time, event đổi giờ/ngày.
- `WorldClockTextUI.cs`: hiển thị đồng hồ thế giới lên UI.
- `DayNightLightingSystem.cs`: đổi ánh sáng/tối màn hình theo thời gian.
- `WeatherSystem.cs`: trạng thái thời tiết, modifier mood/moveSpeed/cultivation/beast aggression.
- `WeatherVisualSystem.cs`: visual mưa/tuyết/linh khí/sấm.
- `WorldEventManager.cs`: quản lý log thế giới, story log, timestamp.
- `WorldEventSystem.cs`: trigger event thế giới theo loại.
- `UIWorldStoryManager.cs`: UI cuốn sổ/thế giới, hiển thị log.
- `WorldScreenNotificationHub.cs`: thông báo nổi trên màn hình, phân loại thường/origin, queue, màu/prefix fallback.

**Luồng chính:** `WorldTimeSystem` cung cấp giờ -> `NpcScheduleController` đọc giờ -> NPC chọn hành vi; đồng thời `DayNightLightingSystem`, `WeatherSystem`, `WorldEventManager` dùng giờ/sự kiện để đổi trạng thái hiển thị và log.

---

### C. Nhánh dữ liệu thực thể, chỉ số, cảnh giới

**Mục tiêu:** tạo profile cho NPC/monster, lưu tính cách, chỉ số, tuổi thọ, cảnh giới, relationship/memory.

Script liên quan:

- `EntityCore.cs`: định nghĩa `EntityKind`, `EntityGender`, `TalentGrade`, `EntityMood`, `EntityGoal`, `EntityProfile`; sinh identity/talent/personality/emotion/relationship/memory.
- `CharacterStats.cs`: quản lý cultivation exp, breakthrough, chỉ số base/item/final, áp dụng item, tính exp cảnh giới.
- `CultivationProgression.cs`: công thức exp, linh thạch exp/efficiency, cảnh giới tiếp theo, có cần thiên kiếp hay không, realm power.
- `NpcRoleUtility.cs`: tiện ích nhận dạng actor là dân thường/tu sĩ/pet, lấy tên hiển thị, cảnh giới, power.
- `NpcHideCultivationInfo.cs`: ẩn thông tin tu luyện với NPC cần giấu.

**Nhận xét:** `EntityCore` là nền dữ liệu tốt nhưng chưa tách triệt để khỏi `VillagerAI`, `SmartNpcAI`, `MonsterAI`, vì mỗi AI vẫn tự giữ nhiều field chỉ số riêng.

---

### D. Nhánh NPC dân làng / VillagerAI

**Mục tiêu:** dân thường làm nghề, đi theo lịch, ăn/ngủ/làm việc/buôn bán/hái tài nguyên, có thể dần liên quan đến tu luyện nhưng đang quá tải.

Script trung tâm:

- `VillagerAI.cs`: script rất lớn, khoảng 7.317 dòng. Đang ôm nhiều vai trò: thông tin dân, tuổi, nghề, chỉ số, nhu cầu, lịch, di chuyển, tránh vật cản, đi đường, mua/bán, làm việc, home routine, map area, nhận damage, apply item, cảnh giới/tu luyện legacy.
- `VillagerAI.Work.cs`: phần work partial.
- `VillagerAI.Trade.cs`: logic trader.
- `VillagerAI.LegacyCultivation.cs`: exp/cảnh giới legacy cho dân.
- `VillagerAI.LegacyDailyTaskPlan.cs`: kế hoạch task daily legacy.
- `VillagerAI.LegacyTreasure.cs`: tham gia tranh đoạt bảo vật legacy.
- `VillagerJobDispatcher.cs`: điều phối job adult/work.
- Job phụ: `BuyerJob.cs`, `SellerJob.cs`, `GuardJob.cs`, `HarvestJob.cs`, `HunterJob.cs`, `HealerJob.cs`.
- Hỗ trợ làng: `VillageHomeManager.cs`, `VillageStorage.cs`, `VillageNpcSetupTool.cs`, `NpcWorkArea.cs`.

Nghề trong `VillagerJob`:

- `Farmer`
- `Hunter`
- `Fisher`
- `Seller`
- `Buyer`
- `Guard`
- `Healer`
- `Child`
- `Trader`

Luồng lịch dân thường hiện có trong `NpcDailyRoutineLibrary`:

- 20h-5h: ngủ
- 5h-11h: làm việc
- 11h-13h: về nhà
- 13h-17h: làm việc
- 17h-20h: về nhà

**Nhận xét quan trọng:** Đây là nhánh cần refactor mạnh nhất. Với mục tiêu mới của bạn, dân làng chỉ cần “đúng giờ đi làm, trưa về, chiều đi làm, tối về nhà”, thì nên cắt khỏi `VillagerAI` các mảng: tu luyện legacy, săn/tranh bảo vật phức tạp, daily task tu sĩ, forge/alchemy nếu không phải nghề dân.

---

### E. Nhánh tu sĩ / SmartNpcAI

**Mục tiêu:** NPC tu sĩ có cảnh giới, tu luyện, săn yêu thú, nhặt tài nguyên, giao dịch, tranh bảo vật, làm nhiệm vụ, dùng item/công pháp/pháp bảo.

Script trung tâm:

- `SmartNpcAI.cs`: khoảng 4.285 dòng. Chứa thông tin tu sĩ, cờ bật/tắt hành vi, cảnh giới, thiên phú, HP/attack/defense, tài sản, tính cách, nhu cầu, di chuyển, combat, skill, daily routine, task visit, địa điểm, effect tu luyện.
- `SmartNpcAI.Brain.cs`: phần brain/hành vi bổ sung.
- `SmartNpcAI.Needs.cs`: phần nhu cầu.
- `SmartNpcAI.Schedule.cs`: phần lịch routine/task.
- `NpcCultivationAwakeningUtility.cs`: chuyển dân thường sang SmartNpc khi được thức tỉnh tu luyện.
- `NpcCombatTechniqueSystem.cs`: áp dụng công pháp/kỹ thuật chiến đấu, mastery, buff damage/speed/defense phản ứng khi nhận damage.
- `NpcRageNearbyAttack.cs`: trạng thái nổi giận đánh gần.
- `NpcRangedSkill.cs`, `RangedProjectile2D.cs`, `Fireball.cs`: skill bắn xa/projectile.

Cảnh giới trong `CultivationRealm`:

- `Mortal`
- `QiRefining`
- `Foundation`
- `GoldenCore`
- `NascentSoul`
- `SoulFormation`
- `Tribulation`
- `Ascension`

Loại thể chất trong `PhysiqueType`:

- `MortalBody`
- `SpiritRoot`
- `HeavenlyBody`
- `DemonicBody`
- `AncientBloodline`

Luồng tu sĩ chính:

1. Đọc lịch từ `NpcScheduleController`.
2. Nếu tới giờ tu luyện thì tìm `cultivationPoint` hoặc vị trí phù hợp.
3. Tăng cultivation theo multiplier từ thiên phú/thời tiết/item.
4. Khi đủ exp thì breakthrough.
5. Nếu major breakthrough có thể gọi `HeavenlyTribulationSystem`.
6. Nếu lịch cho phép hunt/gather/task thì đi rừng, săn yêu thú, nhặt tài nguyên, nhận task.
7. Nếu gặp bảo vật cao cấp thì có thể tham gia `TreasureFrenzySystem`.

**Nhận xét:** SmartNpcAI nên là nhánh tu sĩ duy nhất sau refactor. Dân thường muốn tu luyện thì dùng đan dược/thức tỉnh rồi chuyển sang SmartNpc, không giữ tu luyện trong VillagerAI.

---

### F. Nhánh lịch sinh hoạt / Routine

**Mục tiêu:** một hệ thống lịch chung cho dân, bán tu, tu sĩ, yêu thú.

Script liên quan:

- `NpcScheduleController.cs`: định nghĩa `NpcLifePath`, `NpcScheduleActivity`, slot lịch; kiểm tra hoạt động hiện tại; các hàm `AllowsTrade`, `AllowsGather`, `AllowsAlchemy`, `AllowsForge`, `AllowsTask`, `AllowsSocial`.
- `NpcDailyRoutineLibrary.cs`: build lịch mặc định theo life path.
- `VillageHomeManager.cs`: quản lý home routine, ẩn/hiện NPC ở nhà.

Life path:

- `Commoner`
- `SemiCultivator`
- `Cultivator`
- `Beast`

Activity:

- `Idle`, `Sleep`, `Eat`, `Work`, `SellGoods`, `BuyGoods`, `Gather`, `Hunt`, `Cultivate`, `Alchemy`, `Forge`, `TakeTask`, `ReturnHome`

**Nhận xét:** hệ thống lịch đang là phần đúng hướng. Nên đẩy dân thường về lịch đơn giản, còn tu sĩ dùng lịch riêng nhiều block hơn.

---

### G. Nhánh yêu thú / MonsterAI

**Mục tiêu:** yêu thú có cảnh giới, lãnh địa, săn/đánh, tu luyện hấp thụ linh khí, ăn NPC, rơi vật phẩm, hồi sinh, tham gia tranh bảo vật.

Script trung tâm:

- `MonsterAI.cs`: core monster, thông tin, HP, cảnh giới, damage, bản năng, di chuyển, lãnh địa, phát hiện, tấn công, hồi sinh, loot, fireball, animation.
- `MonsterAI.TargetingMovement.cs`: tìm mục tiêu, ưu tiên intruder, flee nếu bị áp chế cảnh giới, tuần tra/lãnh địa/follow target.
- `MonsterAI.TreasureCombat.cs`: hành vi khi săn bảo vật, attack, shoot fireball, take damage.
- `MonsterAI.NeedsCultivation.cs`: nhu cầu/yêu thú hấp thụ linh khí, add exp, breakthrough.
- `MonsterAI.DevourDeathLootRespawn.cs`: ăn NPC bị hạ, chết, rơi loot, respawn.
- `MonsterAI.ProfileStats.cs`: sync profile, realm stats, beast level.
- `MonsterAI.ItemEffects.cs`: apply item lên monster.
- `MonsterAI.AnimationDebug.cs`: face direction, moving animation, trigger debug gizmo.
- `MonsterAttack.cs`: attack phụ.
- `MonsterDirectionalAnimator.cs`: animation đi/tấn công/chết/hồi sinh theo hướng.
- `BicanhBoneMonsterAI.cs`, `BoneSpiritAmbush.cs`, `BicanhDungeonResident.cs`: yêu thú/xương trong bí cảnh/dungeon.

Kiểu mục tiêu săn trong `HuntTargetType`:

- `Beast`
- `Demon`
- `Spirit`
- `Undead`
- `Bandit`

**Luồng yêu thú:** patrol trong lãnh địa -> detect player/NPC/tu sĩ/monster khác -> đánh hoặc chạy nếu bị áp chế cảnh giới -> chết rơi loot -> respawn -> có thể hấp thụ linh khí/breakthrough -> có thể bị kéo vào bí cảnh/tranh đoạt bảo vật.

---

### H. Nhánh vật phẩm, inventory, dữ liệu item

**Mục tiêu:** toàn bộ vật phẩm tu tiên: đan dược, pháp bảo, công pháp, vật liệu, thực phẩm, linh dược, loot yêu thú; dùng được cho player/NPC/monster.

Script/dữ liệu chính:

- `StatItemData.cs`: ScriptableObject item trung tâm. Định nghĩa type, grade, target, modifier, use policy, equipment, pill/manual/material/food kind, effect khi nhặt/mua, buff tạm thời.
- `ItemInventory.cs`: stack item, inventory player/NPC/shared runtime, add/remove/use, durability.
- `ItemStack`: dữ liệu stack item + amount/durability.
- `WorldStatItemPickup.cs`: item rơi trên map, reserve/take/give cho world actor, quy định NPC/player nhặt.
- `ItemPickup.cs`: pickup item cơ bản.
- `ItemLifecycleSystem.cs`: vòng đời item.
- `ItemEffectSpawner.cs`, `PickupVisualUtility.cs`, `HideRuntimePickupVisual.cs`: visual/effect khi item spawn/nhặt.
- `StatItemApplier.cs`, `TimedStatItemBuff.cs`: áp item/buff lên actor.
- `ItemStatBalanceUtility.cs`: roll/balance chỉ số item theo grade/type.
- `ShopItemAssetPostprocessor.cs`, `Editor/StatItemBalanceEditor.cs`: tool editor cân bằng item tự động.
- `ItemText.cs`: text/description item từ database.

Item type:

- `DanDuoc`
- `PhapBao`
- `VatLieu`
- `CongPhap`
- `ThucPham`

Item grade:

- `Ha`
- `Trung`
- `Thuong`
- `Tien`

Các thư mục item:

- `Assets/Item/CongPhap`: 15 asset + 15 icon.
- `Assets/Item/DanDuoc`: 13 asset + 13 icon.
- `Assets/Item/LinhDuoc`: 9 asset + 6 icon.
- `Assets/Item/LinhThach`: 1 asset + 1 icon.
- `Assets/Item/PhapBao`: 15 asset + 15 icon.
- `Assets/Item/ThucPham`: 3 asset + 3 icon.
- `Assets/Item/yeuthuitem`: 5 asset + 5 icon.

---

### I. Nhánh tài nguyên thế giới / gather / farm / loot

**Mục tiêu:** spawn tài nguyên trên map, NPC đi hái/săn/thu hoạch, item rơi có reservation để tránh tranh cùng lúc.

Script liên quan:

- `WorldResourceField.cs`: vùng spawn tài nguyên, rule natural grade, spawn initial/respawn/save/load, nearest pickup.
- `WorldResourceNode.cs`: node tài nguyên có respawn/visual/relocation.
- `ResourceNode.cs`: phân loại tài nguyên harvest: farm/hunt/fish/herb/ore/beast... dựa trên item/token.
- `NpcResourceGatherer.cs`: NPC bắt đầu gathering theo lịch hoặc tự động, reserve target, cancel gather.
- `HarvestJob.cs`, `HunterJob.cs`: job làm nông/săn, tạo item sản phẩm.
- `VillageStorage.cs`: kho làng, store item/amount.
- `WorldTilemapManager.cs`: cung cấp tile road/farm/hunting/fishing/market cho NPC.

---

### J. Nhánh kinh tế, shop, giao dịch

**Mục tiêu:** player mua/bán, NPC mua/bán với nhau, trader/counter broker, refill shop stock.

Script liên quan:

- `PlayerWallet.cs`, `PlayerWalletTextUI.cs`, `PlayerWalletGrantButton.cs`: tiền/linh thạch player và UI.
- `SimpleItemShop.cs`: shop slot, giá mua/bán, mua vào inventory player/NPC, refresh từ seller inventory.
- `ShopPanelUI.cs`: UI shop lớn, tab đan dược/pháp bảo/công pháp/vật liệu/thực phẩm, detail item.
- `ShopItemButtonUI.cs`, `ShopCardHoverEffect.cs`, `ShopToggleUI.cs`, `OpenShopUi.cs`: button/card/toggle shop.
- `NpcEconomy.cs`: định giá item, giá giao dịch theo context, tiền/linh thạch NPC.
- `NpcTradeAgent.cs`: NPC mua produce, bán item, nhận item đã mua, score mua.
- `NpcCounterBroker.cs`: quầy giao dịch trung gian, vị trí khách, xác định NPC có thể mua/bán với broker.
- `NpcMerchantRole.cs`, `NpcShopStockRefill.cs`: vai trò thương nhân/refill hàng.
- `BuyerJob.cs`, `SellerJob.cs`: job mua/bán đơn giản.

**Nhận xét:** đang có cả shop player và giao dịch NPC nội bộ. Khi nâng cấp nên tách rõ `PlayerShop` và `NpcMarketSimulation` để tránh UI/shop logic dính với AI.

---

### K. Nhánh luyện đan, luyện khí, nghề đặc biệt

**Mục tiêu:** NPC chuyên môn tự chế đan/pháp bảo, cần nguyên liệu, bán thành phẩm hoặc nhận order.

Script liên quan:

- `NpcAlchemyAgent.cs`: recipe luyện đan, nguyên liệu, bắt đầu luyện, bán thành phẩm, refresh catalog từ asset.
- `NpcAlchemyRole.cs`: gắn role luyện đan cho NPC.
- `NpcForgeAgent.cs`: recipe luyện khí/rèn, order khách, tính cost, đề xuất item mong muốn, bán thành phẩm.
- `NpcForgeRole.cs`: gắn role thợ rèn/luyện khí.
- `NpcSpecialProfession.cs`: nghề đặc biệt cho NPC/task provider.

---

### L. Nhánh nhiệm vụ / tửu quán / task board / escort

**Mục tiêu:** NPC nhận nhiệm vụ ở task provider/tavern/board; làm các task gather, hunt, patrol, delivery, harvest, escort.

Script trung tâm:

- `NpcTaskProvider.cs`: khoảng 5.811 dòng. Quản lý offer, task đang chạy, meal/tavern flow, counter/board/provider positions, reward, required item, hunt target, escort, danger response, forest depth, expanded task catalog.
- `TaskBoardInteract.cs`: tương tác bảng nhiệm vụ.
- `TaskBoardPanelUI.cs`: UI list task board.
- `TaskBoardRowUI.cs`: row task.
- `NpcTavernEntrance.cs`: xử lý NPC vào tửu quán/tavern visit.
- `NpcEscortTaskForceStarter.cs`: force start escort task, kiểm tra điều kiện NPC.

Task type:

- `GatherResource`
- `HuntMonster`
- `Cultivate`
- `Patrol`
- `Deliver`
- `HarvestAndDeliver`
- `Escort`

Task rank:

- `Ha`
- `Trung`
- `Thuong`

**Nhận xét:** `NpcTaskProvider` là script rất lớn thứ 2. Nên tách thành `TaskOfferDatabase`, `TaskRuntime`, `TaskExecutor`, `TavernService`, `EscortTask`, `TaskReward`.

---

### M. Nhánh bí cảnh / dungeon session

**Mục tiêu:** mở bí cảnh, chọn participant, đưa vào session, bảo toàn/trả trạng thái sau khi kết thúc.

Script liên quan:

- `BicanhSessionManager.cs`: mở/kết thúc session, yêu cầu power thiên đạo, điều kiện realm, include SmartNpc/Monster/Villager, spawn point, restore death/behaviour, snapshot participant.
- `BicanhDungeonResident.cs`: đánh dấu resident trong dungeon.
- `BicanhBoneMonsterAI.cs`, `BoneSpiritAmbush.cs`: AI đặc thù trong bí cảnh.

**Luồng chính:** unlock bằng `HeavenDaoPower` -> chọn actor đủ cảnh giới -> pause/ghi snapshot behaviour -> teleport/spawn trong bí cảnh -> kết thúc thì restore.

---

### N. Nhánh Thiên Đạo, ban tặng, thiên kiếp, tranh đoạt bảo vật

**Mục tiêu:** player là thiên đạo, mở khóa quyền năng, ban item xuống thế giới, đánh thiên phạt/thiên kiếp, tạo tranh đoạt bảo vật.

Script liên quan:

- `HeavenDaoSystem.cs`: origin/karma, unlock power, story reward, recent logs.
- `HeavenDaoPanelUI.cs`: UI panel thiên đạo, overview, powers, recent logs.
- `HeavenSystem.cs`: hệ thấp hơn cho thiên phạt và drop item tại vị trí/entity.
- `HeavenGiftPlacementController.cs`: đặt bảo vật/ban tặng, effect triệu hồi theo grade, sét, damage, session effect.
- `HeavenlyTribulationSystem.cs`: thiên kiếp khi breakthrough, target motion mode, pill protection/damage reduction.
- `ThienKiepStrikePrefab.cs`: prefab mây/sét/nổ khi thiên kiếp đánh.
- `TreasureFrenzySystem.cs`: sự kiện tranh đoạt bảo vật, chọn participant, bán kính theo grade, attack/flee, cross-map penalty.
- `TreasureHeatSystem.cs`: heat/threat khi NPC nhận item, robbery chance.
- `SkyDropToPosition.cs`, `LightnighHitBurst.cs`, `GoldenEnergyEffect.cs`: hiệu ứng rơi/đánh/sáng.

Power thiên đạo trong `HeavenDaoPower`:

- `InspectFate`
- `GrantItem`
- `StrikePunishment`
- `TriggerHeavenEarthOmen`
- `OpenSecretRealm`

**Luồng ban tặng:** player chọn item trong inventory/bảo khố -> `HeavenGiftPlacementController` đặt xuống map hoặc target -> tạo `WorldStatItemPickup` -> NPC có thể nhặt -> nếu item grade cao thì `TreasureFrenzySystem`/`TreasureHeatSystem` kích hoạt tranh đoạt/đe dọa.

---

### O. Nhánh xã hội, yêu thích, hội thoại, quan hệ

**Mục tiêu:** NPC có quan hệ/ký ức/giao tiếp, player đánh dấu NPC quan tâm.

Script liên quan:

- `NpcSocialSystem.cs`: memory, relationship, rumor, hostility, trade completed, monster defeated, decision kind.
- `DailyConversation.cs`: NPC đối thoại hằng ngày khi gặp nhau.
- `NpcText.cs`: database câu thoại, action/dialogue random line.
- `NpcFavorite.cs`: lưu snapshot NPC được đánh dấu.
- `NpcFavoriteManager.cs`: quản lý danh sách favorite.
- `NpcFavoriteButtonUI.cs`, `NpcFavoriteClickBridge.cs`, `FavoriteNpcListUI.cs`, `FavoriteNpcRowUI.cs`: UI đánh dấu/focus NPC.
- `NpcInteractionPoint.cs`: điểm tương tác NPC.
- `NpcPetCompanion.cs`: companion/pet.

---

### P. Nhánh map, di chuyển, boundary, camera

**Mục tiêu:** NPC/player/camera di chuyển trong map, tránh vật cản, qua cổng, theo vùng.

Script liên quan:

- `NpcMapArea.cs`, `NpcMapBoundaryClamp.cs`: vùng map và giới hạn NPC.
- `NpcMapDestination.cs`, `NpcMapMover2D.cs`, `NpcMapNavigator.cs`: đích đến, di chuyển, resolve zone/gate/route.
- `NpcTeleportGate.cs`: cổng teleport NPC.
- `NpcLocationArea.cs`: khu vực chức năng/danger tier, random point theo mục đích.
- `NpcLawZone.cs`: vùng luật lệ.
- `NpcCollisionRegistry.cs`: registry va chạm NPC.
- `NpcPathMemorySystem.cs`: nhớ đường/path.
- `BlockingTilemap2D.cs`, `TilemapObstacle2D.cs`: cấu hình tilemap collider/obstacle.
- `WorldTilemapManager.cs`: điểm đường/ruộng/săn/câu/market.
- `CameraBounds.cs`, `MobileCameraController.cs`, `InteriorCameraFocus.cs`: camera mobile, giới hạn, focus trong nhà/ngoài nhà.
- `MobileSafeAreaFitter.cs`, `ResponsiveCanvasScaler.cs`, `ResponsivePanelFitter.cs`: responsive UI/mobile safe area.

**Nhận xét:** phần pathfinding/tránh vật cản đang nằm nhiều trong `VillagerAI` và `SmartNpcAI`. Nên tách thành component dùng chung `NpcMovementController`.

---

### Q. Nhánh combat, damage, skill, animation

**Mục tiêu:** damage, projectile, hitbox, animation di chuyển/skill.

Script liên quan:

- `IDamageable.cs`: interface nhận damage.
- `PlayerHealth.cs`: HP player.
- `NpcCombatHitbox2D.cs`: hitbox NPC.
- `NpcCombatTechniqueSystem.cs`: công pháp/buff combat.
- `NpcRangedSkill.cs`, `RangedProjectile2D.cs`, `Fireball.cs`: skill bắn xa.
- `MonsterAttack.cs`, `MonsterDirectionalAnimator.cs`: attack/animation yêu thú.
- `NPCVisualMovement.cs`: visual animation NPC theo hướng/action.
- `Combat/CharacterMovementAnimator.cs`: auto-detect animation và state names.
- `Combat/AutoDestroyEffect.cs`, `AutoDestroy.cs`: tự hủy effect.
- `Effects/AuraRotate.cs`, `Effects/HuyetSatDaoSkill.cs`, `Effects/PhapTuongBreath.cs`, `Effects/SwordFormationSkill.cs`, `_Game/Scripts/Effects/CultivationTechniqueEffect.cs`: hiệu ứng skill/aura/công pháp.

---

### R. Nhánh UI chính, chọn target, inventory panel

**Mục tiêu:** người chơi bấm chọn NPC/item/monster, xem thông tin, mở inventory/shop/map/story.

Script liên quan:

- `TouchSelectTarget.cs`: khoảng 4.141 dòng. Chọn target bằng touch/click, hiển thị info panel, tab info/inventory, header NPC, HP/EXP, equipment/skill list, world item info, portrait, follow panel.
- `InventoryPanelUI.cs`: mở/tắt inventory, grid item, detail, selected item, give/use item.
- `InventoryItemButtonUI.cs`: button item, click/down, setup item/empty.
- `NpcInventoryPanelUI.cs`: xem inventory NPC.
- `GiveItemToNpcButton.cs`: đưa item đang chọn cho NPC.
- `InventoryToggleButton.cs`, `BottomMenuButtonRouter.cs`, `MapToggleUI.cs`, `OpenMap.cs`, `CloseMap.cs`, `menumap.cs`: toggle bottom menu, map, inventory/shop/story.
- `mau.cs`: panel follow/target UI phụ.
- `UIBorderSparkEffect.cs`: hiệu ứng viền UI.

**Nhận xét:** `TouchSelectTarget` đang quá lớn và dính UI + data + inventory + chọn target. Nên tách thành `TargetSelector`, `TargetInfoPresenter`, `WorldItemInfoPresenter`, `NpcInventoryPresenter`.

---

### S. Nhánh vòng quay may mắn

**Mục tiêu:** wheel UI, quay thưởng item, auto layout slot, hiệu ứng thắng, đèn nháy.

Script liên quan:

- `Art/vongquay/LuckyWheelSpinTest.cs`: logic quay, bind slot, refresh item từ asset, random theo grade chance, add reward.
- `LuckyWheelOpenClose.cs`: mở/tắt panel vòng quay.
- `LuckyWheelWinEffect.cs`: hiệu ứng item trúng/pháo hoa.
- `LuckyWheelEntryAliveEffect.cs`: hiệu ứng item/entry sống động.
- `WheelSlotAutoLayout.cs`: bố trí slot theo vòng tròn, chỉnh frame/icon/text.
- `WheelBulbBlinkOnly.cs`: đèn bóng nháy.
- `UIPulse.cs`: pulse UI.

---

### T. Nhánh menu, open/close UI, quảng cáo, hiệu năng

**Mục tiêu:** hiệu ứng menu, toggle UI global, quảng cáo rewarded, cấu hình performance.

Script liên quan:

- `Art/Menu/MenuLightningUI.cs`, `MenuThunderSync.cs`, `MenuVortexParticles.cs`, `MenuVortexRotate.cs`: hiệu ứng menu sấm/vortex/video sync.
- `Art/OpenClose/GlobalUIButtonSwitch.cs`, `GlobalUIButtonSwitchButtonIcon.cs`, `UIButtonToggleTarget.cs`: ẩn/hiện nhiều button UI bằng slide/toggle.
- `AdsInitializer.cs`, `RewardedLTAdsButton.cs`: Unity Ads/rewarded ads.
- `GamePerformanceSettings.cs`, `NpcPerformanceOverlay.cs`: thiết lập FPS/vsync/overlay performance NPC.

---

### U. Nhánh editor/test/tool

**Mục tiêu:** kiểm tra runtime NPC, cân bằng item, test PlayMode.

Script liên quan:

- `Editor/NpcPlayModeTestRunner.cs`: chạy PlayMode test từ editor menu.
- `Editor/StatItemBalanceEditor.cs`: cân bằng item được chọn hoặc toàn bộ asset.
- `Tests/PlayMode/NpcRuntimeAuditTests.cs`: test rất lớn về movement/cultivation/schedule/conflict brain/provider busy/task hunger/cooldown.

---

## 4. Danh sách script theo nhóm để tiện nâng cấp

### Core AI lớn cần ưu tiên tách

- `Scripts/VillagerAI.cs`
- `Scripts/SmartNpcAI.cs`
- `Scripts/NpcTaskProvider.cs`
- `Scripts/TouchSelectTarget.cs`
- `Scripts/MonsterAI.cs` + các partial `MonsterAI.*.cs`
- `Scripts/NpcForgeAgent.cs`
- `Scripts/NpcAlchemyAgent.cs`
- `Scripts/NpcSocialSystem.cs`
- `Scripts/TreasureFrenzySystem.cs`
- `Scripts/BicanhSessionManager.cs`

### NPC dân làng / job / làng

- `VillagerAI.cs`
- `VillagerAI.Work.cs`
- `VillagerAI.Trade.cs`
- `VillagerAI.LegacyCultivation.cs`
- `VillagerAI.LegacyDailyTaskPlan.cs`
- `VillagerAI.LegacyTreasure.cs`
- `VillagerJobDispatcher.cs`
- `BuyerJob.cs`
- `SellerJob.cs`
- `GuardJob.cs`
- `HarvestJob.cs`
- `HunterJob.cs`
- `HealerJob.cs`
- `VillageHomeManager.cs`
- `VillageStorage.cs`
- `VillageNpcSetupTool.cs`
- `NpcWorkArea.cs`

### Tu sĩ / cultivation / combat technique

- `SmartNpcAI.cs`
- `SmartNpcAI.Brain.cs`
- `SmartNpcAI.Needs.cs`
- `SmartNpcAI.Schedule.cs`
- `CharacterStats.cs`
- `CultivationProgression.cs`
- `NpcCultivationAwakeningUtility.cs`
- `NpcCombatTechniqueSystem.cs`
- `HeavenlyTribulationSystem.cs`
- `ThienKiepStrikePrefab.cs`

### Monster / yêu thú

- `MonsterAI.cs`
- `MonsterAI.TargetingMovement.cs`
- `MonsterAI.TreasureCombat.cs`
- `MonsterAI.NeedsCultivation.cs`
- `MonsterAI.DevourDeathLootRespawn.cs`
- `MonsterAI.ProfileStats.cs`
- `MonsterAI.ItemEffects.cs`
- `MonsterAI.AnimationDebug.cs`
- `MonsterAttack.cs`
- `MonsterDirectionalAnimator.cs`
- `BicanhBoneMonsterAI.cs`
- `BoneSpiritAmbush.cs`

### Lịch / hành vi theo giờ

- `NpcScheduleController.cs`
- `NpcDailyRoutineLibrary.cs`
- `WorldTimeSystem.cs`
- `WorldClockTextUI.cs`
- `VillageHomeManager.cs`

### Item / inventory / loot

- `StatItemData.cs`
- `ItemInventory.cs`
- `ItemPickup.cs`
- `WorldStatItemPickup.cs`
- `ItemLifecycleSystem.cs`
- `ItemEffectSpawner.cs`
- `PickupVisualUtility.cs`
- `StatItemApplier.cs`
- `TimedStatItemBuff.cs`
- `ItemStatBalanceUtility.cs`
- `ItemText.cs`

### Shop / economy / trade

- `SimpleItemShop.cs`
- `ShopPanelUI.cs`
- `ShopItemButtonUI.cs`
- `ShopCardHoverEffect.cs`
- `ShopToggleUI.cs`
- `OpenShopUi.cs`
- `NpcEconomy.cs`
- `NpcTradeAgent.cs`
- `NpcCounterBroker.cs`
- `NpcMerchantRole.cs`
- `NpcShopStockRefill.cs`
- `PlayerWallet.cs`
- `PlayerWalletTextUI.cs`
- `PlayerWalletGrantButton.cs`

### Task / tavern / escort

- `NpcTaskProvider.cs`
- `TaskBoardInteract.cs`
- `TaskBoardPanelUI.cs`
- `TaskBoardRowUI.cs`
- `NpcTavernEntrance.cs`
- `NpcEscortTaskForceStarter.cs`

### Map / movement / navigation

- `NpcMapArea.cs`
- `NpcMapBoundaryClamp.cs`
- `NpcMapDestination.cs`
- `NpcMapMover2D.cs`
- `NpcMapNavigator.cs`
- `NpcTeleportGate.cs`
- `NpcLocationArea.cs`
- `NpcLawZone.cs`
- `NpcCollisionRegistry.cs`
- `NpcPathMemorySystem.cs`
- `BlockingTilemap2D.cs`
- `TilemapObstacle2D.cs`
- `WorldTilemapManager.cs`
- `CameraBounds.cs`
- `MobileCameraController.cs`
- `InteriorCameraFocus.cs`

### Thiên đạo / ban tặng / bảo vật

- `HeavenDaoSystem.cs`
- `HeavenDaoPanelUI.cs`
- `HeavenSystem.cs`
- `HeavenGiftPlacementController.cs`
- `HeavenlyTribulationSystem.cs`
- `TreasureFrenzySystem.cs`
- `TreasureHeatSystem.cs`
- `SkyDropToPosition.cs`
- `LightnighHitBurst.cs`
- `GoldenEnergyEffect.cs`

### Social / favorite / dialogue

- `NpcSocialSystem.cs`
- `DailyConversation.cs`
- `NpcText.cs`
- `NpcFavorite.cs`
- `NpcFavoriteManager.cs`
- `NpcFavoriteButtonUI.cs`
- `NpcFavoriteClickBridge.cs`
- `FavoriteNpcListUI.cs`
- `FavoriteNpcRowUI.cs`
- `NpcInteractionPoint.cs`
- `NpcPetCompanion.cs`

### UI / menu / responsive

- `TouchSelectTarget.cs`
- `InventoryPanelUI.cs`
- `InventoryItemButtonUI.cs`
- `NpcInventoryPanelUI.cs`
- `BottomMenuButtonRouter.cs`
- `MapToggleUI.cs`
- `OpenMap.cs`
- `CloseMap.cs`
- `menumap.cs`
- `mau.cs`
- `ResponsiveCanvasScaler.cs`
- `ResponsivePanelFitter.cs`
- `MobileSafeAreaFitter.cs`
- `UIBorderSparkEffect.cs`
- `WorldScreenNotificationHub.cs`
- `UIWorldStoryManager.cs`

### Lucky wheel

- `Art/vongquay/LuckyWheelSpinTest.cs`
- `Art/vongquay/LuckyWheelOpenClose.cs`
- `Art/vongquay/LuckyWheelWinEffect.cs`
- `Art/vongquay/LuckyWheelEntryAliveEffect.cs`
- `Art/vongquay/WheelSlotAutoLayout.cs`
- `Art/vongquay/WheelBulbBlinkOnly.cs`
- `Art/vongquay/UIPulse.cs`

### Effect / combat visual

- `Effects/AuraRotate.cs`
- `Effects/HuyetSatDaoSkill.cs`
- `Effects/PhapTuongBreath.cs`
- `Effects/SwordFormationSkill.cs`
- `_Game/Scripts/Effects/CultivationTechniqueEffect.cs`
- `NPCVisualMovement.cs`
- `Combat/CharacterMovementAnimator.cs`
- `Combat/NpcRangedSkill.cs`
- `Combat/RangedProjectile2D.cs`
- `Combat/AutoDestroyEffect.cs`

---

## 5. Đánh giá nhanh kiến trúc hiện tại

### Điểm tốt

- Game đã có nền mô phỏng thế giới khá rộng: lịch ngày đêm, thời tiết, NPC work, tu sĩ, yêu thú, item, shop, task, thiên đạo, ban tặng, tranh đoạt.
- Đã có `NpcScheduleController` và `NpcDailyRoutineLibrary`, rất hợp với hướng làm NPC sống theo giờ.
- Hệ item `StatItemData` đủ rộng cho đan dược/pháp bảo/công pháp/vật liệu/thực phẩm.
- Yêu thú đã được tách partial khá tốt hơn Villager/SmartNpc.
- Đã có PlayMode test riêng cho NPC, đây là nền tốt để refactor không làm vỡ game.

### Điểm đang nặng / dễ lỗi

1. `VillagerAI.cs` quá lớn và ôm quá nhiều thứ: job, movement, pathfinding, schedule, trade, cultivation, item, damage.
2. `SmartNpcAI.cs` cũng quá lớn, nhưng vẫn hợp lý hơn vì tu sĩ cần nhiều hành vi. Tuy nhiên movement/pathfinding nên tách ra.
3. `NpcTaskProvider.cs` quá lớn, đang ôm cả offer, runtime, tavern meal, board, escort, reward, combat danger response.
4. `TouchSelectTarget.cs` quá lớn, UI chọn target và hiển thị info nên tách khỏi inventory/detail rendering.
5. Logic tu luyện xuất hiện ở cả `VillagerAI.LegacyCultivation`, `SmartNpcAI`, `MonsterAI.NeedsCultivation`, `CharacterStats`, `CultivationProgression`. Nên gom công thức vào service chung.
6. Movement/pathfinding/tránh vật cản bị lặp ở `VillagerAI` và `SmartNpcAI`.
7. Nhiều hệ thống cùng làm “item giao cho NPC/nhặt/sử dụng/bán” nên dễ xung đột: `ItemInventory`, `NpcItemCollector`, `NpcTradeAgent`, `NpcCounterBroker`, `HeavenGiftPlacementController`, `TreasureHeatSystem`.
8. Có nhiều asset backup `_Recovery`, plugin demo, tile asset rất nhiều. Khi gửi Codex nên tránh gửi toàn bộ asset art nếu chỉ sửa code, vì dễ tốn token và nhiễu.

---

## 6. Hướng tách refactor đề xuất

### Ưu tiên 1: Chốt lại vai trò 3 loại actor

- `VillagerAI`: chỉ dân thường, chạy lịch, ăn/ngủ/làm/bán/mua cơ bản, không tu luyện phức tạp.
- `SmartNpcAI`: chỉ tu sĩ, tu luyện/săn/gather/task/tranh bảo vật/dùng công pháp/pháp bảo.
- `MonsterAI`: yêu thú, lãnh địa/săn/tu luyện/loot/respawn.

### Ưu tiên 2: Tạo component dùng chung

Nên tách từ các script lớn ra:

- `NpcMovementController`: move target, avoid obstacle, crowd separation, map boundary.
- `NpcScheduleBrain`: đọc lịch và trả về activity hiện tại.
- `NpcInventoryService`: dùng/nhặt/cho/bán item.
- `CultivationService`: exp, breakthrough, realm power, thiên kiếp.
- `NpcTradeService`: broker/shop/NPC trade.
- `TaskRuntimeService`: chạy task đã nhận.

### Ưu tiên 3: Cắt legacy khỏi Villager

Các phần nên chuyển/xóa khỏi dân thường:

- `VillagerAI.LegacyCultivation.cs`
- `VillagerAI.LegacyTreasure.cs`
- phần dân thường tự hunt/gather tranh bảo vật nếu không còn cần.
- phần chuyển dân sang tu sĩ chỉ giữ ở `NpcCultivationAwakeningUtility`.

### Ưu tiên 4: Giữ lịch dân thường đúng yêu cầu mới

Lịch dân thường nên giữ cực rõ:

- 20:00 - 05:00: ở nhà/ngủ/ẩn trong nhà.
- 05:00 - 11:00: đi làm.
- 11:00 - 13:00: về nhà nghỉ trưa.
- 13:00 - 17:00: đi làm.
- 17:00 - 20:00: về nhà/nghỉ.

Dân thường không nên tự quyết định quá nhiều ngoài lịch, trừ nhu cầu rất cơ bản.

---

## 7. Kết luận ngắn

Cấu trúc hiện tại đã có đủ nền cho game “người chơi là Thiên Đạo quan sát thế giới vận hành”. Vấn đề không phải thiếu hệ thống, mà là hệ thống bị chồng lên nhau trong vài script quá lớn. Hướng tối ưu là giữ lại 3 trục chính:

1. **World Simulation:** thời gian, thời tiết, map, resource, event.
2. **Actor Simulation:** Villager đơn giản, SmartNpc tu sĩ, Monster yêu thú.
3. **Heaven Dao Interaction:** ban tặng, thiên phạt, thiên kiếp, bí cảnh, tranh đoạt bảo vật.

Sau đó tách movement, schedule, item, trade, cultivation thành service/component dùng chung để nâng cấp dễ hơn.
