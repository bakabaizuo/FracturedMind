using UnityEngine;

namespace FracturedStudios
{
    [DisallowMultipleComponent]
    public class Teleporter : MonoBehaviour
    {
        public Vector3 ToPosition => transform.position;
        public Quaternion ToRotation => transform.rotation;
    }
}