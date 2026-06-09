using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThienKiepStrikePrefab : MonoBehaviour
{
    [Header("Mây đen")]
    public GameObject darkCloudRoot;
    public Transform cloudStrikeOrigin;
    public LineRenderer cloudFlashLine;
    public AudioSource cloudRumbleAudio;

    [Header("Hiệu ứng mây hiện dần")]
    public float cloudFadeInTime = 0.8f;
    public float cloudStartScale = 0.65f;
    public float cloudEndScale = 1f;

    [Header("Sấm chớp trong mây")]
    public int cloudFlashSegmentCount = 7;
    public float cloudFlashWidth = 3.8f;
    public float cloudFlashHeight = 0.35f;
    public float cloudFlashJagged = 0.28f;
    public float cloudFlashStartWidth = 0.06f;
    public float cloudFlashEndWidth = 0.025f;

    [Header("Object con")]
    public GameObject warningCircle;
    public LineRenderer lightningLine;
    public ParticleSystem hitEffect;
    public AudioSource thunderAudio;

    [Header("Nổ tia sét khi chạm đất")]
    public bool useHitLightningBurst = true;
    public LineRenderer hitBurstLineTemplate;
    public int hitBurstRayCount = 18;
    public float hitBurstMinLength = 0.35f;
    public float hitBurstMaxLength = 2f;
    public float hitBurstLifeTime = 0.18f;
    public float hitBurstStartWidth = 0.08f;
    public float hitBurstEndWidth = 0.008f;
    public float hitBurstJaggedOffset = 0.15f;

    [Header("Thời gian")]
    public float cloudGatherTime = 0.4f;
    public float cloudFlashInterval = 0.18f;
    public int cloudFlashCount = 5;
    public float warningTime = 0.45f;
    public float lightningLifeTime = 0.16f;
    public float destroyDelay = 1f;

    [Header("Sét chính")]
    public float skyHeight = 7f;
    public float randomX = 1.5f;
    public int segmentCount = 8;
    public float segmentOffset = 0.45f;

    [Header("Damage")]
    public int damage = 80;
    public float damageRadius = 0.9f;
    public LayerMask damageLayers;

    private bool started;
    private Vector3 originalCloudScale = Vector3.one;

    private void Awake()
    {
        HideAllVisualsAtStart();
    }

    private void HideAllVisualsAtStart()
    {
        SetObjectActive(warningCircle, false);

        SetLineActive(cloudFlashLine, false);
        SetLineActive(lightningLine, false);
        SetLineActive(hitBurstLineTemplate, false);

        if (hitEffect != null)
        {
            hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hitEffect.gameObject.SetActive(false);
        }

        if (darkCloudRoot != null)
        {
            originalCloudScale = darkCloudRoot.transform.localScale;
            SetCloudAlpha(0f);
            darkCloudRoot.SetActive(false);
        }
    }

    public void Play(int newDamage, LayerMask newDamageLayers)
    {
        damage = newDamage;
        damageLayers = newDamageLayers;

        if (started)
            return;

        started = true;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        SetObjectActive(darkCloudRoot, true);
        SetObjectActive(warningCircle, false);

        SetLineActive(cloudFlashLine, false);
        SetLineActive(lightningLine, false);
        SetLineActive(hitBurstLineTemplate, false);

        if (hitEffect != null)
        {
            hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hitEffect.gameObject.SetActive(false);
        }

        if (darkCloudRoot != null)
        {
            if (originalCloudScale == Vector3.zero)
            {
                originalCloudScale = Vector3.one;
            }

            darkCloudRoot.transform.localScale = originalCloudScale * cloudStartScale;
            SetCloudAlpha(0f);
        }

        if (cloudRumbleAudio != null)
        {
            cloudRumbleAudio.Play();
        }

        yield return StartCoroutine(FadeCloudIn());

        yield return new WaitForSeconds(cloudGatherTime);

        for (int i = 0; i < cloudFlashCount; i++)
        {
            DrawCloudFlash();

            yield return new WaitForSeconds(cloudFlashInterval);

            SetLineActive(cloudFlashLine, false);

            yield return new WaitForSeconds(cloudFlashInterval);
        }

        SetObjectActive(warningCircle, true);

        yield return new WaitForSeconds(warningTime);

        SetObjectActive(warningCircle, false);

        DrawMainLightning();

        DamageAround();

        if (useHitLightningBurst)
        {
            PlayHitLightningBurst(transform.position);
        }

        if (hitEffect != null)
        {
            hitEffect.gameObject.SetActive(true);
            hitEffect.Play();
        }

        if (thunderAudio != null)
        {
            thunderAudio.Play();
        }

        yield return new WaitForSeconds(lightningLifeTime);

        SetLineActive(lightningLine, false);
        SetLineActive(cloudFlashLine, false);

        Destroy(gameObject, destroyDelay);
    }

    private IEnumerator FadeCloudIn()
    {
        if (darkCloudRoot == null)
            yield break;

        float timer = 0f;

        while (timer < cloudFadeInTime)
        {
            timer += Time.deltaTime;

            float t = timer / cloudFadeInTime;
            t = Mathf.Clamp01(t);

            SetCloudAlpha(t);

            float scaleValue = Mathf.Lerp(cloudStartScale, cloudEndScale, t);
            darkCloudRoot.transform.localScale = originalCloudScale * scaleValue;

            yield return null;
        }

        SetCloudAlpha(1f);
        darkCloudRoot.transform.localScale = originalCloudScale * cloudEndScale;
    }

    private void SetCloudAlpha(float alpha)
    {
        if (darkCloudRoot == null)
            return;

        SpriteRenderer[] spriteRenderers = darkCloudRoot.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    private void DrawCloudFlash()
    {
        if (cloudFlashLine == null)
            return;

        SetLineActive(cloudFlashLine, true);

        if (cloudFlashSegmentCount < 3)
        {
            cloudFlashSegmentCount = 3;
        }

        cloudFlashLine.positionCount = cloudFlashSegmentCount;

        Vector3 center;

        if (cloudStrikeOrigin != null)
        {
            center = cloudStrikeOrigin.position;
        }
        else
        {
            center = transform.position + new Vector3(0f, 3.5f, 0f);
        }

        float startX = -cloudFlashWidth * 0.5f;
        float endX = cloudFlashWidth * 0.5f;

        Vector3 start = center + new Vector3(startX, Random.Range(-cloudFlashHeight, cloudFlashHeight), 0f);
        Vector3 end = center + new Vector3(endX, Random.Range(-cloudFlashHeight, cloudFlashHeight), 0f);

        for (int i = 0; i < cloudFlashLine.positionCount; i++)
        {
            float t = i / (float)(cloudFlashLine.positionCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);

            if (i != 0 && i != cloudFlashLine.positionCount - 1)
            {
                point.x += Random.Range(-cloudFlashJagged, cloudFlashJagged);
                point.y += Random.Range(-cloudFlashJagged, cloudFlashJagged);
            }

            cloudFlashLine.SetPosition(i, point);
        }

        cloudFlashLine.startWidth = cloudFlashStartWidth;
        cloudFlashLine.endWidth = cloudFlashEndWidth;
    }

    private void DrawMainLightning()
    {
        if (lightningLine == null)
            return;

        SetLineActive(lightningLine, true);

        if (segmentCount < 2)
        {
            segmentCount = 2;
        }

        lightningLine.positionCount = segmentCount;

        Vector3 endPosition = transform.position;
        Vector3 startPosition;

        if (cloudStrikeOrigin != null)
        {
            startPosition = cloudStrikeOrigin.position;
        }
        else
        {
            startPosition = endPosition + new Vector3(Random.Range(-randomX, randomX), skyHeight, 0f);
        }

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            Vector3 point = Vector3.Lerp(startPosition, endPosition, t);

            if (i != 0 && i != segmentCount - 1)
            {
                point.x += Random.Range(-segmentOffset, segmentOffset);
                point.y += Random.Range(-segmentOffset * 0.5f, segmentOffset * 0.5f);
            }

            lightningLine.SetPosition(i, point);
        }
    }

    private void PlayHitLightningBurst(Vector3 center)
    {
        if (hitBurstLineTemplate == null)
            return;

        for (int i = 0; i < hitBurstRayCount; i++)
        {
            LineRenderer line = Instantiate(hitBurstLineTemplate, center, Quaternion.identity, transform);
            line.gameObject.SetActive(true);

            float angle = i * Mathf.PI * 2f / hitBurstRayCount;
            angle += Random.Range(-0.45f, 0.45f);

            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float length = Random.Range(hitBurstMinLength, hitBurstMaxLength);

            Vector3 p0 = center;

            Vector3 p1 = center + dir * length * 0.35f;
            p1.x += Random.Range(-hitBurstJaggedOffset, hitBurstJaggedOffset);
            p1.y += Random.Range(-hitBurstJaggedOffset, hitBurstJaggedOffset);

            Vector3 p2 = center + dir * length * 0.7f;
            p2.x += Random.Range(-hitBurstJaggedOffset, hitBurstJaggedOffset);
            p2.y += Random.Range(-hitBurstJaggedOffset, hitBurstJaggedOffset);

            Vector3 p3 = center + dir * length;

            line.positionCount = 4;
            line.SetPosition(0, p0);
            line.SetPosition(1, p1);
            line.SetPosition(2, p2);
            line.SetPosition(3, p3);

            line.startWidth = hitBurstStartWidth;
            line.endWidth = hitBurstEndWidth;

            Destroy(line.gameObject, hitBurstLifeTime);
        }
    }

    private void DamageAround()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius, damageLayers);

        HashSet<GameObject> damagedObjects = new HashSet<GameObject>();

        foreach (Collider2D hit in hits)
        {
            GameObject root = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (damagedObjects.Contains(root))
                continue;

            damagedObjects.Add(root);

            root.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void SetObjectActive(GameObject obj, bool active)
    {
        if (obj != null)
        {
            obj.SetActive(active);
        }
    }

    private void SetLineActive(LineRenderer line, bool active)
    {
        if (line != null)
        {
            line.gameObject.SetActive(active);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, damageRadius);

        if (cloudStrikeOrigin != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(cloudStrikeOrigin.position, transform.position);
            Gizmos.DrawWireSphere(cloudStrikeOrigin.position, 0.15f);
        }
    }
}