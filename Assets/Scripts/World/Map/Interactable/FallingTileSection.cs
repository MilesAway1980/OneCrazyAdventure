using UnityEngine;

public class FallingTileSection : MonoBehaviour
{
    private Vector3 originalPosition;
    public float returnForce;
    private Rigidbody2D rigidBody2D;

    void Start()
    {
        originalPosition = transform.position;
        rigidBody2D = GetComponent<Rigidbody2D>();
    }

    
    void Update()
    {
        if (transform.position.y < originalPosition.y)
        {
            rigidBody2D.AddForce(Vector3.up * returnForce);
        }
        
        if (transform.position.y > originalPosition.y)
        {
            rigidBody2D.AddForce(Vector3.down * returnForce);
        }
    }
}
