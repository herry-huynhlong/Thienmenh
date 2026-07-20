# Báo cáo kiểm tra lại báo cáo rà soát Unity Assets

**Tài liệu được kiểm tra:** `C:/Users/LONG_IT/Downloads/Bao_cao_ra_soat_Assets_2026-07-20.md`

**Ngày kiểm tra:** 20/07/2026

**Mục tiêu:** xác minh báo cáo gốc đúng hay sai đến đâu, nhận định nào giữ nguyên được, nhận định nào cần sửa câu chữ hoặc sửa số liệu trước khi dùng làm báo cáo chính thức.

## Kết luận nhanh

Báo cáo gốc **có nền tảng kỹ thuật khá tốt**: phần lớn nhận định chính bám đúng mã nguồn, nhất là các vấn đề về save/load, localization, bootstrap singleton, phân mảnh scene loading, xung đột giữa `VillagerAI` và `SmartNpcAI`, và việc nhiều class đang quá lớn.

Tuy nhiên, báo cáo gốc **nên được sửa trước khi gửi chính thức** vì có một số lỗi quan trọng:

1. **Lỗi encoding tiếng Việt**: file Markdown đang bị vỡ dấu khi mở trực tiếp.
2. **Sai phạm vi kiểm tra ở phần mở đầu**: câu mô tả “gói chỉ có `Assets`” không còn đúng nếu áp dụng cho workspace hiện tại.
3. **Một số số liệu đếm chưa chính xác**: số scene recovery, số file partial của một vài hệ thống.
4. **Một số kết luận nói mạnh hơn bằng chứng**: đặc biệt ở mục test/build, nên sửa lại để chính xác hơn về mặt phạm vi.

## Phần 1. Các nhận định đúng và có thể giữ nguyên

### 1. Save lớn đang ghi vào `PlayerPrefs`

Nhận định này **đúng**.

- [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:255) thực hiện `SaveFullGame(...)`.
- Dữ liệu full save được serialize JSON và ghi vào `PlayerPrefs` tại [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:288), [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:293), [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:300).
- Inventory/shop và một số dữ liệu khác cũng dùng `PlayerPrefs` tại [GameSaveSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/GameSaveSystem.cs:381), [GameSaveSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/GameSaveSystem.cs:518).

Đánh giá: đây là nhận định đúng và là một rủi ro kiến trúc đáng ưu tiên.

### 2. Thiếu localization hội thoại cho EN/ZH

Nhận định này **đúng**.

- `NpcDialogueRuntime` tải `NpcDialogueDatabase` tại [NpcDialogueRuntime.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/NpcDialogueRuntime.cs:123), [NpcDialogueRuntime.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/NpcDialogueRuntime.cs:149), [NpcDialogueRuntime.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/NpcDialogueRuntime.cs:160).
- `LocalizationSettings.LoadTextAsset(...)` có fallback ngôn ngữ tại [LocalizationSettings.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/LocalizationSettings.cs:42).
- Trong `Assets/Resources/Localization`, chỉ thấy `vi/NpcDialogueDatabase.json`; không có bản `en` và `zh`.

Đánh giá: giữ nguyên nội dung kết luận này.

### 3. Bootstrap tạo singleton trước khi scene load

Nhận định này **đúng**.

- [WorldSimulationBootstrap.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/WorldSimulationBootstrap.cs:5) dùng `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`.
- [WorldSimulationBootstrap.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/WorldSimulationBootstrap.cs:8) gọi `WorldTimeSystem.EnsureInstance()`.
- [WorldTimeSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/WorldTimeSystem.cs:56) cũng có `EnsureInstance()`.
- [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:162) cũng bootstrap theo cách tương tự.

Đánh giá: giữ nguyên, vì đây là mô tả đúng cấu trúc hiện tại.

### 4. Có nhiều đường load scene trực tiếp ngoài một flow chung

Nhận định này **đúng**.

