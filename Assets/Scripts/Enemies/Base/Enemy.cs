using UnityEngine;

public class Enemy : MonoBehaviour
{
    protected SpriteRenderer spriteRenderer;
    protected EnemyMovement enemyMovement;
    protected Rigidbody2D rigidBody2D;

    protected virtual void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyMovement = GetComponent<EnemyMovement>();
        rigidBody2D = GetComponent<Rigidbody2D>();

        CanTriggerFloorCollapse.EnsureOn(gameObject);
    }

    protected virtual void Update()
    {
        if (spriteRenderer)
        {
            spriteRenderer.flipX = enemyMovement.GetSpeed().x < 0;
        }
        else
        {
            print("SPRITE RENDERER NULL");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        //print($"Collision with {collision.gameObject.name}!");
    }
}
