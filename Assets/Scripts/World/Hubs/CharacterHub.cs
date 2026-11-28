using System.Collections.Generic;
using System.Linq;
using Characters;
using UnityEngine;

// A hub: a place where the party can be swapped. The game starts at one, and reaching another takes a puzzle that needs
// characters working together.
//
//  - It is reached as soon as anyone in the party touches its area, which makes it the checkpoint: whoever dies comes back
//    at the hub that was reached last.
//  - A character in its area can press the Hub button (Enter, or Start) to open the hub menu, to swap characters from all of those found, or to bring
//    the rest of the party to the hub.
//  - Characters found elsewhere wait to be chosen here.
//
// Give it a trigger collider for its area, on a layer that collides with the characters.
public class CharacterHub : InteractableObject
{
    [Tooltip("The name shown on the hub menu.")]
    public string hubName = "Hub";

    [Tooltip("Where the game starts. It counts as reached from the beginning, and is the checkpoint until another is reached.")]
    public bool startHub;

    [Tooltip("Where each member of the party stands when they are put at the hub: the first spot for the first, and so on. If there are fewer than the party has, the rest are spread out along a line from the hub, which is what happens if it's empty.")]
    public Transform[] spawnPoints;

    [Min(0.1f)]
    [Tooltip("How far apart, in units, characters are when they are spread out along a line.")]
    public float spawnSpacing = 1.2f;

    [Tooltip("Shown once the hub has been reached, and hidden before then. For a light, or a different picture, that says it's on.")]
    public GameObject reachedVisual;

    [Tooltip("The area that counts as being at the hub. If it's empty, the trigger collider on this object is used.")]
    public Collider2D area;

    private static readonly List<CharacterHub> hubs = new List<CharacterHub>();

    public static IReadOnlyList<CharacterHub> All
    {
        get { return hubs; }
    }

    // The checkpoint: the hub that was reached last.
    public static CharacterHub Current { get; private set; }

    // Whether anyone has reached it.
    public bool Reached { get; private set; }

    void OnEnable()
    {
        hubs.Add(this);
    }

    void OnDisable()
    {
        hubs.Remove(this);

        if (Current == this)
        {
            Current = null;
        }
    }

    void Start()
    {
        if (!area)
        {
            area = GetComponent<Collider2D>();
        }

        if (area)
        {
            area.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"{name}: the hub has no collider for its area, so nobody can reach it.", this);
        }

        if (reachedVisual)
        {
            reachedVisual.SetActive(false);
        }

