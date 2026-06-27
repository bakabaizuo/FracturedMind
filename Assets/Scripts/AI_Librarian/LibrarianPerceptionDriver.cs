using System.Reflection;
using UnityEngine;
using Unity.Profiling;
using FracturedStudios.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using FracturedStudios.Components;

namespace FracturedMind.AI
{
    /// <summary>
    /// Perception driver for the Librarian: collects targets, feeds the NPU, and applies crouch penalties.
    /// </summary>
    public sealed class LibrarianPerceptionDriver : MonoBehaviour
    {
        public enum LibrarianMode
        {
            Passive,
            Guard,
            Investigate,
            Pursue
        }

        public struct PerceptionSnapshot
        {
            public readonly float AlertFlag;
            public readonly float Belief;
            public readonly Vector3? TargetPosition;
            public readonly bool PlayerIsCrouching;
            public readonly float LightLevel;
            public readonly float Darkness;
            public readonly float HalfDarkness;

            public PerceptionSnapshot(float alertFlag, float belief, Vector3? targetPosition, bool playerIsCrouching, float lightLevel, float darkness, float halfDarkness)
            {
                AlertFlag = alertFlag;
                Belief = belief;
                TargetPosition = targetPosition;
                PlayerIsCrouching = playerIsCrouching;
                LightLevel = lightLevel;
                Darkness = darkness;
                HalfDarkness = halfDarkness;
            }
        }
        [Header("Scene refs")]
        [SerializeField] Transform eye;
        [SerializeField] ThirdPersonBasic player;

        [Header("Query")]
        [SerializeField] LayerMask targetMask;
        [SerializeField] float maxViewDistance = 45f;
        [SerializeField] float minViewDistance = 0.4f;
        [SerializeField] float fovDegrees = 130f;
        [SerializeField] float verticalTolerance = 4.0f;
        [SerializeField] int maxHits = 32;

        [Header("Crouch dampening")]
        [SerializeField, Range(0f, 1f)] float crouchVisibilityMultiplier = 0.35f;
        [SerializeField, Range(0f, 1f)] float crouchDistanceMultiplier = 1f;

        [Header("NPU")]
        [SerializeField] bool autoBuildSampleProgram = true;
        [SerializeField] int registerCount = 8;
        [SerializeField] int memorySize = 1;

        [Header("Actuation")] 
        [SerializeField] LibrarianMode mode = LibrarianMode.Guard;
        [SerializeField] bool pushToController = true;
        [SerializeField] LibrarianController controller;

        [Header("Light")]
        [SerializeField] AiLightProcessor aiLightProcessor;

        [Header("Debug")] 
        [SerializeField] float currentBelief;
        [SerializeField] float alertFlag;
        [SerializeField] bool drawGizmos = true;

        float _lastAlertFlag;
        float _lastLightLevel = 1f;
        float _lastDarkness;
        float _lastHalfDarkness;
        bool _lastTargetDetected;

        string _lightLevelDebugKey;
        string _darknessDebugKey;
        string _halfDarknessDebugKey;
        string _beliefDebugKey;
        string _alertDebugKey;
        string _targetDetectedDebugKey;

        Collider[] _hits = null; // assigned in Awake after maxHits set
        Vector3 _lastPlayerPos;
        float _cachedCrouchSpeedMultiplier = 0.5f;
        Vector3? _bestTarget;

        NpuVm _vm;
        NpuVm.Instruction[] _program;

        // Profiling markers/counters so we can inspect outside the player loop in the Unity Profiler window.
        static readonly ProfilerMarker PerceptionTickMarker = new ProfilerMarker("Librarian.Perception.Tick");
        static readonly ProfilerMarker OverlapQueryMarker = new ProfilerMarker("Librarian.Perception.Overlap");
        static readonly ProfilerMarker VmTickMarker = new ProfilerMarker("Librarian.Perception.VM");






