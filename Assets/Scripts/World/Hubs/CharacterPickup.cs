using Characters;
using UnityEngine;

// Where a character is found. It shows the character, bobbing a little, and when anyone in the party touches it, that
// character has been found: they are added to the characters that can be chosen at a hub, and wait there to be swapped in.
// They don't join the party where they are found, so there is never a character in the party who can't get back out of
// where they were found.
//
// Put it in the scene with a trigger collider where the character is to be found. The character itself is the one that's
// already in the scene, switched off.
public class CharacterPickup : MonoBehaviour
{
    [Tooltip("The character that is found here. It's the one that's in the scene, which doesn't need to be anywhere near.")]
    public Character character;

    [Tooltip("What is drawn where the character is found. If it's empty, the character's own walking picture is used.")]
    public Sprite picture;

    [Tooltip("How big the picture is, as a multiple of its own size. This is only the picture: it doesn't change the area that is touched to find the character, and it isn't changed by how this object is scaled. 1 is the picture's own size.")]
    public Vector2 pictureScale = Vector2.one;

    [Tooltip("How far, in units, the picture bobs up and down.")]
    public float bobHeight = 0.15f;

    [Tooltip("How many times a second it bobs.")]
    public float bobSpeed = 0.7f;

    private SpriteRenderer pictureRenderer;
    private Vector3 pictureHome;
    private bool collected;

    void Reset()
    {
        // Something to touch, if there isn't anything yet.
        if (!GetComponent<Collider2D>())
        {
            var circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.5f;
        }
    }

    void Start()
    {
        var touchArea = GetComponent<Collider2D>();

        if (touchArea)
        {
            touchArea.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"{name}: the Character Pickup has no collider, so nobody can find the character.", this);
        }

        if (!character)
        {
            Debug.LogWarning($"{name}: the Character Pickup has no character to find.", this);
            return;
        }

        // Found already, such as a character that was found from the start: there's nothing here to find.
        if (CharacterSelecter.Instance && CharacterSelecter.Instance.IsOwned(character))
        {
            gameObject.SetActive(false);
            return;
        }

        var sprite = picture ? picture : character.walkSprite;

        if (sprite)
        {
            // A picture of its own, so that it can bob without moving what is touched.
            var pictureObject = new GameObject("Picture");
            pictureObject.transform.SetParent(transform, false);

            pictureRenderer = pictureObject.AddComponent<SpriteRenderer>();
            pictureRenderer.sprite = sprite;
            pictureHome = pictureObject.transform.localPosition;
        }
    }

    void Update()
    {
        if (!pictureRenderer)
        {
            return;
        }

        // What the picture inherits from this object, which its own size and how far it bobs are taken from, so that neither
        // depends on how this object is scaled. Every frame, so it can be changed while playing.
        var inherited = transform.lossyScale;

        var offset = Mathf.Sin(Time.time * bobSpeed * 2f * Mathf.PI) * bobHeight;
        var lift = Mathf.Abs(inherited.y) > 0.0001f ? offset / inherited.y : offset;

        pictureRenderer.transform.localPosition = pictureHome + Vector3.up * lift;

        pictureRenderer.transform.localScale = new Vector3(
            Mathf.Abs(inherited.x) > 0.0001f ? pictureScale.x / inherited.x : pictureScale.x,
            Mathf.Abs(inherited.y) > 0.0001f ? pictureScale.y / inherited.y : pictureScale.y,
            1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        var selecter = CharacterSelecter.Instance;

        if (collected || !character || !selecter)
        {
            return;
        }

        var finder = other.GetComponentInParent<Character>();

        // Only whoever is in the game: the party.
        if (!finder || !finder.isActiveAndEnabled || !selecter.IsInParty(finder))
        {
            return;
        }

        collected = true;

        if (selecter.Unlock(character))
        {
            GameMessages.Show(GameText.Fill(GameText.Current.characterFound, character: character.name));
        }

        gameObject.SetActive(false);
    }
}
