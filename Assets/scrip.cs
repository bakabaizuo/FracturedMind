using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class scrip : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        HierarchyLogger.LogFullHierarchy(this.transform, "Assets/HierarchyDevuiLog.txt");
    }


}