        void Awake()
        {
          
            if (player == null) player = ThirdPersonBasic.Instance;
            if (controller == null) controller = GetComponent<LibrarianController>();
            if (aiLightProcessor == null) aiLightProcessor = ResolveAiLightProcessor();
             string objectName = gameObject.name;
  int index = objectName.IndexOf("Librarian_");
if (index >= 0)
    {
   
        Transform realRigEye = this.FindDeep<Transform>("eyes");
        
        if (realRigEye != null)
        {
            eye = realRigEye;
            VerboseLogger.SafeLog($"[Librarian] Successfully bound eye to deep rig transform: {eye.name}");
        }
        else
        {

            if (eye == null) eye = transform;
        }
    }
    else if (eye == null)
    {
        eye = transform;
    }
            string debugKeyPrefix = $"AI.{gameObject.name}.{GetInstanceID()}.Perception";
            _lightLevelDebugKey = debugKeyPrefix + ".LightLevel";
            _darknessDebugKey = debugKeyPrefix + ".Darkness";
            _halfDarknessDebugKey = debugKeyPrefix + ".HalfDarkness";
            _beliefDebugKey = debugKeyPrefix + ".Belief";
            _alertDebugKey = debugKeyPrefix + ".Alert";
            _targetDetectedDebugKey = debugKeyPrefix + ".TargetDetected";

            // Allocate hits buffer
            var size = Mathf.Max(1, maxHits);
            _hits = new Collider[size];

            // Reflect crouch speed multiplier once (private serialized field)
            CacheCrouchMultiplierOnce();
            if (player != null)
            {
                _lastPlayerPos = player.transform.position;
            }
        }

        AiLightProcessor ResolveAiLightProcessor()
        {
            GameObject lightHolder = GameObject.Find(AiLightProcessor.ObjectHolder);
            if (lightHolder != null)
            {
                var processor = lightHolder.GetComponent<AiLightProcessor>();
                if (processor != null)
                    return processor;
            }

            GameObject systemRoot = GameObject.Find("_System");
            if (systemRoot != null)
            {
                var processor = systemRoot.GetComponentInChildren<AiLightProcessor>(true);
                if (processor != null)
                    return processor;
            }

            return FindFirstObjectByType<AiLightProcessor>();
        }

        void OnEnable()
        {
            if (autoBuildSampleProgram)
            {
                _program = NpuProgramBuilder.BuildSampleVisionDetector();
            }

            _vm = new NpuVm(_program ?? new NpuVm.Instruction[0], registerCount, memorySize);
            RegisterDebugTrackedValues();
            VerboseLogger.SafeLog("[Librarian] Perception VM initialized");
        }

        void OnDisable()
        {
            UnregisterDebugTrackedValues();
            _vm = null;
        }

