using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class mau : MonoBehaviour
{
    [Header("UI")]
    public Slider slider;
    public TMP_Text hpText;

    [Header("Size")]
    public Vector2 barSize = new Vector2(0.8f, 0.12f);
    public bool hideHpText = true;
    public bool forceWorldScale = true;
    public Vector3 worldScale = Vector3.one;

    [Header("Target")]
    public CharacterStats targetStats;
    public MonsterAI targetMonster;

    [Header("Follow")]
    public Vector3 offset = new Vector3(0f, 0.85f, 0f);

    Camera mainCam;

    void Awake()
    {
        ResolveTarget();
    }

    void Start()
    {
        mainCam = Camera.main;
        ConfigureBar();
        UpdateHealth();
    }

    void Update()
    {
        if (targetStats == null && targetMonster == null)
        {
            return;
        }

        UpdateHealth();
        FollowTarget();
    }

    void UpdateHealth()
    {
        if (slider != null)
        {
            int maxHp = GetMaxHp();
            slider.maxValue = Mathf.Max(1, maxHp);
            slider.value = Mathf.Clamp(GetCurrentHp(), 0, maxHp);
        }

        if (hpText != null)
        {
            hpText.gameObject.SetActive(!hideHpText);

            if (!hideHpText)
            {
                hpText.text = GetCurrentHp() + " / " + GetMaxHp();
            }
        }
    }

    void FollowTarget()
    {
        Transform target = GetTargetTransform();
        if (target == null)
        {
            return;
        }

        transform.position = target.position + offset;

        if (mainCam != null)
        {
            transform.rotation = mainCam.transform.rotation;
        }
    }

    void ConfigureBar()
    {
        ResolveReferences();

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = barSize;
        }

        if (slider != null)
        {
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            if (sliderRect != null)
            {
                sliderRect.sizeDelta = barSize;
            }
        }

        if (hpText != null)
        {
            hpText.gameObject.SetActive(!hideHpText);
        }

        if (forceWorldScale)
        {
            transform.localScale = worldScale;
        }
    }

    void ResolveTarget()
    {
        if (targetStats != null || targetMonster != null)
        {
            return;
        }

        targetMonster = GetComponentInParent<MonsterAI>();
        targetStats = GetComponentInParent<CharacterStats>();
    }

    void ResolveReferences()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        if (hpText == null)
        {
            hpText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    int GetCurrentHp()
    {
        if (targetMonster != null)
        {
            return targetMonster.currentHP;
        }

        return targetStats != null ? targetStats.currentHP : 0;
    }

    int GetMaxHp()
    {
        if (targetMonster != null)
        {
            return targetMonster.maxHP;
        }

        return targetStats != null ? targetStats.finalHP : 1;
    }

    Transform GetTargetTransform()
    {
        if (targetMonster != null)
        {
            return targetMonster.transform;
        }

        return targetStats != null ? targetStats.transform : null;
    }
}
