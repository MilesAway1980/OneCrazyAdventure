using Characters;
using UnityEngine;

// A little pin, drawn above a character that is pinned to a screen of its own by the group camera, so it can be told from the
// others. It is made and taken away by the GroupCamera.
//
// It isn't a child of the character: some of them roll or spin, and the pin would go round with them. It follows the top of the
// character's picture instead, and bobs gently so it catches the eye.
public class PinMarker : MonoBehaviour
{
    private const int Size = 32;
    private const float PixelsPerUnit = 32f;

    private static Sprite pinSprite;
    private static Sprite arrowSprite;

    private Character target;
    private SpriteRenderer targetPicture;
    private float bobOffset;
    private bool chooser;

    // Makes a pin for the character. A chooser is a yellow arrow instead, for the character a player is choosing to pin.
    public static PinMarker Create(Character character, float scale, int sortingOrder, bool chooser = false)
    {
        var marker = new GameObject($"{(chooser ? "Choice" : "Pin")} on {character.name}").AddComponent<PinMarker>();
        marker.chooser = chooser;
        marker.target = character;
        marker.bobOffset = Random.value * 6.28f;
        marker.transform.localScale = Vector3.one * scale;

        var picture = marker.gameObject.AddComponent<SpriteRenderer>();
        picture.sprite = chooser ? ArrowSprite() : PinSprite();
        picture.sortingOrder = sortingOrder;

        marker.Follow();
        return marker;
    }

    // Moves it on to another character.
    public void Retarget(Character character)
    {
        if (character == target)
        {
            return;
        }

        target = character;
        targetPicture = null;
        name = $"{(chooser ? "Choice" : "Pin")} on {character.name}";
        Follow();
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

    // The pin's point rests just above the top of the character's picture.
    private void Follow()
    {
        if (!targetPicture)
        {
            targetPicture = target.GetComponentInChildren<SpriteRenderer>();
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

        // The arrow floats higher and moves more, so it's seen over a pin.
        var bob = Mathf.Sin(Time.time * (chooser ? 6f : 4f) + bobOffset) * (chooser ? 0.12f : 0.05f);
        transform.position = new Vector3(top.x, top.y + (chooser ? 0.35f : 0.1f) + bob, target.transform.position.z);
        transform.rotation = Quaternion.identity;
    }

    // A yellow arrow pointing down, drawn once in code. Its point is the sprite's pivot, at the bottom.
    private static Sprite ArrowSprite()
    {
        if (arrowSprite)
        {
            return arrowSprite;
        }

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Choice arrow"
        };

        var pixels = new Color[Size * Size];

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var color = Color.clear;
                var dx = Mathf.Abs(x + 0.5f - 16f);

                // The point is at the bottom, the stem above it, and the head of the arrow is a triangle that is wide at the top.
                // A dark rim all round it, so it shows against light and dark backgrounds.
                var triangleHalfWidth = (y + 0.5f - 1f) * 0.55f;
                var inHead = y >= 1 && y < 17 && dx < triangleHalfWidth;
                var inStem = y >= 17 && y < 30 && dx < 4f;
                var rim = (y >= 0 && y < 18 && dx < triangleHalfWidth + 1.5f) || (y >= 16 && y < 31 && dx < 5.5f);

                if (inHead || inStem)
                {
                    color = new Color(1f, 0.85f, 0.15f, 1f);
                }
                else if (rim)
                {
                    color = new Color(0.35f, 0.25f, 0f, 1f);
                }

                pixels[y * Size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        arrowSprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0f), PixelsPerUnit);
        arrowSprite.name = "Choice arrow";
        return arrowSprite;
    }

    // A red-headed pin pointing down, drawn once in code so there's no picture to keep with the project. Its point is the
    // sprite's pivot, at the bottom.
    private static Sprite PinSprite()
    {
        if (pinSprite)
        {
            return pinSprite;
        }

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Pin"
        };

        var pixels = new Color[Size * Size];

        var head = new Vector2(16f, 23f);
        var shine = new Vector2(13f, 26f);

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                var color = Color.clear;

                // The needle, from the head down to the point.
                var needle = Mathf.Clamp01(Mathf.Min(1.5f - Mathf.Abs(point.x - 16f), (point.y - 1f) * 2f));

                if (point.y < 17f && needle > 0f)
                {
                    color = new Color(0.78f, 0.8f, 0.85f, needle);
                }

                // The head: a dark rim, a red face, and a small bright spot.
                var distance = Vector2.Distance(point, head);
                var rim = Mathf.Clamp01(9f - distance);

                if (rim > 0f)
                {
                    var face = distance < 7.5f ? new Color(0.9f, 0.15f, 0.15f) : new Color(0.45f, 0.05f, 0.05f);
                    color = new Color(face.r, face.g, face.b, rim);

                    if (Vector2.Distance(point, shine) < 2.2f)
                    {
                        color = new Color(1f, 0.85f, 0.85f, 1f);
                    }
                }

                pixels[y * Size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        pinSprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0f), PixelsPerUnit);
        pinSprite.name = "Pin";
        return pinSprite;
    }
}
