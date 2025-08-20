using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using UnityEditor;
[InitializeOnLoad]
public class Detector_Spawner: MonoBehaviour
{
public Entity test;
public EntityManager entityManager;
 void Start()
    {
         entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        test = entityManager.CreateEntity(typeof(SimpleConeDetector));
        entityManager.SetComponentData(test, new SimpleConeDetector{
            range = 10,
            viewAngle = 90,
            
            });
//        Debug.Log("Starting");
    }

    // Update is called once per frame
void Update()
    {
   //   bool exist = entityManager.Exists(test);  
 //     Debug.Log(exist);
    }
}
