# RÀ SOÁT LOGIC THEO TRẠNG THÁI – SCRIPTS(6)

## 1. Phạm vi và kết luận

Đã rà 218 script C# trong `Scripts(6).zip`, tập trung vào luồng thực thi thay vì chỉ kiểm tra tên file hoặc lỗi cú pháp. Các luồng được lần theo gồm `Awake/Start/Update/FixedUpdate`, phương thức quyết định hành vi, chuyển trạng thái, chiếm quyền điều khiển, kết thúc/cleanup và save/load.

Kết luận chính: dự án hiện **không có một state machine trung tâm cho NPC**. Trạng thái của cùng một NPC được phân tán trong nhiều lớp và nhiều kiểu dữ liệu; vì vậy một NPC có thể đồng thời mang các trạng thái không tương thích. Đây là nguyên nhân gốc của các biểu hiện như NPC đứng yên, đổi mục tiêu liên tục, đi sai lịch, task không kết thúc, hoặc tải game xong hành vi bị lệch.

## 2. Các lớp trạng thái đang cùng tồn tại trên một NPC

| Miền trạng thái | Kiểu/state chính | Script sở hữu | Ai có thể ghi/chuyển state |
|---|---|---|---|
| Lịch ngày | `NpcScheduleActivity`, `NpcScheduleSlot` | `NpcScheduleController`, `NpcDailyRoutineLibrary` | Schedule controller, VillagerAI, SmartNpcAI, profession controller |
| Mục tiêu Smart AI | `SmartAITaskGoal`, `SmartAITaskPriority`, `SmartAITask` | `SmartNpcAI.Tasks.cs` | Schedule, needs, combat recovery, mission, trade, hunt |
| Task quán/nhiệm vụ | `TavernTaskStage`, `TavernMealStage`, `RunningNpcTask` | `NpcTaskProvider` | Provider update loop, escort flow, interruption cleanup |
| Nghề nghiệp | `NpcJobState`, `AlchemyCycleState`, `ForgeCycleState` | Harvest/Hunter/Guard/Healer/Buyer/Seller, fixed profession controllers | Job Update, profession Update, schedule |
| Di chuyển | nhiều cờ target và `hasTarget` | `VillagerAI`, `SmartNpcAI`, `NpcMapMover2D`, `NpcTaskProvider`, `NpcRoleUtility` | Tất cả các hệ trên |
| Chiến đấu | monster target, retreat, recover, aggro | Smart/Villager/Monster/Combat helper | combat brain, needs, task, schedule |
| Trình bày | `currentAction` kiểu `string` | Smart/Villager/Monster/NpcData | gần như mọi hệ thống |
| Xã hội | `NpcDecisionKind`, needs, memory, relationship | `NpcSocialSystem` | social brain, conversation, world-day tick |
| Thế giới | time/weather/event | World systems | clock, random event, manual weather, save/load |
| Lưu game | snapshot rời rạc | `FullGameSaveController`, `GameSaveSystem`, PlayerPrefs | nhiều subsystem tự lưu riêng |

Không có một lớp nào quyết định rõ: **ai đang nắm quyền điều khiển NPC**, state nào được ưu tiên, state nào bị tạm dừng, và khi trả quyền thì phải phục hồi những gì.

## 3. Thứ tự ưu tiên nên có nhưng code hiện chưa bảo đảm

Đề xuất một trật tự thống nhất:

1. Dead/Disabled
2. Forced cutscene hoặc system lock
3. Combat emergency / flee / heal
4. Provider task / escort / mission đã cam kết
5. Profession cycle không thể ngắt
6. Schedule activity
7. Needs
8. Social/autonomous idle

Hiện tại mỗi hệ tự đưa ra ưu tiên riêng. Ví dụ `SmartAITaskPriority` chỉ điều phối trong Smart task, nhưng không điều phối `NpcTaskProvider`, profession controller, movement component hay `currentAction`.

---

# 4. NHÓM SMART NPC

## 4.1 Luồng thực thi thực tế

`SmartNpcAI.Update()` lần lượt kiểm tra chết, đồng bộ stats, refresh lịch, provider busy, timer, hunt/combat, stale state và cuối cùng gọi brain. `FixedUpdate()` tiếp tục xử lý movement. Brain lại ưu tiên death, locked routine, combat support, Smart task, monster target, schedule, hunt travel, social/weather/needs/autonomous.

