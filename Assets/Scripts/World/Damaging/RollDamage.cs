using UnityEngine;

public class RollDamage : MonoBehaviour
{
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public float GetSpeed()
    {
        //print(rigidbody2D.linearVelocity);
        return rb.linearVelocity.magnitude;
    }
}
