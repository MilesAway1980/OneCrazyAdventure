using System.Collections.Generic;
using System.Linq;
using Characters;
using UnityEngine;

// The menu a hub opens. The player who opened it plays it with their usual controls, and their character stands still until
// it's closed:
//   Up and down, or left and right, move round. Jump picks. In the swap, Action is done, and Interact goes back.
//
// There are two pages: the first has Swap Characters, Bring Everyone Here and Close. The swap shows the characters that
// have been found, and the party being picked. Picking a character puts them in the party, or takes them out, up to the most at
// once, and at least one. Done makes it the party, and puts all of them at the hub.
//
// It's made the first time it's needed. Only one can be open at a time.
public class HubMenu : MonoBehaviour
{
    private enum Page
    {
        Main,
        Swap
    }

    // How many choices the first page has, in the order they are listed: swap, bring everyone, hints, close. Their words are
    // in the master text.
    private const int MainItemCount = 4;

    private static string MainItem(int index)
    {
        var text = GameText.Current;

        switch (index)
        {
            case 0:
                return text.swapCharacters;

            case 1:
                return text.bringEveryoneHere;

            case 2:
                return TutorialHints.Enabled ? text.hintsOn : text.hintsOff;

            default:
                return text.closeMenu;
        }
    }

    // How many characters are in a row of the roster, and how long, in seconds, before holding a direction moves again.
    private const int Columns = 6;
    private const float FirstRepeatSeconds = 0.35f;
    private const float RepeatSeconds = 0.12f;
    private const float StickThreshold = 0.5f;

    private static HubMenu instance;

    private bool open;
    private Page page;
    private CharacterHub hub;
    private Player operatorPlayer;
    private Character initiator;

    private int mainCursor;
    private int rosterCursor;
    private readonly List<Character> roster = new List<Character>();
    private readonly List<Character> candidate = new List<Character>();

    // The party members who are away from the hub when the swap is opened. They can't be swapped out, as they aren't here.
    private readonly HashSet<Character> away = new HashSet<Character>();

    private Vector2Int lastDirection;
    private float nextRepeat;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;

    public static bool IsOpen
    {
        get { return instance && instance.open; }
    }

    public static bool IsOpenFor(Player player)
    {
        return IsOpen && instance.operatorPlayer == player;
    }

    public static void Open(CharacterHub hub, Player player, Character initiator)
    {
        if (!instance)
        {
            instance = new GameObject("Hub Menu").AddComponent<HubMenu>();
        }

        instance.Begin(hub, player, initiator);
    }

    // The buttons the player pressed this frame, and where they are holding the stick. Called every frame by the player's
    // controls while it's open for them.
    public static void Feed(Player player, Vector2 move, bool pick, bool done, bool back)
    {
        if (IsOpenFor(player))
        {
            instance.Handle(move, pick, done, back);
        }
    }

    private void Begin(CharacterHub atHub, Player player, Character who)
    {
        hub = atHub;
        operatorPlayer = player;
        initiator = who;
        page = Page.Main;
        mainCursor = 0;
        lastDirection = Vector2Int.zero;
        nextRepeat = 0f;
        open = true;
    }

    private void Close()
    {
        open = false;
        operatorPlayer = null;
    }

    private void Handle(Vector2 move, bool pick, bool done, bool back)
    {
        var step = ReadStep(move);

        if (page == Page.Main)
        {
            HandleMain(step, pick, back);
        }
        else
        {
            HandleSwap(step, pick, done, back);
        }
    }

    // A step in one direction, from where the stick is held, which repeats while it is held.
    private Vector2Int ReadStep(Vector2 move)
    {
        if (move.magnitude < StickThreshold)
        {
            lastDirection = Vector2Int.zero;
            return Vector2Int.zero;
        }

        var direction = Mathf.Abs(move.x) > Mathf.Abs(move.y)
            ? new Vector2Int(move.x > 0f ? 1 : -1, 0)
            : new Vector2Int(0, move.y > 0f ? 1 : -1);

        if (direction != lastDirection)
        {
            lastDirection = direction;
            nextRepeat = Time.unscaledTime + FirstRepeatSeconds;
            return direction;
        }

        if (Time.unscaledTime >= nextRepeat)
        {
            nextRepeat = Time.unscaledTime + RepeatSeconds;
            return direction;
        }

        return Vector2Int.zero;
    }

