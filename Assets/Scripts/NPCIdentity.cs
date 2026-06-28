using UnityEngine;

public enum Gender
{
    Male,
    Female
}

public enum LifeStage
{
    Baby,
    Child,
    Youth,
    Middle,
    Old
}

[DisallowMultipleComponent]
public class NPCIdentity : MonoBehaviour
{
    [Header("Identity")]
    public string npcId;
    public string npcName;
    public Gender gender = Gender.Male;
    public int age;
    public LifeStage lifeStage = LifeStage.Youth;
    public NPCVisualProfile visualProfile;
    public string homeId;

    [Header("Family")]
    public string fatherId;
    public string motherId;
    public string spouseId;

    void Awake()
    {
        EnsureNpcId();
    }

    void OnValidate()
    {
        if (!gameObject.scene.IsValid())
        {
            return;
        }

        EnsureNpcId();
    }

    void EnsureNpcId()
    {
        if (!string.IsNullOrWhiteSpace(npcId) &&
            !HasDuplicateNpcId(npcId))
        {
            return;
        }

        npcId = System.Guid.NewGuid().ToString("N");
    }

    bool HasDuplicateNpcId(string candidateId)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return false;
        }

        NPCIdentity[] identities =
            FindObjectsByType<NPCIdentity>(FindObjectsInactive.Include);

        for (int i = 0; i < identities.Length; i++)
        {
            NPCIdentity identity = identities[i];
            if (identity == null || identity == this)
            {
                continue;
            }

            if (string.Equals(
                    identity.npcId,
                    candidateId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
