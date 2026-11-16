using UnityEngine;

// Moves a piece of broken tile on a machine that has joined someone else's game, where there is no physics. It falls, and spins, as
// the physics would have made it, from the speed it was thrown at, which is read from its body. It doesn't land on anything: it falls
// through, and fades away in its time, as all debris does.
public class ClientDebris : MonoBehaviour
{
    private Rigidbody2D body;
    private float gravityScale;

    public void Begin(Rigidbody2D piece, float gravity)
    {
        body = piece;
        gravityScale = gravity;
    }

    void Update()
    {
        if (!body)
        {
            return;
        }

        // The body holds how fast it's going, as it was set when the piece was thrown, and is kept up to date here.
        var velocity = body.linearVelocity + Physics2D.gravity * (gravityScale * Time.deltaTime);
        body.linearVelocity = velocity;

        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, body.angularVelocity * Time.deltaTime);
    }
}
