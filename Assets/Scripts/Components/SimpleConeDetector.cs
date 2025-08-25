using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public struct SimpleConeDetector: IComponentData
{
//    private float halfXFOV;
//    private float halfYFOV;
    public quaternion facing;
    public float range;
    public float viewAngle;
    //TODO: Make this a seperate component and add Raycast LOD
    //TODO: Make into a seperate component (and maybe add LOD's to simulate a cone)
    public NativeArray<OverlapBoxCommand> view_boxes;
    public NativeArray<ColliderHit> box_visibles;
    public QueryParameters box_params;
    //TODO: Maybe change to a capsule if performance difference is negligible in tests
//    public Collider[] memory;
//    public Collider[,] rayMemory;
}
