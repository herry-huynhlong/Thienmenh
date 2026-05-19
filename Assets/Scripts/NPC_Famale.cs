using UnityEngine;

public class NPC_Famale : MonoBehaviour
{
    public float speed = 3f;

    public AnimationClip walkUp;
    public AnimationClip walkDown;
    public AnimationClip walkSide;

    public AnimationClip idleUp;
    public AnimationClip idleDown;
    public AnimationClip idleSide;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sr;

    private Vector2 move;

    private string currentAnim = "";

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        move.x = Input.GetAxisRaw("Horizontal");
        move.y = Input.GetAxisRaw("Vertical");

        move.Normalize();

        // RIGHT
        if (move.x > 0)
        {
            ChangeAnim(walkSide.name);
            sr.flipX = true;
        }

        // LEFT
        else if (move.x < 0)
        {
            ChangeAnim(walkSide.name);
            sr.flipX = false;
        }

        // UP
        else if (move.y > 0)
        {
            ChangeAnim(walkUp.name);
        }

        // DOWN
        else if (move.y < 0)
        {
            ChangeAnim(walkDown.name);
        }

        // IDLE
        else
        {
            ChangeAnim(idleDown.name);
        }
    }

    void FixedUpdate()
    {
        rb.linearVelocity = move * speed;
    }

    void ChangeAnim(string animName)
    {
        if (currentAnim == animName)
            return;

        currentAnim = animName;

        animator.Play(animName);
    }
}