using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterScriptables", menuName = "Characters/CharacterScriptables")]
public class CharacterScriptables : ScriptableObject
{
    public int maxCharactersAtOnce = 4;

    public bool
        ladder,
        bowlingBall,
        pebble,
        suctionCup,
        magnet,
        umbrella,
        piton,
        torch,
        brick,
        bulb,
        dynamite,
        balloon,
        brad,
        pencil,
        paperclip,
        die,
        bouncyBall;

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
