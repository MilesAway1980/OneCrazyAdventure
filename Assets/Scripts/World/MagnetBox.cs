using UnityEngine;

public class MagnetBox : MonoBehaviour
{
    public float magnetStrength;
    private int dir = -1;
    public float moveSpeed;
    public float moveRange;

    private Vector3 startPosition;
    
    void Start()
    {
        startPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3(transform.position.x + (dir * moveSpeed), transform.position.y, transform.position.z);
        if (Vector3.Distance(transform.position, startPosition) > moveRange)
        {
            dir *= -1;
        }
    }
}
