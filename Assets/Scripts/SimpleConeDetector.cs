using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleConeDetector : MonoBehaviour
{
    private Vector3 position;
    private Vector3 facing;
    private float halfXFOV;
    private float halfYFOV;
    private float range;
    private float squareRange;
    private float viewAngle;
    public bool detectSingle(Vector3 targetPosition){
      Vector3 offset = position-targetPosition;
      //use an actual normalization if this causes problems
      //float visibility = Vector3.Dot(facing.normalized,offset*(1/targetDistance));
      float theta = Vector3.Angle(facing,offset);
      return offset.sqrMagnitude <= range && 
              halfXFOV< theta 
              && halfYFOV < theta; 

    }    

  // Start is called before the first frame update
    void Start()
    {
      range = 2;
      squareRange = range * range;
      halfYFOV = halfXFOV = 45.0f;        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
