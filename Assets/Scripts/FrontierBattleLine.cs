using UnityEngine;

[DisallowMultipleComponent]
public class FrontierBattleLine : MonoBehaviour
{
    [Header("Identity")]
    public string lineId = "";
    public string displayName = "Ma Thu Son Mach";
    public NpcMapZone battleZone = NpcMapZone.MaThuSonMach;

    [Header("Battlefield")]
    public Transform stagingPoint;
    public Collider2D stagingBounds;
    public Transform battlePoint;
    public Collider2D battleBounds;

    [Header("Wave")]
    [Min(1)] public int defenderCount = 6;
    [Min(1)] public int monsterCount = 8;
    [Min(0f)] public float monsterAggressionBonus = 35f;
    public bool monstersAttackPlayer = false;

    public string GetResolvedId()
    {
        if (!string.IsNullOrWhiteSpace(lineId))
        {
            return lineId.Trim();
        }

        return gameObject.name;
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return GetResolvedId();
    }

    public Vector3 GetBattleCenter()
    {
        if (battlePoint != null)
        {
            return battlePoint.position;
        }

        if (battleBounds != null)
        {
            return battleBounds.bounds.center;
        }

        return transform.position;
    }

    public Vector3 GetStagingCenter()
    {
        if (stagingPoint != null)
        {
            return stagingPoint.position;
        }

        if (stagingBounds != null)
        {
            return stagingBounds.bounds.center;
        }

        return GetBattleCenter();
    }

    public Vector3 GetDefenderPoint(int index)
    {
        return GetDistributedBattlePoint(index);
    }

    public Vector3 GetDistributedBattlePoint(int index)
    {
        return GetDistributedPoint(
            battleBounds,
            GetBattleCenter(),
            index);
    }

    public Vector3 GetStagingPoint(int index)
    {
        return GetDistributedPoint(
            stagingBounds,
            GetStagingCenter(),
            index);
    }

    public bool HasBattleArea =>
        battlePoint != null ||
        battleBounds != null;

    public bool HasStagingArea =>
        stagingPoint != null ||
        stagingBounds != null;

    void OnEnable()
    {
        FrontierDefenseCoordinator.RegisterBattleLine(this);
    }

    void OnDisable()
    {
        FrontierDefenseCoordinator.UnregisterBattleLine(this);
    }

    void OnValidate()
    {
        if (battleBounds == null)
        {
            battleBounds = GetComponent<Collider2D>();
        }

        defenderCount = Mathf.Max(1, defenderCount);
        monsterCount = Mathf.Max(1, monsterCount);
        monsterAggressionBonus = Mathf.Max(0f, monsterAggressionBonus);
    }

    static Vector3 GetDistributedPoint(
        Collider2D boundsCollider,
        Vector3 center,
        int index)
    {
        if (boundsCollider == null)
        {
            return center;
        }

        Bounds bounds = boundsCollider.bounds;
        float angle = index * 137.5f * Mathf.Deg2Rad;
        float radiusFactor =
            0.18f +
            (index % 4) * 0.12f;
        float offsetX =
            Mathf.Cos(angle) *
            bounds.extents.x * radiusFactor;
        float offsetY =
            Mathf.Sin(angle) *
            bounds.extents.y * radiusFactor;
        Vector3 candidate =
            center +
            new Vector3(offsetX, offsetY, 0f);

        Vector2 clamped =
            boundsCollider.ClosestPoint(candidate);
        return new Vector3(clamped.x, clamped.y, center.z);
    }
}
