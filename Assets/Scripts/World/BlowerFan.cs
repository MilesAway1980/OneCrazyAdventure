using System.Collections.Generic;
using UnityEngine;

// A fan that blows things along a direction, harder the closer they are to it. It needs no collider: it finds what is in
// its reach itself, so its area is just a shape set here, shown in the scene view while it is selected. Anything with a
// Rigidbody2D in the area is pushed by a force, as an Area Effector 2D does, but the force falls away with the distance
// from the fan along the curve set here, instead of being the same everywhere in the area.
//
// Something can be caught by the wind more, or less, than most things by having a WindCatcher, such as an open umbrella.
//
// It's a switchable object, so a lever or a button can change how hard it blows: Power When Off and Power When On are multiples of its
// Strength (1 is as it is, 0 is off, 2 is twice as hard), and switching it takes the power from one to the other, over Change Seconds. A
// button that passes on how far it's pressed sets the power by how far, between the two. Left at 1 for both, it isn't changed by a switch.
public class BlowerFan : SwitchableObject
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

    [Tooltip("How much of the strength there is at each distance from the fan. The bottom axis is how close to the fan: 1 is right at the fan and 0 is at the end of the range. The side axis is the fraction of the strength. So that the curve presets, which all rise from left to right, work as they are, a curve that rises is strongest at the fan and weaker the further out, and the default straight line goes from none at the end of the range up to the full strength at the fan.")]
    public AnimationCurve falloff = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Area")]
    public AreaShape shape = AreaShape.Column;

    [Min(0.01f)]
    [Tooltip("Column only: how wide the strip is, in units.")]
    public float width = 2f;

    [Range(1f, 180f)]
    [Tooltip("Cone only: how wide the wedge is, in degrees, across the whole of it.")]
    public float coneAngle = 45f;

    [Tooltip("How far the area is shifted from the fan, in units: across (X, to the right) and up (Y), measured in the fan's own space, as a collider's Offset is, so it turns with the object but not with the Angle. The whole of the column or cone moves, and the strength still falls with the distance out from where the area now starts.")]
    public Vector2 offset;

    [Tooltip("Which layers it pushes. Only things with a Rigidbody2D that moves are pushed.")]
    public LayerMask affects = ~0;

    [Tooltip("Whether the ground is in the way: something the fan can't see, as there is a wall between them, isn't blown. Tiles that collide are looked for, and a thing right at the fan, such as the wall it's built into, is let through.")]
    public bool blockedByGround = true;

    [Header("Switching")]
    [Min(0f)]
    [Tooltip("How hard it blows while it's switched off, as a multiple of Strength: 1 is as set above, 0 is off, 2 is twice as hard. It starts the game like this. Leave this and Power When On at 1 for a fan that no switch changes.")]
    public float powerWhenOff = 1f;

    [Min(0f)]
    [Tooltip("How hard it blows while it's switched on, as a multiple of Strength: 0 turns it off, 1 is as set above, 2 is twice as hard. A lever or a button that is pressed all the way gives this. A button that passes on how far it's pressed gives anywhere between the two.")]
    public float powerWhenOn = 1f;

    [Min(0f)]
    [Tooltip("How long, in seconds, it takes to change by a whole Strength, from off to as set above, or from as set above to off. 0 changes at once. A change of less takes less time.")]
    public float changeSeconds = 1f;

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

    // How hard it blows now, as a multiple of Strength, and how far on it's wanted, from 0 (off) to 1 (on).
    private float power = 1f;
    private float wantedAmount;
    private float lastActivated = float.NegativeInfinity;

    // How hard it blows now, as a multiple of Strength, for the network to send.
    public float Power
    {
        get { return power; }
    }

    // On a machine that has joined someone else's game: how hard the host says it blows, which sets how fast the picture turns.
    public void SetPowerFromNetwork(float newPower)
    {
        power = Mathf.Max(0f, newPower);
    }

    protected override void Start()
    {
        base.Start();

        // Switched off, until it's switched on.
        power = powerWhenOff;

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

    // Not the base class's Update, which switches the object off again by itself every frame unless something keeps switching it on. This
    // one only goes by being told.
    protected override void Update()
    {
        // In someone else's game the host decides how hard it blows, and the power comes from the network.
        if (!GameSession.IsClientOnly)
        {
            if (!staySwitchedOn && switchOffTime > 0f && wantedAmount > 0f && Time.time - lastActivated >= switchOffTime)
            {
                wantedAmount = 0f;
            }

            var goal = Mathf.Lerp(powerWhenOff, powerWhenOn, wantedAmount);

            power = changeSeconds <= 0f ? goal : Mathf.MoveTowards(power, goal, Time.deltaTime / changeSeconds);
        }

        // The picture turns as fast as it blows: faster when it's harder, and still when it's off.
        if (!spriteRenderer || !sprite1)
        {
            return;
        }

        if (!sprite2 || power < 0.02f)
        {
            return;
        }

        if (Time.time - spriteChangeTime < spriteChangeSpeed / Mathf.Clamp(power, 0.25f, 4f))
        {
            return;
        }

        spriteChangeTime = Time.time;
        spriteRenderer.sprite = spriteRenderer.sprite == sprite1 ? sprite2 : sprite1;
    }

    public override void Activate()
    {
        base.Activate();

        wantedAmount = 1f;
        lastActivated = Time.time;
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    // Takes the power that fraction of the way from Power When Off to Power When On: a button that is half pressed blows half way between.
    // One that's let go goes back, unless it stays switched on, which keeps it as far as it has been.
    public override void SetAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f)
        {
            Deactivate();
            return;
        }

        base.Activate();

        wantedAmount = staySwitchedOn ? Mathf.Max(wantedAmount, amount) : amount;
        lastActivated = Time.time;
    }

    public override void Deactivate()
    {
        base.Deactivate();

        if (!staySwitchedOn)
        {
            wantedAmount = 0f;
        }
    }

    void FixedUpdate()
    {
        if (strength * power <= 0.0001f)
        {
            return;
        }

        var direction = BlowDirection();
        var origin = AreaOrigin();

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

    // Where the area starts: the fan, shifted by the offset, in the fan's own space.
    private Vector2 AreaOrigin()
    {
        return (Vector2)(transform.position + transform.rotation * (Vector3)offset);
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

        var strengthHere = strength * power * Mathf.Max(falloff.Evaluate(1f - Mathf.Clamp01(distance / range)), 0f);

        var catcher = body.GetComponentInParent<WindCatcher>();
        if (catcher)
        {
            strengthHere *= catcher.Multiplier;
        }

        if (strengthHere > 0f)
        {
            var force = direction * strengthHere;

            // A character is pushed so that the push is kept as momentum and has to be beaten to walk against it.
            Characters.Character.Push(body, force, ForceMode2D.Force);

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

#if UNITY_EDITOR
    // Puts the falloff back to the default: the full strength at the fan, falling to none at the end of the range. In the component's
    // menu (the three dots, or right-click its name) in the Inspector.
    [ContextMenu("Reset Falloff (strongest at the fan)")]
    private void ResetFalloff()
    {
        UnityEditor.Undo.RecordObject(this, "Reset fan falloff");
        falloff = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Turns the falloff curve round, left for right, for a fan that was made when the curve went the other way: one that is
    // strongest at the end of its range comes out strongest at the fan, and the other way round.
    [ContextMenu("Flip Falloff Direction")]
    private void FlipFalloff()
    {
        var keys = falloff.keys;
        var flipped = new Keyframe[keys.Length];

        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[keys.Length - 1 - i];
            flipped[i] = new Keyframe(1f - key.time, key.value, -key.outTangent, -key.inTangent);
        }

        UnityEditor.Undo.RecordObject(this, "Flip fan falloff");
        falloff = new AnimationCurve(flipped);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    // The area, in the scene view while the fan is selected, with lines across it that are fainter the weaker the wind is.
    void OnDrawGizmosSelected()
    {
        var direction = (Vector2)(Quaternion.Euler(0f, 0f, -angle) * transform.up);
        var origin = AreaOrigin();
        var side = new Vector2(-direction.y, direction.x);

        // How far it's been shifted from the fan.
        if (offset != Vector2.zero)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, origin);
        }

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
        colour.a = Mathf.Clamp01(falloff.Evaluate(1f - t));
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
