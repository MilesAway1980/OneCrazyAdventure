using UnityEngine;

public enum DetectorType
{
    Wall,
    Floor,
    Corners
}

public class Detector : MonoBehaviour
{
    public DetectorType type;
    public float angle = -1;
    
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

