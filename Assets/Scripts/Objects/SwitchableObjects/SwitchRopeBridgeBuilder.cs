using System.Collections;
using System.Collections.Generic;
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

    // The links as built here, which are all that's sent to other machines.
    private readonly List<Transform> builtLinks = new List<Transform>();

    // On a machine that joined someone else's game: the links drawn from what the host sends. They have no joints, so nothing works them out.
    private readonly List<Transform> shownLinks = new List<Transform>();
    private bool ready;
    
    
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
        ready = true;
        base.Start();
    }

    
    protected override void Update()
    {
        // The host builds it. A machine that has joined is shown it, through ShowLinks.
        if (activated && !bridgeStarted && !GameSession.IsClientOnly)
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
            builtLinks.Add(newLink.transform);

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

    // For the network: where each link is (x, y) and how it's turned (z), in the order they were built.
    public void GetLinksForNetwork(List<Vector3> into)
    {
        into.Clear();

        foreach (var link in builtLinks)
        {
            if (link)
            {
                into.Add(new Vector3(link.position.x, link.position.y, link.eulerAngles.z));
            }
        }
    }

    // For the network, on a machine that joined: draws the links where the host says they are, making them as they're built.
    public void ShowLinks(Vector3[] links)
    {
        if (!ready || links == null)
        {
            return;
        }

        var catchUp = 1f - Mathf.Exp(-Time.deltaTime * 20f);

        while (shownLinks.Count < links.Length)
        {
            var index = shownLinks.Count;
            var link = Instantiate(linkTemplate, linkHolder.transform);
            link.name = $"Link {index}";
            link.transform.position = new Vector3(links[index].x, links[index].y, linkTemplate.transform.position.z);
            link.transform.eulerAngles = new Vector3(0, 0, links[index].z);
            link.SetActive(true);

            // Moved by the network, not by physics. Without a body, moving a collider about each frame is slow.
            var body = link.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            shownLinks.Add(link.transform);
        }

        while (shownLinks.Count > links.Length)
        {
            var last = shownLinks.Count - 1;

            if (shownLinks[last])
            {
                Destroy(shownLinks[last].gameObject);
            }

            shownLinks.RemoveAt(last);
        }

        for (var i = 0; i < links.Length; i++)
        {
            var link = shownLinks[i];

            if (!link)
            {
                continue;
            }

            var target = new Vector3(links[i].x, links[i].y, link.position.z);
            link.position = Vector3.Lerp(link.position, target, catchUp);
            link.rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(link.eulerAngles.z, links[i].z, catchUp));
        }
    }

    // For the network: takes away the links that were drawn, when this machine stops being shown the host's game.
    public void ClearShownLinks()
    {
        foreach (var link in shownLinks)
        {
            if (link)
            {
                Destroy(link.gameObject);
            }
        }

        shownLinks.Clear();
    }
}
