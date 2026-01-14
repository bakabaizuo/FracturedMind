using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LampVisionSensor : MonoBehaviour
{
    [Header("Vision Socket")]
    [SerializeField] private Transform socket;
    [SerializeField] private Vector3 socketLocalOffset = new Vector3(0f, 1.6f, 0.1f);

    [Header("Detection Settings")]
    [SerializeField, Min(0.5f)] private float viewDistance = 10f;
    [SerializeField, Range(1f, 60f)] private float halfAngle = 12f;
    [SerializeField] private LayerMask obstructionMask = Physics.DefaultRaycastLayers;

    [Header("Debug")]
    [SerializeField] private bool drawDebug;

    private readonly HashSet<LibraryLampBehavior> lampsInSight = new HashSet<LibraryLampBehavior>();
    private readonly List<LibraryLampBehavior> scratchList = new List<LibraryLampBehavior>();

    public Transform Socket => socket;

    private void Awake()
    {
        EnsureSocket();
    }

    public void EnsureSocket()
    {
        if (socket != null)
            return;

        Transform existing = transform.Find("VisionSocket");
        if (existing != null)
        {
            socket = existing;
            return;
        }

        GameObject socketObj = new GameObject("VisionSocket");
        socketObj.transform.SetParent(transform);
        socketObj.transform.localPosition = socketLocalOffset;
        socketObj.transform.localRotation = Quaternion.identity;
        socket = socketObj.transform;
    }

    private void LateUpdate()
    {
        if (socket == null)
            return;

        ScanForLamps();
    }

    private void ScanForLamps()
    {
        scratchList.Clear();
        scratchList.AddRange(LibraryLampBehavior.ActiveLamps);

        Vector3 origin = socket.position;
        Vector3 forward = socket.forward;
        float cosThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

        for (int i = scratchList.Count - 1; i >= 0; i--)
        {
            LibraryLampBehavior lamp = scratchList[i];
            if (lamp == null)
                continue;

            Vector3 toLamp = lamp.LookAnchor.position - origin;
            float sqrDistance = toLamp.sqrMagnitude;
            if (sqrDistance > viewDistance * viewDistance)
            {
                lampsInSight.Remove(lamp);
                continue;
            }

            Vector3 dir = toLamp.normalized;
            float dot = Vector3.Dot(forward, dir);
            bool inCone = dot >= cosThreshold;
            bool blocked = Physics.Raycast(origin, dir, Mathf.Sqrt(sqrDistance), obstructionMask, QueryTriggerInteraction.Ignore);

            bool isLooking = inCone && !blocked;

            if (isLooking)
            {
                if (lampsInSight.Add(lamp))
                    lamp.RegisterLook(this);

                if (drawDebug)
                    Debug.DrawLine(origin, lamp.LookAnchor.position, Color.yellow);
            }
            else
            {
                lampsInSight.Remove(lamp);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (socket == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(socket.position, viewDistance);
    }
#endif
}
