using UnityEngine;

// Marks something that can bring down a tile that crumbles, by standing on it or by touching it. Such a tile ignores
// anything without this, and anything that has it turned off. How much it weighs, for the tiles that go by weight, is the
// mass of its Rigidbody2D.
public class CanTriggerFloorCollapse : MonoBehaviour
{
    [Tooltip("Whether standing on a floor that crumbles counts towards bringing it down. Turn it off for something that is " +
             "light enough, or that should be able to cross, such as a ghost.")]
    public bool triggersCollapse = true;

    private Rigidbody2D body;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    // How much it adds to the weight on the floor.
    public float Weight
    {
        get
        {
            if (!body)
            {
                body = GetComponent<Rigidbody2D>();
            }

            return body ? body.mass : 0f;
        }
    }

    // Characters, enemies and crates are given this when they start, unless they have it already, so that they count without
    // being set up one by one. Adding it to one by hand with it turned off stops that one counting.
    public static void EnsureOn(GameObject target)
    {
        if (!target.TryGetComponent<CanTriggerFloorCollapse>(out _))
        {
            target.AddComponent<CanTriggerFloorCollapse>();
        }
    }
}
