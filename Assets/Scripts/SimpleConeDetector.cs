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
    public bool DetectSingle(Vector3 targetPosition){
      Vector3 offset = position-targetPosition;
      return Detectors.DetectInSphere(offset,range) && Detectors.DetectInAngle(facing,offset,halfXFOV);
        
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
