using Unity.Collections;
using UnityEngine;
public struct ViewBox
{
    public NativeArray<OverlapBoxCommand> view_boxes;
    public NativeArray<ColliderHit> box_visibles;
    public QueryParameters box_params;
}
