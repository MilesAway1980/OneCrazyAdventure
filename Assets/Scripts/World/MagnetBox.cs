using UnityEngine;

public class MagnetBox : MonoBehaviour
{
    [Tooltip("How strongly it pulls things towards it. Not used at the moment.")]
    public float magnetStrength;
    private int dir = -1;
    [Tooltip("How far it moves each frame, in units, as it goes back and forth.")]
    public float moveSpeed;
    [Tooltip("How far, in units, it goes from where it starts before it turns around.")]
    public float moveRange;

    private Vector3 startPosition;
    
    void Start()
    {
        startPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        // In someone else's game the host moves it, and where it is comes from the network.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        transform.position = new Vector3(transform.position.x + (dir * moveSpeed), transform.position.y, transform.position.z);
        if (Vector3.Distance(transform.position, startPosition) > moveRange)
        {
            dir *= -1;
        }
    }
}
