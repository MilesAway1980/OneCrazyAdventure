using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SwitchForce : SwitchableObject
{
    [Tooltip("0 is up and the angle grows clockwise: 90 is right, 180 is down, 270 is left")]
    [AngleDial]
    public float forceDirection;
    [Tooltip("How hard it pushes while it's switched on.")]
    public float force;
    [Tooltip("If the force is not an impulse, it will be a pushing force")]
    public ForceMode2D forceMode;

    // How much of the force is used, from 0 to 1: all of it, unless a button that passes on how far it's pressed says otherwise.
    private float forceAmount = 1f;

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


    public override void Activate()
    {
        forceAmount = 1f;
        base.Activate();
    }

    public override bool AcceptsAmount
    {
        get { return true; }
    }

    // Pushes with that fraction of the force: a button that is half pressed pushes half as hard.
    public override void SetAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (amount <= 0f)
        {
            Deactivate();
            return;
        }

        base.Activate();
        forceAmount = amount;
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
        forceVector = force * forceAmount * forceMagnitude;
        rigidBody2D.AddForce(forceVector, forceMode);
    }
}
