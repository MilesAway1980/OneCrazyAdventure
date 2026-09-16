using UnityEngine;
using Characters;

public class IceZone : MonoBehaviour
{
    public SpriteRenderer showAreaSprite;
    public bool disableAreaSpriteOnStartup;
    
    [Range(0, 100)]
    public float maxFreeze;

    public float secondsUntilFrozen;
    public float secondsUntilThawed;

    void Start()
    {
        if (disableAreaSpriteOnStartup)
        {
            showAreaSprite.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerStay2D(Collider2D other)
    {
        var character = other.GetComponent<Character>();
        if (character)
        {
            var characterFreeze = character.GetComponent<CharacterFreeze>();
            if (!characterFreeze)
            {
                characterFreeze = character.gameObject.AddComponent<CharacterFreeze>();
            }
            
            characterFreeze.SetMaxFreeze(maxFreeze);
            characterFreeze.SetThawSeconds(secondsUntilThawed);
            characterFreeze.SetFreezeSeconds(secondsUntilFrozen);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        
    }
}