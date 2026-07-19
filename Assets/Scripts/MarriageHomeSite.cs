using UnityEngine;

[DisallowMultipleComponent]
public class MarriageHomeSite : MonoBehaviour
{
    [Header("Identity")]
    public string siteId = "";
    public string displayName = "";
    public int priority;

    [Header("Placement")]
    public Transform homePoint;
    public Transform buildPoint;

    [Header("Visual")]
    public GameObject housePrefab;
    public Vector3 houseSpawnOffset;

    public string GetResolvedSiteId()
    {
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            return siteId.Trim();
        }

        return gameObject.name;
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return GetResolvedSiteId();
    }

    public Transform GetResolvedHomePoint()
    {
        return homePoint != null
            ? homePoint
            : transform;
    }

    public Vector3 GetResolvedBuildPosition()
    {
        Transform anchor =
            buildPoint != null
                ? buildPoint
                : transform;
        return anchor.position + houseSpawnOffset;
    }

    void OnEnable()
    {
        MarriageHomeManager.RegisterSite(this);
    }

    void OnDisable()
    {
        MarriageHomeManager.UnregisterSite(this);
    }
}
