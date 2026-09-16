using Characters;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public virtual bool Interact(Character interacter)
    {
        return false;
    }
}