        if (startHub)
        {
            Reach(false);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CheckReached(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        CheckReached(other);
    }

    // Whether this is the checkpoint now.
    public bool IsCurrent
    {
        get { return Current == this; }
    }

    // For a machine that has joined someone else's game: the hub is shown reached or not, and as the checkpoint or not, as the host says.
    // Who reaches it, and what that does, is the host's.
    public void ShowReached(bool reached, bool isCurrent)
    {
        Reached = reached;

        if (reachedVisual)
        {
            reachedVisual.SetActive(reached);
        }

        if (isCurrent)
        {
            Current = this;
        }
        else if (Current == this)
        {
            Current = null;
        }
    }

    private void CheckReached(Collider2D other)
    {
        if (GameSession.IsClientOnly || Current == this)
        {
            return;
        }

        var character = other.GetComponentInParent<Character>();
        var selecter = CharacterSelecter.Instance;

        // Anyone in the game reaching it is enough.
        if (character && character.isActiveAndEnabled && selecter && selecter.IsInParty(character))
        {
            Reach(true);
        }
    }

    // Makes this the checkpoint.
    private void Reach(bool announce)
    {
        var first = !Reached;

        Reached = true;
        Current = this;

        if (reachedVisual)
        {
            reachedVisual.SetActive(true);
        }

        if (announce)
        {
            var text = GameText.Current;

            GameMessages.ShowEverywhere(GameText.Fill(first ? text.hubReachedFirst : text.hubCheckpoint, hub: hubName));
        }
    }

    // Pressing the Hub button opens the menu of the hub he's at, for the player who is playing that character. It isn't Interact, which belongs to
    // what he's standing by: a hub is a big area, and would take the press from a lever or a crate in it.
    public static bool OpenMenuFor(Character character)
    {
        foreach (var hub in hubs)
        {
            if (hub && hub.OpenMenu(character))
            {
                return true;
            }
        }

        return false;
    }

    private bool OpenMenu(Character interacter)
    {
        if (!Reached || !IsAtHub(interacter))
        {
            return false;
        }

        var player = PlayerOf(interacter);

        if (player)
        {
            if (HubMenu.IsOpen)
            {
                return false;
            }

            HubMenu.Open(this, player, interacter);
            return true;
        }

        // Played by someone on another machine, whose own machine shows the menu.
        return ConnectionPlayer.OpenHubMenuFor(this, interacter);
    }

    // Makes these characters the party, if that's allowed, which is decided here on the host whoever asked. The characters who are out
    // in the field can't be swapped out, as they aren't here, and somebody has to stay at the hub, or whoever plays there would have
    // no character. The ones who have just joined are put at the hub, and everyone else stays where they are. False if it wasn't done.
    public bool ApplyParty(IList<Character> candidate)
    {
        var selecter = CharacterSelecter.Instance;

        if (!selecter || candidate == null)
        {
            return false;
        }

        var before = selecter.Party.ToList();
        var away = new HashSet<Character>(before.Where(member => !IsAtHub(member)));

        if (away.Any(member => !candidate.Contains(member)) || candidate.Count(member => !away.Contains(member)) < 1)
        {
            GameMessages.Show(GameText.Current.mustStay, 2.5f);
            return false;
        }

        // The same characters as there were, in any order: nothing has changed, so there is nothing to do or to say.
        if (candidate.Count == before.Count && !candidate.Except(before).Any())
        {
            return true;
        }

        if (!selecter.SetParty(candidate.ToList()))
        {
            return false;
        }

        var newcomers = candidate.Where(member => !before.Contains(member)).ToList();
        var staying = candidate.Where(member => before.Contains(member) && !away.Contains(member)).ToList();

        PlaceNewcomers(newcomers, staying);

        GameMessages.ShowEverywhere(GameText.Current.partyChanged);
        return true;
    }

    // Whether a character is in the hub's area now. A character that is only shown, on a machine that has joined someone else's game, has
    // no collider working, so its place is used.
    public bool IsAtHub(Character character)
    {
        if (!area || !character)
        {
            return false;
        }

        var body = character.GetComponent<Collider2D>();

        return body && body.enabled
            ? area.bounds.Intersects(body.bounds)
            : area.OverlapPoint(character.transform.position);
    }

    private static Player PlayerOf(Character character)
    {
        if (PlayerManager.players == null)
        {
            return null;
        }

        foreach (var player in PlayerManager.players)
        {
            if (player && player.character == character)
            {
                return player;
            }
        }

        return null;
    }

    // Where the character in this place of the party goes when they are put at the hub.
    public Vector2 SpawnPosition(int index)
    {
        if (spawnPoints != null && index < spawnPoints.Length && spawnPoints[index])
        {
            return spawnPoints[index].position;
        }

        return transform.position + Vector3.right * (index * spawnSpacing);
    }

    // Puts all of the party at the hub.
    public void PlaceParty(IReadOnlyList<Character> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            party[i].Teleport(SpawnPosition(i));
        }
    }

    // Puts characters who have just joined the party at the hub, at the first spots that aren't taken by someone who is staying
    // there. Nobody else is moved.
    public void PlaceNewcomers(IEnumerable<Character> newcomers, IEnumerable<Character> staying)
    {
        var occupied = staying.Where(c => c).Select(c => (Vector2)c.transform.position).ToList();
        var spot = 0;

        foreach (var newcomer in newcomers)
        {
            // A spot that nobody is standing on, or the next one along if there are none left within reach.
            Vector2 position;

            do
            {
                position = SpawnPosition(spot++);
            }
            while (spot < 16 && occupied.Any(o => Vector2.Distance(o, position) < spawnSpacing * 0.5f));

            newcomer.Teleport(position);
            occupied.Add(position);
        }
    }

    // Brings the rest of the party to the hub, leaving the character who called them where they are.
    public void RecallParty(Character caller)
    {
        var selecter = CharacterSelecter.Instance;

        if (!selecter)
        {
            return;
        }

        var party = selecter.Party;

        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != caller)
            {
                party[i].Teleport(SpawnPosition(i));
            }
        }
    }

    // Brings a character back at the checkpoint. If there isn't one, nothing happens.
    public static bool Respawn(Character character)
    {
        var hub = Current;
        var selecter = CharacterSelecter.Instance;

        if (!hub || !character)
        {
            return false;
        }

        var index = selecter ? selecter.Party.ToList().IndexOf(character) : 0;

        character.Teleport(hub.SpawnPosition(Mathf.Max(index, 0)));
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawWireSphere(SpawnPosition(i), 0.25f);
        }
    }
}
