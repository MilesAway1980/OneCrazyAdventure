using System.Collections.Generic;
using UnityEngine;

public class EnemyPathFollow : MonoBehaviour
{
    public List<GameObject> destinations;

    public int whichObjectFirst;
    public bool reversePath;

    [Tooltip(
        "If directPath is true, the enemy will not speed up or slow down, and move point to point.  If it is false, the enemy will use velocity and have to change momentum at each point.")]
    public bool directPath = true;

    public float velocity;
    public float acceleration;
    
    private bool active;
    public int currentDestination;

    private Rigidbody2D rb;
    
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (whichObjectFirst < 0 || whichObjectFirst >= destinations.Count)
        {
            print("whichObjectFirst is outside of the bounds of destination objects.  Setting to 0.");
        }

        if (destinations == null || destinations.Count == 0)
        {
            print("There are no paths to follow.");
            active = false;
        }
        else
        {
            active = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (active == false)
        {
            return;
        }

        if (directPath == true)
        {
            var newPos = GameMath.MoveTowardsTarget(
                transform.position,
                destinations[currentDestination].transform.position,
                velocity
            );
            
            transform.position = newPos;
        }
        else
        {
            var angle = GameMath.GetAngle(transform.position,destinations[currentDestination].transform.position);
            var vector = GameMath.GetVectorFromAngle(angle);
            rb.AddForce(vector * acceleration);
            
            var magnitude = rb.linearVelocity.magnitude;
            if (magnitude > velocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * magnitude;
            }
        }
        
        if (Vector3.Distance(transform.position, destinations[currentDestination].transform.position) <= velocity)
        {
            if (reversePath == true)
            {
                currentDestination--;
            }
            else
            {
                currentDestination++;
            }

            if (currentDestination >= destinations.Count)
            {
                currentDestination = 0;
            }
                
            if (currentDestination < 0)
            {
                currentDestination = destinations.Count - 1;
            }
        }
    }
}