Như vậy một frame có thể đi qua cả schedule refresh, Smart task, combat và movement flags trước khi quyết định cuối cùng.

## 4.2 Lỗi thay thế task sai ưu tiên – P0

**Vị trí:** `SmartNpcAI.Tasks.cs:161-162`

```csharp
if (currentSmartTask.priority < priority ||
    currentSmartTask.canBeInterrupted)
```

Điều kiện này có nghĩa: chỉ cần task hiện tại cho phép ngắt, **bất kỳ task mới nào cũng được quyền thay thế**, kể cả task mới có ưu tiên thấp hơn.

### Kịch bản lỗi

- NPC đang `LowHpRecovery`, priority Emergency, nhưng task được đánh dấu interruptible.
- Một nhu cầu `NeedPotion` hoặc lịch thường gửi task Important/Normal.
- Vì task hiện tại `canBeInterrupted == true`, task thấp hơn vẫn ghi đè hồi phục khẩn cấp.
- NPC có thể ngừng chạy trốn/hồi máu để đi mua thuốc, đi làm nhiệm vụ hoặc đi theo lịch.

### Sửa đúng

```csharp
bool canReplace =
    priority > current.priority ||
    (priority == current.priority && current.canBeInterrupted);
```

Task thấp hơn không bao giờ được thay task cao hơn. Với Emergency recovery, nên đặt non-interruptible cho đến khi HP vượt ngưỡng an toàn hoặc timeout.

## 4.3 Lịch chuyển sang hoạt động không map được nhưng task cũ không bị xóa – P0

**Vị trí:**
- `SmartNpcAI.Schedule.cs:74-76`
- `SmartNpcAI.Schedule.cs:896-916`
- `SmartNpcAI.Tasks.cs:102-106`

Schedule luôn gọi `RequestScheduledTask(MapScheduleActivityToSmartGoal(...))`. Tuy nhiên map chỉ hỗ trợ Cultivate, Mission, Hunt/Gather và Trade. `Idle`, `Sleep`, `Eat`, `Work`, `ReturnHome`, `Alchemy`, `Forge` trả về `None`.

`RequestSmartTask(None)` chỉ trả về false, không clear `scheduleSmartTask` và không chắc chắn clear `currentSmartTask`.

### Kịch bản lỗi

- 17:59 NPC đang `FreeHuntAndGather`.
- 18:00 lịch đổi sang Idle hoặc ReturnHome.
- Mapping trả `None`, yêu cầu bị từ chối.
- Task săn cũ vẫn tồn tại; phần display còn kiểm tra current task/schedule task trước schedule (`SmartNpcAI.Tasks.cs:339-366`).
- NPC có thể tiếp tục mang trạng thái đi săn dù lịch đã đổi.

### Sửa đúng

Tạo `SetScheduledGoal(activity)`:

- Nếu map ra `None`: gọi `ClearScheduledTask()`.
- Nếu slot đổi: huỷ target thuộc slot cũ bằng typed owner token.
- Chỉ giữ flow cũ nếu flow đó được khai báo tương thích với slot mới.

## 4.4 Lịch Smart NPC không lệch pha như chú thích – P1

**Vị trí:** `NpcDailyRoutineLibrary.cs:107-124`

Hàm tạo lịch đặt cứng các khung 7h, 13h, 18h, 19h. Chú thích nói sẽ lệch giờ theo seed, nhưng không gọi hàm offset.

Các hàm:
- `GetSmartCultivatorPhaseOffset()`
- `PickSmartSupportActivity()`
- `ApplySmartBlockOrder()`

không được gọi ở nơi khác.

### Hậu quả

Tất cả Smart cultivator dễ đồng loạt:
- nhận nhiệm vụ lúc 7h,
- săn/gather lúc 13h,
- trade lúc 18h,
- cultivate lúc 19h.

Điều này tạo ùn tắc ở cổng, quán, shop và điểm tài nguyên; đồng thời làm stuck detector kích hoạt hàng loạt.

### Sửa đúng

