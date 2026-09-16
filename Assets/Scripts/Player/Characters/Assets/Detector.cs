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

    void Start()
    {
        UpdateVector();
    }
    
    void Update()
    {
        hit = Map.GetTilePositionInDirection(transform.position, direction);
        //print(hit);
    }

    public void UpdateVector()
    {
        direction = GameMath.GetVectorFromAngle(angle);
    }
}

