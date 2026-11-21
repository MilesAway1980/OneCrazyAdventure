using System.Collections;
using System.Collections.Generic;
using Characters;
using UnityEngine;
using UnityEngine.Events;

// A pad that sends a character who steps on it to another pad. Two pads make a pair, and a pair can be:
//  - One Way: only this pad sends. The pad it sends to only receives.
//  - Two Way: both pads send, each to the other.
// and:
//  - Single Use: the first time anyone is sent, in either direction, the pair is used up, and neither pad does anything again.
//  - Repeated Use: it works again after a short cooldown, as often as it's wanted.
//
// To make a pair: make a GameObject for each pad, give each a collider (a Box Collider 2D is easiest) and a Teleporter, and give one of
// them the other as its Destination. The settings are on the pad that has the Destination. The other pad needs nothing set: if the pair is
// Two Way, it sends back by itself. (GameObject > Teleporter Pair makes both, already linked.)
//
// A character who arrives is put at the destination pad, plus its Arrival Offset, at rest, with whatever it was carrying. It isn't sent back
// the moment it arrives: it has to step off the pad first, and then on again. Nothing is moved by it except characters.
//
// It runs on the host: on a machine that has joined someone else's game the characters are moved for it, and the pad's look is not changed.
public class Teleporter : MonoBehaviour
{
    public enum Direction
    {
        // Only this pad sends. The destination only receives.
        OneWay,
        // Both pads send, each to the other.
        TwoWay
    }

    public enum Uses
    {
        // The first time anyone is sent, in either direction, the pair is used up.
        SingleUse,
        // It works again after the cooldown.
        RepeatedUse
    }

    [Tooltip("The pad a character who steps on this one is sent to. Give it to one pad of the pair: the settings below are the pair's, from this one.")]
    public Teleporter destination;

    [Tooltip("One Way: only this pad sends, and the destination only receives. Two Way: both send, each to the other.")]
    public Direction direction = Direction.TwoWay;

    [Tooltip("Single Use: the first time anyone is sent, in either direction, the pair is used up. Repeated Use: it works again after the cooldown.")]
    public Uses uses = Uses.RepeatedUse;

    [Min(0f)]
    [Tooltip("Repeated Use: how long, in seconds, after someone is sent before either pad of the pair works again.")]
    public float cooldown = 1f;

    [Min(0f)]
    [Tooltip("Speed, in seconds: how long the trip takes. 0 is instant. Anything over 0 is the time the camera takes to pan from this pad to the destination: the character can't be seen while it goes, and arrives at the end.")]
    public float speed = 0f;

    [Tooltip("Where a character who arrives at this pad is put, from the pad's own position, in the pad's own directions: usually a little above it, so it lands on it.")]
    public Vector2 arrivalOffset = new Vector2(0f, 0.5f);

    [Header("How it looks")]
    [Tooltip("The colour the pad's sprite is tinted once the pair has been used up.")]
    public Color usedUpColour = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    [Tooltip("The colour the pad's sprite is tinted while it's cooling down. White leaves it as it is.")]
    public Color coolingColour = new Color(1f, 1f, 1f, 0.6f);

    [Header("Events")]
    [Tooltip("Called when this pad sends someone, with who. For a sound or an effect.")]
    public UnityEvent<Character> onSent = new UnityEvent<Character>();

    [Tooltip("Called when someone arrives at this pad, with who.")]
    public UnityEvent<Character> onArrived = new UnityEvent<Character>();

    // How long after someone arrives they're still held back from stepping back on, if they never touch the pad's collider, and how
    // soon after being sent the same character is ignored (a character with several colliders enters more than once).
    private const float ArrivalHold = 3f;
    private const float SameCharacterGap = 0.3f;

    // The pad that sends to this one, if it's a Two Way pair and this has no destination of its own.
    private Teleporter returnFrom;
    private bool spent;
    private float readyAt;
    private bool warned;

    private SpriteRenderer pad;
    private Color baseColour = Color.white;

    // Who is on their way from this pad, and the pad they're going to.
    private readonly Dictionary<Character, Teleporter> travelling = new Dictionary<Character, Teleporter>();

    private readonly Dictionary<Character, float> arrived = new Dictionary<Character, float>();
    private readonly Dictionary<Character, float> lastSent = new Dictionary<Character, float>();

    // Whether this pair has been used up.
    public bool Spent
    {
        get { return spent; }
    }

    // Where this sends to: its destination, or, in a Two Way pair, the pad that sends here.
    private Teleporter Target
    {
        get
        {
            if (destination && destination != this)
            {
                return destination;
            }

            return returnFrom && returnFrom.direction == Direction.TwoWay ? returnFrom : null;
        }
    }

    // The pad whose settings the pair goes by: this one if it has a destination, or else the one that sends here.
    private Teleporter Owner
    {
        get { return destination && destination != this ? this : (returnFrom ? returnFrom : this); }
    }

    // Where a character who arrives here is put.
    public Vector2 ArrivalPoint
    {
        get { return (Vector2)(transform.position + transform.rotation * (Vector3)arrivalOffset); }
    }

