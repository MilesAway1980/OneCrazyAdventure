using UnityEngine;
using Characters;

public class CharacterSelection : MonoBehaviour
{
    [Tooltip("Whether this character is chosen for the party.")]
    public bool selected;
    [Tooltip("The character this chooses.")]
    public Character character;

    private static CharacterSelecter mainSelecter;
    private bool previousSelection;

    void Start()
    {
        if (!mainSelecter)
        {
            mainSelecter = GetComponent<CharacterSelecter>();
        }

        previousSelection = selected;

        character.SetInGame(selected);
    }

    // Puts the character in the party or takes them out of it straight away, and keeps the change from being seen again by
    // Update below, which looks after the same thing for changes made to the checkbox in the inspector.
    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        previousSelection = isSelected;
        character.SetInGame(isSelected);
    }

    void Update()
    {
        if (previousSelection == selected)
        {
            return;
        }

        if (!selected)
        {
            selected = !mainSelecter.RemoveCharacter(character);
        }
        else
        {
            selected = mainSelecter.AddCharacter(character);
        }
        
        character.SetInGame(selected);
        previousSelection = selected;
    }
}
