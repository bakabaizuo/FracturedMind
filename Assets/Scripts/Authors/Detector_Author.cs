using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
public class Detector_Author : MonoBehaviour
{
    // Start is called before the first frame update
    public float range;
    public float viewAngle;
    private class Baker: Baker<Detector_Author>{

      public override void Bake(Detector_Author author){

        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new SimpleConeDetector{
           range = author.range,
           viewAngle = author.viewAngle,
           memory = new Collider[10],
           rayMemory = new Collider[10,10]
          }
        );
      }
    }

}
