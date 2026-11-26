using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterScriptables", menuName = "Characters/CharacterScriptables")]
public class CharacterScriptables : ScriptableObject
{
    [Tooltip("The most characters that can be in a party at once.")]
    public int maxCharactersAtOnce = 4;

    public bool
        balloon,
        bouncyBall,
        bowlingBall,
        brick,
        bulb,
        die,
        drill,
        dynamite,
        flashlight,
        ladder,
        magnet,
        paperclip,
        pebble,
        pencil,
        piton,
        suctionCup,
        torch,
        umbrella,
        zipper;

    //public List<bool> generic;        //TODO make this so everyone can be this character

    public int GetMaxCharactersAtOnce()
    {
        return maxCharactersAtOnce;
    }

    public void SetMaxCharactersAtOnce(int newMax)
    {
        if (newMax > 0)
        {
            maxCharactersAtOnce = newMax;
        }
    }
}