Sau khi build block, áp dụng offset ổn định dựa trên persistent ID, không dựa `GetInstanceID()`. Có thể chia NPC thành 6–12 nhóm lệch 10–30 phút game.

## 4.5 `currentAction` vừa là text vừa là state – P0

Trong Smart AI có nhiều kiểm tra kiểu:

```csharp
currentAction == NpcText.Action("walkingRoad")
```

`currentAction` còn chứa chuỗi format, text dịch và route action. Khi đổi ngôn ngữ, chuỗi đang lưu không tự đổi nhưng `NpcText.Action()` trả chuỗi mới, làm equality thất bại.

### Sửa đúng

Tách:

```csharp
NpcExecutionState state;
NpcActionReason reason;
NpcActionPayload payload;
string DisplayAction => Localize(state, payload);
```

Không dùng text hiển thị để khóa flow, phát hiện combat, xác định đến đích hoặc quyết định cleanup.

---

# 5. NHÓM VILLAGER NPC

## 5.1 Lịch chỉ xử lý một phần enum – P0

**Vị trí:** `VillagerAI.Brain.cs:144-220`

Code chỉ có handler rõ cho:
- Sleep/Eat/ReturnHome
- Buy/Sell/Trade
- Work

Mọi activity còn lại rơi vào:

```csharp
default:
    GoHomeToRest();
    return true;
```

### Các state bị xử lý sai

`Gather`, `Hunt`, `Cultivate`, `TakeTask`, `DoMission`, `FreeHuntAndGather`, `Alchemy`, `Forge`, `Idle` và bất kỳ enum mở rộng nào có thể bị biến thành “đi về nhà nghỉ”.

### Kịch bản lỗi

Một custom schedule đặt Villager đi Gather 10h–12h. Đến 10h, schedule hợp lệ nhưng switch không có case Gather, NPC gọi `GoHomeToRest()` và không bao giờ đi gather.

### Sửa đúng

- Dùng switch exhaustive, không có default “go home”.
- Mỗi activity phải có `CanExecute`, `Enter`, `Tick`, `Exit`.
- Activity không hỗ trợ phải log lỗi cấu hình một lần và chuyển sang `Idle`, không tự suy diễn thành Sleep/ReturnHome.

## 5.2 `GetSchedule()` có side effect – P1

**Vị trí:** `NpcScheduleController.cs:213-238`

Một hàm tên “Get” lại có thể:
- tự `AddComponent<NpcScheduleController>()`,
- tự detect life path,
- tự rebuild lịch.

### Hậu quả

Kết quả vận hành phụ thuộc thứ tự script nào gọi trước. Chỉ một kiểm tra social/trade cũng có thể làm NPC bắt đầu bị enforce schedule dù prefab chưa cấu hình.

### Sửa đúng

Tách thành:
- `TryGetSchedule()` — tuyệt đối không sửa object.
- `EnsureScheduleInstalled()` — chỉ gọi tại bootstrap/spawn.
- `BuildDefaultSchedule()` — chỉ gọi có chủ đích.

## 5.3 Lịch custom có thể bị ghi đè – P1

**Vị trí:** `NpcScheduleController.cs:76-98`

`Awake()` và `OnValidate()` đều rebuild mặc định khi `autoBuildDefaultSchedule = true`. Inspector schedule có thể bị thay đổi chỉ vì reload scene hoặc edit một field.

### Sửa đúng

Thêm `scheduleSource`:
- GeneratedDefault
- CustomInspector
- RuntimeOverride

Chỉ generated schedule mới được tự rebuild.

## 5.4 Slot chồng nhau chọn slot đầu tiên – P1

**Vị trí:** `NpcScheduleController.cs:247-263`

`GetCurrentSlot()` foreach và trả slot đầu tiên chứa giờ. Không có validator phát hiện overlap/gap.

### Hậu quả

Slot phía sau có thể không bao giờ chạy nhưng không có cảnh báo.

### Sửa đúng

Tạo validator editor/runtime:
- chuẩn hóa wrap-around 24h,
- báo overlap,
- báo gap,
- báo duration 0,
- sắp xếp slot hoặc yêu cầu explicit priority.

---

# 6. NHÓM TASK PROVIDER / ESCORT / TAVERN

