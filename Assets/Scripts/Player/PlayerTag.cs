using Characters;
using UnityEngine;

// A small "P1", "P2", "P3" or "P4" above a character, showing which player is controlling it. PlayerTags makes and takes away one for
// every character a player controls, when there is more than one player in the game.
//
// It isn't a child of the character: some of them roll or spin, and the tag would go round with them. It follows the top of the
// character's picture instead. The letters are drawn once in code, in a small pixel font, so there's no font or picture to keep with
// the project.
[DefaultExecutionOrder(1000)]
public class PlayerTag : MonoBehaviour
{
    // Each player has a colour of their own, as well as a number.
    private static readonly Color[] PlayerColors =
    {
        new Color(1f, 0.38f, 0.32f),
        new Color(0.4f, 0.65f, 1f),
        new Color(0.45f, 0.9f, 0.4f),
        new Color(1f, 0.85f, 0.25f)
    };

    // Each letter is five across and seven down.
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int PixelSize = 2;
    private const int Pad = 2;
    private const float PixelsPerUnit = 32f;

    // Its size in tiles, how far above the character's head it floats, and what it's drawn in front of.
    private const float TagScale = 0.7f;
    private const float Gap = 0.12f;
    private const int SortingOrder = 510;

    private static readonly string[][] Glyphs =
    {
        // P
        new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
        // 1
        new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        // 2
        new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        // 3
        new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
        // 4
        new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." }
    };

    private static readonly Sprite[] sprites = new Sprite[4];

    private Character target;
    private SpriteRenderer targetPicture;
    private SpriteRenderer picture;
    private int number;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        for (var i = 0; i < sprites.Length; i++)
        {
            sprites[i] = null;
        }
    }

    // Makes a tag on the character, for the player with this number, from 1 to 4.
    public static PlayerTag Create(Character character, int playerNumber)
    {
        var made = new GameObject($"Player tag on {character.name}").AddComponent<PlayerTag>();
        made.target = character;
        made.picture = made.gameObject.AddComponent<SpriteRenderer>();
        made.picture.sortingOrder = SortingOrder;
        made.transform.localScale = Vector3.one * TagScale;
        made.SetNumber(playerNumber);
        made.Follow();

        return made;
    }

    // Changes it to show another player's number, when the character has changed hands.
    public void SetNumber(int playerNumber)
    {
        playerNumber = Mathf.Clamp(playerNumber, 1, sprites.Length);

        if (playerNumber == number)
        {
            return;
        }

        number = playerNumber;
        picture.sprite = SpriteFor(number);
    }

    void LateUpdate()
    {
        if (!target)
        {
            Destroy(gameObject);
            return;
        }

        Follow();
    }

    // The tag rests just above the top of the character's picture.
    private void Follow()
    {
        if (!targetPicture)
        {
            targetPicture = target.GetComponentInChildren<SpriteRenderer>();

            if (targetPicture)
            {
                picture.sortingLayerID = targetPicture.sortingLayerID;
            }
        }

        Vector2 top;

        if (targetPicture)
        {
            var bounds = targetPicture.bounds;
            top = new Vector2(bounds.center.x, bounds.max.y);
        }
        else
        {
            top = (Vector2)target.transform.position + Vector2.up * 0.5f;
        }

        transform.position = new Vector3(top.x, top.y + Gap, target.transform.position.z);
        transform.rotation = Quaternion.identity;
    }

    // The tag for a player, drawn once and kept: the letter P and their number, in their colour, with a dark rim so it can be read over
    // light and dark. Its bottom middle is the sprite's pivot.
    private static Sprite SpriteFor(int playerNumber)
    {
        var index = playerNumber - 1;

        if (sprites[index])
        {
            return sprites[index];
        }

        var letters = new[] { Glyphs[0], Glyphs[playerNumber] };
        var inkWidth = (GlyphWidth * 2 + 1) * PixelSize;
        var inkHeight = GlyphHeight * PixelSize;
        var width = inkWidth + Pad * 2;
        var height = inkHeight + Pad * 2;

        // Where there's ink, as the letters' squares at the size they're drawn.
        var ink = new bool[width, height];

        for (var letter = 0; letter < letters.Length; letter++)
        {
            for (var row = 0; row < GlyphHeight; row++)
            {
                for (var column = 0; column < GlyphWidth; column++)
                {
                    // The first row of the text is the top of the letter.
                    if (letters[letter][GlyphHeight - 1 - row][column] != '#')
                    {
                        continue;
                    }

                    for (var dy = 0; dy < PixelSize; dy++)
                    {
                        for (var dx = 0; dx < PixelSize; dx++)
                        {
                            var x = Pad + (letter * (GlyphWidth + 1) + column) * PixelSize + dx;
                            var y = Pad + row * PixelSize + dy;

                            ink[x, y] = true;
                        }
                    }
                }
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = $"Player tag P{playerNumber}"
        };

        var pixels = new Color[width * height];
        var face = PlayerColors[index];
        var rim = new Color(0.1f, 0.1f, 0.12f, 1f);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (ink[x, y])
                {
                    pixels[y * width + x] = face;
                    continue;
                }

                // Next to ink, within the width of the rim, is rim.
                var near = false;

                for (var dy = -1; dy <= 1 && !near; dy++)
                {
                    for (var dx = -1; dx <= 1 && !near; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;

                        near = nx >= 0 && ny >= 0 && nx < width && ny < height && ink[nx, ny];
                    }
                }

                pixels[y * width + x] = near ? rim : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        sprites[index] = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), PixelsPerUnit);
        sprites[index].name = $"Player tag P{playerNumber}";
        return sprites[index];
    }
}
