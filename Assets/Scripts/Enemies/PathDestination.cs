using UnityEngine;

// Optional. Add this to a destination used by EnemyPathFollow to override the enemy's defaults for that point.
public class PathDestination : MonoBehaviour
{
    [Tooltip("Seconds the enemy waits here after arriving. Negative uses the enemy's Pause At Destination.")]
    public float pauseSeconds = -1f;

    [Tooltip("Speed for the leg of the path that ends here (the trip TO this destination). 0 uses the enemy's Velocity.")]
    public float approachSpeed;
}