    private void HandleMain(Vector2Int step, bool pick, bool back)
    {
        // Up is up the list.
        mainCursor = (mainCursor - step.y + MainItemCount) % MainItemCount;

        if (back)
        {
            Close();
            return;
        }

        if (!pick)
        {
            return;
        }

        switch (mainCursor)
        {
            case 0:
                StartSwap();
                break;

            case 1:
                hub.RecallParty(initiator);
                GameMessages.Show(GameText.Current.partyBroughtToHub);
                Close();
                break;

            case 2:
                // Stays on this page, so the button can be seen to change.
                TutorialHints.Enabled = !TutorialHints.Enabled;
                break;

            default:
                Close();
                break;
        }
    }

    private void StartSwap()
    {
        var selecter = CharacterSelecter.Instance;

        roster.Clear();
        candidate.Clear();
        away.Clear();

        if (selecter)
        {
            roster.AddRange(selecter.Owned.OrderBy(c => c.name));
            candidate.AddRange(selecter.Party);

            // Only the characters at the hub can be swapped. The rest are out in the field, and stay as they are.
            foreach (var member in selecter.Party)
            {
                if (!hub.IsAtHub(member))
                {
                    away.Add(member);
                }
            }
        }

        rosterCursor = 0;
        page = Page.Swap;
    }

    private void HandleSwap(Vector2Int step, bool pick, bool done, bool back)
    {
        var count = roster.Count;

        if (count > 0)
        {
            if (step.x != 0)
            {
                rosterCursor = (rosterCursor + step.x + count) % count;
            }

            if (step.y != 0)
            {
                // Down is down the rows, and the stick's up is the other way.
                rosterCursor = Mathf.Clamp(rosterCursor - step.y * Columns, 0, count - 1);
            }
        }

        if (back)
        {
            page = Page.Main;
            return;
        }

        var selecter = CharacterSelecter.Instance;

        if (pick && count > 0 && selecter)
        {
            var chosen = roster[rosterCursor];

            if (away.Contains(chosen))
            {
                // Out in the field, so there is nothing here to swap.
                GameMessages.Show(GameText.Fill(GameText.Current.cantSwapAway, character: chosen.name), 2f);
            }
            else if (candidate.Contains(chosen))
            {
                // Somebody has to stay at the hub, or whoever is playing here would have no character, and nobody out in the
                // field might be able to get back to it.
                if (CountAtHub() > 1)
                {
                    candidate.Remove(chosen);
                }
                else
                {
                    GameMessages.Show(GameText.Current.mustStayPickAnother, 2.5f);
                }
            }
            else if (candidate.Count < selecter.MaxParty)
            {
                candidate.Add(chosen);
            }
        }

        // Checked again before it's made the party, in case of anything that got past the picking.
        if (done && selecter && CountAtHub() < 1)
        {
            GameMessages.Show(GameText.Current.mustStay, 2.5f);
        }
        else if (done && selecter)
        {
            var before = selecter.Party.ToList();

            // The same characters as there were, in any order: nothing has changed, so there is nothing to do or to say.
            if (candidate.Count == before.Count && !candidate.Except(before).Any())
            {
                Close();
            }
            else if (selecter.SetParty(candidate))
            {
                // Only those who have just joined are put at the hub. Everyone else stays where they are, in the field or here.
                var newcomers = candidate.Where(c => !before.Contains(c)).ToList();
                var staying = candidate.Where(c => before.Contains(c) && !away.Contains(c)).ToList();

                hub.PlaceNewcomers(newcomers, staying);

                GameMessages.Show(GameText.Current.partyChanged);
                Close();
            }
        }
    }

    // How many of the characters in the party being picked are at the hub: those who are in it that weren't away from it. Newly
    // picked ones are put there, and so count.
    private int CountAtHub()
    {
        return candidate.Count(c => !away.Contains(c));
    }

    void OnGUI()
    {
        if (!open || Event.current.type != EventType.Repaint)
        {
            return;
        }

        var unit = Screen.height / 100f;

        MakeStyles(unit);

        // Darkens the game behind it.
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var title = new Rect(0f, unit * 6f, Screen.width, unit * 8f);
        GUI.Label(title, hub ? hub.hubName : "Hub", titleStyle);

        if (page == Page.Main)
        {
            DrawMain(unit);
        }
        else
        {
            DrawSwap(unit);
        }
    }

