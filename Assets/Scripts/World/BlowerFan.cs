using System.Collections.Generic;
using UnityEngine;

// A fan that blows things along a direction, harder the closer they are to it. It needs no collider: it finds what is in
// its reach itself, so its area is just a shape set here, shown in the scene view while it is selected. Anything with a
// Rigidbody2D in the area is pushed by a force, as an Area Effector 2D does, but the force falls away with the distance
// from the fan along the curve set here, instead of being the same everywhere in the area.
//
// Something can be caught by the wind more, or less, than most things by having a WindCatcher, such as an open umbrella.
public class BlowerFan : MonoBehaviour
{
    public enum AreaShape
    {
        [Tooltip("A strip of a fixed width, going out from the fan the way it blows, such as for a shaft the wind blows up.")]
        Column,
        [Tooltip("A wedge that spreads out from the fan the way it blows.")]
        Cone
    }

    [Header("Blowing")]
    [Tooltip("The way it blows, as an angle from straight up: 0 is up, 90 is right, 180 is down, 270 is left. It's measured from the way this object is turned, so turning the object turns the wind with it.")]
    [AngleDial]
    public float angle;

    [Min(0f)]
    [Tooltip("How hard it blows at the fan itself, as a force, like the Force Magnitude of an Area Effector 2D: the same force moves something light further than something heavy.")]
    public float strength = 100f;

    [Min(0.01f)]
    [Tooltip("How far out from the fan it blows, in units. Nothing further than this feels it.")]
    public float range = 8f;

    [Tooltip("How much of the strength is left at each distance from the fan. The bottom axis is how far out, from 0 at the fan to 1 at the end of the range, and the side axis is the fraction of the strength, so the default straight line falls from the full strength to none.")]
    public AnimationCurve falloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Area")]
    public AreaShape shape = AreaShape.Column;

    [Min(0.01f)]
    [Tooltip("Column only: how wide the strip is, in units.")]
    public float width = 2f;

    [Range(1f, 180f)]
    [Tooltip("Cone only: how wide the wedge is, in degrees, across the whole of it.")]
    public float coneAngle = 45f;

    [Tooltip("Which layers it pushes. Only things with a Rigidbody2D that moves are pushed.")]
    public LayerMask affects = ~0;

    [Tooltip("Whether the ground is in the way: something the fan can't see, as there is a wall between them, isn't blown. Tiles that collide are looked for, and a thing right at the fan, such as the wall it's built into, is let through.")]
    public bool blockedByGround = true;

    [Header("Picture")]
    public Sprite sprite1;
    public Sprite sprite2;
    public float spriteChangeSpeed = 0.05f;

    // How far from the fan, in units, something blocking it is ignored, so a fan set into a wall can still blow.
    private const float NearFanIgnore = 0.5f;

    // How many things a fan can push at once.
    private const int MaxBodies = 64;

    private SpriteRenderer spriteRenderer;
    private float spriteChangeTime;

    private readonly Collider2D[] found = new Collider2D[MaxBodies];
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[16];
    private readonly HashSet<Rigidbody2D> pushed = new HashSet<Rigidbody2D>();

    private Rigidbody2D ownBody;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownBody = GetComponent<Rigidbody2D>();

        if (spriteRenderer && sprite1)
        {
            spriteRenderer.sprite = sprite1;
        }

        // This script does what they did, and pushing as well would push things twice as hard.
        var effector = GetComponent<AreaEffector2D>();

