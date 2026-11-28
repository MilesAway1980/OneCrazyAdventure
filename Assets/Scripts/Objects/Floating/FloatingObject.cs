using System;
using System.Collections.Generic;
using UnityEngine;

// One string of a floating object: how long it is, where it starts on the float, and what it's tied to.
[Serializable]
public class FloatingString
{
    [Tooltip("What the end of the string is tied to: a Floating Anchor Point on the thing to hold up (a crate, a platform). Left empty, the string hangs loose and holds " +
             "nothing up.")]
    public FloatingAnchorPoint anchor;

    [Min(0.1f)]
    [Tooltip("How long the string is, in units: how high the float is above where it's tied.")]
    public float length = 3f;

    [Tooltip("Where the string starts, on the float, in its own space. The bottom of it, by default.")]
    public Vector2 startOffset = new Vector2(0f, -0.5f);
}

// Something that floats, such as a balloon on a string, and holds something up. Much simpler than the Balloon: no one controls it, it doesn't walk or
// pick anything up, and it only holds up what its strings are tied to.
//
// Its Lift is how much mass it holds up, and it holds that up whatever it's tied to: a platform with a mass of 100 and four floats of Lift 25 tied to it stays
// where it is. If one is taken away (switched off or destroyed) the lift is 75, and it starts to sink. With more lift than mass, it rises. The lift is a
// force on the body, as far to the side of its middle as the string is tied (and at the height of its middle, so that it doesn't matter whether the strings are
// tied above or below it: a push up from below the middle would turn it over), so a float at one end tips a platform if the others don't match it. It's also
// turned back toward level, a little, by what's holding it. The float goes along with what it holds, a string's length above where it's tied. See Docs/Floating.md.
public class FloatingObject : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("How much mass this holds up, in the mass of the Rigidbody2D it's tied to. A platform of mass 100 with four floats of 25 stays where it is; with three, it sinks; " +
             "with five, it rises. If it has more than one string, the lift is shared between them.")]
    public float lift = 25f;

    [Tooltip("The strings. Each one is tied to a Floating Anchor Point, and the float stays above where they're tied.")]
    public FloatingString[] strings = { new FloatingString() };

    [Min(0f)]
    [Tooltip("How much it holds back the body going up or down, as a fraction of its lift each second, so that a body that is out of balance sinks or rises at a " +
             "steady pace and doesn't speed up and up. More makes it slower and steadier; 0 is none, and it speeds up as anything falls. The speed it settles at is " +
             "the lift that's missing, over the lift that's left, times gravity, over this: a platform of 100 with three floats of 25 left settles at a third of " +
             "gravity over this, which is about 1.6 units a second with 2.")]
    public float dragRate = 2f;

    [Min(0f)]
    [Tooltip("How strongly it turns what it holds back to level (to the way it was when the game started), in proportion to how much of its weight it's holding up: a body that " +
             "is all held up is turned back at this strength, one that is half held up at half. It's a spring, and a body that is held unevenly (a float at one end missing) " +
             "tips until the two balance. 0 leaves it to the lift alone, which has nothing to level it.")]
    public float levelStrength = 20f;

    [Min(0f)]
    [Tooltip("How quickly the swinging back and forth of that dies away. Lower swings more times before it's still.")]
    public float levelDamping = 6f;

    [Header("Look")]
    [Min(0f)]
    [Tooltip("How far, in units, the float bobs up and down.")]
    public float bobHeight = 0.05f;

    [Min(0f)]
    [Tooltip("How fast it bobs, in times a second.")]
    public float bobSpeed = 0.4f;

    [Min(0f)]
    [Tooltip("How much its bobbing pulls on what it holds: its lift swings up and down with the bob, by this fraction for each unit of Bob Height. With the default, a bob of " +
             "0.05 swings the lift by 10% either side, which is about 2 mass of its 20. A float that bobs more pulls harder, and the body it holds is rocked and bobbed " +
             "less steadily, more so with floats that don't bob together (each starts at a different point of its bob). The lift is the same on average, so the body " +
             "doesn't sink or rise. 0 is a bob that only looks like one.")]
    public float bobPull = 2f;

    [Min(0f)]
    [Tooltip("How long, in seconds, the float takes to catch up with where it should be, which gives it some lag as what it's tied to moves, and the string some stretch.")]
    public float followTime = 0.15f;

    public Color stringColor = new Color(0.95f, 0.95f, 0.95f);

    [Min(0.005f)]
    public float stringWidth = 0.05f;

    // How many points a hanging string is drawn with.
    private const int LinePoints = 9;

    private Rigidbody2D body;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly List<int> live = new List<int>();
    private Vector2 smoothed;
    private Vector2 smoothVelocity;
    private bool placed;
    private float phase;

    // Whether it has come on and put itself where it should be, above what it's tied to. What's tied to it is held still until all of them have.
    public bool IsPlaced
    {
        get { return placed; }
    }
    private bool warnedBody;

    // How much of the lift goes to a given anchor point: all of it if the string to it is the only one that's tied to something, and a share of it if not.
    public float ShareFor(FloatingAnchorPoint anchor)
    {
        var tied = 0;
        var here = 0;

        foreach (var thing in strings)
        {
            if (thing != null && thing.anchor && thing.anchor.Body && (thing.anchor.Body.bodyType == RigidbodyType2D.Dynamic || thing.anchor.Waiting))
            {
                tied++;
                here += thing.anchor == anchor ? 1 : 0;
            }
        }

        return tied == 0 ? 0f : lift * here / tied;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        // It's moved by this, not by physics. (It can still be a solid thing that's in the way, as it goes.)
        if (body)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        phase = UnityEngine.Random.value * 6.28f;
        MakeLines();
    }

    void OnEnable()
    {
        foreach (var thing in strings)
        {
            if (thing != null && thing.anchor)
            {
                thing.anchor.Add(this);
            }
        }
    }

    // Switched off or destroyed: what it held up isn't any more, and starts to sink.
    void OnDisable()
    {
        foreach (var thing in strings)
        {
            if (thing != null && thing.anchor)
            {
                thing.anchor.Remove(this);
            }
        }

        placed = false;
    }

    // Cuts the strings: the float is no longer holding anything up. For anything that wants to take a float off, such as something that pops it. (Switching it off
    // does the same, and takes the float away as well.)
    public void Cut()
    {
        foreach (var thing in strings)
        {
            if (thing != null && thing.anchor)
            {
                thing.anchor.Remove(this);
                thing.anchor = null;
            }
        }
    }

    private void MakeLines()
    {
        var picture = GetComponentInChildren<SpriteRenderer>();

        for (var i = 0; i < strings.Length; i++)
        {
            var lineObject = new GameObject($"Floating String {i}");
            lineObject.transform.SetParent(transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.positionCount = LinePoints;

            // Drawn like the float's own picture, just behind it.
            if (picture)
            {
                line.sharedMaterial = picture.sharedMaterial;
                line.sortingLayerID = picture.sortingLayerID;
                line.sortingOrder = picture.sortingOrder - 1;
            }

            lines.Add(line);
        }

        StyleLines();
    }

    private void StyleLines()
    {
        foreach (var line in lines)
        {
            line.startColor = stringColor;
            line.endColor = stringColor;
            line.startWidth = stringWidth;
            line.endWidth = stringWidth;
        }
    }

    // Which strings are tied to something that can be held up.
    private void FindLive()
    {
        live.Clear();

        for (var i = 0; i < strings.Length; i++)
        {
            var anchor = strings[i] != null ? strings[i].anchor : null;

            if (!anchor || !anchor.isActiveAndEnabled)
            {
                continue;
            }

            var held = anchor.Body;

            if (!held || (held.bodyType != RigidbodyType2D.Dynamic && !anchor.Waiting))
            {
                if (!warnedBody)
                {
                    warnedBody = true;
                    Debug.LogWarning($"{name}: the anchor point {anchor.name} is on something without a dynamic Rigidbody2D, so there's nothing for it to hold up.", this);
                }

                continue;
            }

            live.Add(i);
        }
    }

    void FixedUpdate()
    {
        FindLive();

        // In someone else's game the host moves what's held up, and this only goes along with it.
        if (live.Count > 0 && lift > 0f && !GameSession.IsClientOnly)
        {
            HoldUp();
        }

        Follow();
    }

    // The lift, as a force on each body at the place its string is tied, and the drag that makes it settle.
    private void HoldUp()
    {
        var share = lift / live.Count;
        var gravity = -Physics2D.gravity.y;

        foreach (var index in live)
        {
            var anchor = strings[index].anchor;
            var held = anchor.Body;

            // Held still while the rest are being tied on: nothing to hold up yet.
            if (anchor.Waiting)
            {
                continue;
            }

            // As far to the side of the middle of it as it's tied, but at the height of its middle. Pushing up from a place below the middle is a balance that can't
            // be kept (the slightest tilt turns it further, until it's upside down and the places are above the middle), and a place above it hangs level by
            // itself; with this, it makes no difference where the strings are tied up and down, and only how far across they are decides what tips it.
            var point = new Vector2(anchor.WorldPoint.x, held.worldCenterOfMass.y);

            // Exactly what that much mass weighs, so that it cancels it, swung up and down by the bob.
            var up = share * gravity * held.gravityScale * BobPullNow();

            // Drag on how fast the body is going up or down as a whole, no more in a step than takes it to a stop. It's on the middle of it, and not at the
            // place the string is tied: drag at the places would also be a drag on its turning, which is stiff, and a body with little to resist turning (one with no
            // collider has next to none) would be kicked back and forth harder with each step. Turning is looked after by the levelling.
            var largest = held.mass * 0.5f / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            var drag = Mathf.Clamp(-held.linearVelocity.y * share * dragRate, -largest, largest);

            held.AddForceAtPosition(new Vector2(0f, up), point);
            held.AddForce(new Vector2(0f, drag));

            Level(held, anchor, share);
        }
    }

    // Where it is in its bob, from -1 (the bottom of it) to 1 (the top).
    private float Bob()
    {
        return Mathf.Sin(Time.time * bobSpeed * 6.28f + phase);
    }

    // What its lift is multiplied by now: 1 on average, a little more as it bobs up, and less as it bobs down.
    private float BobPullNow()
    {
        return Mathf.Max(0f, 1f + Bob() * bobHeight * bobPull);
    }

    // Turns the body back toward the way it was when the game started, as hard as this float's share of its weight: the shares of all that hold it up add
    // to the fraction of its weight that's held, and it's levelled by that much. Worked out as a change of its turning speed and not a torque, so it's the
    // same for any weight and size, and so that it's a spring and not a push.
    private void Level(Rigidbody2D held, FloatingAnchorPoint anchor, float share)
    {
        if (levelStrength <= 0f || held.freezeRotation || (held.constraints & RigidbodyConstraints2D.FreezeRotation) != 0)
        {
            return;
        }

        var fraction = share / Mathf.Max(0.0001f, held.mass);
        var away = Mathf.DeltaAngle(held.rotation, anchor.RestRotation);
        var acceleration = fraction * (levelStrength * away - levelDamping * held.angularVelocity);

        held.angularVelocity += acceleration * Time.fixedDeltaTime;
    }

    // The float stays above where it's tied: in the middle of where they are, as high above them as the strings let it.
    private void Follow()
    {
        var current = body ? body.position : (Vector2)transform.position;

        if (live.Count > 0)
        {
            var x = 0f;

            foreach (var index in live)
            {
                x += strings[index].anchor.WorldPoint.x;
            }

            x /= live.Count;

            var y = 0f;

            foreach (var index in live)
            {
                var thing = strings[index];
                var across = thing.anchor.WorldPoint.x - x;
                var height = Mathf.Sqrt(Mathf.Max(0.01f, thing.length * thing.length - across * across));

                // Where the start of the string is, which is the float's middle less where on it the string starts.
                y += thing.anchor.WorldPoint.y + height - thing.startOffset.y * transform.lossyScale.y;
            }

            y /= live.Count;

            var target = new Vector2(x, y);

            if (!placed)
            {
                smoothed = target;
                smoothVelocity = Vector2.zero;
                placed = true;
            }
            else
            {
                smoothed = Vector2.SmoothDamp(smoothed, target, ref smoothVelocity, Mathf.Max(0.0001f, followTime), Mathf.Infinity, Time.fixedDeltaTime);
            }
        }
        else
        {
            smoothed = current;
            smoothVelocity = Vector2.zero;
            placed = false;
        }

        var next = smoothed + Vector2.up * (Bob() * bobHeight);

        if (body)
        {
            body.MovePosition(next);
        }
        else
        {
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }
    }

    void LateUpdate()
    {
        for (var i = 0; i < strings.Length && i < lines.Count; i++)
        {
            var thing = strings[i];
            var line = lines[i];

            if (thing == null)
            {
                line.enabled = false;
                continue;
            }

            line.enabled = true;

            var from = (Vector2)transform.TransformPoint(thing.startOffset);
            var to = thing.anchor ? thing.anchor.WorldPoint : from + Vector2.down * thing.length;

            // A string that's slack hangs in a curve, and one that's tight is straight.
            var slack = Mathf.Max(0f, thing.length - Vector2.Distance(from, to));
            var sag = Mathf.Sqrt(slack * thing.length) * 0.5f;
            var middle = (from + to) * 0.5f + Vector2.down * sag;

            for (var p = 0; p < LinePoints; p++)
            {
                var t = p / (LinePoints - 1f);

                // A curve through the two ends, bent toward the middle point.
                var a = Vector2.Lerp(from, middle, t);
                var b = Vector2.Lerp(middle, to, t);
                var point = Vector2.Lerp(a, b, t);

                line.SetPosition(p, new Vector3(point.x, point.y, transform.position.z));
            }
        }
    }

    void OnValidate()
    {
        if (lines.Count > 0)
        {
            StyleLines();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f);

        foreach (var thing in strings)
        {
            if (thing == null)
            {
                continue;
            }

            var from = transform.TransformPoint(thing.startOffset);
            var to = thing.anchor ? (Vector3)thing.anchor.WorldPoint : from + Vector3.down * thing.length;

            Gizmos.DrawLine(from, to);
        }
    }
}
