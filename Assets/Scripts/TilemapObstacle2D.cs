using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(TilemapCollider2D))]
public class TilemapObstacle2D : MonoBehaviour
{
    [Header("Collider")]
    public bool useCompositeCollider = true;
    public bool isTrigger = false;

    [Header("Physics")]
    public bool autoStaticRigidbody = true;

    void Reset()
    {
        Configure();
    }

    void Awake()
    {
        Configure();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Configure();
    }
#endif

    [ContextMenu("Configure Obstacle Tilemap")]
    public void Configure()
    {
        TilemapCollider2D tilemapCollider =
            GetComponent<TilemapCollider2D>();

        tilemapCollider.isTrigger = isTrigger;

        if (!useCompositeCollider)
        {
            tilemapCollider.usedByComposite = false;
            return;
        }

        Rigidbody2D rigidbody2d =
            GetComponent<Rigidbody2D>();

        if (rigidbody2d == null &&
            autoStaticRigidbody)
        {
            rigidbody2d =
                gameObject.AddComponent<Rigidbody2D>();
        }

        if (rigidbody2d != null)
        {
            rigidbody2d.bodyType = RigidbodyType2D.Static;
        }

        CompositeCollider2D composite =
            GetComponent<CompositeCollider2D>();

        if (composite == null)
        {
            composite =
                gameObject.AddComponent<CompositeCollider2D>();
        }

        composite.isTrigger = isTrigger;
        composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
        tilemapCollider.usedByComposite = true;
    }
}
