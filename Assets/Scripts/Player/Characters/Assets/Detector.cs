using UnityEngine;

public enum DetectorType
{
    Wall,
    Floor,
    Corners
}

public class Detector : MonoBehaviour
{
    [Tooltip("Which kind of detector this is: Wall, Floor or Corners.")]
    public DetectorType type;
    [Tooltip("The direction it looks along, as an angle in degrees: 0 is up and it grows clockwise, so 90 is right.")]
    public float angle = -1;
    
    [Tooltip("What it found the last time it looked: whether it hit a tile, which one, how far away, and at what angle. For watching, not for setting.")]
    public TileDirection hit;
    private Vector2 direction;

    // Whoever carries the detector, who never finds himself.
    private Rigidbody2D ownBody;

    void Start()
    {
        ownBody = GetComponentInParent<Rigidbody2D>();
        UpdateVector();
    }

    void Update()
    {
        hit = Map.GetTilePositionInDirection(transform.position, direction, 20, ownBody);
        //print(hit);
    }

    public void UpdateVector()
    {
        direction = GameMath.GetVectorFromAngle(angle);
    }
}

