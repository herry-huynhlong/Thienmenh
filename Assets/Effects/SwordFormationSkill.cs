using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordFormationSkill : MonoBehaviour
{
    public GameObject swordPrefab;

    public int swordCount = 9;
    public float radius = 1.5f;

    public float appearDelay = 0.12f;
    public float riseDistance = 0.8f;
    public float riseTime = 0.25f;

    public float rotateSpeed = 120f;
    public float chargeTime = 0.6f;

    public float attackSpeed = 8f;
    public float destroyAfterAttack = 1.2f;

    private List<Transform> swords = new List<Transform>();
    private bool rotating = false;
    private bool attacking = false;

    void Start()
    {
        StartCoroutine(CastSkill());
    }

    IEnumerator CastSkill()
    {
        rotating = true;

        for (int i = 0; i < swordCount; i++)
        {
            SpawnSword(i);
            yield return new WaitForSeconds(appearDelay);
        }

        yield return new WaitForSeconds(chargeTime);

        rotating = false;

        yield return StartCoroutine(AttackRight());
    }

    void SpawnSword(int index)
    {
        float angle = index * 360f / swordCount;

        Vector3 circlePos =
            transform.position +
            Quaternion.Euler(0, 0, angle) * Vector3.right * radius;

        Vector3 startPos = circlePos + Vector3.down * riseDistance;

        GameObject sword = Instantiate(
            swordPrefab,
            startPos,
            Quaternion.Euler(0, 0, angle)
        );

        swords.Add(sword.transform);

        StartCoroutine(RiseSword(sword.transform, startPos, circlePos));
    }

    IEnumerator RiseSword(Transform sword, Vector3 startPos, Vector3 endPos)
    {
        float t = 0;

        while (t < riseTime)
        {
            t += Time.deltaTime;
            float p = t / riseTime;

            sword.position = Vector3.Lerp(startPos, endPos, p);

            yield return null;
        }

        sword.position = endPos;
    }

    void Update()
    {
        if (rotating)
        {
            for (int i = 0; i < swords.Count; i++)
            {
                if (swords[i] == null) continue;

                swords[i].RotateAround(
                    transform.position,
                    Vector3.forward,
                    rotateSpeed * Time.deltaTime
                );

                Vector3 dir = swords[i].position - transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                swords[i].rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        if (attacking)
        {
            for (int i = 0; i < swords.Count; i++)
            {
                if (swords[i] == null) continue;

                swords[i].position += Vector3.right * attackSpeed * Time.deltaTime;
                swords[i].rotation = Quaternion.Euler(0, 0, 0);
            }
        }
    }

    IEnumerator AttackRight()
    {
        attacking = true;

        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] != null)
            {
                swords[i].rotation = Quaternion.Euler(0, 0, 0);
            }
        }

        yield return new WaitForSeconds(destroyAfterAttack);

        for (int i = 0; i < swords.Count; i++)
        {
            if (swords[i] != null)
            {
                Destroy(swords[i].gameObject);
            }
        }

        attacking = false;
    }
}