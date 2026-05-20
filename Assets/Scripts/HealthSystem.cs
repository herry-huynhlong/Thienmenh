using UnityEngine;
using UnityEngine.UI;

public class HealthSystem : MonoBehaviour
{
    public enum Realm
    {
        Mortal,
        QiRefining,
        Foundation
    }

    [Header("Loại")]
    public bool isMonster = false;

    [Header("Cảnh giới")]
    public Realm realm = Realm.Mortal;

    [Header("Máu")]
    public float maxHP;
    public float currentHP;

    [Header("Prefab")]
    public GameObject hpBarPrefab;

    private Image hpFill;

    void Start()
    {
        SetupHP();

        currentHP = maxHP;

        CreateHPBar();
    }

    void SetupHP()
    {
        switch (realm)
        {
            case Realm.Mortal:
                maxHP = isMonster ? 500 : 100;
                break;

            case Realm.QiRefining:
                maxHP = isMonster ? 5000 : 1000;
                break;

            case Realm.Foundation:
                maxHP = isMonster ? 50000 : 10000;
                break;
        }
    }

    void CreateHPBar()
    {
        GameObject hpBar =
            Instantiate(hpBarPrefab,
                        transform);

        hpBar.transform.localPosition =
            new Vector3(0, 2f, 0);

        hpFill =
            hpBar.transform
            .Find("HP_Fill")
            .GetComponent<Image>();

        UpdateHPBar();
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;

        if (currentHP <= 0)
        {
            Die();
        }

        UpdateHPBar();
    }

    void UpdateHPBar()
    {
        hpFill.fillAmount = currentHP / maxHP;
    }

    void Die()
    {
        Destroy(gameObject);
    }
}