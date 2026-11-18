using System.Collections.Generic;
using Characters;
using Mirror;
using UnityEngine;

// The one object each machine has on the network. Mirror calls it the connection's "player", and a connection has to have one before
// the host sends it anything of the game, but it isn't a player in the sense of someone playing: it's how a machine, and all
// the players on it, talk to the host. It's made for each connection by the Network Manager when the machine joins, from the
// prefab at Assets/Resources/ConnectionPlayer (made by Tools > Network > Create Connection Player Prefab).
//
// Every person playing, on any machine, has a seat in the game, and the host keeps the record of who is in which seat and which
// character each controls (CharacterControl). The players on the host's own machine are looked after directly. The players on
// a machine that has joined ask the host for a seat through this, send what their controllers say through it, and are told
// which character they have through it. The host applies what a seat's controller says to its character every frame, exactly as
// it does for a player at its own machine.
public class ConnectionPlayer : NetworkBehaviour
{
    // This machine's own.
    public static ConnectionPlayer Local { get; private set; }

    // ----- On the host -----

    private class Seat
    {
        public int seatId;

        // What the controller says is held now, and the buttons that have gone down since it was last applied.
        public PlayerInputFrame held;
        public bool jumpPressed;
        public bool abilityPressed;
        public bool interactPressed;
    }

    private static readonly List<ConnectionPlayer> onHost = new List<ConnectionPlayer>();

    private readonly Dictionary<int, Seat> seats = new Dictionary<int, Seat>();
    private readonly Dictionary<int, int> seatOfPlayer = new Dictionary<int, int>();

    // Which character each of this machine's seats has, by its network id (0 for none). The machine reads this to know which to follow.
    public readonly SyncDictionary<int, uint> seatCharacters = new SyncDictionary<int, uint>();

    // How many seats are taken by players on other machines, for knowing when the game is full.
    public static int RemoteSeatCount()
    {
        var count = 0;

        foreach (var link in onHost)
        {
            if (link)
            {
                count += link.seats.Count;
            }
        }

        return count;
    }

    // The seats of the players on other machines, in the order they joined: the numbers after the host's own players'.
    public static List<int> RemoteSeatIds()
    {
        var ids = new List<int>();

        foreach (var link in onHost)
        {
            if (link)
            {
                ids.AddRange(link.seats.Keys);
            }
        }

        ids.Sort();
        return ids;
    }

    // How many players there are in the game, on all machines together, and which number (as in P1) is controlling which character,
    // by network id. The host works them out, the same for every connection, and every machine reads them to show the player numbers
    // over the characters.
    [SyncVar] private int playerTotal;
    public readonly SyncDictionary<uint, int> controllerNumbers = new SyncDictionary<uint, int>();

    public int PlayerTotal
    {
        get { return playerTotal; }
    }

    private readonly List<KeyValuePair<Character, int>> numbersNow = new List<KeyValuePair<Character, int>>();
    private readonly Dictionary<uint, int> numbersWanted = new Dictionary<uint, int>();
    private readonly List<uint> numbersStale = new List<uint>();

    private void UpdatePlayerNumbers()
    {
        var total = PlayerTags.Compute(numbersNow);

        if (playerTotal != total)
        {
            playerTotal = total;
        }

        numbersWanted.Clear();

        foreach (var pair in numbersNow)
        {
            var netId = NetIdOf(pair.Key);

            if (netId != 0)
            {
                numbersWanted[netId] = pair.Value;
            }
        }

        numbersStale.Clear();

        foreach (var key in controllerNumbers.Keys)
        {
            if (!numbersWanted.ContainsKey(key))
            {
                numbersStale.Add(key);
            }
        }

        foreach (var key in numbersStale)
        {
            controllerNumbers.Remove(key);
        }

        foreach (var pair in numbersWanted)
        {
            if (!controllerNumbers.TryGetValue(pair.Key, out var current) || current != pair.Value)
            {
                controllerNumbers[pair.Key] = pair.Value;
            }
        }
    }

    // Tells the players on every machine in the game a message, the host's own included. False if there is nobody to tell it through
    // yet, in which case it hasn't been shown anywhere.
    public static bool Broadcast(string text, float seconds)
    {
        foreach (var link in onHost)
        {
            if (link && link.isServer)
            {
                link.RpcShowMessage(text, seconds);
                return true;
            }
        }

        return false;
    }

    [ClientRpc]
    private void RpcShowMessage(string text, float seconds)
    {
        GameMessages.Show(text, seconds);
    }

    public override void OnStartServer()
    {
        onHost.Add(this);
    }

