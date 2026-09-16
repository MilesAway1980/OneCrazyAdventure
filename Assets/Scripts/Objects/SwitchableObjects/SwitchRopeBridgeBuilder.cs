using System.Collections;
using UnityEngine;

public class SwitchRopeBridgeBuilder : SwitchableObject
{
    public GameObject startPoint;
    public GameObject endPoint;
    public GameObject linkTemplate;

    public bool getLinkSizeFromCollider;
    public Vector2 linkSize;

    public float linkMass;
    public float linkLinearDampening;

    public float timeBetweenLinks;

    private GameObject linkHolder;

    public Vector2 distanceBetweenEnds;
    private bool bridgeStarted;
    
    
    protected override void Start()
    {
        var rb = startPoint.GetComponent<Rigidbody2D>();
        if (!rb)
        {
            rb = startPoint.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }
        
        rb = endPoint.GetComponent<Rigidbody2D>();
        if (!rb)
        {
            rb = endPoint.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }
        
        linkHolder = new GameObject();
        linkHolder.transform.parent = transform;
        
        distanceBetweenEnds.x = Mathf.Abs(startPoint.transform.position.x - endPoint.transform.position.x);
        distanceBetweenEnds.y = Mathf.Abs(startPoint.transform.position.y - endPoint.transform.position.y);

        if (getLinkSizeFromCollider)
        {
            var col = linkTemplate.GetComponent<Collider2D>();
            linkSize = col.bounds.size;
        }
        else
        {
            if (linkSize.x == 0 || linkSize.y == 0)
            {
                print ($"ERROR in SwitchRopeBridgeBuilder {gameObject.name} at {startPoint.transform.position}.  LinkSize is not set.");
                return;
            }
        }
        
        linkTemplate.SetActive(false);
        base.Start();
    }

    
    protected override void Update()
    {
        if (activated && !bridgeStarted)
        {
            bridgeStarted = true;
            StartCoroutine(BuildBridge());
        }
        
        base.Update();
    }

    IEnumerator BuildBridge()
    {
        var bridgeDistance = Vector2.Distance(startPoint.transform.position, endPoint.transform.position);
        int numLinks = (int)(bridgeDistance / linkSize.x);
        
        var angle = 90 - GameMath.GetAngle(startPoint.transform.position, endPoint.transform.position);

        Rigidbody2D prevLinkRB = null;

        for (int i = 0; i < numLinks; i++)
        {
            var newLink = Instantiate(linkTemplate, linkHolder.transform);
            newLink.name = $"Link {i}";
            newLink.transform.eulerAngles = new Vector3(0, 0, angle);
            
            //Need to add half the link to the position or the link will be centered on the end point.
            float percentage = (linkSize.x * i + linkSize.x * 0.5f) / bridgeDistance;
            newLink.transform.position = Vector2.Lerp(
                startPoint.transform.position, 
                endPoint.transform.position, 
                percentage
            );
            
            newLink.SetActive(true);

            var linkRb = newLink.gameObject.AddComponent<Rigidbody2D>();
            linkRb.mass = linkMass;
            linkRb.linearDamping = linkLinearDampening;

            var linkHj = newLink.gameObject.AddComponent<HingeJoint2D>();
            if (prevLinkRB)
            {
                linkHj.connectedBody = prevLinkRB;
            }
            else
            {
                linkHj.connectedBody = startPoint.GetComponent<Rigidbody2D>();
            }
            
            prevLinkRB = linkRb;
            
            yield return new WaitForSeconds(timeBetweenLinks);
        }

        var endHj = endPoint.gameObject.AddComponent<HingeJoint2D>();
        endHj.connectedBody = prevLinkRB;
    }
}
