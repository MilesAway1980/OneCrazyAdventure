using UnityEngine;
using UnityEngine.Events;

// A drawbridge: a long platform that swings about a hinge when it is switched, between how it is placed and a turn away from that.
// It might start upright and lower flat across a gap, or start flat and raise to close an opening.
//
// How it's set up:
//  - Put the bridge in the position it starts in, and give it a Rigidbody2D set to Kinematic, so what stands on it is carried (or
//    leave it without one, and it moves by its transform).
//  - Pivot is the hinge: an empty object where the bridge turns. It can be a child of the bridge. Where it is when the game starts
//    is the hinge; it stays there however the bridge swings.
//  - Swing Degrees is how far it turns when it's switched on, from how it's placed: positive is counterclockwise. Lowering a bridge
//    that stands upright, hinged at its foot, to the right is -90; raising a flat one hinged at its left end is 90. It's drawn in the
//    scene view when the bridge is selected, with the place it ends up.
//  - Speed is how fast it swings, and Weight is how heavy it is.
//
// Weight and speed together are what make it feel right. It speeds up and slows down with a motor that does the same job for a
// light bridge as a heavy one, which for a heavy one takes much longer. A light bridge, even a fast one, slows in time and comes
// to rest gently. A heavy one can't: the motor starts slowing it when a light one would have, but there's too much weight to slow,
// so it arrives with speed left, and hits the stop. If that's hard enough (see Slam Energy) it's a slam: it can bounce, and sets off
// the sound and the event for it. A heavy bridge that's slow, or a light one that's fast, doesn't slam.
//
// It works like a door for the switches: a button or a lever swings it, and it swings back when it's switched off. A button set to
// Allow Partial Presses swings it by how far down it's pressed. Stay Switched On leaves it where it's been swung to, and Switch Off Time
// swings it back that many seconds after the last time it was told to.
public class SwitchDrawbridge : SwitchableObject
{
    [Header("What moves")]
    [Tooltip("The platform that swings. If it's empty, this object does.")]
    public Transform bridge;

    [Tooltip("The hinge it turns about. Where this is when the game starts is the hinge. If it's empty, the bridge turns about its own middle, which is rarely what's wanted.")]
    public Transform pivot;

    [Header("How it swings")]
    [Tooltip("How far it turns when it's switched on, in degrees from how it's placed. Positive is counterclockwise. A bridge standing upright that lowers to the right is -90.")]
    [Range(-180f, 180f)]
    public float swingDegrees = -90f;

    [Tooltip("The fastest it swings, in degrees per second.")]
    [Min(1f)]
    public float speed = 90f;

    [Tooltip("How heavy it is. 1 is light: it stops on a coin. Heavier takes longer to get going, and can't be stopped in time at speed, so it hits the stop hard. For a guide: at a speed of 90, 1 is gentle, 4 slams, and 10 slams hard.")]
    [Min(0.05f)]
    public float weight = 1f;

    [Header("When it hits the stop")]
    [Tooltip("How much of its speed it keeps going back off the stop after a slam, from 0 (none: it stops dead) to 1 (all of it). It only bounces after a slam.")]
    [Range(0f, 1f)]
    public float bounciness = 0.3f;

    [Tooltip("How hard it has to hit the stop to count as a slam, as half its weight times its turning speed in radians per second, squared. Below this it just comes to rest. Lower is a bridge that slams more easily.")]
    [Min(0f)]
    public float slamEnergy = 1f;

    [Tooltip("Played where it hits, louder for a harder slam.")]
    public AudioClip slamSound;

    [Tooltip("Told how hard each slam was, the same number that Slam Energy is measured in. For a screen shake, dust or the like.")]
    public SlamEvent onSlam = new SlamEvent();

    [Header("Advanced")]
    [Tooltip("How hard the hinge's motor can speed it up and slow it down, in degrees per second squared, for a weight of 1. A bridge weighing twice as much gets half of this. It's also what the motor expects to be able to slow it with, which a heavy bridge falls short of.")]
    [Min(1f)]
    public float motorStrength = 720f;

    [System.Serializable]
    public class SlamEvent : UnityEvent<float>
    {
    }

    private Rigidbody2D body;
    private Vector2 hinge;
    private Vector2 startPosition;
    private float startRotation;

    // How far it has turned from where it started, in degrees, and how fast it's turning.
    private float angle;
    private float turning;

    private bool wantsSwung;

    // How much of the swing is wanted, from 0 to 1: all of it, unless a button that passes on an amount says otherwise.
    private float swingAmount = 1f;
    private float lastActivated = float.NegativeInfinity;

    // Bounces smaller than this, in degrees per second, don't happen: it's still.
    private const float SmallestBounce = 5f;

