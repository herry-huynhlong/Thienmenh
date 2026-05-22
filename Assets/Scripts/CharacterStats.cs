using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Máu")]
    public int finalHP = 1000;

    public int currentHP = 1000;

    [Header("UI")]
    public GameObject statusBarPrefab;

    void Start()
    {
        if (statusBarPrefab != null)
        {
            GameObject bar =
                Instantiate(
                    statusBarPrefab,
                    transform);

            mau ui =
                bar.GetComponent<mau>();

            ui.targetStats = this;
        }
    }
}