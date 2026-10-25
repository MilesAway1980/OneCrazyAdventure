using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EnemyDirection
{
    UL, U, UR,
    RU, R, RD,
    DR, D, DL,
    LD, L, LU
}

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float movementAcceleration;
    [SerializeField] private float maxSpeed;

    public bool turnAroundAtLedge;
    public bool canFly;
    public bool canClimbWalls;
    public bool canClimbCeilings;

    private float angle = 90;

    private Vector2 currentSpeed;

    //private List<Detector> wallDetectors;
    //private List<Detector> floorDetectors;
    private List<Detector> upCornerDetectors;
    private List<Detector> rightCornerDetectors;
    private List<Detector> downCornerDetectors;
    private List<Detector> leftCornerDetectors;

    protected Dictionary<EnemyDirection, TileDirection> directionTiles;

    /*
     *
     *          There are 8 detectors but we need 12
     *          The corners (UL, UR, DL, DR) are shared and based on the direction they check
     *
     *          UL      U       UR
     *          L               R
     *          DL      D       DR
     *
     *           UL      U       UR
     *        LU                    RU
     *        L                     R
     *        LD                    RD
     *           DL      D       DR
     *
     */

    private bool oldLeft, oldRight;

    void Awake()
    {
        var detectors = GetComponentsInChildren<Detector>().ToList();
        var allCornerDetectors = detectors
            .Where(d => d.type == DetectorType.Corners)
            .ToList();

        upCornerDetectors = allCornerDetectors
            .Where(d =>
                d.name == "UL" ||
                d.name == "U" ||
                d.name == "UR"
            )
            .ToList();

        rightCornerDetectors = allCornerDetectors
            .Where(d =>
                d.name == "UR" ||
                d.name == "R" ||
                d.name == "DR"
            )
            .ToList();

        downCornerDetectors = allCornerDetectors
            .Where(d =>
                d.name == "DL" ||
                d.name == "D" ||
                d.name == "DR"
            )
            .ToList();

        leftCornerDetectors = allCornerDetectors
            .Where(d =>
                d.name == "UL" ||
                d.name == "L" ||
                d.name == "DL"
            )
            .ToList();

        directionTiles = new Dictionary<EnemyDirection, TileDirection>();
        CheckDirections();
    }

    void Update()
    {
        CheckDirections();
    }

    private void CheckDirections()
    {
        CheckUp();
        CheckRight();
        CheckLeft();
        CheckDown();
    }

    public TileDirection GetDirection(EnemyDirection direction)
    {
        return directionTiles[direction];
    }

    private void CheckUp()
    {
        foreach (var detector in upCornerDetectors)
        {
            var enumName = NameToEnum(detector.name);
            directionTiles[enumName] = Map.GetTilePositionInDirection(detector.transform.position, Vector2.up);
        }
    }

    private void CheckRight()
    {
        foreach (var detector in rightCornerDetectors)
        {
            EnemyDirection dir;
            switch (detector.name)
            {
                case "UR": dir = EnemyDirection.RU; break;
                case "DR": dir = EnemyDirection.RD; break;
                default: dir = EnemyDirection.R; break;
            }

            directionTiles[dir] = Map.GetTilePositionInDirection(detector.transform.position, Vector2.right);
        }
    }

    private void CheckLeft()
    {
        foreach (var detector in leftCornerDetectors)
        {
            EnemyDirection dir;
            switch (detector.name)
            {
                case "UL": dir = EnemyDirection.LU; break;
                case "DL": dir = EnemyDirection.LD; break;
                default: dir = EnemyDirection.L; break; //L
            }

            directionTiles[dir] = Map.GetTilePositionInDirection(detector.transform.position, Vector2.left);
        }
    }

    private void CheckDown()
    {
        foreach (var detector in downCornerDetectors)
        {
            var enumName = NameToEnum(detector.name);
            directionTiles[enumName] = Map.GetTilePositionInDirection(detector.transform.position, Vector2.down);
        }
    }

    public Vector2 GetSpeed()
    {
        return currentSpeed;
    }

    public Dictionary<EnemyDirection, TileDirection> GetDirectionTiles()
    {
        return directionTiles;
    }

    public EnemyDirection NameToEnum(string name)
    {
        switch (name)
        {
            case "UL": return EnemyDirection.UL;
            case "U": return EnemyDirection.U;
            case "UR": return EnemyDirection.UR;
            case "RU": return EnemyDirection.RU;
            case "R": return EnemyDirection.R;
            case "RD": return EnemyDirection.RD;
            case "DR": return EnemyDirection.DR;
            case "D": return EnemyDirection.D;
            case "DL": return EnemyDirection.DL;
            case "LD": return EnemyDirection.LD;
            case "L": return EnemyDirection.L;
            case "LU": return EnemyDirection.LU;
            default:
                print("Invalid NameToEnum value: " + name);
                return EnemyDirection.D;
        }
    }

    public float GetAngle()
    {
        return angle;
    }

    public void SetAngle(float newAngle)
    {
        while (newAngle < 0)
        {
            newAngle += 360;
        }

        while (newAngle >= 360)
        {
            newAngle -= 360;
        }

        angle = newAngle;
    }

    public static int GetAngleFromDirection(EnemyDirection direction)
    {
        switch (direction)
        {
            case EnemyDirection.U: return 0;
            case EnemyDirection.UR:
            case EnemyDirection.RU: return 45;
            case EnemyDirection.R: return 90;
            case EnemyDirection.RD:
            case EnemyDirection.DR: return 135;
            case EnemyDirection.D: return 180;
            case EnemyDirection.DL:
            case EnemyDirection.LD: return 225;
            case EnemyDirection.L: return 270;
            case EnemyDirection.UL:
            case EnemyDirection.LU: return 315;
        }

        return -1;
    }
    
    public static EnemyDirection GetDirectionFromAngle(float angle)
    {
        GameMath.FixAngle(ref angle);

        switch (angle)
        {
            case > 337.5f and < 360:
            case >= 0 and <= 22.5f:
                return EnemyDirection.U;
            case > 22.5f and <= 67.5f: return EnemyDirection.UR;
            case > 67.5f and <= 112.5f: return EnemyDirection.R;
            case > 112.5f and <= 157.5f: return EnemyDirection.DR;
            case > 157.5f and <= 202.5f: return EnemyDirection.D;
            case > 202.5f and <= 247.5f: return EnemyDirection.DL;
            case > 247.5f and <= 292.5f: return EnemyDirection.L;
            case > 292.5f and <= 337.5f: return EnemyDirection.UL;
        }

        return EnemyDirection.RU;
    }

    public static Dictionary<float, TileDirection> GetSurroundingTiles(Vector3 position, float arcStart, float arcEnd, float degreesPerTick)
    {
        if (degreesPerTick <= 0 || degreesPerTick >= 360 || arcStart >= arcEnd)
        {
            return null;
        }
        
        var tilePositions = new Dictionary<float, TileDirection>();

        for (float i = arcStart; i <= arcEnd; i += degreesPerTick)
        {
            var vectorAngle = GameMath.GetVectorFromAngle(i);
            var tileDirection = Map.GetTilePositionInDirection(position, vectorAngle, 100);
            
            tilePositions[i] = tileDirection;
        }

        return tilePositions;
    }
}