## 6.1 Provider chỉ pause một base brain – P0

**Vị trí:** `NpcTaskProvider.cs:7199-7225`

Provider chỉ tìm `VillagerAI`, nếu không có mới tìm `SmartNpcAI`, rồi disable component đó. Các controller khác vẫn chạy:
- Harvest/Hunter/Guard/Healer job
- fixed alchemist/blacksmith
- resource gatherer
- social/conversation controller
- mover độc lập

### Kịch bản lỗi

NPC nhận task từ provider. `SmartNpcAI` bị disable nhưng `HarvestJob.Update()` vẫn đổi job target/action. Provider kéo NPC tới nơi task, trong khi job controller tiếp tục đưa lệnh khác. NPC giật, đổi action hoặc rời task route.

### Sửa đúng

Dùng `NpcControlLease`:

```text
Owner = ProviderTask
Priority = CommittedTask
SuspendedControllers = [...]
MovementIntentOwner = ProviderTask
CleanupToken = unique task id
```

Khi acquire phải tạm dừng tất cả controller thuộc nhóm thấp hơn, không chỉ một brain.

## 6.2 Hai đường cleanup không tương đương – P0

**Vị trí:**
- Normal finish: `NpcTaskProvider.cs:4914-4940`
- Interrupted: `NpcTaskProvider.cs:1584-1608`

Finish bình thường gọi:
- `ClearTaskReservations`
- `UnmarkNpcBusyWithProvider`
- `ReleaseTaskOffer`
- restore/resume escort
- unlock escort offer
- resume AI

Interrupted cleanup thiếu ít nhất:
- `ClearTaskReservations`
- `ResumeEscortCompanion`
- `UnlockEscortOffer`

### Hậu quả

Khi disable provider/chuyển scene/thoát object:
- tài nguyên có thể bị reserve vĩnh viễn,
- companion vẫn pause,
- escort offer vẫn khóa,
- task mới không nhận được.

### Sửa đúng

Tạo duy nhất:

```csharp
FinalizeTask(task, TaskEndReason reason)
```

Hàm phải idempotent; gọi hai lần không gây reward đôi hoặc unlock sai. Mọi exit path bắt buộc đi qua hàm này.

## 6.3 Provider có hai kiểu movement khác nhau – P1

**Vị trí:** `NpcTaskProvider.cs:6995-7095`

- Có `NpcMapMover2D`: giao target cho mover.
- Không có mover: trực tiếp `transform.position = MoveTowards(...)`.

### Hậu quả

Cùng một task nhưng prefab khác component sẽ có collision, animation, tốc độ và stuck behavior khác nhau. Raw transform còn có thể xung đột Rigidbody velocity cũ vì pause brain không zero velocity.

### Sửa đúng

Mọi NPC bắt buộc đi qua một `NpcMotor2D`; provider chỉ submit destination và policy, không tự dịch transform.

## 6.4 Set action sau khi pause có thể thất bại với Villager – P1

Provider pause AI rồi mới gọi `NpcRoleUtility.SetAction()`. Với Villager, utility không ghi nếu `IsActionLocked`; nhưng component đã bị disable nên timer khóa có thể không giảm. Task có thể chạy nhưng UI vẫn hiện action cũ.

Sửa bằng typed control state; khi provider acquire lease, provider state phải có quyền presentation cao hơn timer cũ.

---

# 7. NHÓM MOVEMENT

## 7.1 Nhiều movement authority – P0

Các đường ghi vị trí/vận tốc:
- VillagerAI internal movement
- SmartNpcAI internal movement
- NpcMapMover2D
- NpcTaskProvider raw transform
- `NpcRoleUtility.MoveTowards()` raw transform (`NpcRoleUtility.cs:403-435`)

`NpcMapMover2D.Awake()` chỉ tự disable khi thấy VillagerAI active (`NpcMapMover2D.cs:80-87`), không kiểm tra SmartNpcAI.

### Kịch bản lỗi

Prefab có SmartNpcAI + NpcMapMover2D. Smart `FixedUpdate()` ghi `Rigidbody2D.linearVelocity`; mover cũng ghi velocity. Target khác nhau làm NPC rung hoặc quay đầu mỗi physics frame.

