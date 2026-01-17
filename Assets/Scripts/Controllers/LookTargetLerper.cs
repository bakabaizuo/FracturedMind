using UnityEngine;

/// <summary>
/// Moves the GameObject toward the best enemy target (by forward dot) using a simple lerp.
/// Attach this to the placeholder socket so it will follow the most prominent enemy.
/// </summary>
public class LookTargetLerper : MonoBehaviour
{
    [Header("Targeting")]
    public string enemyTag = "Enemy";
    public float maxDistance = 50f;
    [Range(0f,1f)] public float minDot = 0.0f; // ignore targets behind this

    [Header("Motion")]
    public float lerpSpeed = 6f; // world-space lerp speed toward target position
    public bool useLocalPosition = false; // lerp localPosition instead of world

    Vector3 _defaultLocalPos;
    Vector3 _defaultWorldPos;

    void Awake()
    {
        _defaultLocalPos = transform.localPosition;
        _defaultWorldPos = transform.position;
    }

    void Update()
    {
        // Find candidates by tag (simple and robust for editor/prototyping)
        var gos = GameObject.FindGameObjectsWithTag(enemyTag);
        Transform best = null;
        float bestScore = -1f;

        Vector3 forward = transform.forward;
        Vector3 origin = transform.position;

        for (int i = 0; i < gos.Length; i++)
        {
            var g = gos[i];
            if (g == null) continue;
            Vector3 dir = g.transform.position - origin;
            float dist = dir.magnitude;
            if (dist > maxDistance || dist <= 0.001f) continue;
            Vector3 toTarget = dir / dist;
            float dot = Vector3.Dot(forward, toTarget);
            if (dot < minDot) continue;

            // Prefer higher dot (more centered). Break ties by closer distance.
            float score = dot - (dist / maxDistance) * 0.25f;
            if (score > bestScore)
            {
                bestScore = score;
                best = g.transform;
            }
        }

        Vector3 targetPos;
        if (best != null)
        {
            targetPos = best.position;
        }
        else
        {
            targetPos = useLocalPosition ? transform.parent.TransformPoint(_defaultLocalPos) : _defaultWorldPos;
        }

        float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        if (useLocalPosition && transform.parent != null)
        {
            Vector3 curLocal = transform.localPosition;
            Vector3 desiredLocal = transform.parent.InverseTransformPoint(targetPos);
            transform.localPosition = Vector3.Lerp(curLocal, desiredLocal, t);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, t);
        }
    }
}
