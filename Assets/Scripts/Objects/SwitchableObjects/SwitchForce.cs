using UnityEngine;

public class SwitchForce : SwitchableObject
{
    [Range(0, 360)]
    public float forceDirection;
    public float force;
    [Tooltip("If the force is not an impulse, it will be a pushing force")]
    public ForceMode2D forceMode;

    private Vector2 forceMagnitude;
    private Vector2 forceVector;
    private Rigidbody2D rigidBody2D;

    protected override void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        base.Start();
    }

    protected override void Update()
    {
        if (activated)
        {
            ApplyForce();
        }

        base.Update();
    }


    /*public override void Activate(bool repeat)
    {
        base.Activate(repeat);
    }

    public override void Deactivate(bool repeat = false)
    {
        base.Deactivate(repeat);
    }*/

    private void ApplyForce()
    {
        forceMagnitude = GameMath.GetMagnitudeFromAngle(forceDirection);
        forceVector = force * forceMagnitude;
        rigidBody2D.AddForce(forceVector, forceMode);
    }
}
