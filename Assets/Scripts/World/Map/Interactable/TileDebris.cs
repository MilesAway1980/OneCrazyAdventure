using UnityEngine;

// A piece of a broken tile. It falls away, and fades out over the last of its time before it is gone.
public class TileDebris : MonoBehaviour
{
    // How much of its time, from the end, it spends fading.
    private const float FadeFraction = 0.3f;

    private SpriteRenderer pieceRenderer;
    private Color startColor;
    private float born;
    private float lifetime;

    public void Begin(SpriteRenderer renderer, float seconds)
    {
        pieceRenderer = renderer;
        startColor = renderer.color;
        born = Time.time;
        lifetime = Mathf.Max(seconds, 0.1f);
    }

    void Update()
    {
        var age = Time.time - born;

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        var fadeStart = lifetime * (1f - FadeFraction);

        if (age > fadeStart)
        {
            var color = startColor;
            color.a = startColor.a * (1f - (age - fadeStart) / (lifetime - fadeStart));
            pieceRenderer.color = color;
        }
    }
}