        void FixedUpdate()
        {
     
            using (PerceptionTickMarker.Auto())
            {
                if (_vm == null || player == null || eye == null) return;

                bool playerCrouched = IsPlayerCrouched();
                Func<bool, float> sightRangeEvaluator = (crouched) => crouched ? crouchDistanceMultiplier : 1f;
                float distClamp = maxViewDistance * sightRangeEvaluator.Invoke(playerCrouched);
                float fovCos = Mathf.Cos(0.5f * fovDegrees * Mathf.Deg2Rad);
                _bestTarget = FindBestTarget(distClamp, fovCos, out float bestDot, out float bestDist);

                // Compute motion scalar from player displacement (normalized by move speed or crouch speed)
                float motionScalar = 0f;
                if (player != null)
                {
                    float baselineSpeed = player.moveSpeed;
                    float crouchSpeed = baselineSpeed * _cachedCrouchSpeedMultiplier;
                    float denom = playerCrouched ? crouchSpeed : baselineSpeed;
                    if (denom < 0.001f) denom = baselineSpeed;

                    Vector3 pos = player.transform.position;
                    float delta = (pos - _lastPlayerPos).magnitude;
                    motionScalar = Mathf.Clamp01(delta / (denom * Time.fixedDeltaTime));
                    _lastPlayerPos = pos;
                }

                // Sample lighting at the player's position so darkness reflects where the target currently stands,
                // while the librarian eye transform continues to control sight-cone comparisons above.
                Vector3 lightSamplePosition = player.transform.position;
                Vector3 lightSampleForward = eye.forward;
                float lightLevel = aiLightProcessor != null ? aiLightProcessor.SampleLightLevel(lightSamplePosition, lightSampleForward) : 1f;
                float darkness = aiLightProcessor != null ? aiLightProcessor.SampleDarkness(lightSamplePosition, lightSampleForward) : 0f;
                float halfDarkness = aiLightProcessor != null ? aiLightProcessor.SampleHalfDarkness(lightSamplePosition, lightSampleForward) : darkness * 0.5f;
                _lastLightLevel = lightLevel;
                _lastDarkness = darkness;
                _lastHalfDarkness = halfDarkness;
                _lastTargetDetected = _bestTarget.HasValue;

                if (bestDot >= 0f)
                {
                    float distNorm = Mathf.InverseLerp(distClamp, minViewDistance, bestDist);
                    Func<bool, float> evaluateVisibilityScale = isCrouched => isCrouched ? crouchVisibilityMultiplier : 1f;
                        float crouchPenalty = evaluateVisibilityScale.Invoke(playerCrouched);

                    _vm.SetRegister(0, bestDot * crouchPenalty); // dot attenuated if crouched // add * crouchVisibilityMultiplier
                    _vm.SetRegister(1, distNorm);
                    _vm.SetRegister(2, lightLevel * crouchPenalty);
                    _vm.SetRegister(3, motionScalar * crouchPenalty);///* crouchVisibilityMultiplier
                }
                else
                {
                    // No target seen; let VM decay memory naturally (no register update needed)
                }

                using (VmTickMarker.Auto())
                {
                    _vm.Tick(Time.fixedDeltaTime);
                }

                currentBelief = _vm.ReadMem(0);
                alertFlag = _vm.GetRegister(6);

                if (Mathf.Abs(alertFlag - _lastAlertFlag) > 0.01f)
                {
                    VerboseLogger.SafeLog($"[Librarian] AlertFlag changed: {alertFlag:0.00}, belief: {currentBelief:0.00}, crouched:{playerCrouched}");
                    _lastAlertFlag = alertFlag;
                }

                if (pushToController && controller != null)
                {
                    if (controller._agent == null)
                    {
                        VerboseLogger.SafeLog("[Librarian] Controller has no NavMeshAgent; pursuit will not move.");
                    }
                    VerboseLogger.SafeLog($"[Librarian] Pushing snapshot: alert={alertFlag:0.00} belief={currentBelief:0.00} targetSet={_bestTarget.HasValue} light={lightLevel:0.00} dark={darkness:0.00}");
                    controller.OnPerceptionUpdate(new PerceptionSnapshot(alertFlag, currentBelief, _bestTarget, playerCrouched, lightLevel, darkness, halfDarkness));
                }
            }
        }

Vector3? FindBestTarget(float distClamp, float fovCos, out float bestDot, out float bestDist)
{
    // Force clean array states before writing fresh queries
    System.Array.Clear(_hits, 0, _hits.Length);
    int count;
    using (OverlapQueryMarker.Auto())
    {
        count = Physics.OverlapSphereNonAlloc(eye.position, distClamp, _hits, targetMask, QueryTriggerInteraction.Ignore);
    }
    VerboseLogger.SafeLog($"[Librarian] Scan hits={count} distClamp={distClamp:0.0} fov={fovDegrees:0.0}");

    // Initialize with safe boundary defaults
    bestDot = -1f;
    bestDist = maxViewDistance; // Baseline maximum view distance used as standard fallback boundary
    Vector3? bestTarget = null;

    for (int i = 0; i < count; i++)
    {
        var t = _hits[i].transform;
        if (t is null) continue;

        Vector3 dir = t.position - eye.position;
        float dist = dir.magnitude;
        
        // 1. Range Check: Rejects if beyond current dynamic clamp limit
        Predicate<float> isOutOfRange = d => d < minViewDistance || d > distClamp;
        if (isOutOfRange.Invoke(dist)) continue;

        // 2. 3D Orientation Check
        Vector3 dirNormalized = dir / dist;
        float dot3D = Vector3.Dot(eye.forward, dirNormalized);

        // 3. Field of View Boundary Check
        if (dot3D < fovCos) continue;

        // Pick the target most centered in our vision cone
        if (dot3D > bestDot)
        {
            bestDot = dot3D;
            bestDist = dist; 
            bestTarget = t.position;
            VerboseLogger.SafeLog($"[Librarian] Candidate hit {t.name} 3D-dot={dot3D:0.00} dist={dist:0.0}");
        }
    }

    return bestTarget;
}

