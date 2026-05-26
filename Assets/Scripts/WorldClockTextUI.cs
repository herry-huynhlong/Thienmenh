using TMPro;
using UnityEngine;

public class WorldClockTextUI : MonoBehaviour
{
    public TMP_Text clockText;
    public string prefix = "";

    void Awake()
    {
        if (clockText == null)
        {
            clockText = GetComponent<TMP_Text>();
        }
    }

    void OnEnable()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            timeSystem.OnHourChanged += Refresh;
            timeSystem.OnDayChanged += RefreshDay;
        }

        Refresh(0);
    }

    void OnDisable()
    {
        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem != null)
        {
            timeSystem.OnHourChanged -= Refresh;
            timeSystem.OnDayChanged -= RefreshDay;
        }
    }

    // Cập nhật liên tục mỗi khung hình để số Phút nhảy mượt mà theo thời gian thực
    void Update()
    {
        Refresh(0);
    }

    void RefreshDay(int day)
    {
        Refresh(0);
    }

    void Refresh(int hour)
    {
        if (clockText == null)
        {
            return;
        }

        WorldTimeSystem timeSystem = WorldTimeSystem.Instance;
        if (timeSystem == null)
        {
            // Chuỗi ký tự mặc định khi hệ thống chưa load xong
            clockText.text = prefix + "Hoang Cổ Đại Lục - Năm 1 Thg 1 Ngày 1 - 06:00";
            return;
        }

        // Gọi hàm bốc chuỗi văn bản hoàn chỉnh từ hệ thống lõi thời gian
        clockText.text = prefix + timeSystem.GetClockText();
    }
}