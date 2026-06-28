using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class TargetReservationSystem : MonoBehaviour
{
    [System.Serializable]
    class TargetReservation
    {
        public GameObject target;
        public GameObject owner;
        public float expireTime;
        public string purpose;
    }

    static TargetReservationSystem instance;
    static readonly Dictionary<int, TargetReservation> reservations =
        new Dictionary<int, TargetReservation>();

    [Header("Debug")]
    public bool debugReservationLog;
    public float autoCleanupInterval = 2f;

    float nextCleanupTime;

    public static TargetReservationSystem Instance => EnsureInstance();

    public static TargetReservationSystem TryGetExistingInstance()
    {
        return instance != null
            ? instance
            : FindAnyObjectByType<TargetReservationSystem>();
    }

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

    void Update()
    {
        if (Time.time < nextCleanupTime)
        {
            return;
        }

        nextCleanupTime = Time.time + Mathf.Max(0.5f, autoCleanupInterval);
        CleanupInvalidReservations();
    }

    static TargetReservationSystem EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<TargetReservationSystem>();
        if (instance != null)
        {
            return instance;
        }

        GameObject systemObject =
            new GameObject(nameof(TargetReservationSystem));
        instance = systemObject.AddComponent<TargetReservationSystem>();
        return instance;
    }

    public bool TryReserve(
        GameObject target,
        GameObject owner,
        float duration,
        string purpose)
    {
        if (target == null || owner == null)
        {
            return false;
        }

        int key = GetTargetKey(target);
        CleanupReservation(key);
        float expireTime = Time.time + Mathf.Max(0.25f, duration);

        if (reservations.TryGetValue(key, out TargetReservation existing))
        {
            if (IsReservationInvalid(existing))
            {
                reservations.Remove(key);
            }
            else if (existing.owner == owner)
            {
                existing.expireTime = expireTime;
                existing.purpose = purpose;
                LogReservation("refresh", target, owner, purpose);
                return true;
            }
            else if (existing.expireTime > Time.time)
            {
                return false;
            }
            else
            {
                reservations.Remove(key);
            }
        }

        reservations[key] =
            new TargetReservation
            {
                target = target,
                owner = owner,
                expireTime = expireTime,
                purpose = purpose
            };
        LogReservation("reserve", target, owner, purpose);
        return true;
    }

    public bool IsReservedByOther(GameObject target, GameObject owner)
    {
        if (target == null)
        {
            return false;
        }

        TargetReservation reservation = GetValidReservation(target);
        if (reservation == null)
        {
            return false;
        }

        return reservation.owner != null &&
            reservation.owner != owner &&
            reservation.owner.activeInHierarchy;
    }

    public bool IsReservedByOwner(GameObject target, GameObject owner)
    {
        if (target == null || owner == null)
        {
            return false;
        }

        TargetReservation reservation = GetValidReservation(target);
        return reservation != null &&
            reservation.owner == owner &&
            reservation.owner.activeInHierarchy;
    }

    public void Release(GameObject target, GameObject owner)
    {
        if (target == null)
        {
            return;
        }

        int key = GetTargetKey(target);
        if (!reservations.TryGetValue(key, out TargetReservation reservation))
        {
            return;
        }

        if (reservation == null ||
            IsReservationInvalid(reservation) ||
            owner == null ||
            reservation.owner == owner)
        {
            reservations.Remove(key);
            LogReservation("release", target, owner, reservation != null
                ? reservation.purpose
                : "");
        }
    }

    public void ReleaseAllByOwner(GameObject owner)
    {
        if (owner == null)
        {
            return;
        }

        List<int> keys = new List<int>();
        foreach (KeyValuePair<int, TargetReservation> pair in reservations)
        {
            if (pair.Value != null &&
                pair.Value.owner == owner)
            {
                keys.Add(pair.Key);
            }
        }

        foreach (int key in keys)
        {
            reservations.Remove(key);
        }
    }

    public void CleanupInvalidReservations()
    {
        List<int> keys = new List<int>();
        foreach (KeyValuePair<int, TargetReservation> pair in reservations)
        {
            if (IsReservationInvalid(pair.Value))
            {
                keys.Add(pair.Key);
            }
        }

        foreach (int key in keys)
        {
            reservations.Remove(key);
        }
    }

    void CleanupReservation(int key)
    {
        if (!reservations.TryGetValue(key, out TargetReservation reservation))
        {
            return;
        }

        if (IsReservationInvalid(reservation))
        {
            reservations.Remove(key);
        }
    }

    TargetReservation GetValidReservation(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        int key = GetTargetKey(target);
        if (!reservations.TryGetValue(key, out TargetReservation reservation))
        {
            return null;
        }

        if (IsReservationInvalid(reservation))
        {
            reservations.Remove(key);
            return null;
        }

        if (reservation.expireTime <= Time.time)
        {
            reservations.Remove(key);
            return null;
        }

        return reservation;
    }

    bool IsReservationInvalid(TargetReservation reservation)
    {
        return reservation == null ||
            reservation.target == null ||
            reservation.owner == null ||
            !reservation.target.activeInHierarchy ||
            !reservation.owner.activeInHierarchy ||
            reservation.expireTime <= Time.time;
    }

    void LogReservation(
        string verb,
        GameObject target,
        GameObject owner,
        string purpose)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugReservationLog ||
            target == null ||
            owner == null)
        {
            return;
        }

        Debug.Log(
            "[TargetReservation] " + verb +
            " target=" + target.name +
            " owner=" + owner.name +
            " purpose=" + purpose);
#endif
    }

    static int GetTargetKey(Object target)
    {
        return target != null ? RuntimeHelpers.GetHashCode(target) : 0;
    }
}
