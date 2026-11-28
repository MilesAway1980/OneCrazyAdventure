using UnityEngine;
using UnityEngine.Rendering;

namespace Characters
{
    // Keeps the characters from fighting over which is drawn in front when they stand in the same place. With the same sorting layer and order, two
    // sprites at the same depth tie, and which is drawn first is left to chance, so they flicker. This gives each character a sorting order of his own:
    //
    //  - He's given a Sorting Group, which treats all of his pictures (his body, the Balloon's string, the Ladder's top piece, and the rest) as one, keeping
    //    their order among themselves. Only the group's order is set, so nothing about his prefab needs to change.
    //  - The group's order is the order of his own picture, plus one, plus a number that's different for every character, so every character is drawn in the
    //    same place in front of or behind every other, every time. The character a player is controlling is drawn in front of the others, so you can
    //    always see who you are.
    //  - If there's a sorting layer called "Characters" in Project Settings > Tags and Layers, he and all of his pictures are moved onto it. That's how the
    //    characters are kept in front of the scenery and behind the foreground without any of their orders having to be matched to theirs. Without one, he
    //    stays on the layer he's on.
    //
    // Added to every character by Character, so it's never put on a prefab. See Docs/CharacterSorting.md.
    public class CharacterSorting : MonoBehaviour, IKeepsRunningWhenShownOnly
    {
        // The sorting layer the characters go on, if there is one.
        public const string LayerName = "Characters";

        // Whether the character a player controls is drawn in front of the others.
        private const bool ControlledInFront = true;

        private static int nextSerial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            nextSerial = 0;
        }

        private Character character;
        private SortingGroup group;
        private int serial;
        private int baseOrder;
        private int applied = int.MinValue;

        // Puts one on the character, if he hasn't got one.
        public static void EnsureOn(Character target)
        {
            if (target && !target.GetComponent<CharacterSorting>())
            {
                target.gameObject.AddComponent<CharacterSorting>();
            }
        }

        void Awake()
        {
            character = GetComponent<Character>();
            serial = nextSerial++;

            group = GetComponent<SortingGroup>();

            if (!group)
            {
                group = gameObject.AddComponent<SortingGroup>();
            }

            var main = GetComponentInChildren<SpriteRenderer>();
            baseOrder = main ? main.sortingOrder : group.sortingOrder;

            var layer = FindLayer();

            if (layer.found)
            {
                // All of his pictures that were with the main one, so that what copies the layer of his picture (the pointer over his head, his tag) goes
                // with him.
                var was = main ? main.sortingLayerID : group.sortingLayerID;

                foreach (var picture in GetComponentsInChildren<Renderer>(true))
                {
                    if (picture.sortingLayerID == was)
                    {
                        picture.sortingLayerID = layer.id;
                    }
                }

                group.sortingLayerID = layer.id;
            }
            else if (main)
            {
                group.sortingLayerID = main.sortingLayerID;
            }

            Apply();
        }

        void LateUpdate()
        {
            Apply();
        }

        // His order: his own plus one plus his number, and in front of all of those if a player has him.
        private void Apply()
        {
            var order = baseOrder + 1 + serial;

            if (ControlledInFront && IsControlled())
            {
                order += Mathf.Max(1, nextSerial);
            }

            if (order != applied)
            {
                group.sortingOrder = order;
                applied = order;
            }
        }

        private bool IsControlled()
        {
            var players = PlayerManager.players;

            if (players == null || !character)
            {
                return false;
            }

            foreach (var player in players)
            {
                if (player && player.character == character)
                {
                    return true;
                }
            }

            return false;
        }

        private static (bool found, int id) FindLayer()
        {
            foreach (var layer in SortingLayer.layers)
            {
                if (layer.name == LayerName)
                {
                    return (true, layer.id);
                }
            }

            return (false, 0);
        }
    }
}
