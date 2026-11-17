using UnityEngine;

// What a character is made of. Its density, against that of the liquid it's in, is whether it sinks or floats.
[CreateAssetMenu(menuName = "Movement/Material", fileName = "Material")]
public class CharacterMaterial : ScriptableObject
{
    [Tooltip("How dense it is, compared with water, which is 1. Under 1 floats (wood is about 0.6) and over 1 sinks (brick is about 1.9, steel about 7.8).")]
    [Min(0.0001f)]
    public float density = 1f;
}