    public override void OnStopServer()
    {
        onHost.Remove(this);

        // The machine has gone, or the game has stopped: its players leave their characters free for others.
        foreach (var seat in seats.Values)
        {
            CharacterControl.Release(seat.seatId);
        }

        seats.Clear();
        seatOfPlayer.Clear();
    }

    // The characters that have been found and the characters in the party, by network id, kept the same as the host's own record.
    // Every connection has them, since the host's record is the same for all.
    public readonly SyncList<uint> foundCharacters = new SyncList<uint>();
    public readonly SyncList<uint> partyCharacters = new SyncList<uint>();

    // On a machine that has joined: whether the host's lists have changed since they were last applied, or the last try wasn't complete.
    private bool rosterChanged = true;

    public override void OnStartClient()
    {
        foundCharacters.OnChange += (operation, index, item) => rosterChanged = true;
        partyCharacters.OnChange += (operation, index, item) => rosterChanged = true;
        rosterChanged = true;
    }

    void Update()
    {
        if (isServer)
        {
            UpdateSeats();
            UpdateRoster();
            UpdatePlayerNumbers();
        }
        else if (isLocalPlayer)
        {
            ApplyRoster();
        }
    }

    // Sends the host's record of who has been found, and who is in the party, when it changes.
    private void UpdateRoster()
    {
        var selecter = CharacterSelecter.Instance;

        if (!selecter || selecter.Party == null)
        {
            return;
        }

        var found = new List<uint>();
        foreach (var character in selecter.Owned)
        {
            var netId = NetIdOf(character);
            if (netId != 0)
            {
                found.Add(netId);
            }
        }

        // The order they were found in isn't kept, so they're put in a fixed one to tell whether anything has changed.
        found.Sort();

        var party = new List<uint>();
        foreach (var character in selecter.Party)
        {
            var netId = NetIdOf(character);
            if (netId != 0)
            {
                party.Add(netId);
            }
        }

        Replace(foundCharacters, found);
        Replace(partyCharacters, party);
    }

    private static uint NetIdOf(Character character)
    {
        var identity = character ? character.GetComponent<NetworkIdentity>() : null;
        return identity ? identity.netId : 0u;
    }

    private static void Replace(SyncList<uint> list, List<uint> wanted)
    {
        var same = list.Count == wanted.Count;

        for (var i = 0; same && i < wanted.Count; i++)
        {
            same = list[i] == wanted[i];
        }

        if (same)
        {
            return;
        }

        list.Clear();
        list.AddRange(wanted);
    }

    // On a machine that has joined: makes the local record of who has been found, and the party, the same as the host's. Done each
    // frame it might be wrong, as a character it names may not have arrived yet.
    private void ApplyRoster()
    {
        var selecter = CharacterSelecter.Instance;

        if (!rosterChanged || !selecter)
        {
            return;
        }

        var found = new List<Character>();
        var party = new List<Character>();

        var complete = Resolve(foundCharacters, found) & Resolve(partyCharacters, party);

        if (complete)
        {
            selecter.MirrorHost(found, party);
            rosterChanged = false;
        }
    }

