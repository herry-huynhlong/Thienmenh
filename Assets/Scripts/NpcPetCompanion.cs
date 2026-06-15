using UnityEngine;

public class NpcPetCompanion : MonoBehaviour
{
    [Header("Pet")]
    public string petName = "Cho con";
    public bool immuneToNpcAttacks = true;
    public bool immuneToMonsterAttacks = true;
    public bool immuneToSocialDamage = true;
    public bool showInfoOnly = true;

    public static bool HasPetCompanion(GameObject target)
    {
        return target != null &&
            target.GetComponentInParent<NpcPetCompanion>() != null;
    }

    public static bool BlocksNpcAttacks(GameObject target)
    {
        NpcPetCompanion pet = GetPetCompanion(target);
        return pet != null && pet.immuneToNpcAttacks;
    }

    public static bool BlocksMonsterAttacks(GameObject target)
    {
        NpcPetCompanion pet = GetPetCompanion(target);
        return pet != null && pet.immuneToMonsterAttacks;
    }

    public static bool BlocksSocialDamage(GameObject target)
    {
        NpcPetCompanion pet = GetPetCompanion(target);
        return pet != null && pet.immuneToSocialDamage;
    }

    public static string GetDisplayName(GameObject target)
    {
        NpcPetCompanion pet = GetPetCompanion(target);
        if (pet != null && !string.IsNullOrEmpty(pet.petName))
        {
            return pet.petName;
        }

        return "";
    }

    static NpcPetCompanion GetPetCompanion(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        return target.GetComponentInParent<NpcPetCompanion>();
    }
}
