using System.Collections.Generic;
using UnityEngine;

public class StickToPlatform : MonoBehaviour
{
    //private Dictionary<int, Transform> playerParentTransforms;
    private List<GameObject> riders;

    private Vector3 lastPos;
    
    void Start()
    {
        //playerParentTransforms = new Dictionary<int, Transform>();
        riders = new List<GameObject>();
    }
    
    void Update()
    {
        var change = transform.position - lastPos;
        
        foreach (var rider in riders)
        {
            rider.transform.position += change;
        }
        
        lastPos = transform.position;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            /*var playerID = collision.gameObject.GetInstanceID();
            if (!playerParentTransforms.ContainsKey(playerID))
            {
                playerParentTransforms.Add(playerID, collision.gameObject.transform);
            }
            
            collision.gameObject.transform.parent = transform;*/
            if (!riders.Contains(collision.gameObject))
            {
                riders.Add(collision.gameObject);
            }
        }
    }
    
    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            //var playerTransform = playerParentTransforms[collision.gameObject.GetInstanceID()];
            //collision.gameObject.transform.parent = playerTransform;
            if (riders.Contains(collision.gameObject))
            {
                riders.Remove(collision.gameObject);
            }
        }
    }
}
