using Characters;
using UnityEngine;

// An area that sends any character in the party who touches it back to the checkpoint: the hub that was reached last. For
// below the bottom of the map, or anywhere else a character shouldn't be able to stay, such as a pit that is meant to be
// fallen into.
//
// Give it a trigger collider, on a layer that collides with the characters. Characters are sent back to the checkpoint. Anything
// else that falls in is left alone, unless it has Returns To Start (a carryable crate has it automatically), in which case it
// goes back to where it began. That needs the zone's layer to collide with the object's layer too.
public class KillZone : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("How many hit points a character loses on the way back. 0 is none: they are just put back at the checkpoint. If it takes all of theirs, they come back with all of them, as they do from any death.")]
    public float damage;

    [Tooltip("Shows a message saying who was sent back.")]
    public bool showMessage = true;

    private bool warnedNoHub;

    void Reset()
    {
        // Something that is touched, if there isn't anything yet: a wide strip, to put below the map.
        if (!GetComponent<Collider2D>())
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(200f, 4f);
        }
    }

    void Start()
    {
        var area = GetComponent<Collider2D>();

        if (area)
        {
            area.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"{name}: the Kill Zone has no collider, so it can never be touched.", this);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        SendBack(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        SendBack(other);
    }

    private void SendBack(Collider2D other)
    {
        var character = other.GetComponentInParent<Character>();
        var selecter = CharacterSelecter.Instance;

        // Not a character: something that comes back to where it began, such as a crate, if it's one of those.
        if (!character)
        {
            var thing = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<ReturnsToStart>() : other.GetComponentInParent<ReturnsToStart>();

            if (thing)
            {
                thing.ReturnToStart();
            }

            return;
        }

        // Anyone in the game, and no one who is still being put away or hasn't started.
        if (!character.isActiveAndEnabled || !selecter || !selecter.IsInParty(character))
        {
            return;
        }

        if (!CharacterHub.Current)
        {
            if (!warnedNoHub)
            {
                warnedNoHub = true;
                Debug.LogWarning($"{name}: a character touched the Kill Zone, but there is no checkpoint to send them to. " +
                                 "Put a Character Hub in the scene with Start Hub ticked.", this);
            }

            return;
        }

        var hitPoints = character.GetComponent<DamageableEntity>();

        // Losing the last of them brings them back through the usual death, which puts them at the checkpoint too.
        if (damage > 0f && hitPoints)
        {
            hitPoints.Damage(damage);
        }

        var hub = CharacterHub.Current;

        CharacterHub.Respawn(character);

        if (showMessage)
        {
            GameMessages.ShowEverywhere(GameText.Fill(GameText.Current.sentBackToHub, character: character.name, hub: hub.hubName));
        }
    }

    void OnDrawGizmos()
    {
        var area = GetComponent<Collider2D>();

        if (!area)
        {
            return;
        }

        var bounds = area.bounds;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawCube(bounds.center, bounds.size);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
