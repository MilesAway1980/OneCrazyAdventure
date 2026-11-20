using UnityEditor;

// Tools > Gizmos > Show Switch Arrows: turns the arrows from levers and buttons to the things they switch on and off, in the scene view.
public static class SwitchLinkMenu
{
    private const string Path = "Tools/Gizmos/Show Switch Arrows";

    [MenuItem(Path)]
    private static void Toggle()
    {
        SwitchLinkGizmo.Enabled = !SwitchLinkGizmo.Enabled;
        SceneView.RepaintAll();
    }

    [MenuItem(Path, true)]
    private static bool Validate()
    {
        Menu.SetChecked(Path, SwitchLinkGizmo.Enabled);
        return true;
    }
}
