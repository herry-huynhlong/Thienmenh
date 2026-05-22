using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class mau : MonoBehaviour
{
    [Header("UI")]
    public Slider slider;

    public TMP_Text hpText;

    [Header("Target")]
    public CharacterStats targetStats;

    [Header("Follow")]
    public Vector3 offset =
        new Vector3(0, 1.5f, 0);

    Camera mainCam;

    void Start()
    {
        mainCam =
            Camera.main;

        // Kiểm tra targetStats trước
        if (targetStats != null)
        {
            AutoResize();
        }
    }

    void Update()
    {
        // Nếu chưa có target
        if (targetStats == null)
        {
            return;
        }

        UpdateHealth();

        FollowTarget();
    }

    void UpdateHealth()
    {
        // Kiểm tra slider
        if (slider == null)
        {
            return;
        }

        slider.maxValue =
            targetStats.finalHP;

        slider.value =
            targetStats.currentHP;

        // Kiểm tra text
        if (hpText != null)
        {
            hpText.text =
                targetStats.currentHP +
                " / " +
                targetStats.finalHP;
        }
    }

    void FollowTarget()
    {
        transform.position =
            targetStats.transform.position +
            offset;

        // Kiểm tra camera
        if (mainCam != null)
        {
            transform.rotation =
                mainCam.transform.rotation;
        }
    }

    void AutoResize()
    {
        // Kiểm tra targetStats
        if (targetStats == null)
        {
            return;
        }

        SpriteRenderer sr =
            targetStats.GetComponent<SpriteRenderer>();

        if (sr == null)
        {
            return;
        }

        RectTransform rect =
            GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        float size =
            sr.bounds.size.x;

        rect.sizeDelta =
            new Vector2(
                size * 100f,
                20f);

        offset.y =
            sr.bounds.size.y + 0.5f;
    }
}