### Sửa đúng

- Chỉ `NpcMotor2D` được phép ghi Rigidbody/transform.
- Brain, task, profession chỉ phát `MovementIntent`.
- Motor chọn intent có priority cao nhất.
- Thêm prefab validator: phát hiện hơn một component có quyền movement.

## 7.2 Trộn Update/FixedUpdate và transform/Rigidbody – P1

Raw `transform.position` chạy theo `Time.deltaTime`; internal motor chạy physics. Khi cùng tồn tại sẽ tạo jitter, xuyên collider hoặc animation không đồng bộ.

Sửa: tất cả physics movement trong `FixedUpdate`, dùng `Rigidbody2D.MovePosition` hoặc velocity theo một policy thống nhất.

---

# 8. NHÓM ACTION / HIỂN THỊ / COMBAT DETECTION

## 8.1 `currentAction` là namespace hỗn hợp – P0

Có hơn một trăm action key/chuỗi khác nhau, gồm:
- `NpcText.Action(key)` đã dịch,
- chuỗi tiếng Anh raw,
- chuỗi Việt không dấu raw,
- chuỗi format chứa tên target.

`NpcRoleUtility.IsInCombat()` lại suy luận combat bằng tìm chuỗi dịch và literal (`NpcRoleUtility.cs:216-251`). Chỉ cần đổi text, format hoặc ngôn ngữ là combat detection đổi theo.

### Sửa đúng

`IsInCombat` phải đọc `NpcExecutionState.Combat` hoặc combat controller, không đọc text.

## 8.2 `NpcData.currentAction` có thể che state sống – P1

**Vị trí:** `NpcRoleUtility.cs:254-274`

Utility ưu tiên `NpcData.currentAction` nếu không rỗng, trước Villager/Smart action. Trong code không thấy cơ chế đồng bộ trường này với brain. Một giá trị Inspector cũ có thể được trả về mãi, làm `IsInCombat`, UI và các điều kiện khác sai.

### Sửa đúng

- Xóa runtime action khỏi `NpcData`, hoặc
- biến nó thành property đọc từ `NpcStateController`, không serialized.

## 8.3 `SetAction` ghi cả brain active lẫn dormant – P1

**Vị trí:** `NpcRoleUtility.cs:377-400`

Utility có thể tìm cả component disabled rồi ghi state. Với Villager thì tôn trọng lock; Smart thì force set. Một API nhưng hai semantics khác nhau.

### Sửa đúng

`SetAction` chỉ cập nhật presentation của state owner hiện tại. Không ghi trực tiếp vào hai brain.

---

# 9. NHÓM PROFESSION / JOB

## 9.1 Job state không liên kết với brain state – P1

Các job có `NpcJobState` riêng như Moving/Working/Returning, trong khi Villager/Smart vẫn có schedule/action/movement flags. Không có transaction chuyển từ schedule sang job và trả về schedule.

### Sửa đúng

Profession trở thành một `NpcStateHandler` hoặc control lease. Job state là substate của `Profession`, không phải state ngang hàng tự chạy.

## 9.2 Alchemy/Forge cycle không nằm trong save snapshot – P0/P1

Các state/progress như cycle state, state start, purchase/sale day, refine timer không có trong `FullGameSaveData`. Reload giữa chu kỳ có thể restart, bỏ qua hoặc lặp sản xuất/giao dịch.

### Sửa đúng

Save:
- profession type
- cycle state
- progress normalized hoặc world start/end time
- reserved input/output
- last transaction IDs

Khôi phục theo world time, không theo coroutine/timer RAM.

---

# 10. NHÓM SOCIAL

## 10.1 Social decision brain bị vô hiệu cho hai NPC chính – P1

**Vị trí:** `NpcSocialSystem.cs:2861-2874`

Installer tự thêm `NpcDecisionBrain` nhưng tắt `enabledDecisionBrain` nếu NPC có VillagerAI hoặc SmartNpcAI. Vì hai loại này là NPC chính, phần autonomous decision gần như không vận hành cho chúng.

### Sửa đúng

Không tạo một brain song song. Social nên phát nhu cầu/intention vào state arbiter của Villager/Smart.

