using UnityEngine;

// Swings an object back and forth about its own point, so that whatever is on it, such as a Spot Light 2D, doesn't stay pointing the same
// way. Put it on the object that turns (a light's own object, or a parent of it, where the object's point is the pivot).
//
// It turns between two angles, set from how the object was turned when it began: Min Angle and Max Angle. A positive angle turns
// counter-clockwise, as Unity's rotation does. Each sweep goes at Speed, slowing a little into each end if Smoothness says so, and can
// be made less regular so that it never quite repeats:
//  - End Randomness pulls each end in by a random amount, up to that many degrees, so no sweep goes quite as far as the last. It never
//    goes beyond the two angles.
//  - Speed Randomness makes each sweep a little faster or slower.
//  - Pause At Ends holds it at each end for a moment, with Pause Randomness adding a random extra.
//  - Wobble adds a small, slow, smooth drifting to the angle on top of all that.
//  - Random Start begins it part of the way through a sweep, so lights next to each other aren't in step.
//
// It only turns the object's picture and its light: nothing is moved by it, so it's not for platforms. It's decoration, so on a machine
// that has joined someone else's game it runs by itself, and isn't in step with the host's.
public class SwayRotation : MonoBehaviour
{
    [Header("Where it swings")]
    [Tooltip("The angle at one end of the swing, in degrees from how the object is turned to begin with. Positive turns counter-clockwise.")]
    public float minAngle = -30f;

    [Tooltip("The angle at the other end of the swing, in degrees from how the object is turned to begin with.")]
    public float maxAngle = 30f;

    [Header("How fast")]
    [Min(0.1f)]
    [Tooltip("How fast it swings, in degrees a second, on average.")]
    public float speed = 20f;

    [Range(0f, 1f)]
    [Tooltip("How much it eases in and out of each end: 0 is the same speed all the way, 1 slows right down at each end and is quickest in the middle.")]
    public float smoothness = 1f;

    [Header("Randomness")]
    [Min(0f)]
    [Tooltip("Each sweep stops short of the end by a random amount, up to this many degrees, so no two are quite the same. It never goes past the two angles.")]
    public float endRandomness = 0f;

    [Range(0f, 0.9f)]
    [Tooltip("How much each sweep's speed can differ from the one before, as a fraction of Speed: 0.2 is anywhere from 20% slower to 20% faster.")]
    public float speedRandomness = 0f;

    [Min(0f)]
    [Tooltip("How long it holds still at each end, in seconds.")]
    public float pauseAtEnds = 0f;

    [Min(0f)]
    [Tooltip("A random extra, up to this many seconds, added to each pause.")]
    public float pauseRandomness = 0f;

    [Min(0f)]
    [Tooltip("A small, smooth drifting added to the angle, up to this many degrees either way, so it never settles into a pattern.")]
    public float wobble = 0f;

    [Min(0.01f)]
    [Tooltip("How quickly the wobble drifts, in changes a second.")]
    public float wobbleSpeed = 0.5f;

    [Tooltip("Start part of the way through a sweep, in either direction, instead of the middle going toward Max Angle, so that several are not in step.")]
    public bool randomStart = true;

    private Quaternion baseRotation;
    private float from;
    private float to;
    private float sweepSeconds = 1f;
    private float sweepTime;
    private bool towardMax;
    private float pauseLeft;
    private float noiseSeed;
    private bool started;

    void OnValidate()
    {
        if (maxAngle < minAngle)
        {
            maxAngle = minAngle;
        }
    }

    void Start()
    {
        baseRotation = transform.localRotation;
        noiseSeed = Random.value * 1000f;

        if (randomStart)
        {
            from = Random.Range(minAngle, maxAngle);
            towardMax = Random.value < 0.5f;
        }
        else
        {
            from = (minAngle + maxAngle) * 0.5f;
            towardMax = true;
        }

        StartSweep();
        started = true;
        Apply(from);
    }

    void OnEnable()
    {
        // Switched off and on again: carries on from where the object is, with the same base.
        if (started)
        {
            Apply(Mathf.Lerp(from, to, Ease(sweepTime)));
        }
    }

    void Update()
    {
        if (!started)
        {
            return;
        }

        var dt = Time.deltaTime;

        if (pauseLeft > 0f)
        {
            pauseLeft -= dt;
        }
        else
        {
            sweepTime += dt / sweepSeconds;

            if (sweepTime >= 1f)
            {
                // At the end: turns round, after a pause if there's one.
                from = to;
                towardMax = !towardMax;
                pauseLeft = pauseAtEnds + Random.value * pauseRandomness;
                StartSweep();
            }
        }

        Apply(Mathf.Lerp(from, to, Ease(sweepTime)));
    }

    // Starts a sweep from where it is to an end, a little short of it if it's to be random, at a speed that may differ.
    private void StartSweep()
    {
        var end = towardMax ? maxAngle : minAngle;
        var inward = Random.value * endRandomness;

        to = Mathf.Clamp(towardMax ? end - inward : end + inward, minAngle, maxAngle);

        var pace = speed * Random.Range(1f - speedRandomness, 1f + speedRandomness);
        sweepSeconds = Mathf.Max(0.01f, Mathf.Abs(to - from) / Mathf.Max(0.1f, pace));
        sweepTime = 0f;
    }

    private float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(t, Mathf.SmoothStep(0f, 1f, t), smoothness);
    }

    private void Apply(float angle)
    {
        if (wobble > 0f)
        {
            // Perlin noise goes from 0 to 1: made to go either way, around nothing.
            angle += (Mathf.PerlinNoise(noiseSeed, Time.time * wobbleSpeed) * 2f - 1f) * wobble;
        }

        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
    }

    // The two ends of the swing and the way between them, from the object, in the scene view while it's selected. The direction drawn is
    // the object's up, which is the way a Spot Light 2D points.
    void OnDrawGizmosSelected()
    {
        var parent = transform.parent ? transform.parent.rotation : Quaternion.identity;
        var basis = Application.isPlaying && started ? baseRotation : transform.localRotation;
        var origin = transform.position;
        const float length = 3f;

        var low = Mathf.Min(minAngle, maxAngle);
        var high = Mathf.Max(minAngle, maxAngle);

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        Gizmos.DrawLine(origin, origin + Direction(parent, basis, low) * length);
        Gizmos.DrawLine(origin, origin + Direction(parent, basis, high) * length);

        const int steps = 16;
        var previous = origin + Direction(parent, basis, low) * length;

        for (var i = 1; i <= steps; i++)
        {
            var next = origin + Direction(parent, basis, Mathf.Lerp(low, high, i / (float)steps)) * length;

            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        // Which way it's pointing as it began.
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        Gizmos.DrawLine(origin, origin + Direction(parent, basis, 0f) * length);
    }

    private static Vector3 Direction(Quaternion parent, Quaternion basis, float angle)
    {
        return parent * basis * Quaternion.Euler(0f, 0f, angle) * Vector3.up;
    }
}
