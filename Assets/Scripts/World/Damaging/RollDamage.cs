using UnityEngine;

// Lets something destroy tiles that are damaged by rolling into them, while it is enabled. Its speed is the fastest it has
// been going over the last moment, as by the time it touches a tile it has already been slowed by the hit.
public class RollDamage : MonoBehaviour
{
    [Tooltip("How far back, in seconds, its fastest speed counts. It needs to be long enough to reach back to before the hit.")]
    public float speedMemorySeconds = 0.1f;

    // It has to be going at least this fast, in units per second, to do any damage: sitting still is not rolling.
    private const float MinimumSpeed = 0.1f;

    private Rigidbody2D rb;
    private Vector2[] recentVelocities;
    private int next;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Starts over each time it's switched on, so a speed from before is not counted.
    void OnEnable()
    {
        recentVelocities = new Vector2[Mathf.CeilToInt(speedMemorySeconds / Time.fixedDeltaTime) + 1];
        next = 0;
    }

    void FixedUpdate()
    {
        recentVelocities[next] = rb.linearVelocity;
        next = (next + 1) % recentVelocities.Length;
    }

    // The fastest it has been going lately, including this moment.
    public float GetSpeed()
    {
        return GetImpactVelocity().magnitude;
    }

    // The velocity it had when it was going fastest lately, which is how hard and which way it hit, rather than how it is
    // after the hit has slowed it or turned it away.
    public Vector2 GetImpactVelocity()
    {
        var fastest = rb.linearVelocity;

        // Nothing is remembered until it has been switched on.
        if (recentVelocities != null)
        {
            foreach (var recent in recentVelocities)
            {
                if (recent.sqrMagnitude > fastest.sqrMagnitude)
                {
                    fastest = recent;
                }
            }
        }

        return fastest;
    }

    // Whether it is going fast enough to destroy a tile that needs that speed. It has to be going somewhere, so a speed
    // needed of 0 means any speed, but not none.
    public bool CanBreak(float speedNeeded)
    {
        if (!enabled)
        {
            return false;
        }

        var speed = GetSpeed();

        return speed > MinimumSpeed && speed >= speedNeeded;
    }
}
