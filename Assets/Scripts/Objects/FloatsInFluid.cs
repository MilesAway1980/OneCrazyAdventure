using UnityEngine;

// Makes something the physics moves, such as a crate, float or sink in a liquid, as a character does: held up by as much of it as is
// under the surface, against how dense it is, and slowed by how thick the liquid is. Something lighter than the liquid rises until it
// is only as far under as it weighs, and settles at the surface, bobbing; something heavier sinks.
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

    private Rigidbody2D body;
    private CarryableObject carryable;

    // What it was like before the liquid, which is what it goes back to out of one.
    private float baseGravity;
    private float baseDamping;

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
        var submerged = Buoyancy.Sample(body, colliders, out var fluid);

        if (submerged > 0f && fluid != null)
        {
            var tuning = MovementTuning.Current;
            var mine = material ? material.density : density;
            var around = fluid.density * submerged + tuning.airDensity * (1f - submerged);
            var lift = Mathf.Min(around / Mathf.Max(0.0001f, mine), tuning.maxBuoyancyRatio);

            gravity = baseGravity * Buoyancy.Weight(lift, fluid.density, mine, tuning);
            damping = baseDamping + Buoyancy.Damping(fluid, submerged, body.linearVelocity.magnitude, tuning);
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
    }
}