## 10.2 Relationship/memory không đồng bộ save với world day – P0/P1

World time được lưu riêng, nhưng relationship graph/memory không có trong full snapshot. Load có thể giữ ngày cao nhưng quan hệ quay về mặc định.

Ngoài ra `TriggerTimeEvents(force=true)` phát `OnDayChanged` nhưng bỏ qua daily relationship và birth tick (`WorldTimeSystem.cs:336-366`). Điều này tránh tick đôi nhưng cũng khiến restoration cần có snapshot đầy đủ; hiện chưa có.

---

# 11. NHÓM WORLD TIME / WEATHER / EVENT

## 11.1 Bootstrap thiếu clock trung tâm – P0

**Vị trí:** `WorldSimulationBootstrap.cs:5-55`

Bootstrap tạo lighting, weather, accumulation, heaven và event nhưng không tạo `WorldTimeSystem`. Các system khác chỉ return khi clock null.

### Hậu quả

Scene thiếu clock vẫn chạy game mà không báo lỗi rõ:
- lịch NPC đứng,
- weather không đổi,
- event không kích hoạt,
- day/night không tiến đúng.

### Sửa đúng

Bootstrap `WorldTimeSystem` đầu tiên; các subsystem cần fail-fast/log once nếu không có clock.

## 11.2 World event chỉ là chuỗi, không có vòng đời – P1

**Vị trí:** `WorldEventSystem.cs:19-83`

`currentEvent` là string. Không có:
- event instance ID,
- start/end world time,
- duration,
- active effect handles,
- cleanup,
- persistence.

Random check thất bại sẽ xóa `currentEvent`; vì vậy duration thực tế phụ thuộc khoảng check chứ không phải thiết kế event.

Ngoài BeastWave và HeavenlyTribulation, nhiều event chủ yếu chỉ ghi log, chưa có gameplay state rõ.

### Sửa đúng

Dùng `ActiveWorldEvent` typed object và `StartEvent/TickEvent/EndEvent`, save toàn bộ instance.

## 11.3 Weather/random event không được restore – P1

Random weather, manual override, next-change time và active world event không nằm trong full save. Reload có thể đổi thời tiết/sự kiện ngay lập tức dù thời gian thế giới giữ nguyên.

---

# 12. NHÓM MONSTER

## 12.1 Respawn là coroutine RAM – P1

Nếu monster chết rồi save/quit trước respawn, coroutine mất. Khi reload prefab/scene, monster có thể sống lại ngay, không theo thời gian còn lại.

Sửa: lưu `isDead`, `deathWorldHour`, `respawnWorldHour`, loot consumed state.

## 12.2 Fallback world hour có thể biến một ngày thành 24 giờ thật – P1

Khi không có `WorldTimeSystem`, logic có nơi dùng `Time.time / 3600f`. Một đơn vị giờ thế giới trở thành một giờ thật; respawn/day-based behavior cực chậm. Đây liên quan trực tiếp lỗi bootstrap thiếu clock.

---

# 13. NHÓM SAVE / LOAD / INVENTORY

## 13.1 Full save không phải snapshot thế giới – P0

**Vị trí:** `FullGameSaveController.cs:7-26`

Payload chỉ trực tiếp chứa:
- scene
- player transform/stats
- camera
- favorites

World time, wallet, inventory, shop... được lưu ở key riêng; NPC state, tasks, profession, weather, event, monster respawn và relationship không nằm trong transaction chung.

### Hậu quả

Thoát đột ngột giữa các lần ghi key có thể tạo save lai giữa hai thời điểm.

### Sửa đúng

Một `SaveRoot` versioned gồm snapshot của tất cả subsystem, ghi temp → verify checksum → replace primary → giữ backup.

## 13.2 HP bằng 0 không được restore – P0

**Vị trí:** `FullGameSaveController.cs:291-296`

```csharp
health.currentHP = data.playerCurrentHP > 0
    ? data.playerCurrentHP
    : health.currentHP;
```

Save lúc chết HP=0 sẽ không tải 0. Đồng thời `PlayerHealth` và `CharacterStats` cùng sở hữu HP; SavePlayer ghi PlayerHealth trước rồi CharacterStats ghi đè (`236-263`).

### Sửa đúng

