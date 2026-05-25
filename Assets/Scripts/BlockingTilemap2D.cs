using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(TilemapCollider2D))]
public class BlockingTilemap2D : MonoBehaviour
{
    [Header("Collider")]
    public bool useCompositeCollider = true;
    public bool isTrigger = false;

    [Header("Physics")]
    public bool addStaticRigidbody = true;
    public string blockingLayerName = "Obstacle";

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

    [ContextMenu("Configure Blocking Tilemap")]
    public void Configure()
    {
        TilemapCollider2D tilemapCollider =
            GetComponent<TilemapCollider2D>();

        tilemapCollider.isTrigger = isTrigger;

        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (addStaticRigidbody && rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.gravityScale = 0f;
        }

        if (useCompositeCollider)
        {
            CompositeCollider2D composite =
                GetComponent<CompositeCollider2D>();

            if (composite == null)
            {
                composite = gameObject.AddComponent<CompositeCollider2D>();
            }

            composite.isTrigger = isTrigger;
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            tilemapCollider.compositeOperation =
                Collider2D.CompositeOperation.Merge;
        }
        else
        {
            tilemapCollider.compositeOperation =
                Collider2D.CompositeOperation.None;
        }

        int layer = LayerMask.NameToLayer(blockingLayerName);
        if (layer >= 0)
        {
            gameObject.layer = layer;
        }
    }
}
