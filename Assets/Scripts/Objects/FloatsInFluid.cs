using UnityEngine;

// Makes something the physics moves, such as a crate, float or sink in a liquid, as a character does: held up by as much of it as is
// under the surface, against how dense it is, and slowed by how thick the liquid is. Something lighter than the liquid rises until it
// is only as far under as it weighs, and settles at the surface, bobbing; something heavier sinks.
//
// It also levels itself with the surface. The buoyancy pushes up through the middle of what is under the liquid, which is off to the side of the middle of the
// body when it's tipped (the low end is the wet end), so it turns the body back toward level, as it would a boat or a raft, and rocking dies away by the liquid's
// thickness. Without that, an end that is pushed down stays down, and the high end hangs above the water.
//
// Added by itself to a carryable crate, as wood, which floats. To make something else float, or a crate sink or float differently,
// add it by hand to the object, and set what it's made of or how dense it is. A character doesn't use this: it has its own.
[RequireComponent(typeof(Rigidbody2D))]
public class FloatsInFluid : MonoBehaviour
{
    [Tooltip("What it's made of. If it's empty, Density is used instead.")]
    public CharacterMaterial material;

    [Min(0.0001f)]
    [Tooltip("How dense it is, compared with water, which is 1. Under 1 floats (wood is about 0.6), and over 1 sinks.")]
    public float density = 0.6f;

    [Range(0f, 3f)]
    [Tooltip("How strongly the liquid turns it level with the surface, as a multiple of what it would be: 1 is the buoyancy pushing up through the middle of what's under, " +
             "which is how a raft sits, and more rights it harder and faster. 0 doesn't turn it at all, which leaves it at whatever angle it's pushed to. It needs a " +
             "collider, and it's ignored if the body can't turn (Freeze Rotation Z).")]
    public float leveling = 1f;

    [Tooltip("A character standing on it weighs what his mass says. Characters are made to fall and jump well by gravity that's much heavier than the world's, and standing, " +
             "all of it presses down on what they're on (a character of 30 might press like 250), which would sink a raft far deeper than he weighs. With this on, in a " +
             "liquid, all of that but his mass is taken back off it, as an upward force where he stands. Turn it off to have him as heavy as he presses.")]
    public bool characterWeighsMass = true;

    private Rigidbody2D body;
    private CarryableObject carryable;

    // What it was like before the liquid, which is what it goes back to out of one.
    private float baseGravity;
    private float baseDamping;
    private float baseAngularDamping;

    private static readonly Collider2D[] colliders = new Collider2D[8];

    public static void EnsureOn(GameObject thing)
    {
        // A character is looked after by its own code.
        if (thing && !thing.GetComponent<FloatsInFluid>() && thing.GetComponent<Rigidbody2D>() && !thing.GetComponent<Characters.Character>())
        {
            thing.AddComponent<FloatsInFluid>();
        }
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        carryable = GetComponent<CarryableObject>();
        baseGravity = body.gravityScale;
        baseDamping = body.linearDamping;
        baseAngularDamping = body.angularDamping;
    }

    void FixedUpdate()
    {
        // In someone else's game the host moves everything, and it's shown here by where it's sent.
        if (GameSession.IsClientOnly || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated)
        {
            return;
        }

        // Held by a character, which looks after its weight itself.
        if (carryable && carryable.IsCarried)
        {
            return;
        }

        var gravity = baseGravity;
        var damping = baseDamping;
        var turning = baseAngularDamping;
        var submerged = Buoyancy.Sample(body, colliders, out var fluid);

        if (submerged > 0f && fluid != null)
        {
            var tuning = MovementTuning.Current;
            var mine = material ? material.density : density;
            var around = fluid.density * submerged + tuning.airDensity * (1f - submerged);
            var lift = Mathf.Min(around / Mathf.Max(0.0001f, mine), tuning.maxBuoyancyRatio);

            var weight = Buoyancy.Weight(lift, fluid.density, mine, tuning);

            gravity = baseGravity * weight;
            damping = baseDamping + Buoyancy.Damping(fluid, submerged, body.linearVelocity.magnitude, tuning);

            // Turning is slowed as it moves through the liquid too, so that it settles and doesn't rock for ever.
            turning = baseAngularDamping + Buoyancy.Damping(fluid, submerged, Mathf.Abs(body.angularVelocity) * Mathf.Deg2Rad, tuning);

            // How much of its air weight the liquid holds up: what turns it level.
            LevelWithSurface(Mathf.Max(0f, 1f - weight));

            // A character standing on it is only his mass to it, in a liquid (out of one, it's as it was).
            if (characterWeighsMass)
            {
                StandingWeight.TakeBack(body);
            }
        }

        // Only written when it changes, so something at rest on dry ground is still allowed to go to sleep.
        if (!Mathf.Approximately(body.gravityScale, gravity))
        {
            body.gravityScale = gravity;
        }

        if (!Mathf.Approximately(body.linearDamping, damping))
        {
            body.linearDamping = damping;
        }

        if (!Mathf.Approximately(body.angularDamping, turning))
        {
            body.angularDamping = turning;
        }
    }

    // How big the squares it's sampled in are, in units, and the most across each way.
    private const float SampleSize = 0.4f;
    private const int MostAcross = 12;
    private const int MostUp = 8;

    private static readonly Collider2D[] shape = new Collider2D[8];

    // Turns it level. The liquid pushes up with as much as it holds up, but through the middle of what's under it, not through the middle of the body, and the two
    // are apart when it's tipped, so the push turns it. That's worked out as the push up there and the same push down at the middle of the body, which is no
    // change to how hard it's pushed up (that's already done by its weight) and only turns it. The middle of what's under is found by looking at squares across
    // the body, and counting those that are in it (the collider is there) and in the liquid.
    private void LevelWithSurface(float heldUp)
    {
        if (leveling <= 0f || heldUp <= 0f || body.freezeRotation || (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0)
        {
            return;
        }

        var count = body.GetAttachedColliders(shape);
        var bounds = new Bounds();
        var found = false;

        for (var i = 0; i < count; i++)
        {
            if (shape[i].isTrigger || !shape[i].enabled)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(shape[i].bounds);
            }
            else
            {
                bounds = shape[i].bounds;
                found = true;
            }
        }

        if (!found)
        {
            return;
        }

        var across = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x / SampleSize), 3, MostAcross);
        var up = Mathf.Clamp(Mathf.CeilToInt(bounds.size.y / SampleSize), 3, MostUp);
        var z = body.transform.position.z;

        var total = Vector2.zero;
        var wet = 0;

        for (var x = 0; x < across; x++)
        {
            for (var y = 0; y < up; y++)
            {
                var point = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / across),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (y + 0.5f) / up));

                var inside = false;

                for (var i = 0; i < count && !inside; i++)
                {
                    inside = !shape[i].isTrigger && shape[i].enabled && shape[i].OverlapPoint(point);
                }

                if (inside && Map.FluidAt(new Vector3(point.x, point.y, z)) != null)
                {
                    total += point;
                    wet++;
                }
            }
        }

        if (wet == 0)
        {
            return;
        }

        var centre = total / wet;
        var push = heldUp * body.mass * -Physics2D.gravity.y * baseGravity * leveling;

        body.AddForceAtPosition(new Vector2(0f, push), centre);
        body.AddForce(new Vector2(0f, -push));
    }
}
