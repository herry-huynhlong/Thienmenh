using UnityEngine;

public class TestThienKiep : MonoBehaviour
{
    public ThienKiepStrikePrefab strikePrefab;
    public Transform target;

    public int testDamage = 80;
    public LayerMask damageLayers;

    public bool testAtMousePosition = true;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestStrike();
        }
    }

    void TestStrike()
    {
        if (strikePrefab == null)
        {
            Debug.LogWarning("Chưa kéo PF_ThienKiepStrike vào ô Strike Prefab.");
            return;
        }

        Vector3 spawnPosition;

        if (testAtMousePosition)
        {
            spawnPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            spawnPosition.z = 0f;
        }
        else
        {
            if (target == null)
            {
                Debug.LogWarning("Chưa kéo NPC vào ô Target.");
                return;
            }

            spawnPosition = target.position;
        }

        ThienKiepStrikePrefab strike = Instantiate(
            strikePrefab,
            spawnPosition,
            Quaternion.identity
        );

        strike.Play(testDamage, damageLayers);
    }
}