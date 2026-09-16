using UnityEngine;
using System.Collections.Generic;

public class GameHelper
{
    public static List<GameObject> ChildObjectsWithTag(GameObject gameObject, string tag)
    {
        var children = GetChildObjects(gameObject, tag);
        return children;
    }

    private static List<GameObject> GetChildObjects(GameObject gameObject, string tag)
    {
        var childObjects = new List<GameObject>();
        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            //childObjects.Add(gameObject.transform.GetChild(i).gameObject);
            var child = gameObject.transform.GetChild(i).gameObject;
            if (child.tag == tag)
            {
                childObjects.Add(child);
            }
            
            childObjects.AddRange(GetChildObjects(child, tag));
        }

        return childObjects;
    }
}