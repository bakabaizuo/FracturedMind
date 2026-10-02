    using UnityEngine;
    using UnityEngine.AI;
    using FracturedStudios.UI;
    using System;
    using System.Linq;
    using System.Collections;
    using UnityEngine;
            namespace FracturedMind.AI
                    {
                        /// <summary>
                        /// Flat switch-based replacement for LibrarianController.
                        /// Consumes perception snapshots and drives NavMesh/animation via a direct
                        /// OnPerceptionUpdate -> UpdateAI -> switch(mode) dispatch. Each mode method
                        /// performs its own acting instead of returning a verdict to a caller.
                        /// </summary>
                        [RequireComponent(typeof(NavMeshAgent))]
                        public sealed class LibrarianAiStates : MonoBehaviour
                        {
                            [Header("Mode & thresholds")]
                            [SerializeField] LibrarianPerceptionDriver.LibrarianMode mode = LibrarianPerceptionDriver.LibrarianMode.Guard;
                            [SerializeField] float alertThreshold = 0.5f;
                            [SerializeField] float investigateAlertThreshold = 0.2f;
                            [SerializeField] float repathInterval = 0.25f;
                            [SerializeField] bool autoEscalateToPursueOnTarget = true;
                            [SerializeField] float baseSpeed = 3.5f;
                            [SerializeField] float pursueSpeedMultiplier = 1.5f;
                            [SerializeField] bool scaleSpeedWithAlert = true;
                            [SerializeField] bool respectPlayerCrouchInGuard = true;
                            [SerializeField] bool ignorePlayerCrouchForFollow = true;
                            [SerializeField, Range(0f, 1f)] float cautionBeliefThreshold = 0.33f;
                            [SerializeField, Range(0f, 1f)] float lightDarkThreshold = 0.3f;
                            [SerializeField, Range(0f, 1f)] float darknessDarkThreshold = 0.9f;
                            [SerializeField] float investigateSearchDistance = 4f;
                            [SerializeField] float investigateNodeSwitchInterval = 2.2f;
                            [SerializeField] LibrarianPerceptionDriver perception;
                            [SerializeField] LibrarianAnimatorDriver animatorDriver;
                            [Header("Light / Darkness")]
                            [SerializeField, Range(0f, 1f)] float darknessThreshold = 0.96f;
                            [SerializeField, Range(0f, 1f)] float darknessFallbackThreshold = 0.35f;
                            [SerializeField, Range(0f, 1f)] float darknessAlertMultiplier = 0.5f;

                            [Header("Animation hooks")]
                            [SerializeField] Animator animator;
                            [SerializeField] string alertParam = "Alert";
                            [SerializeField] bool driveAnimator = true;

                            const float CautionDuration = 16f;
                            float _cautionTimer;
                            float _repathTimer;
                            bool _hasSeenPlayerBefore;
                    
                                

                                // Return cache to store the eye's original orientation
                                private Quaternion? _originalRotationCache;

                                // Predicate to check validity
                            private readonly Predicate<Transform> _isValid = eye => eye != null;
                            public NavMeshAgent _agent;

                            float _scaledAlert;
                            bool _shouldPursue;
                            bool _isDark;
                            bool _isFallbackDark;
                            bool _playerCrouched;
                            string _stateReason = "startup";

                            float _lastDarknessDelta;
                            float _lastHalfDarknessDelta;
                            float _lastPursueAlertDelta;
                            float _lastInvestigateAlertDelta;

                            Vector3 _investigateDestination;
                            bool _hasInvestigateDestination;
                            int _investigateNodeIndex;
                            float _investigateNodeTimer;

                            bool _alertParamChecked;
                            bool _alertParamExists;
                            int _alertParamHash;

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
                            string _stateReasonDebugKey;
                            string _activeFlagsDetailKey;

                            const int ActiveFlagShouldPursueBit = 0;
                            const int ActiveFlagIsDarkBit = 1;
                            const int ActiveFlagFallbackDarkBit = 2;
                            const int ActiveFlagPlayerCrouchedBit = 3;

                            public bool IsPlayerCrouching => perception != null && perception.IsPlayerCrouched();

                            void Awake()
                            {
                                _agent = GetComponent<NavMeshAgent>();
                                if (animatorDriver == null) animatorDriver = GetComponent<LibrarianAnimatorDriver>();
                                if (perception == null) perception = GetComponent<LibrarianPerceptionDriver>();
                                if (animator == null) animator = GetComponentInChildren<Animator>();
                                string debugKeyPrefix = $"AI.{gameObject.name}.{GetInstanceID()}.AiStates";
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
                                _stateReasonDebugKey = debugKeyPrefix + ".StateReason";
                                _activeFlagsDetailKey = debugKeyPrefix + ".ActiveFlagsDetail";
                                if (animator == null)
                                {
                                    VerboseLogger.SafeLog("[LibrarianAiStates] No Animator found on self or children.");
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

                            public Vector3 RetriveLastPosition()
                            {
                                return transform.position - _investigateDestination;
                            }

            public struct ScanData
                {
                    public float TargetAngle;
                    public float TurnSpeed;
                }
                            public void ScanForPlayer()
                            {
                            if (_isValid.Invoke(perception?.eye))
                            {
                                // If is null set (??=) - caches the original rotation once
                                _originalRotationCache ??= perception.eye.localRotation;

                                animatorDriver.LibLostPlayerAnimation();
                            VerboseLogger.SafeLog("[LibrarianAiStates] Starting scan for player sequence.");
                                // Start scanning sequence
                                StartCoroutine(ExecuteScanRoutine());
                            }




                            }
                    private IEnumerator ExecuteScanRoutine()
                        {
                            // Data[] / new Data{,,,} Semantics
                            ScanData[] scanSequence = new ScanData[]
                            {
                                new ScanData { TargetAngle = -135f, TurnSpeed = 3f }, // Look Left 
                                new ScanData { TargetAngle = 135f,  TurnSpeed = 3f }, // Look Right (270 total arc)
                                new ScanData { TargetAngle = 0f,    TurnSpeed = 4f }  // Return to Center faster
                            };

                            float[] skippedAngles = { 999f }; 

                            // Func, =>, and "if not this contains" logic
                            Func<ScanData, bool> isValidStep = data => !skippedAngles.Contains(data.TargetAngle);

                            // Where and explicit <T> usage
                            ScanData[] validSteps = scanSequence.Where<ScanData>(isValidStep).ToArray();

                            // Accessing the final center step using ^ (Index from end)
                            ScanData returnToCenterStep = validSteps[^1];

                            foreach (var step in validSteps)
                            {
                                // Calculate target rotation relative to the cached original rotation
                                Quaternion targetRotation = _originalRotationCache.Value * Quaternion.Euler(0, step.TargetAngle, 0);

                                // Smoothly rotate until the angle is within a small threshold
                                while (Quaternion.Angle(perception.eye.localRotation, targetRotation) > 0.1f)
                                {
                                    perception.eye.localRotation = Quaternion.Slerp(
                                        perception.eye.localRotation,
                                        targetRotation,
                                        Time.deltaTime * step.TurnSpeed
                                    );
                                    
                                    yield return null; 
                                }

                                // Snap to exact target to prevent floating point offset
                                perception.eye.localRotation = targetRotation;
                                
                                // Brief pause at the end of each look direction
                                yield return new WaitForSeconds(0.25f);
                            }

                            // Final Return cache reset to ensure exact starting position
                            perception.eye.localRotation = _originalRotationCache.Value;
                        }

                            public void OnPerceptionUpdate(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                _playerCrouched = IsPlayerCrouching || snap.PlayerIsCrouching;
                                float darkness = Mathf.Clamp01(snap.Darkness);
                                float halfDarkness = Mathf.Clamp01(snap.HalfDarkness);

                                float darknessBlend = 1f - darkness;
                                float fallbackBlend = 1f - halfDarkness;
                                _scaledAlert = snap.AlertFlag * Mathf.Lerp(darknessAlertMultiplier, 1f, Mathf.Clamp01(Mathf.Max(darknessBlend, fallbackBlend)));

                                _isDark = IsDark(snap, darkness);
                                _isFallbackDark = halfDarkness >= darknessFallbackThreshold || snap.LightLevel <= lightDarkThreshold;

                                float darknessDelta = darkness - darknessThreshold;
                                float halfDarknessDelta = halfDarkness - darknessFallbackThreshold;
                                float pursueAlertDelta = _scaledAlert - alertThreshold;
                                float investigateAlertDelta = _scaledAlert - investigateAlertThreshold;
                                _lastDarknessDelta = darknessDelta;
                                _lastHalfDarknessDelta = halfDarknessDelta;
                                _lastPursueAlertDelta = pursueAlertDelta;
                                _lastInvestigateAlertDelta = investigateAlertDelta;

                                if (snap.TargetPosition.HasValue && snap.Belief < cautionBeliefThreshold)
                                {
                                    _hasSeenPlayerBefore = true;
                                }

                                if (_agent == null)
                                {
                                    VerboseLogger.SafeLog("[LibrarianAiStates] No NavMeshAgent; cannot move.");
                                }
                                else
                                {
                                    VerboseLogger.SafeLog($"[LibrarianAiStates] Incoming snap alert={_scaledAlert:0.00} belief={snap.Belief:0.00} targetSet={snap.TargetPosition.HasValue} mode={mode} dark={darkness:0.00} half={halfDarkness:0.00}");
                                }

                                if (snap.TargetPosition.HasValue && _scaledAlert > 0.05f && !_shouldPursue)
                                {
                                    VerboseLogger.SafeLog($"[LibrarianAiStates] Not pursuing. mode={mode} reason={_stateReason} alert={_scaledAlert:0.00} pursueDelta={pursueAlertDelta:0.00} darknessDelta={darknessDelta:0.00} halfDelta={halfDarknessDelta:0.00} crouch={_playerCrouched}");
                                }

                                UpdateAI(snap);
                                DriveAnimator();
                            }

                            void UpdateAI(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                mode = NextMode(snap);
                                _shouldPursue = false;

                                switch (mode)
                                {
                                    case LibrarianPerceptionDriver.LibrarianMode.Passive:
                                        Passive(snap);
                                        break;
                                    case LibrarianPerceptionDriver.LibrarianMode.Guard:
                                        Guard(snap);
                                        break;
                                    case LibrarianPerceptionDriver.LibrarianMode.Caution:
                                        Caution(snap);
                                        break;
                                    case LibrarianPerceptionDriver.LibrarianMode.Investigate:
                                        Investigate(snap);
                                        break;
                                    case LibrarianPerceptionDriver.LibrarianMode.Pursue:
                                        Pursue(snap);
                                        break;
                                    default:
                                        _stateReason = "unknown-mode";
                                        break;
                                }

                                UpdateActiveFlags();
                            }

                            LibrarianPerceptionDriver.LibrarianMode NextMode(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                float darkness = Mathf.Clamp01(snap.Darkness);
                                float halfDarkness = Mathf.Clamp01(snap.HalfDarkness);
                                bool isDark = IsDark(snap, darkness);
                                float pursueAlertDelta = _scaledAlert - alertThreshold;
                                float investigateAlertDelta = _scaledAlert - investigateAlertThreshold;

                                if (mode == LibrarianPerceptionDriver.LibrarianMode.Caution)
                                {
                                    _cautionTimer -= Time.fixedDeltaTime;

                                    if (pursueAlertDelta >= 0f && snap.TargetPosition.HasValue && !isDark)
                                        return LibrarianPerceptionDriver.LibrarianMode.Pursue;

                                    if (snap.TargetPosition.HasValue && snap.Belief < cautionBeliefThreshold && !isDark)
                                    {
                                        VerboseLogger.SafeLog($"[LibrarianAiStates] Caution escalated to Investigate from belief {snap.Belief:0.00}");
                                        return LibrarianPerceptionDriver.LibrarianMode.Investigate;
                                    }

                                    if (!snap.TargetPosition.HasValue || snap.Belief < cautionBeliefThreshold || isDark || _cautionTimer <= 0f)
                                    {
                                        VerboseLogger.SafeLog("[LibrarianAiStates] Caution fell back to Guard.");
                                        return LibrarianPerceptionDriver.LibrarianMode.Guard;
                                    }

                                    return LibrarianPerceptionDriver.LibrarianMode.Caution;
                                }

                                if (snap.TargetPosition.HasValue && (snap.Belief >= cautionBeliefThreshold || _hasSeenPlayerBefore))
                                {
                                    VerboseLogger.SafeLog($"[LibrarianAiStates] Investigation triggered by detected target or prior sighting (belief={snap.Belief:0.00}, seenBefore={_hasSeenPlayerBefore}).");
                                    return LibrarianPerceptionDriver.LibrarianMode.Investigate;
                                }

                                if (mode == LibrarianPerceptionDriver.LibrarianMode.Guard
                                    && snap.TargetPosition.HasValue
                                    && !isDark
                                    && investigateAlertDelta >= 0f)
                                {
                                    VerboseLogger.SafeLog("[LibrarianAiStates] Target spotted! Entering Caution state for 90s.");
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
                                    VerboseLogger.SafeLog("[LibrarianAiStates] Auto-escalated Guard -> Pursue");
                                    return LibrarianPerceptionDriver.LibrarianMode.Pursue;
                                }

                                return mode;
                            }

                            void Passive(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                if (_agent != null && _agent.hasPath)
                                {
                                    _agent.ResetPath();
                                }
                                _shouldPursue = false;
                                _stateReason = "passive";
                            }

                            void Guard(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                float darkness = Mathf.Clamp01(snap.Darkness);
                                bool isDark = IsDark(snap, darkness);
                                float pursueAlertDelta = _scaledAlert - alertThreshold;

                                if (_playerCrouched && respectPlayerCrouchInGuard && !ignorePlayerCrouchForFollow)
                                {
                                    VerboseLogger.SafeLog("[LibrarianAiStates] Guard hold due to player crouch");
                                    _stateReason = "guard-crouch-hold";
                                    return;
                                }

                                if (_playerCrouched)
                                {
                                    VerboseLogger.SafeLog("[LibrarianAiStates] Guard ignoring crouch for follow logic");
                                }

                                if (isDark)
                                {
                                    _stateReason = "guard-dark";
                                    return;
                                }

                                if (_isFallbackDark)
                                {
                                    VerboseLogger.SafeLog("[LibrarianAiStates] Guard tracking within low light fallback range");
                                }

                                if (pursueAlertDelta < 0f)
                                {
                                    _stateReason = "guard-alert-low";
                                    return;
                                }

                                _shouldPursue = true;
                                _stateReason = _isFallbackDark ? "guard-fallback-pass" : "guard-pass";
                                RepathTo(snap);
                            }

                            void Caution(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                if (_agent != null && _agent.hasPath && _cautionTimer == 0f)
                                {
                                    _agent.ResetPath();
                                }

                                bool isDark = IsDark(snap, Mathf.Clamp01(snap.Darkness));

                                if (snap.TargetPosition.HasValue && snap.Belief >= cautionBeliefThreshold && !isDark)
                                {
                                    _stateReason = "caution-investigate";
                                    return;
                                }

                                if (!snap.TargetPosition.HasValue || snap.Belief < cautionBeliefThreshold || isDark)
                                {
                                    _stateReason = "caution-guard";
                                    return;
                                }

                                _stateReason = "caution-stare";
                            }

                            void Investigate(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                if (_agent == null)
                                {
                                    _stateReason = "investigate-no-agent";
                                    return;
                                }

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
                                        VerboseLogger.SafeLog("[LibrarianAiStates] Investigate fallback move to search node");
                                    
                                        ScanForPlayer();
                                    }

                                    _stateReason = "investigate-no-target";
                                    return;
                                }

                                if (snap.Belief < 0.33f)
                                {
                                    ScanForPlayer();
                                    _stateReason = "investigate-belief-low";
                                    return;
                                }

                                _investigateNodeTimer -= Time.fixedDeltaTime;
                                bool shouldRefreshDestination = !_hasInvestigateDestination || _investigateNodeTimer <= 0f || !_agent.hasPath || Vector3.Distance(transform.position, _investigateDestination) <= _agent.stoppingDistance;
                                if (!shouldRefreshDestination)
                                {
                                    _stateReason = "investigate-pass";
                                    return;
                                }

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
                                ScanForPlayer();
                                _investigateDestination = nextNode;
                                _hasInvestigateDestination = true;
                                _investigateNodeTimer = investigateNodeSwitchInterval;
                                _agent.SetDestination(_investigateDestination);
                                VerboseLogger.SafeLog($"[LibrarianAiStates] Investigate node -> {nextNode}");
                                _stateReason = _isFallbackDark ? "investigate-fallback-pass" : "investigate-pass";
                            }

                            void Pursue(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                float darkness = Mathf.Clamp01(snap.Darkness);
                                bool isDark = IsDark(snap, darkness);
                                float pursueAlertDelta = _scaledAlert - alertThreshold;

                                if (isDark)
                                {
                                    _stateReason = "pursue-dark";
                                    return;
                                }

                                if (pursueAlertDelta < 0f)
                                {
                                    _stateReason = "pursue-alert-low";
                                    return;
                                }

                                float targetSpeed = baseSpeed;
                                if (scaleSpeedWithAlert)
                                {
                                    float blend = Mathf.Clamp01(_scaledAlert);
                                    targetSpeed = Mathf.Lerp(baseSpeed, baseSpeed * pursueSpeedMultiplier, blend);
                                }
                                else
                                {
                                    targetSpeed = baseSpeed * pursueSpeedMultiplier;
                                }

                                if (_agent != null)
                                {
                                    _agent.speed = targetSpeed;
                                }

                                _shouldPursue = true;
                                _stateReason = "pursue-pass";
                                RepathTo(snap);
                            }

                            void RepathTo(LibrarianPerceptionDriver.PerceptionSnapshot snap)
                            {
                                if (_agent == null || !snap.TargetPosition.HasValue)
                                    return;

                                _repathTimer -= Time.fixedDeltaTime;
                                if (_repathTimer <= 0f)
                                {
                                    _agent.SetDestination(snap.TargetPosition.Value);
                                    VerboseLogger.SafeLog($"[LibrarianAiStates] Repath to {snap.TargetPosition.Value} alert={_scaledAlert:0.00} mode={mode}");
                                    _repathTimer = repathInterval;
                                }
                            }

                            bool IsDark(LibrarianPerceptionDriver.PerceptionSnapshot snap, float darkness)
                            {
                                bool lightDark = snap.LightLevel <= lightDarkThreshold;
                                bool darknessDark = darkness >= darknessDarkThreshold;
                                return lightDark || darknessDark;
                            }

                            void DriveAnimator()
                            {
                                if (!driveAnimator || animator == null || string.IsNullOrEmpty(alertParam))
                                    return;

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
                                        VerboseLogger.SafeLog($"[LibrarianAiStates] Animator parameter '{alertParam}' not found; animation driving disabled.");
                                        driveAnimator = false;
                                    }
                                    _alertParamChecked = true;
                                }

                                if (_alertParamExists)
                                {
                                    animator.SetFloat(_alertParamHash, _scaledAlert);
                                }
                            }

                            public void SetMode(LibrarianPerceptionDriver.LibrarianMode newMode)
                            {
                                mode = newMode;
                            }

                            void UpdateActiveFlags()
                            {
                                var console = DebugDevConsoleUI.Instance;
                                if (console == null)
                                    return;

                                console.SetActiveFlagBit(ActiveFlagShouldPursueBit, _shouldPursue);
                                console.SetActiveFlagBit(ActiveFlagIsDarkBit, _isDark);
                                console.SetActiveFlagBit(ActiveFlagFallbackDarkBit, _isFallbackDark);
                                console.SetActiveFlagBit(ActiveFlagPlayerCrouchedBit, _playerCrouched);
                            }

                            void RegisterDebugTrackedValues()
                            {
                                DevConsoleBridge.RegisterTrackedValue(_modeDebugKey, () => mode.ToString());
                                DevConsoleBridge.RegisterTrackedValue(_scaledAlertDebugKey, () => _scaledAlert);
                                DevConsoleBridge.RegisterTrackedValue(_shouldPursueDebugKey, () => _shouldPursue);
                                DevConsoleBridge.RegisterTrackedValue(_isDarkDebugKey, () => _isDark);
                                DevConsoleBridge.RegisterTrackedValue(_isFallbackDarkDebugKey, () => _isFallbackDark);
                                DevConsoleBridge.RegisterTrackedValue(_darknessThresholdDebugKey, () => darknessThreshold);
                                DevConsoleBridge.RegisterTrackedValue(_darknessFallbackThresholdDebugKey, () => darknessFallbackThreshold);
                                DevConsoleBridge.RegisterTrackedValue(_darknessAlertMultiplierDebugKey, () => darknessAlertMultiplier);
                                DevConsoleBridge.RegisterTrackedValue(_investigateAlertThresholdDebugKey, () => investigateAlertThreshold);
                                DevConsoleBridge.RegisterTrackedValue(_darknessDeltaDebugKey, () => _lastDarknessDelta);
                                DevConsoleBridge.RegisterTrackedValue(_halfDarknessDeltaDebugKey, () => _lastHalfDarknessDelta);
                                DevConsoleBridge.RegisterTrackedValue(_pursueAlertDeltaDebugKey, () => _lastPursueAlertDelta);
                                DevConsoleBridge.RegisterTrackedValue(_investigateAlertDeltaDebugKey, () => _lastInvestigateAlertDelta);
                                DevConsoleBridge.RegisterTrackedValue(_stateReasonDebugKey, () => _stateReason);
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
                                DevConsoleBridge.UnregisterTrackedValue(_stateReasonDebugKey);
                                DevConsoleBridge.UnregisterActiveFlagsDetail(_activeFlagsDetailKey);
                            }

                            string BuildActiveFlagsDetailText()
                            {
                                return $"Mode: {mode}\n" +
                                    $"ScaledAlert: {_scaledAlert:0.00}\n" +
                                    $"ShouldPursue: {_shouldPursue}\n" +
                                    $"IsDark: {_isDark}\n" +
                                    $"IsFallbackDark: {_isFallbackDark}\n" +
                                    $"PlayerCrouched: {_playerCrouched}\n" +
                                    $"StateReason: {_stateReason}\n" +
                                    $"DarknessDelta: {_lastDarknessDelta:0.00}\n" +
                                    $"HalfDarknessDelta: {_lastHalfDarknessDelta:0.00}\n" +
                                    $"PursueAlertDelta: {_lastPursueAlertDelta:0.00}\n" +
                                    $"InvestigateAlertDelta: {_lastInvestigateAlertDelta:0.00}\n" +
                                    $"DarknessThreshold: {darknessThreshold:0.00}\n" +
                                    $"FallbackThreshold: {darknessFallbackThreshold:0.00}\n" +
                                    $"InvestigateThreshold: {investigateAlertThreshold:0.00}\n" +
                                    $"DarknessAlertMultiplier: {darknessAlertMultiplier:0.00}";
                            }
                        }
                    }
