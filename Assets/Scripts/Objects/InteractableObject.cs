using System.Collections.Generic;
using Characters;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public virtual bool Interact(Character interacter)
    {
        return false;
    }

    // What this switches on and off, for the arrows drawn to them in the scene view. Nothing, unless it's something that switches things.
    protected virtual IEnumerable<SwitchableObject> SwitchTargets
    {
        get { return null; }
    }

    // Whether the arrows to the things are numbered, in the order they're switched.
    protected virtual bool NumberSwitchTargets
    {
        get { return false; }
    }

    // An arrow from here to each of the things this switches, in the scene view while this is selected.
    protected void DrawSwitchArrows()
    {
        SwitchLinkGizmo.Draw(transform.position, SwitchTargets, NumberSwitchTargets);
    }

    void OnDrawGizmosSelected()
    {
        DrawSwitchArrows();
    }
}