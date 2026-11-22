using UnityEngine;
using UnityEngine.Tilemaps;

public class RotatingCenterPlatform : MonoBehaviour
{
    [Tooltip("The object that the platform spins around. It needs a Rigidbody 2D.")]
    public GameObject center;

    [Tooltip("Lets physics turn the center, such as things pushing on it, instead of its rotation being held, so that it only turns at Rotation Speed.")]
    public bool freeSpinning;
    [Tooltip("How fast it spins, in degrees a second.")]
    public float rotationSpeed;
    [Tooltip("How much it resists being turned. Not used at the moment.")]
    public float rotationResistance;
    
    private Tilemap[] platforms;

    //private Rigidbody2D rigidbody2D;
    
    private Vector3 centerPos;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        centerPos = center.transform.position;
        platforms = GetComponentsInChildren<Tilemap>();
        
        Rigidbody2D rigidbody2D = center.GetComponent<Rigidbody2D>();

        var constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
        
        if (!freeSpinning)
        {
            constraints = constraints | RigidbodyConstraints2D.FreezeRotation;
        }
        
        rigidbody2D.constraints = constraints;
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < platforms.Length; i++)
        {
            var cur = platforms[i].transform.eulerAngles;
            
            platforms[i].transform.eulerAngles = new Vector3(cur.x, cur.y, -center.transform.rotation.z);
        }
        
        var ccur = center.transform.eulerAngles;
        center.transform.eulerAngles = new Vector3(ccur.x, ccur.y, ccur.z + rotationSpeed * Time.deltaTime);
        center.transform.position = centerPos;
    }

    void FixedUpdate()
    {
        //center.transform.position = centerPos;
    }
}
