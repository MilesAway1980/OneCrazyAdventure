using UnityEngine;

public class BlowerFan : MonoBehaviour
{
    public Vector2 power;
    //public BoxCollider2D areaOfEffect;
    public Sprite sprite1;
    public Sprite sprite2;
    public float spriteChangeSpeed;
    private float spriteChangeTime;
    
    
    
    private SpriteRenderer spriteRenderer;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite1;
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time - spriteChangeTime >= spriteChangeSpeed)
        {
            spriteChangeTime = Time.time;
            if (spriteRenderer.sprite == sprite1)
            {
                spriteRenderer.sprite = sprite2;
            }
            else
            {
                spriteRenderer.sprite = sprite1;
            }
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var rigidBody = other.GetComponent<Rigidbody2D>();
        if (rigidBody)
        {
            var distance = Vector3.Distance(transform.position, other.transform.position);
            
            float forceX = power.x / distance;
            float forceY = power.y / distance;
            
            //print (distance + " " + forceX + " " + forceY + " " + rigidBody.gameObject.name);
            rigidBody.AddForce(new Vector2(forceX, forceY), ForceMode2D.Impulse);
        }
    }
}