    private static bool Resolve(SyncList<uint> ids, List<Character> into)
    {
        foreach (var netId in ids)
        {
            if (NetworkClient.spawned.TryGetValue(netId, out var identity) && identity && identity.GetComponent<Character>())
            {
                into.Add(identity.GetComponent<Character>());
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    // Does what each seat's controller says to the seat's character, as for a player at the host's own machine, and tells the
    // machine which character each of its seats has.
    private void UpdateSeats()
    {
        foreach (var seat in seats.Values)
        {
            // A seat with no character, or whose character has been put away, is given the first free one, as for any player.
            var character = CharacterControl.GetCharacter(seat.seatId);
            if (!character || !character.InGame)
            {
                character = CharacterControl.Claim(seat.seatId);
            }

            if (character)
            {
                var frame = seat.held;
                frame.jumpPressed = seat.jumpPressed;
                frame.abilityPressed = seat.abilityPressed;
                frame.interactPressed = seat.interactPressed;

                CharacterMove.ApplyInput(character, frame);
            }

            seat.jumpPressed = false;
            seat.abilityPressed = false;
            seat.interactPressed = false;

            var identity = character ? character.GetComponent<NetworkIdentity>() : null;
            var netId = identity ? identity.netId : 0u;

            if (!seatCharacters.TryGetValue(seat.seatId, out var told) || told != netId)
            {
                seatCharacters[seat.seatId] = netId;
            }
        }
    }

    [Command]
    private void CmdRequestSeat(int playerId)
    {
        if (seatOfPlayer.TryGetValue(playerId, out var existing))
        {
            TargetSeatGranted(connectionToClient, playerId, existing);
            return;
        }

        // The most that can be playing, all machines together.
        if (PlayerManager.GetJoinedPlayerCount() + RemoteSeatCount() >= PlayerManager.MaxPlayers)
        {
            TargetSeatDenied(connectionToClient, playerId);
            return;
        }

        var seat = new Seat { seatId = Player.AllocateSeatId() };

        seats.Add(seat.seatId, seat);
        seatOfPlayer.Add(playerId, seat.seatId);

        CharacterControl.Claim(seat.seatId);
        TargetSeatGranted(connectionToClient, playerId, seat.seatId);
    }

    [Command]
    private void CmdInput(int seatId, PlayerInputFrame frame)
    {
        if (!seats.TryGetValue(seatId, out var seat))
        {
            return;
        }

        seat.held = frame;
        seat.jumpPressed |= frame.jumpPressed;
        seat.abilityPressed |= frame.abilityPressed;
        seat.interactPressed |= frame.interactPressed;
    }

    [Command]
    private void CmdSwitch(int seatId, bool next)
    {
        if (seats.ContainsKey(seatId))
        {
            CharacterControl.Switch(seatId, next);
        }
    }

    [Command]
    private void CmdReleaseSeat(int seatId)
    {
        if (!seats.ContainsKey(seatId))
        {
            return;
        }

        CharacterControl.Release(seatId);
        seats.Remove(seatId);
        seatCharacters.Remove(seatId);

        if (menuOpenSeat == seatId)
        {
            menuOpenSeat = -1;
        }

        foreach (var pair in new List<KeyValuePair<int, int>>(seatOfPlayer))
        {
            if (pair.Value == seatId)
            {
                seatOfPlayer.Remove(pair.Key);
            }
        }
    }

    // ----- The hub menu, for a player on another machine -----
    //
    // The menu is shown and played on the player's own machine, with their own controls. The host only hears about what it changes in
    // the game: bringing the party to the hub, and making a different party. It checks them again, since it's the host that decides.

    // The seat of this machine's whose menu is open, or -1. Only one at a time for each machine.
    private int menuOpenSeat = -1;

    // Tells the machine of whoever is playing this character to open the hub menu for them. False if nobody on another machine is.
    public static bool OpenHubMenuFor(CharacterHub hub, Character who)
    {
        if (!CharacterControl.TryGetPlayerId(who, out var seatId))
        {
            return false;
        }

        var hubIdentity = hub.GetComponent<NetworkIdentity>();
        var whoIdentity = who.GetComponent<NetworkIdentity>();

        if (!hubIdentity || !whoIdentity)
        {
            return false;
        }

        foreach (var link in onHost)
        {
            if (!link || !link.seats.ContainsKey(seatId))
            {
                continue;
            }

            if (link.menuOpenSeat >= 0)
            {
                return false;
            }

            link.menuOpenSeat = seatId;
            link.TargetOpenHubMenu(link.connectionToClient, seatId, hubIdentity.netId, whoIdentity.netId);
            return true;
        }

        return false;
    }

    private static T OnHost<T>(uint netId) where T : Component
    {
        return NetworkServer.spawned.TryGetValue(netId, out var identity) && identity ? identity.GetComponent<T>() : null;
    }

    private static T OnThisMachine<T>(uint netId) where T : Component
    {
        return NetworkClient.spawned.TryGetValue(netId, out var identity) && identity ? identity.GetComponent<T>() : null;
    }

    private static uint NetIdOfObject(Component component)
    {
        var identity = component ? component.GetComponent<NetworkIdentity>() : null;
        return identity ? identity.netId : 0u;
    }

    [TargetRpc]
    private void TargetOpenHubMenu(NetworkConnectionToClient target, int seatId, uint hubNetId, uint whoNetId)
    {
        var player = Player.FindBySeat(seatId);
        var hub = OnThisMachine<CharacterHub>(hubNetId);
        var who = OnThisMachine<Character>(whoNetId);

        if (player && hub && who && !HubMenu.IsOpen)
        {
            HubMenu.Open(hub, player, who);
        }
        else
        {
            // It couldn't be shown, so the host isn't left waiting for it to be closed.
            CmdHubMenuClosed(seatId);
        }
    }

    [Command]
    private void CmdHubMenuClosed(int seatId)
    {
        if (menuOpenSeat == seatId)
        {
            menuOpenSeat = -1;
        }
    }

    [Command]
    private void CmdRecallParty(int seatId, uint hubNetId, uint whoNetId)
    {
        var hub = OnHost<CharacterHub>(hubNetId);
        var who = OnHost<Character>(whoNetId);

        if (seats.ContainsKey(seatId) && hub && who)
        {
            hub.RecallParty(who);
            GameMessages.ShowEverywhere(GameText.Current.partyBroughtToHub);
        }
    }

    [Command]
    private void CmdSetParty(int seatId, uint hubNetId, uint[] partyNetIds)
    {
        var hub = OnHost<CharacterHub>(hubNetId);

        if (!seats.ContainsKey(seatId) || !hub || partyNetIds == null)
        {
            return;
        }

        var party = new List<Character>();

        foreach (var netId in partyNetIds)
        {
            var character = OnHost<Character>(netId);

            if (!character)
            {
                return;
            }

            party.Add(character);
        }

        hub.ApplyParty(party);
    }

    // ----- On a machine that has joined -----

    private readonly HashSet<int> seatsAskedFor = new HashSet<int>();
    private readonly Dictionary<int, PlayerInputFrame> lastSent = new Dictionary<int, PlayerInputFrame>();

    public override void OnStartLocalPlayer()
    {
        Local = this;
    }

    public override void OnStopLocalPlayer()
    {
        if (Local == this)
        {
            Local = null;
        }
    }

    // The character a seat has, for the player on this machine who has it. Null if none, or if it hasn't been told yet.
    public static Character CharacterOfSeat(int seatId)
    {
        if (!Local || !Local.seatCharacters.TryGetValue(seatId, out var netId) || netId == 0)
        {
            return null;
        }

        return NetworkClient.spawned.TryGetValue(netId, out var identity) && identity
            ? identity.GetComponent<Character>()
            : null;
    }

    // Asks the host for a seat for a player on this machine, once.
    public void EnsureSeat(Player player)
    {
        if (player.seatId >= 0 || !seatsAskedFor.Add(player.id))
        {
            return;
        }

        CmdRequestSeat(player.id);
    }

    // Sends what the player's controller says. What is held is only sent again when it changes, and a button going down always is.
    public void SendInput(Player player, PlayerInputFrame frame)
    {
        if (player.seatId < 0)
        {
            return;
        }

        if (!frame.AnyPressed && lastSent.TryGetValue(player.id, out var last) && last.HeldEquals(frame))
        {
            return;
        }

        lastSent[player.id] = frame;
        CmdInput(player.seatId, frame);
    }

    public void RequestSwitch(Player player, bool next)
    {
        if (player.seatId >= 0)
        {
            CmdSwitch(player.seatId, next);
        }
    }

    // The hub menu the player was playing is closed, so the host can offer it again.
    public void NotifyHubMenuClosed(Player player)
    {
        if (player.seatId >= 0)
        {
            CmdHubMenuClosed(player.seatId);
        }
    }

    // Asks the host to bring the rest of the party to the hub, leaving this player's character where it is.
    public void RequestRecallParty(Player player, CharacterHub hub, Character who)
    {
        if (player.seatId >= 0)
        {
            CmdRecallParty(player.seatId, NetIdOfObject(hub), NetIdOfObject(who));
        }
    }

    // Asks the host to make these characters the party. The host checks it again.
    public void RequestSetParty(Player player, CharacterHub hub, IList<Character> party)
    {
        if (player.seatId < 0)
        {
            return;
        }

        var ids = new uint[party.Count];

        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = NetIdOfObject(party[i]);
        }

        CmdSetParty(player.seatId, NetIdOfObject(hub), ids);
    }

    // The player has left the game: their seat goes back to the host.
    public void ReleaseSeat(Player player)
    {
        if (player.seatId >= 0)
        {
            CmdReleaseSeat(player.seatId);
        }

        player.seatId = -1;
        seatsAskedFor.Remove(player.id);
        lastSent.Remove(player.id);
    }

    [TargetRpc]
    private void TargetSeatGranted(NetworkConnectionToClient target, int playerId, int seatId)
    {
        var player = Player.FindById(playerId);

        if (player)
        {
            player.seatId = seatId;
        }
    }

    [TargetRpc]
    private void TargetSeatDenied(NetworkConnectionToClient target, int playerId)
    {
        Debug.LogWarning("The game is full, so there is no seat for this player.");

        var player = Player.FindById(playerId);

        if (player)
        {
            PlayerManager.RemovePlayer(player);
        }
    }
}
