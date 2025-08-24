
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
public struct ViewRays: IComponentData
{

    public NativeArray<RaycastCommand> view_rays;
    public NativeArray<RaycastHit> visibles;
    public QueryParameters ray_params;
}
