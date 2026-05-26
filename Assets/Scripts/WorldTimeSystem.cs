using System;
using UnityEngine;

public enum WorldTimePhase
{
    Dawn,       // Bình minh
    Morning,    // Sáng
    Noon,       // Trưa
    Afternoon,  // Chiều
    Evening,    // Tối
    Night       // Đêm
}

public class WorldTimeSystem : MonoBehaviour
{
    public static WorldTimeSystem Instance;

    [Header("Cấu hình Đại Lục")]
    public string continentName = "Hoang Cổ Đại Lục"; 

    [Header("Cấu hình thời gian (Tính bằng giây ngoài đời)")]
    [Tooltip("1 ngày game = 15 phút ngoài đời = 15 * 60 = 900 giây")]
    public float realSecondsPerGameDay = 900f; 

    [Header("Runtime Variables")]
    public int currentYear = 1;
    public int currentMonth = 1;
    public int currentDay = 1;
    public float currentHour = 6f; // Bắt đầu từ 6 giờ sáng cho sáng sủa

    // Quy ước lịch tu tiên: 1 tháng = 30 ngày, 1 năm = 12 tháng
    private const int DAYS_IN_MONTH = 30;
    private const int MONTHS_IN_YEAR = 12;

    // Các sự kiện kích hoạt để các hệ thống khác (UI, AI dân làng) lắng nghe
    public event Action<int> OnHourChanged;
    public event Action<int> OnDayChanged;

    private int lastTriggeredHour = -1;
    private int lastTriggeredDay = -1;

    // Thuộc tính trả về buổi trong ngày (để AI Villager tính toán hành động)
    public WorldTimePhase CurrentPhase => GetCurrentPhase();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        UpdateTime();
    }

    void UpdateTime()
    {
        // Tốc độ tăng trưởng giờ mỗi giây ngoài đời thật = 24 giờ / 900 giây
        float hoursPerSecond = 24f / realSecondsPerGameDay;
        
        // Thời gian trôi liên tục khi người chơi đang chạy game
        currentHour += Time.deltaTime * hoursPerSecond;

        // Xử lý khi hết 1 ngày (Qua 24 giờ)
        if (currentHour >= 24f)
        {
            currentHour -= 24f;
            currentDay++;

            // Xử lý qua tháng (Qua 30 ngày)
            if (currentDay > DAYS_IN_MONTH)
            {
                currentDay = 1;
                currentMonth++;

                // Xử lý qua năm (Qua 12 tháng)
                if (currentMonth > MONTHS_IN_YEAR)
                {
                    currentMonth = 1;
                    currentYear++;
                }
            }
        }

        // Kích hoạt Event báo hiệu khi số Giờ nguyên hoặc số Ngày thay đổi
        int intHour = Mathf.FloorToInt(currentHour);
        if (intHour != lastTriggeredHour)
        {
            lastTriggeredHour = intHour;
            OnHourChanged?.Invoke(lastTriggeredHour);
        }

        if (currentDay != lastTriggeredDay)
        {
            lastTriggeredDay = currentDay;
            OnDayChanged?.Invoke(lastTriggeredDay);
        }
    }

    // Hàm trả về chuỗi text hoàn chỉnh để UI bốc lên màn hình
    public string GetClockText()
    {
        int hour = Mathf.FloorToInt(currentHour);
        int minute = Mathf.FloorToInt((currentHour - hour) * 60f);

        // Định dạng hiển thị: "Hoang Cổ Đại Lục - Năm X Thg Y Ngày Z - HH:MM"
        return $"{continentName} - Năm {currentYear} Thg {currentMonth} Ngày {currentDay} - {hour:00}:{minute:00}";
    }

    // Hàm phân chia buổi dựa vào giờ (phục vụ cho logic của VillagerAI)
    private WorldTimePhase GetCurrentPhase()
    {
        int hour = Mathf.FloorToInt(currentHour);
        if (hour >= 5 && hour < 8) return WorldTimePhase.Dawn;       
        if (hour >= 8 && hour < 11) return WorldTimePhase.Morning;   
        if (hour >= 11 && hour < 13) return WorldTimePhase.Noon;     
        if (hour >= 13 && hour < 17) return WorldTimePhase.Afternoon;
        if (hour >= 17 && hour < 21) return WorldTimePhase.Evening;  
        return WorldTimePhase.Night;                                 
    }
    // Thêm hàm này vào WorldTimeSystem.cs để sửa lỗi dòng 86
   
    // ==========================================
    // ĐOẠN CODE THÊM VÀO ĐỂ SỬA TRIỆT ĐỂ LỖI ĐỎ
    // ==========================================

    // 1. Đường tắt sửa lỗi gọi 'CurrentDay' viết Hoa (Dòng 47)
    public int CurrentDay => currentDay;

    // 2. Đường tắt sửa lỗi gọi 'CurrentHour' viết Hoa (Dòng 48)
    public float CurrentHour => currentHour;

    // 3. Sửa lỗi CS1955 ở dòng 86 (Lỗi "cannot be used like a method")
    // Lý do: Code cũ của bạn gọi IsDangerousNight() như một hàm có dấu ngoặc tròn,
    // nhưng mình lại viết nó dạng thuộc tính biến (Property).
    // Chuyển nó thành hàm chuẩn (Method) như dưới đây sẽ hết lỗi:
    public bool IsDangerousNight()
    {
        int hour = Mathf.FloorToInt(currentHour);
        return hour >= 21 || hour < 5; 
    }
}