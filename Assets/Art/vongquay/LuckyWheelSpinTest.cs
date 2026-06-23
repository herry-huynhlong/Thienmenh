using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LuckyWheelSpinTest : MonoBehaviour
{
    [Header("References")]
    public RectTransform wheelDisk;
    public Button spinButton;
    public WheelBulbBlinkOnly bulbBlink;

    [Header("Spin Settings")]
    public float spinDuration = 4f;
    public int minRounds = 5;
    public int maxRounds = 8;

    bool isSpinning;

    void Start()
    {
        if (spinButton != null)
            spinButton.onClick.AddListener(Spin);
    }

    public void Spin()
    {
        if (isSpinning) return;
        StartCoroutine(SpinRoutine());
    }

    IEnumerator SpinRoutine()
    {
        isSpinning = true;

        if (spinButton != null)
            spinButton.interactable = false;

        if (bulbBlink != null)
            bulbBlink.SetSpinning(true);

        float startZ = wheelDisk.localEulerAngles.z;

        int rounds = Random.Range(minRounds, maxRounds + 1);
        float randomStopAngle = Random.Range(0f, 360f);

        float endZ = startZ - rounds * 360f - randomStopAngle;

        float timer = 0f;

        while (timer < spinDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / spinDuration);

            // Quay nhanh rồi chậm dần
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            float z = Mathf.Lerp(startZ, endZ, eased);
            wheelDisk.localRotation = Quaternion.Euler(0f, 0f, z);

            yield return null;
        }

        wheelDisk.localRotation = Quaternion.Euler(0f, 0f, endZ);

        if (bulbBlink != null)
            bulbBlink.SetSpinning(false);

        if (spinButton != null)
            spinButton.interactable = true;

        isSpinning = false;
    }
}