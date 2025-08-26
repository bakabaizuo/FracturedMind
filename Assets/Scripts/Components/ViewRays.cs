using Unity.Collections;
using UnityEngine;
public struct ViewRays
{

    public NativeArray<RaycastCommand> view_rays;
    public NativeArray<RaycastHit> visibles;
    public QueryParameters ray_params;
}
