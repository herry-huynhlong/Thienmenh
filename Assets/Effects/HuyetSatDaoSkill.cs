using System.Collections;
using UnityEngine;

public class HuyetSatDaoSkill : MonoBehaviour
{
    public Transform bloodEye;
    public SpriteRenderer bloodEyeRenderer;

    public Transform bloodBlade;
    public SpriteRenderer bloodBladeRenderer;

    public float eyeOpenTime = 0.3f;
    public float bladeSummonTime = 0.35f;
    public float slashTime = 0.18f;
    public float vanishTime = 0.3f;

    public Vector3 eyeMaxScale = new Vector3(3f, 1.6f, 1f);
    public Vector3 bladeMaxScale = new Vector3(3f, 3f, 1f);

    public float bladeUpRotation = 90f;
    public float bladeSlashRotation = -25f;

    void Start()
    {
        Cast();
    }

    public void Cast()
    {
        StopAllCoroutines();
        StartCoroutine(CastRoutine());
    }

    IEnumerator CastRoutine()
    {
        SetAlpha(bloodEyeRenderer, 0f);
        SetAlpha(bloodBladeRenderer, 0f);

        bloodEye.localScale = Vector3.zero;
        bloodBlade.localScale = Vector3.zero;
        bloodBlade.localRotation = Quaternion.Euler(0, 0, bladeUpRotation);

        // 1. Mắt máu mở
        float t = 0;
        while (t < eyeOpenTime)
        {
            t += Time.deltaTime;
            float p = t / eyeOpenTime;

            bloodEye.localScale = Vector3.Lerp(Vector3.zero, eyeMaxScale, EaseOutBack(p));
            SetAlpha(bloodEyeRenderer, Mathf.Lerp(0f, 0.45f, p));

            yield return null;
        }

        // 2. Đao hiện ra, phóng đại
        t = 0;
        while (t < bladeSummonTime)
        {
            t += Time.deltaTime;
            float p = t / bladeSummonTime;

            bloodBlade.localScale = Vector3.Lerp(Vector3.zero, bladeMaxScale, EaseOutBack(p));
            SetAlpha(bloodBladeRenderer, Mathf.Lerp(0f, 1f, p));

            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        // 3. Chém sang phải
        t = 0;
        Quaternion startRot = Quaternion.Euler(0, 0, bladeUpRotation);
        Quaternion endRot = Quaternion.Euler(0, 0, bladeSlashRotation);

        while (t < slashTime)
        {
            t += Time.deltaTime;
            float p = t / slashTime;

            bloodBlade.localRotation = Quaternion.Lerp(startRot, endRot, EaseOut(p));

            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        // 4. Tan biến
        t = 0;
        while (t < vanishTime)
        {
            t += Time.deltaTime;
            float p = t / vanishTime;

            SetAlpha(bloodEyeRenderer, Mathf.Lerp(0.45f, 0f, p));
            SetAlpha(bloodBladeRenderer, Mathf.Lerp(1f, 0f, p));

            yield return null;
        }

        // gameObject.SetActive(false);
    }

    void SetAlpha(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;

        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    float EaseOut(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    float EaseOutBack(float x)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}