    private void MakeStyles(float unit)
    {
        if (titleStyle == null)
        {
            // No padding, and text that isn't cut off when it is taller than the space it is given: the default label style
            // pads its text, which left the bottom of it outside the box.
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0),
                clipping = TextClipping.Overflow
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0),
                clipping = TextClipping.Overflow
            };
        }

        titleStyle.fontSize = Mathf.RoundToInt(unit * 5f);
        labelStyle.fontSize = Mathf.RoundToInt(unit * 2.6f);
    }

    private void DrawMain(float unit)
    {
        var width = unit * 50f;
        var height = unit * 8f;
        var x = (Screen.width - width) * 0.5f;
        var y = unit * 30f;

        for (int i = 0; i < MainItemCount; i++)
        {
            var row = new Rect(x, y + i * (height + unit * 2f), width, height);

            DrawBox(row, i == mainCursor ? new Color(0.25f, 0.55f, 0.9f, 0.95f) : new Color(0.15f, 0.15f, 0.15f, 0.9f));
            GUI.Label(row, MainItem(i), labelStyle);
        }

        DrawHint(unit, GameText.Current.mainHint);
    }

    private void DrawSwap(float unit)
    {
        var selecter = CharacterSelecter.Instance;
        var max = selecter ? selecter.MaxParty : candidate.Count;

        // The party being picked.
        var slot = unit * 11f;
        var slotsWidth = max * slot + (max - 1) * unit;
        var slotX = (Screen.width - slotsWidth) * 0.5f;

        GUI.Label(new Rect(0f, unit * 15f, Screen.width, unit * 4f), GameText.Current.partyHeading, labelStyle);

        for (int i = 0; i < max; i++)
        {
            var box = new Rect(slotX + i * (slot + unit), unit * 20f, slot, slot);

            DrawBox(box, new Color(0.15f, 0.15f, 0.15f, 0.9f));

            if (i < candidate.Count)
            {
                DrawCharacter(box, candidate[i], unit, away.Contains(candidate[i]));
            }
        }

        // Everyone that has been found.
        var tile = unit * 11f;
        var gap = unit;
        var gridWidth = Columns * tile + (Columns - 1) * gap;
        var gridX = (Screen.width - gridWidth) * 0.5f;

        GUI.Label(new Rect(0f, unit * 36f, Screen.width, unit * 4f), GameText.Current.foundHeading, labelStyle);

        for (int i = 0; i < roster.Count; i++)
        {
            var box = new Rect(gridX + (i % Columns) * (tile + gap), unit * 41f + (i / Columns) * (tile + gap), tile, tile);
            var inParty = candidate.Contains(roster[i]);
            var locked = away.Contains(roster[i]);

            DrawBox(box, i == rosterCursor ? new Color(0.25f, 0.55f, 0.9f, 0.95f) : new Color(0.15f, 0.15f, 0.15f, 0.9f));
            DrawCharacter(box, roster[i], unit, locked);

            // A tick for the ones in the party.
            if (inParty && !locked)
            {
                // The last one at the hub can't be taken out until somebody else is picked, which is said here.
                var last = CountAtHub() <= 1;

                GUI.color = last ? new Color(1f, 0.9f, 0.3f) : new Color(0.4f, 1f, 0.4f);
                GUI.Label(new Rect(box.x, box.y, box.width, unit * 5f), last ? GameText.Current.staysHere : GameText.Current.inParty, labelStyle);
                GUI.color = Color.white;
            }
        }

        DrawHint(unit, GameText.Current.swapHint);
    }

    // A character's picture, if they have one, and their name under it. One that is away from the hub is dimmed and marked,
    // to show that they can't be swapped.
    private void DrawCharacter(Rect box, Character character, float unit, bool awayFromHub)
    {
        var sprite = character.walkSprite;

        if (awayFromHub)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
        }

        if (sprite && sprite.texture)
        {
            var rect = sprite.textureRect;
            var texture = sprite.texture;
            var coords = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            var picture = new Rect(box.x + box.width * 0.15f, box.y + box.height * 0.1f, box.width * 0.7f, box.height * 0.6f);

            GUI.DrawTextureWithTexCoords(picture, texture, coords);
        }

        GUI.Label(new Rect(box.x, box.y + box.height * 0.7f, box.width, unit * 4f), character.name, labelStyle);

        GUI.color = Color.white;

        if (awayFromHub)
        {
            // Over the middle of the picture, in a colour that stands out from the party's green.
            GUI.color = new Color(1f, 0.6f, 0.15f);
            GUI.Label(new Rect(box.x, box.y + box.height * 0.28f, box.width, unit * 5f), GameText.Current.awayFromHub, labelStyle);
            GUI.color = Color.white;
        }
    }

    private void DrawBox(Rect box, Color colour)
    {
        GUI.color = colour;
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawHint(float unit, string text)
    {
        GUI.Label(new Rect(0f, Screen.height - unit * 9f, Screen.width, unit * 5f), text, labelStyle);
    }
}
