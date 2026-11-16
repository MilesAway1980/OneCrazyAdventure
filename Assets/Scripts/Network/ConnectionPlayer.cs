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

    void Update()
    {
        if (isServer)
        {
            UpdateSeats();
        }
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

        foreach (var pair in new List<KeyValuePair<int, int>>(seatOfPlayer))
        {
            if (pair.Value == seatId)
            {
                seatOfPlayer.Remove(pair.Key);
            }
        }
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
