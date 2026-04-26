using UnityEngine;
using UnityEngine.AI;
using FracturedStudios.UI;

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

        [Header("Light / Darkness")]
        [SerializeField, Range(0f, 1f)] float darknessThreshold = 0.65f;
        [SerializeField, Range(0f, 1f)] float darknessFallbackThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] float darknessAlertMultiplier = 0.5f;

        [Header("Animation hooks")]
        [SerializeField] Animator animator;
        [SerializeField] string alertParam = "Alert";
        [SerializeField] bool driveAnimator = true;

        public NavMeshAgent _agent;
        float _repathTimer;
        bool _alertParamChecked;
        bool _alertParamExists;
        int _alertParamHash;
        float _lastScaledAlert;
        bool _lastShouldPursue;
        bool _lastIsDark;
        bool _lastIsFallbackDark;

        string _modeDebugKey;
        string _scaledAlertDebugKey;
        string _shouldPursueDebugKey;
        string _isDarkDebugKey;
        string _isFallbackDarkDebugKey;
        string _darknessThresholdDebugKey;
        string _darknessFallbackThresholdDebugKey;
        string _darknessAlertMultiplierDebugKey;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (perception == null) perception = GetComponent<LibrarianPerceptionDriver>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            string debugKeyPrefix = $"AI.{gameObject.name}.{GetInstanceID()}.Controller";
            _modeDebugKey = debugKeyPrefix + ".Mode";
            _scaledAlertDebugKey = debugKeyPrefix + ".ScaledAlert";
            _shouldPursueDebugKey = debugKeyPrefix + ".ShouldPursue";
            _isDarkDebugKey = debugKeyPrefix + ".IsDark";
            _isFallbackDarkDebugKey = debugKeyPrefix + ".IsFallbackDark";
            _darknessThresholdDebugKey = debugKeyPrefix + ".DarknessThreshold";
            _darknessFallbackThresholdDebugKey = debugKeyPrefix + ".FallbackThreshold";
            _darknessAlertMultiplierDebugKey = debugKeyPrefix + ".DarknessAlertMultiplier";
            if (animator == null)
            {
                VerboseLogger.SafeLog("[LibrarianController] No Animator found on self or children.");
            }
        }

        void OnEnable()
        {
            RegisterDebugTrackedValues();
        }

        void OnDisable()
        {
            UnregisterDebugTrackedValues();
        }

        public bool IsPlayerCrouching => perception != null && perception.IsPlayerCrouched();

        public void OnPerceptionUpdate(LibrarianPerceptionDriver.PerceptionSnapshot snap)
        {
            bool shouldPursue = false;
            float alert = snap.AlertFlag;
            bool playerCrouched = IsPlayerCrouching || snap.PlayerIsCrouching;
            float darkness = Mathf.Clamp01(snap.Darkness);
            float halfDarkness = Mathf.Clamp01(snap.HalfDarkness);
            bool isDark = darkness >= darknessThreshold;
            bool isFallbackDark = darkness >= darknessFallbackThreshold;

            float darknessBlend = 1f - darkness;
            float fallbackBlend = 1f - halfDarkness;
            alert *= Mathf.Lerp(darknessAlertMultiplier, 1f, Mathf.Clamp01(Mathf.Max(darknessBlend, fallbackBlend)));
            _lastScaledAlert = alert;
            _lastIsDark = isDark;
            _lastIsFallbackDark = isFallbackDark;

            if (_agent == null)
            {
                VerboseLogger.SafeLog("[LibrarianController] No NavMeshAgent; cannot move.");
            }
            else
            {
                VerboseLogger.SafeLog($"[LibrarianController] Incoming snap alert={alert:0.00} belief={snap.Belief:0.00} targetSet={snap.TargetPosition.HasValue} mode={mode} dark={darkness:0.00} half={halfDarkness:0.00}");
            }

            switch (mode)
            {
                case LibrarianPerceptionDriver.LibrarianMode.Passive:
                    shouldPursue = false;
                    break;
                case LibrarianPerceptionDriver.LibrarianMode.Guard:
                    shouldPursue = !isDark && alert >= alertThreshold;
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
                    else if (isFallbackDark)
                    {
                        shouldPursue = alert >= (alertThreshold * 1.25f);
                        VerboseLogger.SafeLog("[LibrarianController] Guard softened by low light fallback");
                    }
                    break;
                case LibrarianPerceptionDriver.LibrarianMode.Pursue:
                    shouldPursue = !isDark && alert >= alertThreshold;

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
            if (autoEscalateToPursueOnTarget && snap.TargetPosition.HasValue && !isDark && alert >= alertThreshold && mode == LibrarianPerceptionDriver.LibrarianMode.Guard)
            {
                mode = LibrarianPerceptionDriver.LibrarianMode.Pursue;
                VerboseLogger.SafeLog("[LibrarianController] Auto-escalated Guard -> Pursue");
                shouldPursue = true;
            }

            _lastShouldPursue = shouldPursue;

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

        void RegisterDebugTrackedValues()
        {
            DevConsoleBridge.RegisterTrackedValue(_modeDebugKey, () => mode.ToString());
            DevConsoleBridge.RegisterTrackedValue(_scaledAlertDebugKey, () => _lastScaledAlert);
            DevConsoleBridge.RegisterTrackedValue(_shouldPursueDebugKey, () => _lastShouldPursue);
            DevConsoleBridge.RegisterTrackedValue(_isDarkDebugKey, () => _lastIsDark);
            DevConsoleBridge.RegisterTrackedValue(_isFallbackDarkDebugKey, () => _lastIsFallbackDark);
            DevConsoleBridge.RegisterTrackedValue(_darknessThresholdDebugKey, () => darknessThreshold);
            DevConsoleBridge.RegisterTrackedValue(_darknessFallbackThresholdDebugKey, () => darknessFallbackThreshold);
            DevConsoleBridge.RegisterTrackedValue(_darknessAlertMultiplierDebugKey, () => darknessAlertMultiplier);
        }

        void UnregisterDebugTrackedValues()
        {
            DevConsoleBridge.UnregisterTrackedValue(_modeDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_scaledAlertDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_shouldPursueDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_isDarkDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_isFallbackDarkDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_darknessThresholdDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_darknessFallbackThresholdDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_darknessAlertMultiplierDebugKey);
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
