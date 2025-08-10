using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
public struct SimpleConeDetector: IComponentData
{
    private Vector3 position;
    private Vector3 facing;
    private float halfXFOV;
    private float halfYFOV;
//make a public get private set later
    public float range;
    private float squareRange;
    private float viewAngle;
}