Một nguồn HP authoritative; `0` là giá trị hợp lệ. Dùng flag `hasHealthData` thay vì dùng 0 làm sentinel.

## 13.3 Camera bị bỏ follow sau load – P1

**Vị trí:** `FullGameSaveController.cs:332-336`

Load đặt `followTarget = null` nhưng không có guarantee rebind. Camera có thể đứng đúng vị trí save nhưng không theo player nữa.

## 13.4 Inventory có thể hồi sinh item đã dùng – P0/P1

**Vị trí:** `ItemInventory.cs:663-703`

Merge dùng `Mathf.Max(existing.amount, stack.amount)`. Khi giữ item Inspector lúc load, số lượng mặc định có thể thắng số đã save; item đã dùng hết xuất hiện lại.

### Sửa đúng

Chỉ seed Inspector khi new game. Load game phải replace snapshot, không max-merge, trừ khi mỗi nguồn có ownership rõ.

## 13.5 Durability 0 bị nạp đầy – P0/P1

**Vị trí:** `ItemInventory.cs:740-766`

`durability <= 0` bị xem là chưa khởi tạo và set về max. Item hỏng thật durability=0 có thể tự sửa sau clone/load.

### Sửa đúng

Dùng `runtimeFieldsInitialized` hoặc sentinel `-1`; 0 phải được bảo toàn.

## 13.6 Runtime key theo tên object – P1

**Vị trí:** `ItemInventory.cs:624-632`

Fallback key là `gameObject.name`. Hai NPC/shop cùng tên có thể chia sẻ inventory state. Cần persistent GUID được tạo từ spawn record và save lại.

---

# 14. KỊCH BẢN XUNG ĐỘT ĐIỂN HÌNH

## Kịch bản A – NPC săn rồi đến giờ ngủ nhưng vẫn săn

1. Schedule set FreeHuntAndGather.
2. Smart task giữ goal hunt/gather.
3. Đến slot Sleep, mapping trả None.
4. Request None bị reject, task cũ chưa clear.
5. currentAction/target hunt vẫn còn hoặc display vẫn lấy task cũ.
6. NPC tiếp tục đi săn/hiện goHunt thay vì ngủ.

## Kịch bản B – NPC đang hồi máu bị task thấp hơn giành quyền

1. HP thấp tạo Emergency recovery.
2. Task được phép interrupt.
3. NeedPotion/Trade/Schedule gửi task thấp hơn.
4. Điều kiện `current.canBeInterrupted` cho phép thay thế.
5. NPC bỏ recovery và chuyển hành vi.

## Kịch bản C – NPC nhận task provider nhưng profession vẫn kéo đi

1. Provider disable VillagerAI/SmartNpcAI.
2. Job/fixed profession controller vẫn Update.
3. Provider dịch NPC về task target.
4. Job controller tiếp tục thay target/action hoặc motor.
5. NPC giật, sai action, không đến stage hoặc stuck.

## Kịch bản D – Continue tạo thế giới lai

1. Full save ghi player/camera/favorites.
2. Inventory/world time/shop lưu ở key riêng.
3. Task/NPC/weather/event/monster state không snapshot.
4. Load player tại vị trí cũ và ngày cũ/mới, nhưng NPC/task/world reset khác thời điểm.
5. Có thể lặp sản xuất, mất reservation, hồi sinh monster hoặc reset quan hệ.

## Kịch bản E – Đổi ngôn ngữ giữa một flow

1. `currentAction` đang lưu text tiếng Việt.
2. Language đổi sang English.
3. Code so sánh với `NpcText.Action(key)` trả English.
4. Equality false, flow lock/route/combat cleanup không nhận ra state.
5. NPC có thể không thoát hoặc bị reset sai.

---

# 15. KIẾN TRÚC XỬ LÝ ĐỀ XUẤT

## 15.1 Một state controller duy nhất

```text
NpcStateController
├── CurrentState: NpcExecutionState
├── CurrentOwner: NpcControlOwner
├── Priority
├── EnterTimeWorld
├── TargetId / TargetPosition
├── ResumeState
└── ControlLeaseToken
```

`NpcExecutionState` gợi ý:

