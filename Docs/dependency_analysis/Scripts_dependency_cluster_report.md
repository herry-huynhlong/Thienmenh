# Báo cáo dependency scripts

## Phạm vi kiểm tra

- Nguồn quét: `C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts`.
- Tổng file C#: **208**.
- Tổng type phát hiện: **334**.
- Số dòng bị loại khỏi phân tích vì preprocessor tắt: **797**.
- Đã bỏ qua block `#if false` / `#else` không active trước khi dựng graph.

## 1. Trục lõi theo số liệu

| Type | Incoming | Outgoing |
|---|---:|---:|
| `VillagerAI` | 97 | 67 |
| `StatItemData` | 90 | 25 |
| `SmartNpcAI` | 86 | 64 |
| `NpcText` | 74 | 10 |
| `WorldTimeSystem` | 71 | 5 |
| `MonsterAI` | 60 | 33 |
| `NpcRoleUtility` | 58 | 16 |
| `ItemInventory` | 58 | 15 |
| `ItemText` | 57 | 14 |
| `CharacterStats` | 56 | 14 |
| `NpcTradeAgent` | 47 | 19 |
| `NpcScheduleController` | 47 | 8 |

## 2. Mức độ dính chùm của hệ thống

- Weak component: **15**.
- Component lớn nhất: **320 type**.
- Strongly connected component (SCC) lớn nhất: **147 type**.
- Mẫu trong SCC lớn nhất: `AlchemyCost, AlchemyIngredient, AlchemyRecipe, BehaviourState, BicanhDungeonResident, BicanhSessionManager, CharacterMovementAnimator, CharacterStats, CombatPowerUtility, CombatRecord, DailyConversation, DailyTaskNeed, DoorTeleportSameScene, EntityEmotion, EntityGenerator, EntityIdentity, EntityMemory, EntityNeeds, EntityPersonality, EntityProfile`.

## 3. No incoming đã tách nhóm

- Data/enum/support: **2** type.
- Mẫu: `ItemStatBalanceUtility, NpcLawZone`.
- Runtime root / Inspector / Unity entrypoint: **49** type.
- Mẫu: `AutoDestroy, AutoDestroyEffect, BlockingTilemap2D, BoneSpiritAmbush, BottomMenuButtonRouter, BuyerJob, DontDestroy, DoorTeleport, GiveItemToNpcButton, GoldenEnergyEffect, HeavenDaoPanelUI, HeavenNurturePanelUI, HeavenNurtureToggleButtonUI, InteriorCameraFocus, ItemPickup, LightningHitBurst, MapToggleUI, MobileSafeAreaFitter, MonsterAttack, NpcAlchemyRole`.

## 4. Naming và cấu trúc đáng chú ý

- Case-collision: **0**.
- File/type mismatch tổng: **172**.
- Lệch tên cùng chữ khác hoa-thường: **2**.
  - `OpenShopUi.cs:3` khai báo `OpenShopUI`.
  - `menumap.cs:3` khai báo `Menumap`.
- File/type mismatch ưu tiên xử lý: **108**.
  - `BicanhSessionManager.cs:956` khai báo `BehaviourState` (class).
  - `BicanhSessionManager.cs:968` khai báo `ParticipantSnapshot` (class).
  - `EntityCore.cs:56` khai báo `EntityIdentity` (class).
  - `EntityCore.cs:65` khai báo `EntityStats` (class).
  - `EntityCore.cs:97` khai báo `EntityTalent` (class).
  - `EntityCore.cs:108` khai báo `EntityPersonality` (class).
  - `EntityCore.cs:122` khai báo `EntityEmotion` (class).
  - `EntityCore.cs:133` khai báo `EntityNeeds` (class).
  - `EntityCore.cs:142` khai báo `EntityMemory` (class).
  - `EntityCore.cs:152` khai báo `EntityRelationship` (class).
  - `EntityCore.cs:162` khai báo `EntityProfile` (class).
  - `EntityCore.cs:232` khai báo `EntityGenerator` (class).
  - `FullGameSaveController.cs:8` khai báo `FullGameSaveData` (class).
  - `FullGameSaveController.cs:27` khai báo `SavedFavoriteNpcData` (class).
  - `GameSaveSystem.cs:7` khai báo `SavedItemStack` (class).
- File chứa nhiều MonoBehaviour: **2**.
  - `NpcCombatTechniqueSystem.cs` => `NpcCombatTechniqueRuntime, NpcCombatTechniqueBuff`
  - `NpcSocialSystem.cs` => `NpcSocialSystem, NpcSocialIdentity, NpcNeeds, NpcPersonality, NpcRelationshipGraph, NpcMemory, NpcOverheadDialogueUI, NpcConversationAgent, NpcDecisionBrain, NpcNegotiationAgent, NpcLawZoneInternal, NpcSocialWorldInstaller`

## 5. Ghi chú sử dụng graph

- Graph này chỉ phản ánh quan hệ nhìn thấy trong code active.
- Quan hệ Scene/Prefab/Inspector/Button OnClick vẫn cần kiểm tra trong Unity.
- Type trong file có nhiều class có thể làm tăng over-link cục bộ; vì vậy cần đọc kỹ các file lớn như `NpcSocialSystem.cs` hoặc `EntityCore.cs` trước khi refactor.

## 6. File đầu ra

- `direct_refs.json`: ref có line/text theo source type.
- `code_graph.json`: graph đã tổng hợp và thống kê count.
- `Scripts_dependency_cluster_report.md`: bản tóm tắt này.
