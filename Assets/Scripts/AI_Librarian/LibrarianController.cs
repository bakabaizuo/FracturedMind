using UnityEngine;
using UnityEngine.AI;

namespace FracturedMind.AI
{
    /// <summary>
    /// Consumes perception updates and drives NavMesh/animation based on mode.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class LibrarianController : MonoBehaviour
    {
        [Header("Mode & thresholds")]
        [SerializeField] LibrarianPerceptionDriver.LibrarianMode mode = LibrarianPerceptionDriver.LibrarianMode.Guard;
        [SerializeField] float alertThreshold = 0.5f;
        [SerializeField] float repathInterval = 0.25f;
        [SerializeField] bool autoEscalateToPursueOnTarget = true;
        [SerializeField] float baseSpeed = 3.5f;
        [SerializeField] float pursueSpeedMultiplier = 1.5f;
        [SerializeField] bool scaleSpeedWithAlert = true; // blends speed by alert
        [SerializeField] bool respectPlayerCrouchInGuard = true;
        [SerializeField] LibrarianPerceptionDriver perception;

        [Header("Animation hooks")]
        [SerializeField] Animator animator;
        [SerializeField] string alertParam = "Alert";
        [SerializeField] bool driveAnimator = true;

        public NavMeshAgent _agent;
        float _repathTimer;
        bool _alertParamChecked;
        bool _alertParamExists;
        int _alertParamHash;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (perception == null) perception = GetComponent<LibrarianPerceptionDriver>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                VerboseLogger.SafeLog("[LibrarianController] No Animator found on self or children.");
            }
        }

        public bool IsPlayerCrouching => perception != null && perception.IsPlayerCrouched();

        public void OnPerceptionUpdate(LibrarianPerceptionDriver.PerceptionSnapshot snap)
        {
            bool shouldPursue = false;
            float alert = snap.AlertFlag;
            bool playerCrouched = IsPlayerCrouching || snap.PlayerIsCrouching;

            if (_agent == null)
            {
                VerboseLogger.SafeLog("[LibrarianController] No NavMeshAgent; cannot move.");
            }
            else
            {
                VerboseLogger.SafeLog($"[LibrarianController] Incoming snap alert={alert:0.00} belief={snap.Belief:0.00} targetSet={snap.TargetPosition.HasValue} mode={mode}");
            }

            switch (mode)
            {
                case LibrarianPerceptionDriver.LibrarianMode.Passive:
                    shouldPursue = false;
                    break;
                case LibrarianPerceptionDriver.LibrarianMode.Guard:
                    shouldPursue = alert >= alertThreshold;
                    if (playerCrouched)
                    {
                        if (respectPlayerCrouchInGuard)
                        {
                            shouldPursue = false;
                            VerboseLogger.SafeLog("[LibrarianController] Guard hold due to player crouch");
                        }
                        else
                        {
                            VerboseLogger.SafeLog("[LibrarianController] Guard ignoring crouch (respect off)");
                        }
                    }
                    break;
                case LibrarianPerceptionDriver.LibrarianMode.Pursue:
                    shouldPursue = alert >= alertThreshold;

                    if (shouldPursue)
                        HandlePursue(snap, alert);
                    break;
            }

            // Debug: if we have a target and alert but are not pursuing, log why.
            if (snap.TargetPosition.HasValue && alert > 0.05f && !shouldPursue)
            {
                VerboseLogger.SafeLog($"[LibrarianController] Not pursuing. mode={mode} alert={alert:0.00} thresh={alertThreshold:0.00} crouch={playerCrouched} respectCrouch={respectPlayerCrouchInGuard}");
            }

            // Escalate Guard -> Pursue when we have a target and alert crosses threshold
            if (autoEscalateToPursueOnTarget && snap.TargetPosition.HasValue && alert >= alertThreshold && mode == LibrarianPerceptionDriver.LibrarianMode.Guard)
            {
                mode = LibrarianPerceptionDriver.LibrarianMode.Pursue;
                VerboseLogger.SafeLog("[LibrarianController] Auto-escalated Guard -> Pursue");
                shouldPursue = true;
            }

            _repathTimer -= Time.fixedDeltaTime;
            if (shouldPursue && _agent != null && snap.TargetPosition.HasValue && _repathTimer <= 0f)
            {
                _agent.SetDestination(snap.TargetPosition.Value);
                VerboseLogger.SafeLog($"[LibrarianController] Repath to {snap.TargetPosition.Value} alert={alert:0.00} mode={mode}");
                _repathTimer = repathInterval;
            }

            if (driveAnimator && animator != null && !string.IsNullOrEmpty(alertParam))
            {
                if (!_alertParamChecked)
                {
                    _alertParamHash = Animator.StringToHash(alertParam);
                    foreach (var p in animator.parameters)
                    {
                        if (p.nameHash == _alertParamHash)
                        {
                            _alertParamExists = true;
                            break;
                        }
                    }
                    if (!_alertParamExists)
                    {
                        VerboseLogger.SafeLog($"[LibrarianController] Animator parameter '{alertParam}' not found; animation driving disabled.");
                        driveAnimator = false;
                    }
                    _alertParamChecked = true;
                }

                if (_alertParamExists)
                {
                    animator.SetFloat(_alertParamHash, alert);
                }
            }
        }

        public void SetMode(LibrarianPerceptionDriver.LibrarianMode newMode)
        {
            mode = newMode;
        }

        void HandlePursue(LibrarianPerceptionDriver.PerceptionSnapshot snap, float alert)
        {
            if (_agent == null) return;

            // Blend speed using alert or use fixed multiplier.
            float targetSpeed = baseSpeed;
            if (scaleSpeedWithAlert)
            {
                float blend = Mathf.Clamp01(alert); // alert already 0..1
                targetSpeed = Mathf.Lerp(baseSpeed, baseSpeed * pursueSpeedMultiplier, blend);
            }
            else
            {
                targetSpeed = baseSpeed * pursueSpeedMultiplier;
            }

            _agent.speed = targetSpeed;
        }
    }
}
