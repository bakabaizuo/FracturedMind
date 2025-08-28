using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
//TODO: TURN INTO AN ARCHETYPE
public struct SimpleConeDetector: IComponentData
{

//    private float halfXFOV;
//    private float halfYFOV;
    public quaternion front;
    public float range;
    public float viewAngle;
}
