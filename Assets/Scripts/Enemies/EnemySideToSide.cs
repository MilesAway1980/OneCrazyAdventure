using UnityEngine;

public class EnemySideToSide : Enemy
{
    [SerializeField] private float acceleration;
    //[SerializeField] private float deceleration;
    [SerializeField] private float maxSpeed;
    private float currentSpeed;

    public float distanceFromWallToTurnAround;
    public float distanceToFloorBelowToTurnAround;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    /*protected override void Start()
    {
        base.Start();
    }*/

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();

        var downLeftTileDirection = enemyMovement.GetDirection(EnemyDirection.DL);
        var downRightTileDirection = enemyMovement.GetDirection(EnemyDirection.DR);
        var rightTileDirection = enemyMovement.GetDirection(EnemyDirection.R);
        var leftTileDirection = enemyMovement.GetDirection(EnemyDirection.L);

        if (downLeftTileDirection.distance > distanceToFloorBelowToTurnAround && (int)enemyMovement.GetAngle() == 270)
        {
            enemyMovement.SetAngle(90);
            rigidBody2D.linearVelocityX = 0;
        }

        if (downRightTileDirection.distance > distanceToFloorBelowToTurnAround && (int)enemyMovement.GetAngle() == 90)
        {
            enemyMovement.SetAngle(270);
            rigidBody2D.linearVelocityX = 0;
        }

        //Right
        if ((int)enemyMovement.GetAngle() == 90)
        {
            if (rightTileDirection.position != null && rightTileDirection.distance < distanceFromWallToTurnAround)
            {
                rigidBody2D.linearVelocityX = 0;
                enemyMovement.SetAngle(270);
            }
            else if (rigidBody2D.linearVelocity.magnitude < maxSpeed)
            {
                rigidBody2D.AddForceX(acceleration * Time.deltaTime, ForceMode2D.Force);
            }
        }
        
        //Left
        if ((int)enemyMovement.GetAngle() == 270)
        {
            if (leftTileDirection.position != null && leftTileDirection.distance < distanceFromWallToTurnAround)
            {
                rigidBody2D.linearVelocityX = 0;
                enemyMovement.SetAngle(90);
            }
            else if (rigidBody2D.linearVelocity.magnitude < maxSpeed)
            {
                rigidBody2D.AddForceX(-acceleration * Time.deltaTime, ForceMode2D.Force);
            }
        }
    }
}
