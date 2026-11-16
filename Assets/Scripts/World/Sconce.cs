using UnityEngine;
using UnityEngine.Serialization;

// A wall sconce: its sprite shows whether its light is lit. The light itself, how long it burns and how it looks lit
// and unlit are all the LightTileSource's.
[RequireComponent(typeof(SpriteRenderer))]
public class Sconce : MonoBehaviour
{
    public Sprite unlitSprite;
    public Sprite litSprite;

    [Tooltip("The light of this sconce. It is lit and snuffed out through the sconce, or by itself when its time is up, and the sprite follows.")]
    public LightTileSource lightTileSource;

    private SpriteRenderer spriteRenderer;
    private bool shownLit;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (!lightTileSource)
        {
            lightTileSource = GetComponent<LightTileSource>();
        }

        if (!lightTileSource)
        {
            Debug.LogWarning($"{name}: a Sconce needs a LightTileSource.", this);
            return;
        }

        ShowLit(lightTileSource.lit);
    }

    void Update()
    {
        if (lightTileSource && lightTileSource.lit != shownLit)
        {
            ShowLit(lightTileSource.lit);
        }
    }

    public void Light()
    {
        if (lightTileSource)
        {
            lightTileSource.Light();
        }
    }

    public void Snuff()
    {
        if (lightTileSource)
        {
            lightTileSource.Snuff();
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
