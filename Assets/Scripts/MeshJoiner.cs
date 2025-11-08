//From: https://discussions.unity.com/t/combine-children-apply-mesh-collider-to-parent/380340/12
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]

public class MeshCombine : MonoBehaviour
{
// Start is called before the first frame update
void Start()
{
Quaternion OldRot = transform.rotation;
Vector3 OldPos = transform.position;

transform.rotation = Quaternion.identity;
transform.position = Vector3.zero;

MeshFilter[ ] filters = GetComponentsInChildren<MeshFilter>();

//Debug.Log(name + “is combining” + filters.Length + “meshes!”);

Mesh finalMesh = new Mesh ();
GetComponent < MeshCollider> ().sharedMesh = null;
GetComponent< MeshCollider> () .sharedMesh= finalMesh;

CombineInstance[ ] combiners = new CombineInstance[filters.Length];

for(int a = 0 ; a <filters.Length ; a++)
{
if(filters[a].transform == transform)
continue;

combiners[a].subMeshIndex = 0;
combiners[a].mesh = filters [a].sharedMesh;
combiners[a].transform = filters[a].transform.localToWorldMatrix;
}

finalMesh.CombineMeshes (combiners);

GetComponent< MeshCollider>  ().sharedMesh = finalMesh;

transform.rotation = OldRot;
transform.position = OldPos;

for(int a = 0; a < transform.childCount; a++)

transform.GetChild(a).gameObject.SetActive(false);

}

}
