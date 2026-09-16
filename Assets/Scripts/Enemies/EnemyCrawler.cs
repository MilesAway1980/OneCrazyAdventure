using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using Random = UnityEngine.Random;

public class EnemyCrawler : Enemy
{
    [SerializeField] private float speed;

    public bool clockwise;
    public EnemyDirection currentDirection;

    public float detectorDistance;
    
    private float gravity;
    private Dictionary<int, (Vector2Int?, float?)> directionTiles;
    private Detector[] detectors;

    private GameObject detectorHolder;

    private float[] directionDistances;
    
    
    protected override void Start()
    {
        gravity = Mathf.Abs(Physics2D.gravity.y);

        base.Start();

        //detectors = GetComponentsInChildren<Detector>().ToList();

        int startDir = Random.Range(0, 2);
        if (startDir == 0)
        {
            clockwise = true;
        }
        else
        {
            clockwise = false;
        }

        clockwise = true; //TEMP

        float shortestDist = float.MaxValue;
        int shortestDir = -1;

        for (int i = 0; i < 360; i += 45)
        {
            var vectorAngle = GameMath.GetVectorFromAngle(i);
            var dirTiles = Map.GetTilePositionInDirection(transform.position, vectorAngle, 100);
            var dist = dirTiles.distance;

            if (dist == null)
            {
                continue;
            }

            if (dist < shortestDist)
            {
                shortestDir = i;
                shortestDist = (float)dist;
            }
        }

        switch (shortestDir)
        {
            case 0: currentDirection = clockwise ? EnemyDirection.L : EnemyDirection.R; break;
            case 45: currentDirection = clockwise ? EnemyDirection.UL : EnemyDirection.DR; break;
            case 90: currentDirection = clockwise ? EnemyDirection.U : EnemyDirection.D; break;
            case 135: currentDirection = clockwise ? EnemyDirection.UR : EnemyDirection.DL; break;
            case 180: currentDirection = clockwise ? EnemyDirection.R : EnemyDirection.L; break;
            case 225: currentDirection = clockwise ? EnemyDirection.DR : EnemyDirection.UL; break;
            case 270: currentDirection = clockwise ? EnemyDirection.D : EnemyDirection.U; break;
            case 315: currentDirection = clockwise ? EnemyDirection.DL : EnemyDirection.UR; break;
        }

        //SetDetectors();
    }
    
    protected override void Update()
    {
        base.Update();
        
        var enemyAngle = EnemyMovement.GetAngleFromDirection(currentDirection);
        
        var surroundingTiles = EnemyMovement.GetSurroundingTiles(transform.position, enemyAngle, enemyAngle + 90, 5);

        if (surroundingTiles != null)
        {
            foreach (var kvp in surroundingTiles)
            {
                var angle = kvp.Key;
                var tileDirection = kvp.Value;
                if (tileDirection.position != null)
                {
                    print($"{ (int)tileDirection.angle}: {tileDirection.position}   -  {tileDirection.distance}");
                }
            }
        }


        /*var enemyVectorAngle = GameMath.GetVectorFromAngle(enemyAngle);
        var tileDirection = Map.GetTilePositionInDirection(transform.position, enemyVectorAngle, 100);

        //print(enemyAngle + "  " + distInFront);

        if (tileDirection.position != null && tileDirection.distance < 1)
        {
            //There's a block in front of it
            for (int i = 0; i < 360; i += 45)
            {
                var vectorAngle = GameMath.GetVectorFromAngle(i);
                var tDirection = Map.GetTilePositionInDirection(transform.position, vectorAngle, 100);
                //theDirs[detector.name] = dirTiles.Item2;

                if (i == enemyAngle)
                {
                    //print($"{i}  {angle}  {dist}  <===");
                }
                else
                {
                    //print($"{i}  {angle}  {dist}");
                }
            }
        }
        else
        {

        }*/
        
        var dir = rigidBody2D.linearVelocity.magnitude;
        var dirX = rigidBody2D.linearVelocity.x;
        var dirY = rigidBody2D.linearVelocity.y;

        if (Mathf.Abs(dir) < 0.01)
        {
            dir = 0;
        }

        if (Mathf.Abs(dirX) < 0.01)
        {
            dirX = 0;
        }

        if (Mathf.Abs(dirY) < 0.01)
        {
            dirY = 0;
        }
        
        //print ($"{dir}  {dirX}  {dirY}");

        //Once all done, move the enemy forward

        var travelAngle = EnemyMovement.GetAngleFromDirection(currentDirection);
        var moveVector = GameMath.GetVectorFromAngle(travelAngle);
        transform.position = new Vector3(
            transform.position.x + moveVector.x * speed * Time.deltaTime,
            transform.position.y + moveVector.y * speed * Time.deltaTime,
            transform.position.z
        );
        
        //Have them fall toward the wall they're stuck to

        var fallVector = gravity * GameMath.GetVectorFromAngle(enemyAngle + 90);
        rigidBody2D.AddForce(fallVector, ForceMode2D.Force);
        rigidBody2D.AddForce(moveVector * speed, ForceMode2D.Force);
        
        //print (rigidBody2D.linearVelocity);
    }

    private void SetDetectors()
    {
        if (!detectorHolder)
        {
            detectorHolder = new GameObject
            {
                transform =
                {
                    parent = transform,
                    localPosition = Vector3.zero
                },
                name = "Detectors"
            };
        }

        if (detectors == null)
        {
            detectors = new Detector[3];

            for (int i = 0; i < 3; i++)
            {
                var detectObject = new GameObject();
                detectObject.transform.SetParent(detectorHolder.transform);
                detectObject.name = $"Detector {i + 1}";

                detectors[i] = detectObject.AddComponent<Detector>();
            }
        }
    }
}
