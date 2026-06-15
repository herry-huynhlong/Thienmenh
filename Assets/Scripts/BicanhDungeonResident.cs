using UnityEngine;

[DisallowMultipleComponent]
public class BicanhDungeonResident : MonoBehaviour
{
    [Tooltip("Mark this entity as a dungeon-local resident/boss so BicanhSessionManager does not auto-collect it.")]
    public bool excludeFromAutoCollection = true;
}
