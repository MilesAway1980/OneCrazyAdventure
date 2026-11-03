using UnityEngine;

// Marks something on the Ground layer that characters can push, such as the Brick while it's solid. Characters stop
// walking when they sense a wall close ahead, which would stop them reaching anything they were meant to push; they
// skip over anything with this enabled. It does nothing else: the object itself needs a dynamic Rigidbody2D.
//
// Give the object's Rigidbody2D a zero-friction material, or whatever stands on it will drag it along as they walk.
public class PushableSolid : MonoBehaviour
{
}
