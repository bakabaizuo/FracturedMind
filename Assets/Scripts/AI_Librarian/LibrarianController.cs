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
        readonly struct DecisionResult
        {
            public readonly LibrarianPerceptionDriver.LibrarianMode Mode;
            public readonly bool ShouldPursue;
            public readonly string Reason;
            public readonly float DarknessDelta;
            public readonly float HalfDarknessDelta;
            public readonly float PursueAlertDelta;
            public readonly float InvestigateAlertDelta;

            public DecisionResult(
                LibrarianPerceptionDriver.LibrarianMode mode,
                bool shouldPursue,
                string reason,
                float darknessDelta,
                float halfDarknessDelta,
                float pursueAlertDelta,
                float investigateAlertDelta)
            {
                Mode = mode;
                ShouldPursue = shouldPursue;
                Reason = reason;
                DarknessDelta = darknessDelta;
                HalfDarknessDelta = halfDarknessDelta;
                PursueAlertDelta = pursueAlertDelta;
                InvestigateAlertDelta = investigateAlertDelta;
            }
        }

        [Header("Mode & thresholds")]
        [SerializeField] LibrarianPerceptionDriver.LibrarianMode mode = LibrarianPerceptionDriver.LibrarianMode.Guard;
        [SerializeField] float alertThreshold = 0.5f;
        [SerializeField] float investigateAlertThreshold = 0.2f;
        [SerializeField] float repathInterval = 0.25f;
        [SerializeField] bool autoEscalateToPursueOnTarget = true;
        [SerializeField] float baseSpeed = 3.5f;
        [SerializeField] float pursueSpeedMultiplier = 1.5f;
        float _cautionTimer;
        const float CautionDuration = 16f;
        [SerializeField] bool scaleSpeedWithAlert = true; // blends speed by alert
        [SerializeField] bool respectPlayerCrouchInGuard = true;
        [SerializeField] bool ignorePlayerCrouchForFollow = true;
        [SerializeField, Range(0f, 1f)] float cautionBeliefThreshold = 0.33f;
        [SerializeField, Range(0f, 1f)] float lightDarkThreshold = 0.3f;
        [SerializeField, Range(0f, 1f)] float darknessDarkThreshold = 0.9f;
        [SerializeField] float investigateSearchDistance = 4f;
        [SerializeField] float investigateNodeSwitchInterval = 2.2f;
        [SerializeField] LibrarianPerceptionDriver perception;

        [Header("Light / Darkness")]
        [SerializeField, Range(0f, 1f)] float darknessThreshold = 0.96f;
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
        bool _lastPlayerCrouched;
        bool _hasSeenPlayerBefore;
        float _lastDarknessDelta;
        float _lastHalfDarknessDelta;
        float _lastPursueAlertDelta;
        float _lastInvestigateAlertDelta;
        string _lastDecisionReason = "startup";
        Vector3 _investigateDestination;
        bool _hasInvestigateDestination;
        int _investigateNodeIndex;
        float _investigateNodeTimer;

        string _modeDebugKey;
        string _scaledAlertDebugKey;
        string _shouldPursueDebugKey;
        string _isDarkDebugKey;
        string _isFallbackDarkDebugKey;
        string _darknessThresholdDebugKey;
        string _darknessFallbackThresholdDebugKey;
        string _darknessAlertMultiplierDebugKey;
        string _investigateAlertThresholdDebugKey;
        string _darknessDeltaDebugKey;
        string _halfDarknessDeltaDebugKey;
        string _pursueAlertDeltaDebugKey;
        string _investigateAlertDeltaDebugKey;
        string _decisionReasonDebugKey;
        string _activeFlagsDetailKey;

        const int ActiveFlagShouldPursueBit = 0;
        const int ActiveFlagIsDarkBit = 1;
        const int ActiveFlagFallbackDarkBit = 2;
        const int ActiveFlagPlayerCrouchedBit = 3;

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
            _investigateAlertThresholdDebugKey = debugKeyPrefix + ".InvestigateThreshold";
            _darknessDeltaDebugKey = debugKeyPrefix + ".DarknessDelta";
            _halfDarknessDeltaDebugKey = debugKeyPrefix + ".HalfDarknessDelta";
            _pursueAlertDeltaDebugKey = debugKeyPrefix + ".PursueAlertDelta";
            _investigateAlertDeltaDebugKey = debugKeyPrefix + ".InvestigateAlertDelta";
            _decisionReasonDebugKey = debugKeyPrefix + ".DecisionReason";
            _activeFlagsDetailKey = debugKeyPrefix + ".ActiveFlagsDetail";
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
        public Vector3 RetriveLastPosition()//new 7/28/26
        {
        var distance = Vector3.Distance(transform.position, _investigateDestination);
            return distance ?? _investigateDestination; 

        } 
        public bool IsPlayerCrouching => perception != null && perception.IsPlayerCrouched();

        public void OnPerceptionUpdate(LibrarianPerceptionDriver.PerceptionSnapshot snap)
        {
            float alert = snap.AlertFlag;
            bool playerCrouched = IsPlayerCrouching || snap.PlayerIsCrouching;
            float darkness = Mathf.Clamp01(snap.Darkness);
            float halfDarkness = Mathf.Clamp01(snap.HalfDarkness);

            float darknessBlend = 1f - darkness;
            float fallbackBlend = 1f - halfDarkness;
            alert *= Mathf.Lerp(darknessAlertMultiplier, 1f, Mathf.Clamp01(Mathf.Max(darknessBlend, fallbackBlend)));
            bool isDark = IsDarkValue(snap, darkness);
            bool isFallbackDark = halfDarkness >= darknessFallbackThreshold || snap.LightLevel <= lightDarkThreshold;
            DecisionResult decision = EvaluateDecision(snap, alert, playerCrouched, darkness, halfDarkness);
            mode = decision.Mode;
            _lastScaledAlert = alert;
            _lastIsDark = isDark;
            _lastIsFallbackDark = isFallbackDark;
            _lastPlayerCrouched = playerCrouched;
            _lastDarknessDelta = decision.DarknessDelta;
            _lastHalfDarknessDelta = decision.HalfDarknessDelta;
            _lastPursueAlertDelta = decision.PursueAlertDelta;
            _lastInvestigateAlertDelta = decision.InvestigateAlertDelta;
            _lastDecisionReason = decision.Reason;

            if (snap.TargetPosition.HasValue && snap.Belief >= cautionBeliefThreshold)
            {
                _hasSeenPlayerBefore = true;
            }

            if (_agent == null)
            {
                VerboseLogger.SafeLog("[LibrarianController] No NavMeshAgent; cannot move.");
            }
            else
            {
                VerboseLogger.SafeLog($"[LibrarianController] Incoming snap alert={alert:0.00} belief={snap.Belief:0.00} targetSet={snap.TargetPosition.HasValue} mode={mode} dark={darkness:0.00} half={halfDarkness:0.00}");
            }

            // Debug: if we have a target and alert but are not pursuing, log why.
            if (snap.TargetPosition.HasValue && alert > 0.05f && !decision.ShouldPursue)
            {
                VerboseLogger.SafeLog($"[LibrarianController] Not pursuing. mode={mode} reason={decision.Reason} alert={alert:0.00} pursueDelta={decision.PursueAlertDelta:0.00} darknessDelta={decision.DarknessDelta:0.00} halfDelta={decision.HalfDarknessDelta:0.00} crouch={playerCrouched}");
            }

            _lastShouldPursue = decision.ShouldPursue;
            UpdateActiveFlags(decision.ShouldPursue, _lastIsDark, _lastIsFallbackDark, playerCrouched);

            if (mode == LibrarianPerceptionDriver.LibrarianMode.Investigate)
            {
                HandleInvestigateMove(snap);
            }
            else
            {
                _repathTimer -= Time.fixedDeltaTime;
                if (decision.ShouldPursue && _agent != null && snap.TargetPosition.HasValue && _repathTimer <= 0f)
                {
                    _agent.SetDestination(snap.TargetPosition.Value);
                    VerboseLogger.SafeLog($"[LibrarianController] Repath to {snap.TargetPosition.Value} alert={alert:0.00} mode={mode}");
                    _repathTimer = repathInterval;
                }
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
            DevConsoleBridge.RegisterTrackedValue(_investigateAlertThresholdDebugKey, () => investigateAlertThreshold);
            DevConsoleBridge.RegisterTrackedValue(_darknessDeltaDebugKey, () => _lastDarknessDelta);
            DevConsoleBridge.RegisterTrackedValue(_halfDarknessDeltaDebugKey, () => _lastHalfDarknessDelta);
            DevConsoleBridge.RegisterTrackedValue(_pursueAlertDeltaDebugKey, () => _lastPursueAlertDelta);
            DevConsoleBridge.RegisterTrackedValue(_investigateAlertDeltaDebugKey, () => _lastInvestigateAlertDelta);
            DevConsoleBridge.RegisterTrackedValue(_decisionReasonDebugKey, () => _lastDecisionReason);
            DevConsoleBridge.RegisterActiveFlagsDetail(_activeFlagsDetailKey, BuildActiveFlagsDetailText);
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
            DevConsoleBridge.UnregisterTrackedValue(_investigateAlertThresholdDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_darknessDeltaDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_halfDarknessDeltaDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_pursueAlertDeltaDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_investigateAlertDeltaDebugKey);
            DevConsoleBridge.UnregisterTrackedValue(_decisionReasonDebugKey);
            DevConsoleBridge.UnregisterActiveFlagsDetail(_activeFlagsDetailKey);
        }

        string BuildActiveFlagsDetailText()
        {
            return $"Mode: {mode}\n" +
                   $"ScaledAlert: {_lastScaledAlert:0.00}\n" +
                   $"ShouldPursue: {_lastShouldPursue}\n" +
                   $"IsDark: {_lastIsDark}\n" +
                   $"IsFallbackDark: {_lastIsFallbackDark}\n" +
                   $"PlayerCrouched: {_lastPlayerCrouched}\n" +
                   $"DecisionReason: {_lastDecisionReason}\n" +
                   $"DarknessDelta: {_lastDarknessDelta:0.00}\n" +
                   $"HalfDarknessDelta: {_lastHalfDarknessDelta:0.00}\n" +
                   $"PursueAlertDelta: {_lastPursueAlertDelta:0.00}\n" +
                   $"InvestigateAlertDelta: {_lastInvestigateAlertDelta:0.00}\n" +
                   $"DarknessThreshold: {darknessThreshold:0.00}\n" +
                   $"FallbackThreshold: {darknessFallbackThreshold:0.00}\n" +
                   $"InvestigateThreshold: {investigateAlertThreshold:0.00}\n" +
                   $"DarknessAlertMultiplier: {darknessAlertMultiplier:0.00}";
        }

        void UpdateActiveFlags(bool shouldPursue, bool isDark, bool isFallbackDark, bool playerCrouched)
        {
            var console = DebugDevConsoleUI.Instance;
            if (console == null)
                return;

            console.SetActiveFlagBit(ActiveFlagShouldPursueBit, shouldPursue);
            console.SetActiveFlagBit(ActiveFlagIsDarkBit, isDark);
            console.SetActiveFlagBit(ActiveFlagFallbackDarkBit, isFallbackDark);
            console.SetActiveFlagBit(ActiveFlagPlayerCrouchedBit, playerCrouched);
        }

        DecisionResult EvaluateDecision(
            LibrarianPerceptionDriver.PerceptionSnapshot snap,
            float alert,
            bool playerCrouched,
            float darkness,
            float halfDarkness)
        {
            float darknessDelta = darkness - darknessThreshold;
            float halfDarknessDelta = halfDarkness - darknessFallbackThreshold;
            float pursueAlertDelta = alert - alertThreshold;
            float investigateAlertDelta = alert - investigateAlertThreshold;
            bool isDark = IsDarkValue(snap, darkness);
            bool isFallbackDark = halfDarkness >= darknessFallbackThreshold || snap.LightLevel <= lightDarkThreshold;
            LibrarianPerceptionDriver.LibrarianMode resolvedMode = ResolveMode(snap, pursueAlertDelta, investigateAlertDelta, isDark);

            switch (resolvedMode)
            {
                case LibrarianPerceptionDriver.LibrarianMode.Passive:
                    return new DecisionResult(resolvedMode, false, "passive", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

                case LibrarianPerceptionDriver.LibrarianMode.Guard:
                    return EvaluateGuardMode(resolvedMode, playerCrouched, isDark, isFallbackDark, darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

                case LibrarianPerceptionDriver.LibrarianMode.Caution:
                    return EvaluateCautionMode(resolvedMode, snap, isDark, darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

                case LibrarianPerceptionDriver.LibrarianMode.Investigate:
                    return EvaluateInvestigateMode(resolvedMode, snap, isDark, isFallbackDark, darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

                case LibrarianPerceptionDriver.LibrarianMode.Pursue:
                    return EvaluatePursueMode(resolvedMode, snap, isDark, darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

                default:
                    return new DecisionResult(resolvedMode, false, "unknown-mode", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);
            }
        }
DecisionResult EvaluateCautionMode(
    LibrarianPerceptionDriver.LibrarianMode resolvedMode,
    LibrarianPerceptionDriver.PerceptionSnapshot snap,
    bool isDark,
    float darknessDelta,
    float halfDarknessDelta,
    float pursueAlertDelta,
    float investigateAlertDelta)
{
    if (_agent != null && _agent.hasPath && _cautionTimer == 0f)
    {
        _agent.ResetPath();
    }

    if (snap.TargetPosition.HasValue && snap.Belief >= cautionBeliefThreshold && !isDark)
        return new DecisionResult(resolvedMode, false, "caution-investigate", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

    if (!snap.TargetPosition.HasValue || snap.Belief < cautionBeliefThreshold || isDark)
        return new DecisionResult(resolvedMode, false, "caution-guard", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

    return new DecisionResult(resolvedMode, false, "caution-stare", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);
}
        DecisionResult EvaluateGuardMode(
            LibrarianPerceptionDriver.LibrarianMode resolvedMode,
            bool playerCrouched,
            bool isDark,
            bool isFallbackDark,
            float darknessDelta,
            float halfDarknessDelta,
            float pursueAlertDelta,
            float investigateAlertDelta)
        {
            if (playerCrouched && respectPlayerCrouchInGuard && !ignorePlayerCrouchForFollow)
            {
                VerboseLogger.SafeLog("[LibrarianController] Guard hold due to player crouch");
                return new DecisionResult(resolvedMode, false, "guard-crouch-hold", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);
            }

            if (playerCrouched)
            {
                VerboseLogger.SafeLog("[LibrarianController] Guard ignoring crouch for follow logic");
            }

            float requiredAlertDelta = pursueAlertDelta;
         // Complete pitch-black darkness logic takes absolute priority
    if (isDark)
        return new DecisionResult(resolvedMode, false, "guard-dark", darknessDelta, halfDarknessDelta, requiredAlertDelta, investigateAlertDelta);

    // Low light fallback adjustment—softens threshold requirements safely without locking down the state
    if (isFallbackDark)
    {
        VerboseLogger.SafeLog("[LibrarianController] Guard tracking within low light fallback range");
    }

    // Check if the current alert exceeds the threshold required to attack/pursue
    if (requiredAlertDelta < 0f)
        return new DecisionResult(resolvedMode, false, "guard-alert-low", darknessDelta, halfDarknessDelta, requiredAlertDelta, investigateAlertDelta);

    return new DecisionResult(resolvedMode, true, isFallbackDark ? "guard-fallback-pass" : "guard-pass", darknessDelta, halfDarknessDelta, requiredAlertDelta, investigateAlertDelta);
        }

        DecisionResult EvaluateInvestigateMode(
            LibrarianPerceptionDriver.LibrarianMode resolvedMode,
            LibrarianPerceptionDriver.PerceptionSnapshot snap,
            bool isDark,
            bool isFallbackDark,
            float darknessDelta,
            float halfDarknessDelta,
            float pursueAlertDelta,
            float investigateAlertDelta)
        {
            if (!snap.TargetPosition.HasValue)
                return new DecisionResult(resolvedMode, false, "investigate-no-target", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

            if (snap.Belief < 0.33f)
                return new DecisionResult(resolvedMode, false, "investigate-belief-low", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

            return new DecisionResult(resolvedMode, true, isFallbackDark ? "investigate-fallback-pass" : "investigate-pass", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);
        }

        DecisionResult EvaluatePursueMode(
            LibrarianPerceptionDriver.LibrarianMode resolvedMode,
            LibrarianPerceptionDriver.PerceptionSnapshot snap,
            bool isDark,
            float darknessDelta,
            float halfDarknessDelta,
            float pursueAlertDelta,
            float investigateAlertDelta)
        {
            if (isDark)
                return new DecisionResult(resolvedMode, false, "pursue-dark", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

            if (pursueAlertDelta < 0f)
                return new DecisionResult(resolvedMode, false, "pursue-alert-low", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);

            HandlePursue(snap, pursueAlertDelta + alertThreshold);

            return new DecisionResult(resolvedMode, true, "pursue-pass", darknessDelta, halfDarknessDelta, pursueAlertDelta, investigateAlertDelta);
        }

        LibrarianPerceptionDriver.LibrarianMode ResolveMode(
            LibrarianPerceptionDriver.PerceptionSnapshot snap,
            float pursueAlertDelta,
            float investigateAlertDelta,
            bool isDark)
        {
            if (mode == LibrarianPerceptionDriver.LibrarianMode.Caution)
            {
                _cautionTimer -= Time.fixedDeltaTime;

                if (pursueAlertDelta >= 0f && snap.TargetPosition.HasValue && !isDark)
                    return LibrarianPerceptionDriver.LibrarianMode.Pursue;

                if (snap.TargetPosition.HasValue && snap.Belief >= cautionBeliefThreshold && !isDark)
                {
                    VerboseLogger.SafeLog($"[LibrarianController] Caution escalated to Investigate from belief {snap.Belief:0.00}");
                    return LibrarianPerceptionDriver.LibrarianMode.Investigate;
                }

                if (!snap.TargetPosition.HasValue || snap.Belief < cautionBeliefThreshold || isDark || _cautionTimer <= 0f)
                {
                    VerboseLogger.SafeLog("[LibrarianController] Caution fell back to Guard.");
                    return LibrarianPerceptionDriver.LibrarianMode.Guard;
                }

                return LibrarianPerceptionDriver.LibrarianMode.Caution;
            }

            if (snap.TargetPosition.HasValue && (snap.Belief >= cautionBeliefThreshold || _hasSeenPlayerBefore))
            {
                VerboseLogger.SafeLog($"[LibrarianController] Investigation triggered by detected target or prior sighting (belief={snap.Belief:0.00}, seenBefore={_hasSeenPlayerBefore}).");
                return LibrarianPerceptionDriver.LibrarianMode.Investigate;
            }

            if (mode == LibrarianPerceptionDriver.LibrarianMode.Guard
                && snap.TargetPosition.HasValue
                && !isDark
                && investigateAlertDelta >= 0f)
            {
                VerboseLogger.SafeLog("[LibrarianController] Target spotted! Entering Caution state for 90s.");
                _cautionTimer = CautionDuration;
                return LibrarianPerceptionDriver.LibrarianMode.Caution;
            }

            if (mode == LibrarianPerceptionDriver.LibrarianMode.Investigate)
            {
                if (!snap.TargetPosition.HasValue)
                {
                    if (_hasSeenPlayerBefore)
                    {
                        return LibrarianPerceptionDriver.LibrarianMode.Investigate;
                    }

                    return LibrarianPerceptionDriver.LibrarianMode.Guard;
                }

                if (snap.Belief < 0.33f && !_hasSeenPlayerBefore)
                    return LibrarianPerceptionDriver.LibrarianMode.Guard;

                return LibrarianPerceptionDriver.LibrarianMode.Investigate;
            }

            if (autoEscalateToPursueOnTarget
                && snap.TargetPosition.HasValue
                && !isDark
                && pursueAlertDelta >= 0f
                && mode == LibrarianPerceptionDriver.LibrarianMode.Guard)
            {
                VerboseLogger.SafeLog("[LibrarianController] Auto-escalated Guard -> Pursue");
                return LibrarianPerceptionDriver.LibrarianMode.Pursue;
            }

            return mode;
        }

        public void SetMode(LibrarianPerceptionDriver.LibrarianMode newMode)
        {
            mode = newMode;
        }

        void HandleInvestigateMove(LibrarianPerceptionDriver.PerceptionSnapshot snap)
        {
            if (_agent == null) return;

            if (!snap.TargetPosition.HasValue)
            {
                if (!_hasInvestigateDestination || _agent.pathPending || !_agent.hasPath || Vector3.Distance(transform.position, _investigateDestination) <= _agent.stoppingDistance)
                {
                    Vector3 fallbackNode = transform.position + (transform.forward * investigateSearchDistance) + (Vector3.right * investigateSearchDistance * 0.5f);
                    if (NavMesh.SamplePosition(fallbackNode, out NavMeshHit fallbackHit, 2f, NavMesh.AllAreas))
                    {
                        fallbackNode = fallbackHit.position;
                    }

                    _investigateDestination = fallbackNode;
                    _hasInvestigateDestination = true;
                    _investigateNodeTimer = investigateNodeSwitchInterval;
                    _agent.SetDestination(_investigateDestination);
                    VerboseLogger.SafeLog("[LibrarianController] Investigate fallback move to search node");
                }

                return;
            }

            _investigateNodeTimer -= Time.fixedDeltaTime;
            bool shouldRefreshDestination = !_hasInvestigateDestination || _investigateNodeTimer <= 0f || !_agent.hasPath || Vector3.Distance(transform.position, _investigateDestination) <= _agent.stoppingDistance;
            if (!shouldRefreshDestination)
                return;

            Vector3 anchor = snap.TargetPosition.Value;
            Vector3 approach = anchor - transform.position;
            if (approach.sqrMagnitude < 0.001f)
            {
                approach = transform.forward;
            }
            else
            {
                approach.Normalize();
            }

            Vector3 side = Vector3.Cross(approach, Vector3.up).normalized;
            Vector3 nodeA = anchor + approach * investigateSearchDistance * 0.7f + side * investigateSearchDistance * 0.45f;
            Vector3 nodeB = anchor - approach * investigateSearchDistance * 0.7f - side * investigateSearchDistance * 0.45f;
            Vector3 nextNode = _investigateNodeIndex == 0 ? nodeA : nodeB;
            _investigateNodeIndex = 1 - _investigateNodeIndex;

            if (NavMesh.SamplePosition(nextNode, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                nextNode = hit.position;
            }

            _investigateDestination = nextNode;
            _hasInvestigateDestination = true;
            _investigateNodeTimer = investigateNodeSwitchInterval;
            _agent.SetDestination(_investigateDestination);
            VerboseLogger.SafeLog($"[LibrarianController] Investigate node -> {nextNode}");
        }

        bool IsDarkValue(LibrarianPerceptionDriver.PerceptionSnapshot snap, float darkness)
        {
            bool lightDark = snap.LightLevel <= lightDarkThreshold;
            bool darknessDark = darkness >= darknessDarkThreshold;
            return lightDark || darknessDark;
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
