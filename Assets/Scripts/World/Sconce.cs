using UnityEngine;
using UnityEngine.Serialization;

// A wall sconce: its sprite shows whether its light is lit. The light itself, how long it burns and how it looks lit
// and unlit are all the LightSource's.
[RequireComponent(typeof(SpriteRenderer))]
public class Sconce : MonoBehaviour
{
    public Sprite unlitSprite;
    public Sprite litSprite;

    [Tooltip("The light of this sconce. It is lit and snuffed out through the sconce, or by itself when its time is up, and the sprite follows.")]
    [FormerlySerializedAs("lightTileSource")]
    public LightSource lightSource;

    private SpriteRenderer spriteRenderer;
    private bool shownLit;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (!lightSource)
        {
            lightSource = GetComponent<LightSource>();
        }

        if (!lightSource)
        {
            Debug.LogWarning($"{name}: a Sconce needs a LightSource.", this);
            return;
        }

        ShowLit(lightSource.lit);
    }

    void Update()
    {
        if (lightSource && lightSource.lit != shownLit)
        {
            ShowLit(lightSource.lit);
        }
    }

    public void Light()
    {
        if (lightSource)
        {
            lightSource.Light();
        }
    }

    public void Snuff()
    {
        if (lightSource)
        {
            lightSource.Snuff();
        }
    }

    private void ShowLit(bool lit)
    {
        shownLit = lit;

        var sprite = lit ? litSprite : unlitSprite;

        if (sprite)
        {
            spriteRenderer.sprite = sprite;
        }
    }
}
