using System.Reflection;
using UnityEngine;
using Unity.Profiling;
using FracturedStudios.UI;

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
        [SerializeField] float maxViewDistance = 20f;
        [SerializeField] float minViewDistance = 1.5f;
        [SerializeField] float fovDegrees = 110f;
        [SerializeField] float verticalTolerance = 2.0f;
        [SerializeField] int maxHits = 32;

        [Header("Crouch dampening")]
        [SerializeField, Range(0f, 1f)] float crouchVisibilityMultiplier = 0.35f;
        [SerializeField, Range(0f, 1f)] float crouchDistanceMultiplier = 0.65f;

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
            if (eye == null) eye = transform;
            if (player == null) player = ThirdPersonBasic.Instance;
            if (controller == null) controller = GetComponent<LibrarianController>();
            if (aiLightProcessor == null) aiLightProcessor = FindFirstObjectByType<AiLightProcessor>();

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
                float distClamp = maxViewDistance * (playerCrouched ? crouchDistanceMultiplier : 1f);
                float fovCos = Mathf.Cos(0.5f * fovDegrees * Mathf.Deg2Rad);

                int count;
                using (OverlapQueryMarker.Auto())
                {
                    // Snapshot candidate targets in a clamped volume using OverlapSphereNonAlloc (no allocations)
                    count = Physics.OverlapSphereNonAlloc(eye.position, distClamp, _hits, targetMask, QueryTriggerInteraction.Ignore);
                }
                VerboseLogger.SafeLog($"[Librarian] Scan hits={count} distClamp={distClamp:0.0} fov={fovDegrees:0.0}");

                float bestDot = -1f;
                float bestDist = distClamp;
                _bestTarget = null;

                for (int i = 0; i < count; i++)
                {
                    var t = _hits[i].transform;
                    Vector3 dir = t.position - eye.position;
                    float dist = dir.magnitude;
                    if (dist < minViewDistance || dist > distClamp) continue;
                    if (Mathf.Abs(dir.y) > verticalTolerance) continue;

                    Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z).normalized;
                    Vector3 fwdXZ = new Vector3(eye.forward.x, 0f, eye.forward.z).normalized;
                    float dot = Vector3.Dot(fwdXZ, dirXZ);
                    if (dot < fovCos) continue;

                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestDist = dist;
                        _bestTarget = t.position;
                        VerboseLogger.SafeLog($"[Librarian] Candidate hit {t.name} dot={dot:0.00} dist={dist:0.0}");
                    }
                }

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

                // Light level and darkness now feed the perception snapshot and controller mode logic.
                float lightLevel = aiLightProcessor != null ? aiLightProcessor.SampleLightLevel(eye.position, eye.forward) : 1f;
                float darkness = aiLightProcessor != null ? aiLightProcessor.SampleDarkness(eye.position, eye.forward) : 0f;
                float halfDarkness = aiLightProcessor != null ? aiLightProcessor.SampleHalfDarkness(eye.position, eye.forward) : darkness * 0.5f;
                _lastLightLevel = lightLevel;
                _lastDarkness = darkness;
                _lastHalfDarkness = halfDarkness;
                _lastTargetDetected = _bestTarget.HasValue;

                if (bestDot >= 0f)
                {
                    float distNorm = Mathf.InverseLerp(distClamp, minViewDistance, bestDist);
                    float crouchPenalty = playerCrouched ? crouchVisibilityMultiplier : 1f;

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

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(pivot.position, maxViewDistance);

            Vector3 fwd = pivot.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            float halfFovDeg = 0.5f * fovDegrees;
            Vector3 left = Quaternion.Euler(0f, -halfFovDeg, 0f) * fwd;
            Vector3 right = Quaternion.Euler(0f, halfFovDeg, 0f) * fwd;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawLine(pivot.position, pivot.position + left * maxViewDistance);
            Gizmos.DrawLine(pivot.position, pivot.position + right * maxViewDistance);
            Gizmos.DrawLine(pivot.position, pivot.position + fwd * maxViewDistance);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
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
