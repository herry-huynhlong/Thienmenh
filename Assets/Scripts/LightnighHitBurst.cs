using System.Collections;
using UnityEngine;

public class LightningHitBurst : MonoBehaviour
{
    public LineRenderer lineTemplate;

    public int rayCount = 8;
    public float minLength = 0.25f;
    public float maxLength = 0.65f;
    public float lifeTime = 0.12f;

    public float startWidth = 0.035f;
    public float endWidth = 0.005f;

    public void Play(Vector3 center)
    {
        StartCoroutine(PlayRoutine(center));
    }

    IEnumerator PlayRoutine(Vector3 center)
    {
        for (int i = 0; i < rayCount; i++)
        {
            LineRenderer line = Instantiate(lineTemplate, center, Quaternion.identity, transform);

            float angle = i * Mathf.PI * 2f / rayCount;
            angle += Random.Range(-0.35f, 0.35f);

            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float length = Random.Range(minLength, maxLength);

            Vector3 end = center + dir * length;

            line.positionCount = 3;
            line.SetPosition(0, center);
            line.SetPosition(1, center + dir * length * 0.55f + new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(-0.08f, 0.08f), 0));
            line.SetPosition(2, end);

            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.enabled = true;

            Destroy(line.gameObject, lifeTime);
        }

        yield return new WaitForSeconds(lifeTime);
    }
}