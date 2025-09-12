using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
/// <summary>
/// [TODO:description]
/// </summary>
public class Detector_Author : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject parent;
    public float range;
    public float viewAngle;
    public float3 float3One = new float3(1.0f);
    //TODO: Make our custom layermask an enum. layer 6 is what i propose for player layer mask
    private class Baker: Baker<Detector_Author>{

      public override void Bake(Detector_Author author){


        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        
       
        AddComponent(entity,new ParentComponent{parent = GetEntity(author.parent,TransformUsageFlags.Dynamic)});
        AddComponent(entity,new FrontComponent{
            front = author.float3One,
            });
        
        
        AddBuffer<BoxJobs>(entity);
        AddBuffer<BoxHits>(entity);
        AddBuffer<RayJobs>(entity);
        AddBuffer<RayHits>(entity);

        AppendToBuffer(entity, new BoxJobs{
            viewBox = new OverlapBoxCommand(author.float3One,author.float3One, Quaternion.identity, QueryParameters.Default)
            });
        for(int i = 0; i < 8; i++){
          AppendToBuffer(entity,new RayJobs{
           viewRays = new RaycastCommand(author.float3One,author.float3One,  QueryParameters.Default,author.range) 
              });
        }
      }
    }
  }


