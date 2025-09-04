using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
public class Detector_Author : MonoBehaviour
{
    // Start is called before the first frame update
    public float range;
    public float viewAngle;
    public quaternion front = quaternion(0.0f,0.0f,0.1f,0.0f);
    //TODO: Make our custom layermask an enum. layer 6 is what i propose for player layer mask
    public QueryParameters boxParams = new QueryParameters(6,false,QueryTriggerInteraction.Ignore,false);
    public QueryParameters rayParams = new QueryParameters(6,true,QueryTriggerInteraction.Ignore,false);
    private class Baker: Baker<Detector_Author>{

      public override void Bake(Detector_Author author){
        DetectorJobs<OverlapBoxCommand,ColliderHit> boxCommandsBaked = new DetectorJobs<OverlapBoxCommand,ColliderHit>{
          colliders = 
            new NativeArray<OverlapBoxCommand>(1,Allocator.Domain,NativeArrayOptions.UninitializedMemory),
          collisions = 
            new NativeArray<ColliderHit>(10,Allocator.Domain,NativeArrayOptions.UninitializedMemory),
          minJobs = 1,
          maxHits = 10,
          queryParams = author.boxParams
        };
        

        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity,new FrontComponent{
            front = author.front,
            });
        AddComponent(entity, new ViewBoxJobs{
            boxCommands = boxCommandsBaked
            });
        AddComponent(entity,new ViewRayJobs{
            rayCommands = new DetectorJobs<RaycastCommand, RaycastHit>{
                colliders = 
                  new NativeArray<RaycastCommand>(8,Allocator.Domain,NativeArrayOptions.UninitializedMemory),
                collisions = 
                  new NativeArray<RaycastHit>(80,Allocator.Domain,NativeArrayOptions.UninitializedMemory),
                minJobs = 1,
                maxHits = 10,
                queryParams = author.rayParams
                
              }
            });
      }
    }
  }