```text
Disabled, Dead,
Idle, Sleeping, Eating,
Moving,
Working, Trading, Gathering, Hunting, Cultivating,
DoingMission, Escorting,
Combat, Fleeing, Recovering,
Alchemy, Forging,
ReturningHome,
Conversation
```

## 15.2 Một motor duy nhất

```text
NpcMotor2D
- SubmitIntent(owner, priority, destination, stopDistance, routePolicy)
- CancelIntent(token)
- Chỉ class này ghi Rigidbody2D/transform
```

## 15.3 Schedule chỉ phát intent

Schedule không tự viết action hoặc target. Nó phát `ScheduleIntent(activity, slotId)`. Arbiter quyết định có được chạy hay bị task/combat chặn.

## 15.4 Task/provider dùng lease

Provider acquire lease; lease tự pause lower-priority state, stop motor cũ, giữ resume context. Mọi kết thúc đi qua một cleanup idempotent.

## 15.5 Text chỉ là view

UI đọc typed state và payload để dịch. Không script gameplay nào đọc ngược text để biết state.

---

# 16. THỨ TỰ SỬA TỐI ƯU

## Giai đoạn 1 – Chặn lỗi vận hành ngay

1. Sửa điều kiện priority của Smart task.
2. Clear scheduled task khi mapping ra None.
3. Thay Villager default schedule branch bằng explicit unsupported-state handling.
4. Bổ sung cleanup chung cho interrupted task.
5. Chặn SmartNpcAI + NpcMapMover2D cùng hoạt động.
6. Zero Rigidbody velocity khi provider chiếm quyền.
7. Bootstrap `WorldTimeSystem` trước world subsystem.
8. Sửa restore HP=0 và durability=0.

## Giai đoạn 2 – Hợp nhất quyền điều khiển

1. Tạo `NpcStateController` và `NpcControlLease`.
2. Tạo `NpcMotor2D` duy nhất.
3. Chuyển provider, schedule, combat, profession sang intent/lease.
4. Ngừng dùng `currentAction` trong điều kiện gameplay.

## Giai đoạn 3 – Save snapshot

1. SaveRoot version 2.
2. Persistent ID cho NPC/monster/shop/resource/task.
3. Lưu NPC state, profession progress, task stage, relationship, weather/event, monster respawn.
4. Migration từ PlayerPrefs cũ.

## Giai đoạn 4 – Tách class lớn

Tách `SmartNpcAI`, `VillagerAI`, `NpcTaskProvider` theo state handler sau khi đã có arbiter; không nên tách cơ học trước khi định nghĩa ownership, vì sẽ chỉ phân tán xung đột sang nhiều file hơn.

---

# 17. TEST BẮT BUỘC SAU KHI SỬA

1. Smart NPC HP thấp trong lúc đang hunt; task thấp hơn không được cướp recovery.
2. Chuyển slot Hunt → Sleep/Idle; hunt target/task phải clear đúng.
3. Villager chạy từng enum schedule; không enum nào rơi về nhà ngoài ReturnHome/Sleep.
4. Nhận task khi NPC đang làm nghề; chỉ provider có movement authority.
5. Disable provider giữa escort; reservation, companion và offer đều được giải phóng.
6. Prefab có SmartNpcAI + NpcMapMover2D phải fail validation.
7. Đổi ngôn ngữ giữa combat/task/travel; state không đổi.
8. Save khi HP=0; load vẫn 0 theo thiết kế.
9. Save item durability=0; load vẫn hỏng.
10. Save giữa forge/alchemy/task/monster respawn; load tiếp tục đúng tiến độ.
11. Load game và camera tự rebind player.
12. 30–100 Smart NPC có schedule lệch pha, không dồn cùng điểm.

## Mức độ xác nhận

Các lỗi trên được xác định bằng static code tracing trực tiếp. Chưa có scene, prefab, ScriptableObject asset, package manifest và ProjectSettings nên chưa thể xác nhận prefab nào đang gắn đồng thời các component xung đột hoặc tái hiện runtime bằng Play Mode. Tuy nhiên các nhánh logic sai ưu tiên, thiếu cleanup, unsupported schedule fallback, save HP/durability và bootstrap thiếu clock là lỗi có thể kết luận trực tiếp từ code.
