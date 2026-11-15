using System.Collections.Generic;
using Characters;
using UnityEngine;

// A socket the Bulb can screw himself into, which makes him far brighter. Put it anywhere, with a collider that covers where
// he stands to reach it: a solid collider on the Ground layer is something he can stand on, and a trigger just marks the
// place. He goes into it by standing at it and pressing down with the action, and stays lit, and stuck in it, until he
// presses the action again to unscrew himself. One bulb at a time fits.
public class LightSocket : MonoBehaviour
{
    [Tooltip("Where the middle of the bulb sits once he is screwed in. If it's empty, he sits on the top of this object's collider, in the middle of it.")]
    public Transform seat;

    [Min(1f)]
    [Tooltip("How many times larger the radius of his light is while he is screwed in. At 1 his radius doesn't change.")]
    public float radiusMultiplier = 4f;

    [Min(0f)]
    [Tooltip("How many times brighter his light is while he is screwed in. At 1 it isn't any brighter, only larger.")]
    public float intensityMultiplier = 1.5f;

    [Min(0.01f)]
    [Tooltip("Seconds his light takes to grow when he is screwed in, and to shrink when he is unscrewed.")]
    public float boostSeconds = 0.6f;

    [Min(0f)]
    [Tooltip("How far, in units, from the collider he can be and still count as being at the socket.")]
    public float reach = 0.15f;

    private static readonly List<LightSocket> active = new List<LightSocket>();

    // Every socket in the scene that is switched on, so that the bulb can find the one he is at without searching for them.
    public static IReadOnlyList<LightSocket> Active
    {
        get { return active; }
    }

    private Collider2D socketCollider;

    // The bulb that is screwed into it, if there is one.
    public Bulb Occupant { get; private set; }

    public bool IsFree
    {
        get { return !Occupant; }
    }

    void Awake()
    {
        socketCollider = GetComponent<Collider2D>();

        if (!socketCollider)
        {
            Debug.LogWarning($"{name}: the Light Socket has no collider, so the Bulb can never be at it.", this);
        }
    }

    void OnEnable()
    {
        active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    // Whether something with these bounds is at the socket: in its collider, or no further from it than its reach.
    public bool Reaches(Bounds body)
    {
        if (!socketCollider || !socketCollider.enabled)
        {
            return false;
        }

        var area = socketCollider.bounds;
        area.Expand(reach * 2f);

        return area.Intersects(body);
    }

    // Where the middle of a body with these bounds goes when it is in the socket.
    public Vector2 SeatFor(Bounds body)
    {
        if (seat)
        {
            return seat.position;
        }

        var area = socketCollider ? socketCollider.bounds : new Bounds(transform.position, Vector3.zero);

        return new Vector2(area.center.x, area.max.y + body.extents.y);
    }

    // Takes the bulb, if nothing else is in it.
    public bool TryOccupy(Bulb bulb)
    {
        if (Occupant && Occupant != bulb)
        {
            return false;
        }

        Occupant = bulb;
        return true;
    }

    public void Release(Bulb bulb)
    {
        if (Occupant == bulb)
        {
            Occupant = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        var spot = seat ? seat.position : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spot, 0.1f);
    }
}