- [MainMenuManager.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/MainMenuManager.cs:132)
- [NewGameIntroPanel.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/NewGameIntroPanel.cs:587)
- [OpenMap.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/OpenMap.cs:30)
- [CloseMap.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/CloseMap.cs:54)
- [DoorTeleport.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/DoorTeleport.cs:101)
- [BottomMenuButtonRouter.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/BottomMenuButtonRouter.cs:456)

Đánh giá: giữ nguyên.

### 5. `CurrentWorldHour` ép từ `double` sang `float`

Nhận định này **đúng**.

- [WorldTimeSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/WorldTimeSystem.cs:52) có `CurrentWorldHourExact`.
- [WorldTimeSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/WorldTimeSystem.cs:54) có `CurrentWorldHour => (float)CurrentWorldHourExact`.

Đánh giá: giữ nguyên.

### 6. Lịch đang hard-code 30 ngày/tháng, 12 tháng/năm

Nhận định này **đúng**.

- [WorldTimeSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/WorldTimeSystem.cs:19)
- [WorldTimeSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/WorldTimeSystem.cs:20)

Đánh giá: giữ nguyên, nhưng nên diễn đạt đây là “thiết kế game hiện tại” thay vì “lỗi”.

### 7. Cơ chế Villager brain vô hiệu hóa Smart brain là có thật

Nhận định này **đúng**.

- `EnforceVillagerPrimaryBrain()` nằm ở [SmartNpcAI.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/SmartNpcAI.cs:409).
- Các điểm chặn Smart AI khi Villager brain đang active nằm ở [SmartNpcAI.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/SmartNpcAI.cs:761), [SmartNpcAI.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/SmartNpcAI.cs:1004), [SmartNpcAI.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/SmartNpcAI.cs:1064), [SmartNpcAI.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/SmartNpcAI.cs:1072).
- Báo cáo gốc mô tả đây là cơ chế chống xung đột runtime chứ chưa phải validation prefab; nhận định này hợp lý.

Đánh giá: giữ nguyên.

### 8. Save bị chặn khi đang có thiên kiếp

Nhận định này **đúng**.

- [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:257) kiểm tra `HeavenlyTribulationSystem.HasActiveTribulation`.

Đánh giá: giữ nguyên, vì đây là hành vi có thật trong code.

### 9. `BicanhSessionManager` có cơ chế save/restore riêng

Nhận định này **đúng**.

- [BicanhSessionManager.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/BicanhSessionManager.cs:294) có `CaptureSaveData()`.
- [BicanhSessionManager.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/BicanhSessionManager.cs:327) có `RestoreFromSaveData(...)`.
- `FullGameSaveController` có gọi chúng tại [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:605) và [FullGameSaveController.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/FullGameSaveController.cs:631).

Đánh giá: giữ nguyên.

### 10. Có nhiều class rất lớn

Nhận định này **đúng**.

Kích thước file hiện tại:

- [TouchSelectTarget.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/TouchSelectTarget.cs:1): 88,864 byte
- [NpcSocialSystem.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/NpcSocialSystem.cs:1): 83,228 byte
- [InventoryPanelUI.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/InventoryPanelUI.cs:1): 82,344 byte
- [ShopPanelUI.cs](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Scripts/ShopPanelUI.cs:1): 70,888 byte

Đánh giá: giữ nguyên.

### 11. `Assets/Scripts` có khoảng 90 vòng `Update/FixedUpdate/LateUpdate`

Nhận định này **đúng nếu phạm vi là `Assets/Scripts`**.

- Đếm trong toàn `Assets`: **111**
- Đếm trong `Assets/Scripts`: **90**

Đánh giá: mục này trong báo cáo gốc là hợp lệ, nhưng nên ghi rõ phạm vi là `Assets/Scripts`, không phải toàn bộ `Assets`.

## Phần 2. Các nhận định cần sửa hoặc viết lại

### 1. Phần mở đầu nói “gói chỉ có Assets”

Nhận định này **chỉ đúng một phần**.

Nếu báo cáo đang tự giới hạn đúng theo file `Assets.zip` thì câu này chấp nhận được. Nhưng nếu người đọc hiểu là đang rà soát toàn bộ project hiện tại thì câu đó **không còn đúng**.

Workspace hiện tại có:

