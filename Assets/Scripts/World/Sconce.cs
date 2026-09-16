using UnityEngine;

public class Sconce : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public bool lit;
    public Sprite unlitSprite;
    public Sprite litSprite;

    public BrightTileSource BrightTileSourceOn;
    public BrightTileSource BrightTileSourceOff;

    public Color lightColorOn;
    public Color lightColorOff;

    public float timeUntilGoOut;
    
    private SpriteRenderer spriteRenderer;
    private float startTime;
    private bool oldLit;
    
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = lightColorOff;
    }

    void Update()
    {
        if (Time.realtimeSinceStartup - startTime > timeUntilGoOut)
        {
            Snuff();
        }

        /*if (lit != oldLit)
        {
            if (lit)
            {
                Light();
            }
            else
            {
                Snuff();
            }

            oldLit = lit;
        }*/
    }

    public void Light()
    {
        lit = true;
        BrightTileSourceOn.enabled = true;
        BrightTileSourceOff.enabled = false;
        spriteRenderer.color = lightColorOn;
        spriteRenderer.sprite = litSprite;
        startTime = Time.realtimeSinceStartup;
    }

    public void Snuff()
    {
        lit = false;
        BrightTileSourceOn.enabled = false;
        BrightTileSourceOff.enabled = true;
        spriteRenderer.color = lightColorOff;
        spriteRenderer.sprite = unlitSprite;
    }
}
