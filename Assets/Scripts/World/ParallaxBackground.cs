using System;
using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public Vector2 parallaxEffect = Vector2.one;
    public float buffer = 1.01f;
    //private Vector3 cameraStartPosition;
    private Camera cam;
    private SpriteRenderer[] spriteRenderers;
    private Vector3 scale;
    //private Vector2 totalOffset;
    private float originalZDistance; 
    
    /*
     *       1
     *   8   |   2
     *    \     /
     * 7 -   0    - 3
     *    /     \
     *   6   |   4
     *       5
     */
    
    void Start()
    {
        cam = GetComponentInParent<Camera>();
        
        spriteRenderers = new SpriteRenderer[9];
        spriteRenderers[0] = GetComponentInChildren<SpriteRenderer>();
        
        scale = spriteRenderers[0].transform.localScale;
        originalZDistance = spriteRenderers[0].transform.localPosition.z;
        
        var sortingOrder = spriteRenderers[0].sortingOrder;
        var layer = spriteRenderers[0].gameObject.layer;
        spriteRenderers[0].name = "Renderer - Center";

        for (var i = 1; i <= 8; i++)
        {
            var spriteObject = new GameObject();
            spriteObject.transform.SetParent(transform);
            switch (i)
            {
                case 1: spriteObject.name = "Renderer - U"; break;
                case 2: spriteObject.name = "Renderer - UR"; break;
                case 3: spriteObject.name = "Renderer - R"; break;
                case 4: spriteObject.name = "Renderer - DR"; break;
                case 5: spriteObject.name = "Renderer - D"; break;
                case 6: spriteObject.name = "Renderer - DL"; break;
                case 7: spriteObject.name = "Renderer - L"; break;
                case 8: spriteObject.name = "Renderer - UL"; break;
            }

            spriteRenderers[i] = spriteObject.AddComponent<SpriteRenderer>();
            spriteRenderers[i].sprite = spriteRenderers[0].sprite;
            spriteRenderers[i].transform.localScale = new Vector3(scale.x, scale.y, scale.z);
            spriteRenderers[i].sortingOrder = sortingOrder;
            spriteRenderers[i].gameObject.layer = layer;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        for (int i = 0; i < 9; i++)
        {
            var offset = Vector2.zero;
            switch (i)
            {
                case 1: offset.y = scale.y; break;
                case 2: offset.x = scale.x; offset.y = scale.y; break;
                case 3: offset.x = scale.x; break;
                case 4: offset.x = scale.x; offset.y = -scale.y; break;
                case 5: offset.y = -scale.y; break;
                case 6: offset.x = -scale.x; offset.y = -scale.y; break;
                case 7: offset.x = -scale.x; break;
                case 8: offset.x = -scale.x; offset.y = scale.y; break;
            }

            float offsetX = (cam.transform.position.x * parallaxEffect.x) % scale.x;
            float offsetY = (cam.transform.position.y * parallaxEffect.y) % scale.y;
            
            var totalOffset = new Vector2(offsetX, offsetY);

            spriteRenderers[i].transform.localPosition = new Vector3(
                offset.x - totalOffset.x,
                offset.y - totalOffset.y,
                originalZDistance
            );
        }
    }
}
