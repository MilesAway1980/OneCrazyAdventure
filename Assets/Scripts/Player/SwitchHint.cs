using System.Collections.Generic;
using Characters;
using UnityEngine;

// A short hint of which character a player has just taken over, so it can be found among others that are close by: a small triangle
// that points down at the character, fading in and out above its head for about a second.
//
// It's for a game with one player in it. When there are several, every character a player controls has a PlayerTag (P1, P2...) above
// it all the time instead, which says more, so this isn't shown.
//
// It's made by the Player, on the machine the player is sitting at (including one that has joined someone else's game), when the
// character they control changes. It isn't part of the character, so a character that spins or rolls doesn't spin it, and nothing
// that looks over the character's own parts sees it. It follows the character, and takes itself away when it's done.
[DefaultExecutionOrder(1000)]
public class SwitchHint : MonoBehaviour
{
    // How long the whole thing lasts, and how long it takes to fade in and out of that.
    private const float Seconds = 1f;
    private const float FadeIn = 0.2f;
    private const float FadeOut = 0.35f;

    // Its size in tiles, how far above the character's head its tip is, and how far it bobs.
    private const float PointerScale = 0.7f;
    private const float PointerGap = 0.15f;
    private const float PointerBob = 0.06f;

    // Drawn in front of everything else on the character's layer, including the camera's pins.
    private const int SortingOrderAbove = 520;

    private const int Size = 32;
    private const float PixelsPerUnit = 32f;

    private static Sprite triangleSprite;
    private static readonly Dictionary<Character, SwitchHint> active = new Dictionary<Character, SwitchHint>();

    private Character target;
    private SpriteRenderer mainPicture;
    private SpriteRenderer pointer;
    private float started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        active.Clear();
        triangleSprite = null;
    }

    // Starts the hint on a character. If it's already showing one, that one starts again.
    public static SwitchHint Show(Character character)
    {
        if (!character)
        {
            return null;
        }

        if (active.TryGetValue(character, out var existing) && existing)
        {
            existing.Cancel();
        }

        var hint = new GameObject($"Switch hint on {character.name}").AddComponent<SwitchHint>();
        hint.Begin(character);
        active[character] = hint;

        return hint;
    }

    // Takes it away at once, if the player moves on before it has finished.
    public void Cancel()
    {
        Destroy(gameObject);
    }

    private void Begin(Character character)
    {
        target = character;
        started = Time.unscaledTime;
        mainPicture = target.GetComponentInChildren<SpriteRenderer>();

        pointer = gameObject.AddComponent<SpriteRenderer>();
        pointer.sprite = TriangleSprite();
        pointer.color = new Color(1f, 1f, 1f, 0f);
        pointer.sortingOrder = SortingOrderAbove;
        transform.localScale = Vector3.one * PointerScale;

        if (mainPicture)
        {
            pointer.sortingLayerID = mainPicture.sortingLayerID;
        }

        Follow();
    }

    // After everything has moved, so it's where the character is.
    void LateUpdate()
    {
        var elapsed = Time.unscaledTime - started;

        if (!target || elapsed >= Seconds)
        {
            Destroy(gameObject);
            return;
        }

        Follow();

        var alpha = Mathf.Clamp01(Mathf.Min(elapsed / FadeIn, (Seconds - elapsed) / FadeOut));
        pointer.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, alpha));
    }

    // The tip rests just above the top of the character's picture, and bobs a little.
    private void Follow()
    {
        if (!target)
        {
            return;
        }

        if (!mainPicture)
        {
            mainPicture = target.GetComponentInChildren<SpriteRenderer>();
        }

        Vector2 top;

        if (mainPicture)
        {
            var bounds = mainPicture.bounds;
            top = new Vector2(bounds.center.x, bounds.max.y);
        }
        else
        {
            top = (Vector2)target.transform.position + Vector2.up * 0.5f;
        }

        var bob = Mathf.Sin((Time.unscaledTime - started) * 8f) * PointerBob;

        transform.position = new Vector3(top.x, top.y + PointerGap + bob, target.transform.position.z);
        transform.rotation = Quaternion.identity;
    }

    void OnDestroy()
    {
        if (target && active.TryGetValue(target, out var current) && current == this)
        {
            active.Remove(target);
        }
    }

    // A white triangle pointing down, with a dark rim so it shows against light and dark backgrounds, drawn once in code. Its point is
    // the sprite's pivot, at the bottom.
    private static Sprite TriangleSprite()
    {
        if (triangleSprite)
        {
            return triangleSprite;
        }

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Switch hint triangle"
        };

        // The corners, going anticlockwise: top left, point, top right.
        Vector2[] corners = { new Vector2(5f, 26f), new Vector2(16f, 3f), new Vector2(27f, 26f) };
        var pixels = new Color[Size * Size];

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);

                // How far inside the triangle the point is, in pixels: the shortest distance to any of its three sides.
                var inside = float.MaxValue;

                for (var i = 0; i < 3; i++)
                {
                    var from = corners[i];
                    var to = corners[(i + 1) % 3];
                    var side = to - from;
                    var distance = (side.x * (point.y - from.y) - side.y * (point.x - from.x)) / side.magnitude;

                    inside = Mathf.Min(inside, distance);
                }

                var rim = Mathf.Clamp01(inside + 1f);
                var face = Mathf.Clamp01(inside - 1f);
                var shade = Mathf.Lerp(0.15f, 1f, face);

                pixels[y * Size + x] = rim <= 0f ? Color.clear : new Color(shade, shade, shade, rim);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        triangleSprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0f), PixelsPerUnit);
        triangleSprite.name = "Switch hint triangle";
        return triangleSprite;
    }
}
