using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChainLinker : MonoBehaviour
{
    private List<Joint2D> links;
    Dictionary<Joint2D, float> linkDistances;
    void Start()
    {
        linkDistances = new Dictionary<Joint2D, float>();
        links = FindObjectsByType<Joint2D>(FindObjectsSortMode.None).Where(l => l.enabled == true).ToList();
        foreach (var link in links)
        {
            var attachedTo = link.connectedBody;
            var thisBody = link.attachedRigidbody;
            
            var distance = Vector3.Distance(attachedTo.position, thisBody.position);
            linkDistances.Add(link, distance);
        }
    }

    // Update is called once per frame
    void Update()
    {
        foreach (var distances in linkDistances)
        {
            var link = distances.Key;
            var attachedTo = link.connectedBody;
            var thisBody = link.attachedRigidbody;
        
            /*Vector2 anchor = Vector2.zero;
            Vector2 anchorPoint = Vector2.zero;

            if (link is FixedJoint2D)
            {
                var joint = (FixedJoint2D)link;
                anchor = joint.anchor;
                anchorPoint = joint.connectedAnchor;
            }

            if (link is HingeJoint2D)
            {
                var joint = (HingeJoint2D)link;
                anchor = joint.anchor;
                anchorPoint = joint.connectedAnchor;
            }*/
        
            var distance = Vector3.Distance(attachedTo.position, thisBody.position);
            if (distance > distances.Value * 1.5)
            {
                //The links have moved too far apart.
                
                var angle = GameMath.GetAngle(attachedTo.position, thisBody.position);
                var newPosition = GameMath.MoveTowardsAngle(attachedTo.position, angle, distances.Value);
                
                thisBody.transform.position = newPosition;
            }
            //linkDistances.Add(link, distance);
            //var anchorDistance = Vector2.Distance(anchor, anchorPoint);

            //print($"{distance}  {anchorPoint}");
            /*var fixedJoint = (FixedJoint2D)link;
            var hingeJoint = (HingeJoint2D)link;

            if (fixedJoint)
            {
                print("fixed");
            }

            if (hingeJoint)
            {
                print("hinge");
            }*/
        }
    }
}
