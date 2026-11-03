using UnityEngine;

// Shows a float as a dial in the inspector, with the angle in degrees from 0 up to 360. As with GameMath, 0 points up
// and the angle grows clockwise: 90 is right, 180 is down and 270 is left. Hold Shift while dragging to snap to 15°.
public class AngleDialAttribute : PropertyAttribute
{
}