    void Reset()
    {
        // Something to step on, if there isn't anything yet.
        if (!GetComponent<Collider2D>())
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.5f, 0.5f);
        }

        MakeTriggers();
    }

    void OnValidate()
    {
        MakeTriggers();
    }

    void Awake()
    {
        pad = GetComponent<SpriteRenderer>();

        if (!pad)
        {
            pad = GetComponentInChildren<SpriteRenderer>();
        }

        if (pad)
        {
            baseColour = pad.color;
        }
    }

    void Start()
    {
        MakeTriggers();

        if (!GetComponent<Collider2D>())
        {
            Debug.LogWarning($"{name}: the Teleporter has no collider, so nothing can step on it. Add a Box Collider 2D.", this);
        }
    }

    void OnEnable()
    {
        // In a Two Way pair, the other pad sends back by itself, unless it has somewhere of its own to send.
        if (destination && destination != this && direction == Direction.TwoWay && !destination.destination)
        {
            destination.returnFrom = this;
        }
    }

    void OnDisable()
    {
        if (destination && destination.returnFrom == this)
        {
            destination.returnFrom = null;
        }

        // Whoever is part of the way there is put down at the end, as this can't take them the rest.
        foreach (var pair in travelling)
        {
            Arrive(pair.Key, pair.Value);
        }

        travelling.Clear();
        arrived.Clear();
        lastSent.Clear();
    }

    // The collider is only for telling who has stepped on, so it's made a trigger, and characters walk onto it.
    public void MakeTriggers()
    {
        foreach (var area in GetComponents<Collider2D>())
        {
            if (!area.isTrigger)
            {
                area.isTrigger = true;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(area);
#endif
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // In someone else's game the host moves everything, and where each is comes from the network.
        if (GameSession.IsClientOnly)
        {
            return;
        }

        var character = other.GetComponentInParent<Character>();

        if (!character || !character.InGame || !character.isActiveAndEnabled)
        {
            return;
        }

        // Someone who has just arrived here has to step off before they're sent on, or a Two Way pad would send them straight back.
        if (arrived.TryGetValue(character, out var when))
        {
            if (Time.time - when < ArrivalHold)
            {
                return;
            }

            arrived.Remove(character);
        }

        if (lastSent.TryGetValue(character, out var sentAt) && Time.time - sentAt < SameCharacterGap)
        {
            return;
        }

        Send(character);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var character = other.GetComponentInParent<Character>();

        if (character)
        {
            arrived.Remove(character);
        }
    }

    // Sends a character to the other pad, if this one is ready.
    private void Send(Character character)
    {
        var target = Target;

        if (!target)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"{name}: this Teleporter has nowhere to send anyone. Give it a Destination, or make it the other end of a Two Way pair.", this);
            }

            return;
        }

        if (spent || target.spent || Time.time < readyAt || Time.time < target.readyAt)
        {
            return;
        }

        lastSent[character] = Time.time;

        // Used: the pair is used up, or goes cool for a moment, from when they set off, as the pair's settings say.
        var owner = Owner;
        var seconds = Mathf.Max(0f, owner.speed);

        if (owner.uses == Uses.SingleUse)
        {
            spent = true;
            target.spent = true;
        }
        else
        {
            readyAt = Time.time + seconds + owner.cooldown;
            target.readyAt = readyAt;
        }

        onSent?.Invoke(character);

        if (seconds <= 0f)
        {
            Arrive(character, target);
        }
        else
        {
            travelling[character] = target;
            StartCoroutine(Travel(character, target, seconds));
        }
    }

    // Puts a character down at the other pad, at rest, ready to step off it.
    private void Arrive(Character character, Teleporter target)
    {
        if (!character)
        {
            return;
        }

        target.arrived[character] = Time.time;

        if (character.InTransit)
        {
            character.SetInTransit(false);
        }

        character.Teleport(target.ArrivalPoint);
        target.onArrived?.Invoke(character);
    }

    // The trip, when it takes time: the character can't be seen, and is moved from one pad to the other, easing in and out. The cameras
    // follow whatever character a player has, so this is the camera panning across.
    private IEnumerator Travel(Character character, Teleporter target, float seconds)
    {
        var from = (Vector2)character.transform.position;
        var to = target.ArrivalPoint;

        character.SetInTransit(true);

        for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            if (!character)
            {
                travelling.Remove(character);
                yield break;
            }

            character.MoveInTransit(Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / seconds)));
            yield return null;
        }

        travelling.Remove(character);
        Arrive(character, target);
    }

    // The pad's colour shows whether the pair is used up or cooling down.
    void Update()
    {
        if (!pad)
        {
            return;
        }

        var wanted = baseColour;

        if (spent)
        {
            wanted = usedUpColour;
        }
        else if (Time.time < readyAt)
        {
            wanted = coolingColour;
        }

        if (pad.color != wanted)
        {
            pad.color = wanted;
        }
    }

    // In the scene view while it's selected: arrows to where it sends, in both directions if it's Two Way, and where a character arrives.
    void OnDrawGizmosSelected()
    {
        var target = destination ? destination : returnFrom;

        Gizmos.color = new Color(0.8f, 0.4f, 1f, 1f);
        Gizmos.DrawWireSphere(ArrivalPoint, 0.15f);

        if (!target || target == this)
        {
            return;
        }

        if (destination)
        {
            SwitchLinkGizmo.Arrow(transform.position, target.transform.position, new Color(0.8f, 0.4f, 1f, 1f));

            if (direction == Direction.TwoWay && !destination.destination)
            {
                SwitchLinkGizmo.Arrow(target.transform.position, transform.position, new Color(0.8f, 0.4f, 1f, 1f));
            }

            Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.5f);
            Gizmos.DrawWireSphere(target.ArrivalPoint, 0.15f);
        }
    }
}