- [Packages/manifest.json](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Packages/manifest.json:1)
- [ProjectSettings/ProjectVersion.txt](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/ProjectSettings/ProjectVersion.txt:1)
- [ProjectSettings/EditorBuildSettings.asset](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/ProjectSettings/EditorBuildSettings.asset:1)
- thư mục `Library/`
- thư mục `Logs/`

**Câu nên sửa:**

> Phạm vi đối chiếu ban đầu của báo cáo gốc là `Assets.zip`, tức chủ yếu ở thư mục `Assets`. Tuy nhiên tại thời điểm kiểm tra lại ngày 20/07/2026, workspace hiện tại đã có thêm `Packages/`, `ProjectSettings/`, `Library/` và `Logs/`, nên một số giới hạn trong báo cáo gốc không còn áp dụng cho toàn bộ project.

### 2. Mục scene recovery ghi 61/68 scene

Nhận định này **sai số**.

Kết quả kiểm tra:

- Tổng scene trong `Assets`: **68**
- Scene trong `Assets/_Recovery`: **60**
- Có thêm **1** file corrupt recovery riêng: [Lang_corrupt_before_restore_20260528_232353.unity](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/_Recovery/Lang_corrupt_before_restore_20260528_232353.unity:1)

**Câu nên sửa:**

> Có 60/68 scene nằm trong `Assets/_Recovery`, ngoài ra còn 1 file recovery corrupt riêng. Đây là dấu hiệu project đang giữ nhiều bản hồi phục trong cây source chính.

### 3. Mục “không có test Unity Test Framework thực thụ trong gói”

Nhận định này **đúng một phần nhưng cần viết chặt hơn**.

Những gì mình xác minh được:

- Không tìm thấy source test có `[Test]` hoặc `[UnityTest]` trong `Assets`.
- Có package test framework trong [Packages/manifest.json](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Packages/manifest.json:1).
- Có kết quả chạy test và log batch trong repo, ví dụ `PlayModeTestResults.xml`, `BatchPlayMode.log`, `CodexPlayModeTestSummary.txt`.

Vì vậy, câu “không có test thực thụ” dễ bị hiểu là project chưa từng có test. Chính xác hơn nên viết là:

> Trong phần `Assets` được rà soát, chưa thấy source test NUnit/`[UnityTest]` rõ ràng. Project hiện có package test framework và dấu vết chạy PlayMode test/batch runner, nhưng mức độ bao phủ test source cần được xác minh thêm trong môi trường Unity đầy đủ.

### 4. Mục “không thể xác nhận build Android từ ZIP này”

Nhận định này **không còn đúng nếu áp dụng cho workspace hiện tại**.

Hiện tại đã có:

- Unity version tại [ProjectVersion.txt](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/ProjectSettings/ProjectVersion.txt:1)
- Build Settings tại [EditorBuildSettings.asset](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/ProjectSettings/EditorBuildSettings.asset:1)
- Package manifest tại [Packages/manifest.json](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Packages/manifest.json:1)
- Profile Android tại [Android™.asset](/C:/Users/LONG_IT/Documents/GitHub/Thienmenh/Assets/Settings/Build%20Profiles/Android%E2%84%A2.asset:1)

Tuy nhiên, **báo cáo gốc chỉ đúng nếu đang nói về riêng `Assets.zip`**.

**Câu nên sửa:**

> Nếu chỉ xét gói `Assets.zip` tách rời thì chưa thể kết luận build Android thành công. Với workspace hiện tại đã có `Packages` và `ProjectSettings`, có thể tiếp tục xác minh build bằng cách mở Unity và chạy compile/build thật.

### 5. Một số số lượng partial file không còn khớp

Nhận định “được tách partial nhiều file” là **đúng**, nhưng một số con số trong báo cáo gốc cần cập nhật:

- `SmartNpcAI`: **23** file
- `VillagerAI`: **22** file
- `NpcTaskProvider`: **14** file
- `BicanhSessionManager`: **4** file
- `MonsterAI`: **9** file

Nếu báo cáo muốn giữ phong cách định lượng thì nên sửa theo số hiện tại.

