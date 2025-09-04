using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
//TODO: TURN INTO AN ARCHETYPE
public readonly partial struct SimpleConeDetector: IAspect
{
  readonly RefRW<FrontComponent> facing;
  readonly RefRW<ViewBoxJobs> viewbox;
  readonly RefRW<ViewRayJobs> viewrays;
//    private float halfXFOV;
//    private float halfYFOV;
}
