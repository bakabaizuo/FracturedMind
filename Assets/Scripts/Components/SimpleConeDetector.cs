using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
public struct SimpleConeDetector: IComponentData
{
//    private float halfXFOV;
//    private float halfYFOV;
    public quaternion front;
    public float range;
    public float viewAngle;
    //TODO: Make this a seperate component and add Raycast LOD
    //TODO: Make into a seperate component (and maybe add LOD's to simulate a cone)
    public Detector<OverlapBoxCommand,ColliderHit> boxes;
    public Detector<RaycastCommand,RaycastHit> rays;
    //TODO: Maybe change to a capsule if performance difference is negligible in tests
//    public Collider[] memory;
//    public Collider[,] rayMemory;
}
