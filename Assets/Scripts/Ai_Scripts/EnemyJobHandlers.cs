using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{

    public AIController[] AIs; 
    // Start is called before the first frame update
    void Start()
    {
        AIs = Object.FindObjectsByType<AIController>(FindObjectsSortMode.None);
    }

    // Update is called once per frame
    void Update()
    {
    }
}