## Phần 3. Các nhận định đúng nhưng cần hạ mức chắc chắn

### 1. “Inventory load có thể từ chối toàn bộ inventory chỉ vì một item ID không resolve”

Nhận định này có vẻ **hợp lý**, nhưng trong lần kiểm tra này mình chưa audit sâu từng nhánh logic load inventory như một code review độc lập. Báo cáo có thể giữ, nhưng nên ghi là:

> Đây là rủi ro logic có dấu hiệu hiện hữu trong luồng load inventory và nên được xác minh thêm bằng test mất item ID.

### 2. “Runtime NPC có thể mất sau load”

Nhận định này cũng là **cảnh báo hợp lý**, nhưng nên diễn đạt theo hướng rủi ro thay vì khẳng định chắc chắn:

> Luồng restore NPC runtime phụ thuộc vào prefab resolution ở runtime; nếu không resolve được thì có nguy cơ khôi phục thiếu NPC.

### 3. “Pathfinding/navigation sẽ gây spike khi nhiều NPC”

Nhận định này **hợp lý về mặt kiến trúc**, nhưng chưa phải kết luận hiệu năng đã được chứng minh. Cách viết tốt nhất là:

> Đây là điểm nóng hiệu năng tiềm năng và cần xác nhận bằng profiler khi tăng population.

## Phần 4. Những điểm báo cáo gốc chưa nhấn đủ

### 1. File gốc đang lỗi encoding

Đây là vấn đề thực tế, ảnh hưởng trực tiếp đến khả năng sử dụng báo cáo.

Khuyến nghị:

- Chuẩn hóa file báo cáo sang UTF-8.
- Nếu cần gửi nội bộ, dùng tên file mới thay vì sửa đè lên bản bị vỡ dấu.

### 2. Workspace hiện tại đã có thêm ngữ cảnh build

So với giả định trong báo cáo gốc, project hiện tại đã có nhiều dữ liệu hơn để kiểm tra tiếp:

- phiên bản Unity
- build settings
- package dependencies
- log và kết quả test/batch

Điều này có nghĩa là vòng rà soát tiếp theo có thể đi xa hơn báo cáo gốc.

## Phần 5. Đề xuất bản kết luận thay thế

Đây là đoạn kết luận mình đề xuất dùng thay cho phần kết luận nhanh trong báo cáo gốc:

> Dự án có nhiều hệ thống gameplay và simulation đã được triển khai thực chất, đặc biệt ở các mảng save/load, world time-weather, AI dân làng/NPC, chiến đấu, bí cảnh, UI và localization. Các vấn đề đáng ưu tiên nhất hiện tại là kiến trúc save dựa nhiều vào `PlayerPrefs`, thiếu dữ liệu hội thoại cho EN/ZH, cơ chế bootstrap singleton còn phân tán, và nhiều lớp lớn đang ôm quá nhiều trách nhiệm.
>
> Báo cáo gốc nhìn chung phản ánh đúng các rủi ro chính trong mã nguồn. Tuy vậy, trước khi phát hành chính thức cần sửa lại các lỗi encoding, chuẩn hóa phạm vi “kiểm tra trên `Assets.zip`” so với “kiểm tra trên toàn workspace”, và cập nhật lại một số số liệu đếm như scene recovery và số file partial.

## Phần 6. Kết luận cuối cùng

**Phán định tổng thể:** báo cáo gốc **đúng khoảng 80–90% về hướng nhận định kỹ thuật**, nhưng **không nên gửi nguyên văn**.

Nên sửa trước khi dùng chính thức vì:

1. Có lỗi encoding làm giảm độ tin cậy tài liệu.
2. Có vài số liệu đếm sai hoặc cũ.
3. Có vài câu mô tả vượt quá phạm vi bằng chứng hiện tại.
4. Workspace hiện tại đã có thêm dữ liệu mà báo cáo gốc chưa phản ánh.

**Khuyến nghị sử dụng:** dùng báo cáo gốc làm nền, nhưng thay bằng bản đã hiệu chỉnh về encoding, phạm vi và số liệu trước khi gửi cho người khác.
