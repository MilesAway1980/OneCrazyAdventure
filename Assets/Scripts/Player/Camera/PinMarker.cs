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

    private Character target;
    private SpriteRenderer targetPicture;
    private float bobOffset;

    // Makes a pin for the character.
    public static PinMarker Create(Character character, float scale, int sortingOrder)
    {
        var marker = new GameObject($"Pin on {character.name}").AddComponent<PinMarker>();
        marker.target = character;
        marker.bobOffset = Random.value * 6.28f;
        marker.transform.localScale = Vector3.one * scale;

        var picture = marker.gameObject.AddComponent<SpriteRenderer>();
        picture.sprite = PinSprite();
        picture.sortingOrder = sortingOrder;

        marker.Follow();
        return marker;
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

        var bob = Mathf.Sin(Time.time * 4f + bobOffset) * 0.05f;
        transform.position = new Vector3(top.x, top.y + 0.1f + bob, target.transform.position.z);
        transform.rotation = Quaternion.identity;
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
