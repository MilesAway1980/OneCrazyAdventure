using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FollowPath : MonoBehaviour
{
    [SerializeField]
    private FollowPathTarget[] pathMarkers;

    public int nextTarget;
    private Vector2 startPosition;

    public float moveSpeed;
    public bool useVelocity;
    
    void Start()
    {
        if (pathMarkers == null)
        {
            print("Setting to empty");
            pathMarkers = Array.Empty<FollowPathTarget>();
        }

        //Remove null entries
        /*pathMarkers = pathMarkers
                        .ToList()
                        .Where(p => p?.gameObject != null)
                        .ToArray();*/

        startPosition = transform.position;
    }

    
    void Update()
    {
        // In someone else's game the host moves it, and where it is comes from the network.
        if (GameSession.IsClientOnly || pathMarkers.Length == 0)
        {
            return;
        }

        if (!useVelocity)
        {
            var target = pathMarkers[nextTarget];
            var distance = Vector2.Distance(startPosition, target.transform.position);
            var newPos = GameMath.MoveTowardsTarget(transform.position, target.transform.position, moveSpeed * Time.deltaTime);
            transform.position = newPos;
            var distanceMoved = Vector2.Distance(startPosition, transform.position);

            if (distanceMoved > distance)
            {
                transform.position = target.transform.position;
                startPosition = target.transform.position;
                nextTarget += 1;
                if (nextTarget >= pathMarkers.Length)
                {
                    nextTarget = 0;
                }
            }
        }
    }
}
