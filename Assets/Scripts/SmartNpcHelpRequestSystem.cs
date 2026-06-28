using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class SmartNpcHelpRequestSystem : MonoBehaviour
{
    [System.Serializable]
    public class HelpRequest
    {
        public GameObject requester;
        public GameObject monster;
        public Vector3 position;
        public float createdTime;
        public float expireTime;
        public float minPowerNeeded;
        public List<GameObject> acceptedHelpers = new List<GameObject>();
        public int maxHelpers = 1;
        public string purpose = "Combat";

        public bool IsExpired => Time.time >= expireTime;
    }

    static SmartNpcHelpRequestSystem instance;
    static readonly List<HelpRequest> requests = new List<HelpRequest>();
    static readonly Dictionary<int, float> nextRequestTimeByRequester =
        new Dictionary<int, float>();

    [Header("Help Request")]
    public bool debugHelpLog;
    public float requestLifetime = 15f;
    public float requestCooldownMin = 10f;
    public float requestCooldownMax = 20f;
    public float nearbyRequestRadius = 18f;

    public static SmartNpcHelpRequestSystem Instance => EnsureInstance();

    void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    static SmartNpcHelpRequestSystem EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<SmartNpcHelpRequestSystem>();
        if (instance != null)
        {
            return instance;
        }

        GameObject systemObject =
            new GameObject(nameof(SmartNpcHelpRequestSystem));
        instance = systemObject.AddComponent<SmartNpcHelpRequestSystem>();
        return instance;
    }

    public void RequestHelp(
        GameObject requester,
        GameObject monster,
        float dangerScore)
    {
        if (requester == null ||
            monster == null)
        {
            return;
        }

        SmartNpcAI requesterNpc = requester.GetComponent<SmartNpcAI>();
        if (requesterNpc == null ||
            !requesterNpc.enabled ||
            !requesterNpc.gameObject.activeInHierarchy)
        {
            return;
        }

        MonsterAI monsterAi = monster.GetComponent<MonsterAI>();
        if (monsterAi == null ||
            monsterAi.IsDead)
        {
            return;
        }

        if (!NpcAreaUtility.IsSameArea(requester, monster))
        {
            return;
        }

        int requesterKey = GetRequesterKey(requester);
        if (nextRequestTimeByRequester.TryGetValue(requesterKey, out float nextTime) &&
            Time.time < nextTime)
        {
            return;
        }

        nextRequestTimeByRequester[requesterKey] =
            Time.time +
            Mathf.Lerp(
                requestCooldownMin,
                requestCooldownMax,
                Mathf.Clamp01(dangerScore * 0.5f));

        HelpRequest request = FindExistingRequest(requester, monster);
        if (request == null)
        {
            request = new HelpRequest();
            requests.Add(request);
        }

        request.requester = requester;
        request.monster = monster;
        request.position = monster.transform.position;
        request.createdTime = Time.time;
        request.expireTime = Time.time + Mathf.Max(5f, requestLifetime);
        request.minPowerNeeded =
            Mathf.Max(1f, CombatPowerUtility.GetPower(monster) * 0.75f);
        request.maxHelpers = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Max(1f, dangerScore)),
            1,
            3);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugHelpLog)
        {
            Debug.Log(
                "[SmartNpcHelp] " + requester.name +
                " gặp yêu thú mạnh, phát tín hiệu cầu cứu.");
        }
#endif
    }

    public bool TryAcceptHelp(
        GameObject helper,
        HelpRequest request)
    {
        if (helper == null ||
            request == null ||
            request.IsExpired ||
            request.requester == null ||
            request.monster == null)
        {
            return false;
        }

        SmartNpcAI helperNpc = helper.GetComponent<SmartNpcAI>();
        if (helperNpc == null ||
            !helperNpc.enabled ||
            !helperNpc.gameObject.activeInHierarchy ||
            helperNpc.IsDead)
        {
            return false;
        }

        if (!NpcAreaUtility.IsSameArea(helper, request.requester) ||
            !NpcAreaUtility.IsSameArea(helper, request.monster))
        {
            return false;
        }

        if (request.acceptedHelpers.Contains(helper))
        {
            return true;
        }

        if (request.acceptedHelpers.Count >= request.maxHelpers)
        {
            return false;
        }

        float helperPower = CombatPowerUtility.GetPower(helper);
        if (helperPower < request.minPowerNeeded * 0.75f)
        {
            return false;
        }

        request.acceptedHelpers.Add(helper);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugHelpLog)
        {
            Debug.Log(
                "[SmartNpcHelp] " + helper.name +
                " đáp lại lời cầu cứu của " +
                request.requester.name + ".");
        }
#endif
        return true;
    }

    public List<HelpRequest> GetRequestsNear(
        GameObject npc,
        float radius)
    {
        List<HelpRequest> result = new List<HelpRequest>();
        if (npc == null)
        {
            return result;
        }

        CleanupExpiredRequests();

        Vector3 position = npc.transform.position;
        float maxRadius = Mathf.Max(0.5f, radius > 0f ? radius : nearbyRequestRadius);

        foreach (HelpRequest request in requests)
        {
            if (request == null ||
                request.IsExpired ||
                request.requester == null ||
                request.monster == null)
            {
                continue;
            }

            if (!NpcAreaUtility.IsSameArea(npc, request.requester) ||
                !NpcAreaUtility.IsSameArea(npc, request.monster))
            {
                continue;
            }

            if (Vector2.Distance(position, request.position) > maxRadius)
            {
                continue;
            }

            result.Add(request);
        }

        return result;
    }

    public HelpRequest GetBestRequestNear(GameObject npc, float radius)
    {
        List<HelpRequest> candidates = GetRequestsNear(npc, radius);
        if (candidates.Count == 0)
        {
            return null;
        }

        Vector3 position = npc.transform.position;
        HelpRequest best = null;
        float bestScore = float.NegativeInfinity;

        foreach (HelpRequest request in candidates)
        {
            float distance = Vector2.Distance(position, request.position);
            float score = request.minPowerNeeded - distance;
            if (score > bestScore)
            {
                bestScore = score;
                best = request;
            }
        }

        return best;
    }

    public void CleanupExpiredRequests()
    {
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            HelpRequest request = requests[i];
            if (request == null ||
                request.requester == null ||
                request.monster == null ||
                request.IsExpired ||
                !request.requester.activeInHierarchy ||
                !request.monster.activeInHierarchy)
            {
                requests.RemoveAt(i);
            }
        }
    }

    HelpRequest FindExistingRequest(
        GameObject requester,
        GameObject monster)
    {
        foreach (HelpRequest request in requests)
        {
            if (request != null &&
                request.requester == requester &&
                request.monster == monster &&
                !request.IsExpired)
            {
                return request;
            }
        }

        return null;
    }

    static int GetRequesterKey(GameObject requester)
    {
        return requester != null
            ? RuntimeHelpers.GetHashCode(requester)
            : 0;
    }
}
