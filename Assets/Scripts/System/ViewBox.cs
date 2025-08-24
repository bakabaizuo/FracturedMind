
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
public struct ViewBox: IComponentData
{
    public NativeArray<OverlapBoxCommand> view_boxes;
    public NativeArray<ColliderHit> box_visibles;
    public QueryParameters box_params;
}
