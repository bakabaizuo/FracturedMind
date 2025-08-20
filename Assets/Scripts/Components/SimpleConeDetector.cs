using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
public struct SimpleConeDetector: IComponentData
{
//    private float halfXFOV;
//    private float halfYFOV;
    public quaternion facing;
    public float range;
    public float viewAngle;
//    public Collider[] memory;
//    public Collider[,] rayMemory;
}
