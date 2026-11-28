using System.Collections.Generic;
using UnityEngine;

// What a character presses on what he's standing on, put right for things that are held up by a force that goes by mass: the floating objects that hold up a
// platform, and a liquid that holds up a raft.
//
// A character's gravity is much heavier than the world's (a multiple of it, and a harder pull again falling) so that he falls and jumps well, and standing, all of
// it comes down on what he's on. A character of mass 30 might press like 250. But the lift that holds something up is worked out in mass, so it would take about
// eight times his mass to hold him up. TakeBack gives back to the body all of what he presses with but his mass (as an upward force where he stands), so that he's
// his mass: 30 more lift holds up a character of 30. See Docs/Floating.md.
public static class StandingWeight
{
    private const int MaxContacts = 32;
    private static readonly ContactPoint2D[] contacts = new ContactPoint2D[MaxContacts];
    private static ContactFilter2D contactFilter = new ContactFilter2D { useTriggers = false };
    private static readonly Dictionary<Rigidbody2D, float> loads = new Dictionary<Rigidbody2D, float>();
    private static readonly Dictionary<Rigidbody2D, Vector2> places = new Dictionary<Rigidbody2D, Vector2>();
    private static readonly Dictionary<Rigidbody2D, int> counts = new Dictionary<Rigidbody2D, int>();

    // Call it each physics step for a body that characters stand on. What's pressing is measured from the contact, up to what each character is worked out to press
    // with, less his mass times gravity, so a landing is still a push.
    public static void TakeBack(Rigidbody2D body)
    {
        if (!body || body.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        var count = body.GetContacts(contactFilter, contacts);

        if (count == 0)
        {
            return;
        }

        loads.Clear();
        places.Clear();
        counts.Clear();

        var step = Mathf.Max(0.0001f, Time.fixedDeltaTime);

        for (var i = 0; i < count; i++)
        {
            var contact = contacts[i];
            var ours = contact.rigidbody == body;
            var other = ours ? contact.otherRigidbody : contact.rigidbody;

            if (!other || other == body)
            {
                continue;
            }

            // Standing on it: a flat enough surface, and he's above it.
            if (Mathf.Abs(contact.normal.y) < 0.7f || contact.point.y >= other.worldCenterOfMass.y)
            {
                continue;
            }

            loads.TryGetValue(other, out var load);
            places.TryGetValue(other, out var where);
            counts.TryGetValue(other, out var number);

            loads[other] = load + contact.normalImpulse / step;
            places[other] = where + contact.point;
            counts[other] = number + 1;
        }

        var gravity = -Physics2D.gravity.y;

        foreach (var pair in loads)
        {
            var standing = pair.Key;
            var character = standing.GetComponent<Characters.Character>();

            if (!character)
            {
                continue;
            }

            var weight = standing.mass * gravity;
            var expected = weight * character.StandingWeightRatio;

            // What's pressing, but not more than what he's worked out to press with, less what he weighs.
            var excess = Mathf.Min(pair.Value, expected) - weight;

            if (excess > 0f)
            {
                body.AddForceAtPosition(new Vector2(0f, excess), places[standing] / counts[standing]);
            }
        }
    }
}
