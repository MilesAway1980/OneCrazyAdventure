using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    // The Balloon's string. It's a rope worked out by this script: a line of points, joined by a fixed distance, hanging from him, drawn as a line of a colour
    // through them so that it always looks joined up. It has no bodies or joints or weight of its own, so it can't stretch or go wild with something heavy on
    // the end, and what hangs from it is looked after elsewhere (the Balloon's Lift Capacity). It drapes over what it meets and piles up on it.
    //
    // Added to the Balloon by his own script, which says how long it is, how far apart the points are, and what it looks like. At the end of it is a Kinematic
    // body, where a character takes hold, or the Balloon takes hold of a character or a crate. What hangs from it is joined by a rope to the Balloon himself, at
    // the top of the string, which does nothing while the string is slack, and when it's tight the two pull on each other, as heavy as each is, as a rope does.
    // The string goes to what hangs from it, whatever that is: it's the string that's pulled to it, and it isn't what's pulled to the string.
    public class BalloonString : MonoBehaviour
    {
        [Tooltip("How much the rope sags and falls, as a multiple of gravity. More piles up faster and hangs straighter.")]
        public float ropeGravity = 1.5f;

        [Tooltip("How much of its speed the rope keeps each step. Lower settles faster.")]
        [Range(0.9f, 1f)]
        public float damping = 0.985f;

        [Tooltip("How many times each step the rope is pulled back to its length, at the least (a longer rope gets more). More is stiffer.")]
        public int iterations = 12;

        [Tooltip("How far, in units, the rope stays off the ground it lies on.")]
        public float thickness = 0.05f;

        [Tooltip("How much of its sliding the rope loses to the ground each step.")]
        [Range(0f, 1f)]
        public float groundFriction = 0.35f;

        // The Kinematic body at the end of the string, where things are carried and taken hold of.
        public Rigidbody2D End { get; private set; }

        // Where a character takes hold of the string.
        public GrabPoint Grab
        {
            get { return grab; }
        }

        // How long the string is, in units.
        public float Length { get; private set; }

        // How tight it is, from 0 (slack, lying on the ground or hanging with room to spare) to 1 (stretched out straight): how much of what's on the end
        // it's holding up. It's 1 a little before it's as far as it goes (a hanging load, a rope that has given a very little) so what's on it counts in full.
        public float Tautness { get; private set; }

        // How much of what's on the end is hanging from it, from 0 to 1: it's tight and straight down from him. A load on the ground at the end of a string that
        // goes sideways, or that's tight with it above him, isn't hanging, and weighs nothing on him.
        public float Hang { get; private set; }

        // How near it is to being tight, from 0 (further off than a good way from its length) to 1 (tight), which starts well before it's tight, so that what's
        // on it can be settled before it snaps tight.
        public float Approach { get; private set; }

        // Whether someone is hanging from it, and where they are and how they're moving.
        public bool HasHanger { get; private set; }
        public Vector2 HangerVelocity { get; private set; }

        // How fast who's hanging is walking, as he's trying to: his own doing, and not how fast he's going, which the string is part of. 0 for a crate.
        public float HangerWalk { get; private set; }

        // How far off the ground who's hanging is, in units: the clear space under the bottom of it, measured down from it, and not from what a character's feet
        // say, which jump from standing to well off the ground the moment he leaves it.
        public float HangerHeight { get; private set; }
        public Vector2 HangerPosition { get; private set; }

        // How far it is from the top of the string to the end of it, or to who's hanging, and the top of it: for watching.
        public float Reach { get; private set; }
        public Vector2 AttachPoint { get; private set; }

        // How hard the rope is pulling on who's hanging, as of the last step: for watching.
        public float Tension
        {
            get { return grab ? grab.Tension() : 0f; }
        }

        // How many points the rope has, from the top of it to the end.
        public int PointCount
        {
            get { return points == null ? 0 : points.Length; }
        }

        private Balloon balloon;
        private Rigidbody2D ownBody;
        private LineRenderer line;
        private Vector3[] linePoints;

        private Vector2[] points;
        private Vector2[] previous;
        private Vector2 attachLocal;
        private Vector2 lastAttach;
        private float segment;
        private bool ready;

        private GrabPoint grab;
        private readonly List<Rigidbody2D> held = new List<Rigidbody2D>();
        private readonly Collider2D[] bodyColliders = new Collider2D[16];

        // How far down to look for what's under what hangs, and so the most it can be above the ground.
        private const float MaxClearance = 20f;

        // What the rope lies on: whatever the Balloon himself is stopped by, not only the Ground layer, so it goes over platforms and crates as well. Not
        // triggers, and not anything on his own layer, which is characters.
        private ContactFilter2D solids;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private readonly Collider2D[] overlaps = new Collider2D[8];

        // Makes the string as the Balloon says: how long it is, how far apart its points are, and what it looks like. What was in the scene for it (the pieces
        // of a string made of bodies and joints) is taken away.
        public void Setup(Balloon owner)
        {
            if (ready)
            {
                return;
            }

            balloon = owner;
            ownBody = GetComponent<Rigidbody2D>();

            // The old string, made of bodies and joints, if it's still there: gone, with what looked after it.
            var chain = GetComponentInChildren<ChainLinker>(true);

            if (chain)
            {
                chain.enabled = false;
                Destroy(chain.gameObject);
            }

            Length = Mathf.Max(0.5f, balloon.stringLength);

            var count = Mathf.Max(2, Mathf.RoundToInt(Length / Mathf.Max(0.05f, balloon.segmentSpacing)));
            segment = Length / count;

            points = new Vector2[count + 1];
            previous = new Vector2[count + 1];
            linePoints = new Vector3[count + 1];

            // Held to him at the bottom of him.
            var circle = GetComponent<CircleCollider2D>();
            attachLocal = circle ? circle.offset + Vector2.down * circle.radius : Vector2.down * 0.5f;

            solids = new ContactFilter2D { useTriggers = false };
            solids.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer) & ~(1 << gameObject.layer));

            MakeBodies();
            MakeLine();

            Lay((Vector2)transform.TransformPoint(attachLocal));
            ready = true;
        }

        // The end, which is somewhere to take hold of with Interact and hang from.
        private void MakeBodies()
        {
            End = new GameObject("Balloon String End").AddComponent<Rigidbody2D>();
            End.bodyType = RigidbodyType2D.Kinematic;
            End.gravityScale = 0f;

            grab = End.gameObject.AddComponent<GrabPoint>();
            grab.ropeAnchor = ownBody;
            grab.ropeAnchorOffset = attachLocal;
            grab.ropeLength = Length;
            Character.RegisterInteractable(grab);
        }

        // The line that's drawn along the rope, in the colour and thickness he says.
        private void MakeLine()
        {
            var lineObject = new GameObject("Balloon String Line");
            lineObject.transform.SetParent(transform, false);

            line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.positionCount = points.Length;

            // Drawn like his own picture, just behind it.
            var picture = GetComponentInChildren<SpriteRenderer>();

            if (picture)
            {
                line.sharedMaterial = picture.sharedMaterial;
                line.sortingLayerID = picture.sortingLayerID;
                line.sortingOrder = picture.sortingOrder - 1;
            }

            StyleLine();
        }

        private void StyleLine()
        {
            line.startColor = balloon.stringColor;
            line.endColor = balloon.stringColor;
            line.startWidth = balloon.stringWidth;
            line.endWidth = balloon.stringWidth;
        }

        void OnDestroy()
        {
            if (End)
            {
                Destroy(End.gameObject);
            }
        }

        // Lays the rope straight down from the top of it, as far as the ground, and what's left of it piled up on the ground there, and not down
        // through it.
        private void Lay(Vector2 attach)
        {
            var count = Physics2D.Raycast(attach, Vector2.down, solids, hits, Length);
            var floor = FirstSolid(count);
            var straight = floor.collider ? Mathf.Min(points.Length - 1, Mathf.FloorToInt(floor.distance / segment)) : points.Length - 1;
            var tangent = floor.collider ? new Vector2(floor.normal.y, -floor.normal.x) : Vector2.right;

            for (var i = 0; i < points.Length; i++)
            {
                if (i <= straight)
                {
                    points[i] = attach + Vector2.down * (segment * i);
                }
                else
                {
                    // Spread along the ground either side of where it lands, a little further out for each.
                    var k = i - straight;
                    var side = k % 2 == 0 ? 1f : -1f;

                    points[i] = floor.point + floor.normal * thickness + tangent * (side * segment * 0.5f * ((k + 1) / 2));
                }

                previous[i] = points[i];
            }

            lastAttach = attach;

            if (End)
            {
                End.position = points[points.Length - 1];
            }

            DrawLine();
        }

        void FixedUpdate()
        {
            // In someone else's game the host works the rope out, and sends where its points are.
            if (!ready || GameSession.IsClientOnly)
            {
                return;
            }

            var dt = Time.fixedDeltaTime;

            // What's hanging from the end, a character or a crate: the rope goes to it.
            held.Clear();
            grab.GetHeldBodies(held);

            var hanging = held.Count > 0 ? held[0] : null;

            var attach = (Vector2)transform.TransformPoint(attachLocal);

            // Put somewhere else in one go: the rope goes with him, straight down from him, and doesn't whip round to catch up.
            if (Vector2.Distance(attach, lastAttach) > Mathf.Max(Length * 0.5f, 1f))
            {
                Lay(attach);
            }

            lastAttach = attach;
            grab.ropeLength = Length;

            var last = points.Length - 1;

            // Where each point goes if nothing stops it.
            points[0] = attach;
            previous[0] = attach;

            for (var i = 1; i < points.Length; i++)
            {
                var here = points[i];
                var velocity = (here - previous[i]) * damping;

                previous[i] = here;
                points[i] = here + velocity + Physics2D.gravity * (ropeGravity * dt * dt);
            }

            // Pulled back to its length, with the top held to him and the bottom held to what hangs from it. A longer rope needs more going over to stay its
            // length, so it's more times for more pieces, and from the top down and the bottom up in turn, as one way alone leaves the far end stretched.
            var steps = Mathf.Max(Mathf.Max(1, iterations), points.Length / 2);

            for (var step = 0; step < steps; step++)
            {
                points[0] = attach;

                if (hanging)
                {
                    points[last] = hanging.position;
                }

                var upwards = step % 2 == 1;

                for (var n = 0; n < last; n++)
                {
                    var i = upwards ? last - 1 - n : n;
                    var along = points[i + 1] - points[i];
                    var distance = along.magnitude;

                    if (distance < 0.00001f)
                    {
                        continue;
                    }

                    var pinnedTop = i == 0;
                    var pinnedBottom = hanging && i + 1 == last;
                    var weightTop = pinnedTop ? 0f : 1f;
                    var weightBottom = pinnedBottom ? 0f : 1f;
                    var total = weightTop + weightBottom;

                    if (total <= 0f)
                    {
                        continue;
                    }

                    var correction = along * ((distance - segment) / distance);
                    points[i] += correction * (weightTop / total);
                    points[i + 1] -= correction * (weightBottom / total);
                }

                if (step % 6 == 5 || step == steps - 1)
                {
                    CollideWithGround(hanging ? last - 1 : last, attach);
                }
            }

            // How tight it is, by how near the end is to as far as it reaches.
            var endPoint = hanging ? hanging.position : points[last];
            var reach = Vector2.Distance(attach, endPoint);

            Reach = reach;
            AttachPoint = attach;
            Tautness = Mathf.Clamp01((reach - (Length - TightRange * 2f)) / TightRange);
            Approach = Mathf.InverseLerp(Length * 0.75f, Length - TightRange, reach);

            var straightDown = Mathf.Clamp01((attach.y - endPoint.y) / Mathf.Max(reach, 0.01f));
            Hang = Tautness * Mathf.SmoothStep(0f, 1f, straightDown);
            grab.ropeTaut = Tautness > 0.5f;

            HasHanger = hanging;
            HangerVelocity = hanging ? hanging.linearVelocity : Vector2.zero;

            var hangingCharacter = hanging ? hanging.GetComponent<Character>() : null;

            HangerWalk = hangingCharacter ? hangingCharacter.WalkSpeedNow : 0f;
            HangerHeight = hanging ? Clearance(hanging) : 0f;
            HangerPosition = hanging ? hanging.position : Vector2.zero;

            End.MovePosition(points[last]);
        }

        // How far, in units, the bottom of a body is above what's under it: 0 when it's standing on something, and the most there is to look for if there's
        // nothing. Measured with a flat box the width of it, cast down from just above its bottom, so the edge of a ledge or a bump doesn't make it jump.
        private float Clearance(Rigidbody2D body)
        {
            var count = body.GetAttachedColliders(bodyColliders);
            var any = false;
            var bounds = new Bounds();

            for (var i = 0; i < count; i++)
            {
                var own = bodyColliders[i];

                if (!own || own.isTrigger || !own.enabled)
                {
                    continue;
                }

                if (any)
                {
                    bounds.Encapsulate(own.bounds);
                }
                else
                {
                    bounds = own.bounds;
                    any = true;
                }
            }

            if (!any)
            {
                return 0f;
            }

            const float Inset = 0.03f;
            const float Thin = 0.05f;

            var origin = new Vector2(bounds.center.x, bounds.min.y + Inset);
            var found = Physics2D.BoxCast(origin, new Vector2(Mathf.Max(0.05f, bounds.size.x * 0.9f), Thin), 0f, Vector2.down, solids, hits, MaxClearance);

            for (var h = 0; h < found; h++)
            {
                var hit = hits[h];

                if (!hit.collider || hit.collider.attachedRigidbody == body || hit.collider.attachedRigidbody == ownBody)
                {
                    continue;
                }

                // A platform that's only solid from above is something to stand on, or not under it at all.
                if (IsOneWay(hit.collider) && Vector2.Angle(hit.normal, Vector2.up) > hit.collider.GetComponent<PlatformEffector2D>().surfaceArc * 0.5f + 10f)
                {
                    continue;
                }

                // The box starts a little above the bottom, and is a little thick.
                return Mathf.Max(0f, hit.distance - (Inset - Thin * 0.5f));
            }

            return MaxClearance;
        }

        // How near, in units, to as far as the rope goes it counts as tight: half of this, or less, and it's fully so. (A load hanging from it reaches very nearly
        // its length, and a rope that counted as a little slack would only count for a little of what's on it.)
        private const float TightRange = 0.15f;

        // A point this much further from the top of the rope than the rope is long up to it, and more, can't be where the rope puts it: it has gone wrong
        // somehow, and is left to the rope alone, ignoring what it's lying on, until it's back in reach. (A rope that has only stretched a little isn't
        // that far out, so it isn't let through the ground for it.)
        private const float LostFactor = 1.5f;
        private const float LostMargin = 2f;

        // Whether a collider is a platform that's only solid from above.
        private static bool IsOneWay(Collider2D collider)
        {
            if (!collider.usedByEffector)
            {
                return false;
            }

            var effector = collider.GetComponent<PlatformEffector2D>();

            return effector && effector.useOneWay;
        }

        // Whether a hit is something the rope stops on: not himself, and, for a platform, only if it's hit from above, on the top of it.
        private bool Stops(RaycastHit2D hit)
        {
            if (!hit.collider || hit.collider.attachedRigidbody == ownBody)
            {
                return false;
            }

            if (!IsOneWay(hit.collider))
            {
                return true;
            }

            var effector = hit.collider.GetComponent<PlatformEffector2D>();

            return hit.fraction > 0f && Vector2.Angle(hit.normal, Vector2.up) <= effector.surfaceArc * 0.5f + 10f;
        }

        // The first of the hits (nearest first) that the rope stops on.
        private RaycastHit2D FirstSolid(int count)
        {
            for (var h = 0; h < count; h++)
            {
                if (Stops(hits[h]))
                {
                    return hits[h];
                }
            }

            return default;
        }

        // Whether a point is inside something the rope can't be in: anything solid, except a platform, which can be gone up through from below.
        private bool InSolid(Vector2 point)
        {
            var count = Physics2D.OverlapPoint(point, solids, overlaps);

            for (var i = 0; i < count; i++)
            {
                if (overlaps[i].attachedRigidbody != ownBody && !IsOneWay(overlaps[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // Keeps the rope on top of what it lies on: a point that has gone through it is put back on the side it came from, and slides a little less.
        //
        // One that has got inside it, and would never get out again, is put on top of it, at the first free place straight up. And one that has gone
        // wrong, much further from the top of the rope than it can be, ignores it until it's back.
        private void CollideWithGround(int upTo, Vector2 attach)
        {
            for (var i = 1; i <= upTo; i++)
            {
                if (Vector2.Distance(points[i], attach) > segment * i * LostFactor + LostMargin)
                {
                    continue;
                }

                if (InSolid(points[i]))
                {
                    points[i] = FreePlaceAbove(points[i]);
                    previous[i] = points[i];
                    continue;
                }

                var from = previous[i];
                var to = points[i];

                if ((to - from).sqrMagnitude < 0.0000001f)
                {
                    continue;
                }

                var count = Physics2D.Linecast(from, to, solids, hits);
                var hit = FirstSolid(count);

                if (!hit.collider)
                {
                    continue;
                }

                // Started inside something: that's the other way out, above.
                if (hit.fraction <= 0f || hit.normal == Vector2.zero)
                {
                    points[i] = FreePlaceAbove(from);
                    previous[i] = points[i];
                    continue;
                }

                points[i] = hit.point + hit.normal * thickness;

                var slide = points[i] - previous[i];
                var along = slide - hit.normal * Vector2.Dot(slide, hit.normal);

                previous[i] = points[i] - along * (1f - groundFriction);
            }
        }

        // The first place straight up from a point that's clear of the ground, a little above it.
        private Vector2 FreePlaceAbove(Vector2 point)
        {
            for (var step = 1; step <= 80; step++)
            {
                var candidate = point + Vector2.up * (0.15f * step);

                if (!InSolid(candidate))
                {
                    return candidate + Vector2.up * thickness;
                }
            }

            return point;
        }

        void LateUpdate()
        {
            if (!ready)
            {
                return;
            }

            // In someone else's game the top of it is where his picture is here, and the rest is where the host says.
            if (GameSession.IsClientOnly)
            {
                points[0] = transform.TransformPoint(attachLocal);
            }

            StyleLine();
            DrawLine();
        }

        // The line along the rope, through every point, so that it looks like one piece however far apart they've been pulled.
        private void DrawLine()
        {
            if (!line)
            {
                return;
            }

            for (var i = 0; i < points.Length; i++)
            {
                linePoints[i] = new Vector3(points[i].x, points[i].y, transform.position.z);
            }

            line.SetPositions(linePoints);
        }

        // ----- On a machine that has joined someone else's game -----

        // Where the points are, after the top of the rope, for sending.
        public void CopyPoints(Vector3[] into)
        {
            for (var i = 0; i < into.Length && i + 1 < points.Length; i++)
            {
                into[i] = new Vector3(points[i + 1].x, points[i + 1].y, 0f);
            }
        }

        // Moves the points to where the host says they are, a fraction of the way each time, so they don't jump between what is sent.
        public void ShowPoints(Vector3[] received, float catchUp)
        {
            for (var i = 0; i < received.Length && i + 1 < points.Length; i++)
            {
                points[i + 1] = Vector2.Lerp(points[i + 1], received[i], catchUp);
            }

            End.position = points[points.Length - 1];
        }
    }
}