        if (effector && effector.enabled)
        {
            Debug.LogWarning($"{name}: this Blower Fan has an Area Effector 2D that is switched on, which pushes things as well as " +
                             "the fan does. Remove it, and the Box Collider 2D it uses, to let the fan work on its own.", this);
        }
    }

    void Update()
    {
        if (!spriteRenderer || !sprite1 || !sprite2 || Time.time - spriteChangeTime < spriteChangeSpeed)
        {
            return;
        }

        spriteChangeTime = Time.time;
        spriteRenderer.sprite = spriteRenderer.sprite == sprite1 ? sprite2 : sprite1;
    }

    void FixedUpdate()
    {
        if (strength <= 0f)
        {
            return;
        }

        var origin = (Vector2)transform.position;
        var direction = BlowDirection();

        var count = FindColliders(origin, direction);

        pushed.Clear();

        for (int i = 0; i < count; i++)
        {
            var body = found[i].attachedRigidbody;

            // Each body is pushed once, however many colliders it has, and not if it doesn't move by the physics.
            if (!body || body == ownBody || body.bodyType != RigidbodyType2D.Dynamic || !pushed.Add(body))
            {
                continue;
            }

            Push(body, origin, direction);
        }
    }

    // The way it blows, as a direction in the world.
    private Vector2 BlowDirection()
    {
        return Quaternion.Euler(0f, 0f, -angle) * transform.up;
    }

    // What is in the area, which is more than it will push, as the shape of a cone is checked body by body afterward.
    private int FindColliders(Vector2 origin, Vector2 direction)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(affects);

        if (shape == AreaShape.Cone)
        {
            return Physics2D.OverlapCircle(origin, range, filter, found);
        }

        var centre = origin + direction * (range * 0.5f);
        var rotation = Vector2.SignedAngle(Vector2.up, direction);

        return Physics2D.OverlapBox(centre, new Vector2(width, range), rotation, filter, found);
    }

    // Pushes a body along the way the fan blows, by how far from the fan it is, if it is in the area and in sight.
    private void Push(Rigidbody2D body, Vector2 origin, Vector2 direction)
    {
        var offset = body.worldCenterOfMass - origin;
        var along = Vector2.Dot(offset, direction);

        float distance;

        if (shape == AreaShape.Cone)
        {
            distance = offset.magnitude;

            // Outside the wedge, by the middle of the body. A body overlapping the circle isn't necessarily in the cone.
            if (distance > range || (distance > 0.0001f && Vector2.Angle(direction, offset) > coneAngle * 0.5f))
            {
                return;
            }
        }
        else
        {
            // How far along the strip it is. Something touching the strip but with its middle off the end of it is as far
            // as the end.
            distance = Mathf.Clamp(along, 0f, range);
        }

        if (blockedByGround && Blocked(origin, body))
        {
            return;
        }

        var strengthHere = strength * Mathf.Max(falloff.Evaluate(Mathf.Clamp01(distance / range)), 0f);

        var catcher = body.GetComponentInParent<WindCatcher>();
        if (catcher)
        {
            strengthHere *= catcher.Multiplier;
        }

        if (strengthHere > 0f)
        {
            var force = direction * strengthHere;

            body.AddForce(force, ForceMode2D.Force);

            // So that what has a WindCatcher can tell it is being blown, and which way.
            if (catcher)
            {
                catcher.Report(force);
            }
        }
    }

    // Whether tiles that collide are between the fan and the body, ignoring the body itself and whatever is right by the fan.
    private bool Blocked(Vector2 origin, Rigidbody2D body)
    {
        var target = body.worldCenterOfMass;
        var length = Vector2.Distance(origin, target);

        if (length <= NearFanIgnore)
        {
            return false;
        }

        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask("Ground"));

        var count = Physics2D.Linecast(origin, target, filter, sightHits);

        for (int i = 0; i < count; i++)
        {
            var hit = sightHits[i];

            // Something that joined the ground layer, such as a solid Brick, doesn't block the wind from itself, and what is
            // right by the fan, which it may be built into, doesn't block it at all.
            if (hit.collider.attachedRigidbody == body || hit.distance < NearFanIgnore)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // The area, in the scene view while the fan is selected, with lines across it that are fainter the weaker the wind is.
    void OnDrawGizmosSelected()
    {
        var origin = (Vector2)transform.position;
        var direction = (Vector2)(Quaternion.Euler(0f, 0f, -angle) * transform.up);
        var side = new Vector2(-direction.y, direction.x);

        var edge = new Color(0.3f, 0.8f, 1f, 1f);

        const int steps = 10;

        if (shape == AreaShape.Cone)
        {
            var half = coneAngle * 0.5f;
            var left = (Vector2)(Quaternion.Euler(0f, 0f, half) * direction);
            var right = (Vector2)(Quaternion.Euler(0f, 0f, -half) * direction);

            Gizmos.color = edge;
            Gizmos.DrawLine(origin, origin + left * range);
            Gizmos.DrawLine(origin, origin + right * range);

            for (int i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;

                Gizmos.color = Fade(edge, t);
                Gizmos.DrawLine(origin + left * range * t, origin + right * range * t);
            }
        }
        else
        {
            var halfWidth = side * (width * 0.5f);
            var end = origin + direction * range;

            Gizmos.color = edge;
            Gizmos.DrawLine(origin - halfWidth, end - halfWidth);
            Gizmos.DrawLine(origin + halfWidth, end + halfWidth);
            Gizmos.DrawLine(end - halfWidth, end + halfWidth);

            for (int i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var point = origin + direction * range * t;

                Gizmos.color = Fade(edge, t);
                Gizmos.DrawLine(point - halfWidth, point + halfWidth);
            }
        }

        // Which way it blows.
        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, origin + direction * Mathf.Min(range, 1f));
    }

    // The colour for how strong the wind is a fraction of the way out.
    private Color Fade(Color colour, float t)
    {
        colour.a = Mathf.Clamp01(falloff.Evaluate(t));
        return colour;
    }
}

// Marks something as caught by the wind of a BlowerFan more, or less, than most things are. The fan's force on it is
// multiplied by this. Without one, a body is pushed as it is. Put it on the object that has the Rigidbody2D, or a parent of
// it. Something that changes how much it catches, such as an umbrella that opens, sets the multiplier.
public class WindCatcher : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("How many times the fan's force it feels. 1 is the same as anything else, more is caught more, and 0 isn't moved by the wind at all.")]
    public float multiplier = 1f;

    public float Multiplier
    {
        get { return multiplier; }
        set { multiplier = Mathf.Max(value, 0f); }
    }

    // The last force a fan pushed it with, after the multiplier, and when, so that whatever has it can tell it is being
    // blown, and which way. Never blown is a long time ago.
    public Vector2 LastWind { get; private set; }
    public float LastWindTime { get; private set; } = float.NegativeInfinity;

    // Called by a fan each time it pushes this.
    public void Report(Vector2 force)
    {
        LastWind = force;
        LastWindTime = Time.time;
    }
}