    protected override void Start()
    {
        base.Start();

        if (!bridge)
        {
            bridge = transform;
        }

        body = KinematicBody();

        startPosition = body.position;
        startRotation = body.rotation;

        if (pivot)
        {
            hinge = pivot.position;
        }
        else
        {
            hinge = startPosition;
            Debug.LogWarning($"{name}: the drawbridge has no Pivot, so it turns about its own middle.", this);
        }

        // Moved a step at a time by the physics, so it's drawn smoothly between them.
        if (body)
        {
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    // The bridge's body. It has to be kinematic, or what stands on it isn't carried: one moved by its transform has no movement for a
    // character to take. So one is added, or made so, if it isn't already, and said so.
    private Rigidbody2D KinematicBody()
    {
        var found = bridge.GetComponent<Rigidbody2D>();

        if (!found)
        {
            found = bridge.gameObject.AddComponent<Rigidbody2D>();
            Debug.LogWarning($"{name}: {bridge.name} had no Rigidbody2D, so a Kinematic one has been added, which is what carries " +
                             "whatever stands on it. Add one yourself, set to Kinematic, to stop this message.", this);
        }

        if (found.bodyType != RigidbodyType2D.Kinematic)
        {
            found.bodyType = RigidbodyType2D.Kinematic;
            Debug.LogWarning($"{name}: {bridge.name}'s Rigidbody2D wasn't Kinematic, so it has been made Kinematic. " +
                             "Set it to Kinematic yourself to stop this message.", this);
        }

        return found;
    }

    // Not the base class's Update, which switches the object off again by itself every frame unless something keeps switching it on.
    protected override void Update()
    {
        // In someone else's game the host decides, and where the bridge is comes from the network.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        if (!staySwitchedOn && switchOffTime > 0f && wantsSwung && Time.time - lastActivated >= switchOffTime)
        {
            wantsSwung = false;
        }
    }

    void FixedUpdate()
    {
        if (GameSession.IsClientOnly)
        {
            return;
        }

        var goal = wantsSwung ? swingDegrees * swingAmount : 0f;

        Turn(goal, Time.fixedDeltaTime);
        Place();
    }

    // One physics step of the motor turning it toward the goal.
    //
    // The motor aims to arrive slowly: it asks for the speed from which it could stop in the distance left if it was as strong as it
    // is for a weight of 1. A light bridge can do as it asks. A heavy one can only change its speed so fast, so it falls behind, and
    // arrives with speed it couldn't get rid of.
    private void Turn(float goal, float dt)
    {
        var remaining = goal - angle;
        var distance = Mathf.Abs(remaining);

        if (distance < 0.02f && Mathf.Abs(turning) < 1f)
        {
            angle = goal;
            turning = 0f;
            return;
        }

        var wanted = Mathf.Sign(remaining) * Mathf.Min(speed, Mathf.Sqrt(2f * motorStrength * distance));
        var change = motorStrength / Mathf.Max(0.05f, weight) * dt;

        turning = Mathf.MoveTowards(turning, wanted, change);

        var next = angle + turning * dt;

        // The ends of its swing are stops.
        var low = Mathf.Min(0f, swingDegrees);
        var high = Mathf.Max(0f, swingDegrees);

        if (next > high || next < low)
        {
            angle = Mathf.Clamp(next, low, high);
            HitStop();
            return;
        }

        angle = next;
    }

    // Arrived at a stop with the speed it has. A slam if it's hard enough, which bounces; anything less is just stopping.
    private void HitStop()
    {
        var radians = turning * Mathf.Deg2Rad;
        var energy = 0.5f * weight * radians * radians;

        if (energy < slamEnergy)
        {
            turning = 0f;
            return;
        }

        turning = -turning * bounciness;

        if (Mathf.Abs(turning) < SmallestBounce)
        {
            turning = 0f;
        }

        if (slamSound)
        {
            var position = body ? body.position : (Vector2)bridge.position;
            AudioSource.PlayClipAtPoint(slamSound, position, Mathf.Clamp01(energy / (slamEnergy * 8f) + 0.2f));
        }

        onSlam.Invoke(energy);
    }

    // Puts the bridge where the swing has got to: the same turn, about the hinge.
    private void Place()
    {
        var turn = Quaternion.Euler(0f, 0f, angle);
        var position = hinge + (Vector2)(turn * (startPosition - hinge));
        var rotation = startRotation + angle;

        if (body)
        {
            body.MovePosition(position);
            body.MoveRotation(rotation);
        }
        else
        {
            bridge.position = new Vector3(position.x, position.y, bridge.position.z);
            bridge.rotation = Quaternion.Euler(0f, 0f, rotation);
        }
    }

    public override void Activate()
    {
        base.Activate();

        wantsSwung = true;
        swingAmount = 1f;
        lastActivated = Time.time;
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    // Swings it that fraction of the way: a button that is half pressed swings it half way. One that's let go swings it back, unless
    // it stays switched on, which keeps it as far round as it has been.
    public override void SetAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f)
        {
            Deactivate();
            return;
        }

        base.Activate();

        wantsSwung = true;
        swingAmount = staySwitchedOn ? Mathf.Max(swingAmount, amount) : amount;
        lastActivated = Time.time;
    }

    public override void Deactivate()
    {
        base.Deactivate();

        if (!staySwitchedOn)
        {
            wantsSwung = false;
        }
    }

    // The way it swings, and the place it ends up, drawn when the bridge is selected.
    public override Vector3 GizmoPoint
    {
        get { return bridge ? bridge.position : transform.position; }
    }

    void OnDrawGizmosSelected()
    {
        var target = bridge ? bridge : transform;
        var playing = Application.isPlaying;

        var origin = playing ? hinge : (pivot ? (Vector2)pivot.position : (Vector2)target.position);
        var from = playing ? startPosition : (Vector2)target.position;
        var fromRotation = playing ? startRotation : target.eulerAngles.z;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, 0.15f);

        const int steps = 24;
        var previous = from;

        Gizmos.color = Color.green;

        for (var i = 1; i <= steps; i++)
        {
            var point = origin + (Vector2)(Quaternion.Euler(0f, 0f, swingDegrees * i / steps) * (from - origin));
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        // The pose it ends in, drawn as its outline.
        var picture = target.GetComponentInChildren<SpriteRenderer>();
        var bounds = picture && picture.sprite ? picture.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);

        Gizmos.matrix = Matrix4x4.TRS(previous, Quaternion.Euler(0f, 0f, fromRotation + swingDegrees), target.lossyScale);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
