using UnityEngine;
using Characters;

public class CharacterSelection : MonoBehaviour
{
    public bool selected;
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

        character.gameObject.SetActive(selected);
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
        
        character.gameObject.SetActive(selected);
        previousSelection = selected;
    }
}