        bool playerHasCrouchFlagFromAnimator(ThirdPersonBasic tp)
        {
            return tp != null && tp.isCrouching;
        }

        public bool IsPlayerCrouched()
        {
            if (player == null) return false;

            bool crouched = player.isCrouching;

            // Try animator flag if available on player
            var anim = player.GetComponent<StringscriptAnimatior>();
            if (anim != null)
            {
                crouched |= anim.IsCrouched;
            }

            return crouched;
        }

        void CacheCrouchMultiplierOnce()
        {
            if (player == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = typeof(ThirdPersonBasic).GetField("crouchSpeedMultiplier", flags);
            if (field != null && field.FieldType == typeof(float))
            {
                _cachedCrouchSpeedMultiplier = (float)field.GetValue(player);
            }
        }

        void OnDrawGizmosSelected()
{
    if (!drawGizmos) return;
    Transform pivot = eye != null ? eye : transform;
    if (pivot == null) return;

    // 1. Draw the absolute maximum physics query range (Light Blue)
    Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.05f);
    Gizmos.DrawWireSphere(pivot.position, maxViewDistance);

    // Calculate current dynamic range based on live state
    bool playerCrouched = IsPlayerCrouched();
    Func<bool, float> distanceScaleQuery = isCrouching => isCrouching ? crouchDistanceMultiplier : 1f;
    float dynamicMaxDist = maxViewDistance * distanceScaleQuery.Invoke(playerCrouched);

    // 2. Draw the forward lookup vector gaze (Solid Red Line)
    Gizmos.color = Color.red;
    Gizmos.DrawLine(pivot.position, pivot.position + pivot.forward * dynamicMaxDist);

    // 3. Generate FOV Cone Edges (Yellow)
    Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
    float halfFovDeg = 0.5f * fovDegrees;

    // Left and Right boundaries relative to local eye orientation matrix
    Vector3 leftRay = Quaternion.AngleAxis(-halfFovDeg, pivot.up) * pivot.forward;
    Vector3 rightRay = Quaternion.AngleAxis(halfFovDeg, pivot.up) * pivot.forward;

    // Up and Down boundaries (to visualize the true 3D dot product cone)
    Vector3 upRay = Quaternion.AngleAxis(-halfFovDeg, pivot.right) * pivot.forward;
    Vector3 downRay = Quaternion.AngleAxis(halfFovDeg, pivot.right) * pivot.forward;

    // Draw boundary perimeter lines
    Gizmos.DrawLine(pivot.position, pivot.position + leftRay * dynamicMaxDist);
    Gizmos.DrawLine(pivot.position, pivot.position + rightRay * dynamicMaxDist);
    Gizmos.DrawLine(pivot.position, pivot.position + upRay * dynamicMaxDist);
    Gizmos.DrawLine(pivot.position, pivot.position + downRay * dynamicMaxDist);

    // 4. Draw vertical tolerance bounding indicators (Red Box)
    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
    Gizmos.DrawWireCube(pivot.position + Vector3.up * verticalTolerance * 0.5f, new Vector3(1f, verticalTolerance, 1f));
}

        void RegisterDebugTrackedValues()
        {
            DevConsoleBridge.RegisterTrackedValue(_lightLevelDebugKey, () => _lastLightLevel);
            DevConsoleBridge.RegisterTrackedValue(_darknessDebugKey, () => _lastDarkness);
            DevConsoleBridge.RegisterTrackedValue(_halfDarknessDebugKey, () => _lastHalfDarkness);
            DevConsoleBridge.RegisterTrackedValue(_beliefDebugKey, () => currentBelief);
            DevConsoleBridge.RegisterTrackedValue(_alertDebugKey, () => alertFlag);
            DevConsoleBridge.RegisterTrackedValue(_targetDetectedDebugKey, () => _lastTargetDetected);
        }

        void UnregisterDebugTrackedValues()
        {
            DevConsoleBridge.UnregisterTrackedValue(_lightLevelDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_darknessDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_halfDarknessDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_beliefDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_alertDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_targetDetectedDebugKey);
        }
        
    }
}
