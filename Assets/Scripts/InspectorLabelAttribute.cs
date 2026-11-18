using UnityEngine;

// Shows a different name for a field in the Inspector, without changing the field itself. Renaming a field in the code would lose
// what's been set on it in every scene and prefab that has it, so this is for when only the words need to change.
//
//     [InspectorLabel("Allow Partial Presses")]
//     public bool passOnAmount;
public class InspectorLabelAttribute : PropertyAttribute
{
    public readonly string label;

    public InspectorLabelAttribute(string label)
    {
        this.label = label;
    }
}
