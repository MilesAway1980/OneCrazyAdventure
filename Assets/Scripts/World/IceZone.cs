using UnityEngine;
using Characters;

public class IceZone : MonoBehaviour
{
    [Tooltip("A picture that shows where the zone is. It's not needed for it to work.")]
    public SpriteRenderer showAreaSprite;
    [Tooltip("Hides the area picture when the game starts.")]
    public bool disableAreaSpriteOnStartup;
    
    [Range(0, 100)]
    [Tooltip("The most a character can freeze in here, as a percentage: 100 freezes them solid.")]
    public float maxFreeze;

    [Tooltip("How long, in seconds, a character has to be in here to freeze all the way to Max Freeze.")]
    public float secondsUntilFrozen;
    [Tooltip("How long, in seconds, after they have left it takes a fully frozen character to thaw out.")]
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