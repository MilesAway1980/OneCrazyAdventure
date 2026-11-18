using UnityEngine;

// Works out what a body is standing on, from what it's touching, and how fast that is moving: the same for a character and for a crate.
//
// Call Update each physics step, before anything is done to the body. Then:
//  - standing: it's touching something solid on a flat enough surface under it, whatever layer that's on.
//  - riding: some of what it's standing on is a kinematic body, such as a platform.
//  - velocity: how fast the place on that body under it is moving, from how far that place has gone since the last step. A body that
//    is turning carries it round as well. Only known (measured) once it has been on the same body for a step.
//  - wallOnLeft, wallOnRight: a kinematic body, such as a bridge standing up, is touching its side.
public sealed class SupportTracker
{
    // How flat a surface has to be, as how much of its direction is up, to be stood on, and how upright to be a wall.
    public const float FlatEnough = 0.7f;

    public bool standing;
    public bool riding;
    public bool measured;
    public bool wallOnLeft;
    public bool wallOnRight;
    public Vector2 velocity;

    private Rigidbody2D ridden;
    private Vector2 worldPoint;
    private Vector2 localPoint;
    private ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];

    // root is the object the body belongs to: nothing that is part of it counts, such as what a character holds by its own point.
    public void Update(Rigidbody2D self, Transform root)
    {
        standing = false;
        riding = false;
        measured = false;
        wallOnLeft = false;
        wallOnRight = false;
        velocity = Vector2.zero;

        var count = self.GetContacts(filter, contacts);
        Rigidbody2D found = null;
        var point = Vector2.zero;

        for (var i = 0; i < count; i++)
        {
            var contact = contacts[i];
            var ours = contact.rigidbody == self;
            var other = ours ? contact.otherRigidbody : contact.rigidbody;
            var otherCollider = ours ? contact.otherCollider : contact.collider;

            if (other == self ||
                (other && other.transform.IsChildOf(root)) ||
                (otherCollider && otherCollider.transform.IsChildOf(root)))
            {
                continue;
            }

            // A wall it's against the side of, if it's a kinematic body such as a platform, a bridge standing up or a door. Tiles
            // aren't used: the joins between them can make contacts that aren't walls.
            if (other && other.bodyType == RigidbodyType2D.Kinematic && Mathf.Abs(contact.normal.x) >= FlatEnough)
            {
                if (contact.point.x > self.worldCenterOfMass.x)
                {
                    wallOnRight = true;
                }
                else
                {
                    wallOnLeft = true;
                }
            }

            // On top of it, rather than against the side of it or under it.
            if (Mathf.Abs(contact.normal.y) < FlatEnough || contact.point.y >= self.worldCenterOfMass.y)
            {
                continue;
            }

            standing = true;

            if (!found && other && other.bodyType == RigidbodyType2D.Kinematic)
            {
                found = other;
                point = contact.point;
            }
        }

        if (!found)
        {
            ridden = null;
            return;
        }

        // The same body as last step: that place on it, as it is now, against where it was.
        if (found == ridden && Time.fixedDeltaTime > 0f)
        {
            velocity = (found.GetRelativePoint(localPoint) - worldPoint) / Time.fixedDeltaTime;
            measured = true;
        }

        ridden = found;
        worldPoint = point;
        localPoint = found.GetPoint(point);
        riding = true;
    }
}
