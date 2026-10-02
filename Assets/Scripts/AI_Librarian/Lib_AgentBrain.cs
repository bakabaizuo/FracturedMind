        using System.Collections;
        using System.Collections.Generic;
        using System.Linq;
        using System;
        using FracturedStudios.Invoker;
        using UnityEngine;
        using UnityEngine.AI;
        using FracturedStudios.UI;

        namespace FracturedMind.AI
        {
            [DisallowMultipleComponent]
            [RequireComponent(typeof(NavMeshAgent))]
            public sealed class Lib_AgentBrain : MonoBehaviour, IDisposable
            {
                [Header("References")]
                [SerializeField] private LibrarianPerceptionDriver perception;
                [SerializeField] private LibrarianAnimatorDriver animatorDriver;
                [SerializeField] private Animator animator;

                [Header("Movement")]
                [SerializeField] private float baseSpeed = 3.5f;
                [SerializeField] private float pursueSpeedMultiplier = 1.5f;
                [SerializeField] private float repathInterval = 0.25f;
                [SerializeField] private bool scaleSpeedWithAlert = true;

                [Header("Awareness")]
                [SerializeField] private float alertThreshold = 0.5f;
                [SerializeField] private float investigateAlertThreshold = 0.2f;
                [SerializeField, Range(0f, 1f)] private float cautionBeliefThreshold = 0.31f;

                [Header("Darkness")]
                [SerializeField, Range(0f, 1f)] private float darknessThreshold = 0.96f;
                [SerializeField, Range(0f, 1f)] private float lightDarkThreshold = 0.3f;
                [SerializeField, Range(0f, 1f)] private float darknessFallbackThreshold = 0.35f;
                [SerializeField, Range(0f, 1f)] private float darknessAlertMultiplier = 0.5f;

                [Header("Investigation")]
                [SerializeField] private float investigateSearchDistance = 4f;
                [SerializeField] private float investigateNodeSwitchInterval = 2.2f;
                [SerializeField, Min(0.1f)] private float lostTargetInvestigationDuration = 6f;
                [SerializeField, Min(1)] private int lostTargetSearchPassLimit = 3;

                [Header("Behaviour")]
                [SerializeField] private bool respectPlayerCrouch = true;
                [SerializeField] private bool automaticallyPursue = true;

                [Header("Animation")]
                [SerializeField] private string alertParam = "Alert";

                private NavMeshAgent _agent;
                private float _alert;
                private float _belief;
                private bool _playerCrouched;
                private bool _isDark;
                private bool _isFallbackDark;
                private bool _hasCurrentTarget;
                private bool _targetWasJustLost;
                private bool _hasSeenTarget;
                private Vector3 _lastKnownTargetPosition;
                private Vector3 _searchPosition;
                private bool _hasSearchPosition;
                private int _searchSide;
                private int _lostTargetSearchPasses;
                private float _lostTargetExpiryTime;
                private float _searchTimer;
                private float _repathTimer;
                private Quaternion? _originalEyeRotation;
                private Coroutine _scanRoutine;
                private bool _alertParamResolved;
                private bool _hasAlertParam;
                private bool _alertParamIsBool;
                private int _alertParamHash;

                private Func<bool> _canPursue;
                private Func<bool> _canInvestigate;
                private Func<bool> _canFollow;
                private Func<bool> _canSearch;
                private Func<bool> _canIdle;
                private Func<float, bool> _isDarkness;
                private Func<float, float, bool> _isFallbackDarkness;
                private Func<float, float> _calculatePursueSpeed;
                private Func<Vector3, Vector3> _calculateSearchPosition;
                private Action<Vector3> _moveTo;
                private Action _stopMoving;
                private Action<Vector3> _pursue;
                private Action<Vector3> _investigate;
                private Action<Vector3> _follow;
                private Action _search;
                IDisposable AnimationToken1;
                private Action _idle; 
                private Coroutine _investigationRoutine;
                public bool isSearchingforPlayer;
                private void Awake()
                {
                    _agent = GetComponent<NavMeshAgent>();

                    if (perception == null)
                        perception = GetComponent<LibrarianPerceptionDriver>();
                    
                    if (animatorDriver == null)
                        animatorDriver = GetComponent<LibrarianAnimatorDriver>();

                    if (animator == null)
                        animator = GetComponentInChildren<Animator>();

                    BuildBrain();
                }
                public void Dispose()
                {
                     AnimationToken1 = null;
                }
                private void OnDisable()
                {
                    StopScan();
                }
                private IEnumerator ScanIKCoroutine()
                {
                    while (isSearchingforPlayer)
                    {
                animatorDriver.LibLostPlayerAnimation();
                        yield return null;
                    }
                    animatorDriver.canSearch = false;
                }
                private void BuildBrain()
                {
                    
                            //Condition for darkness: if darkness is above threshold
                    _isDarkness = darkness => darkness >= darknessThreshold;
                            
                            //Fallback darkness condition: if darkness is above fallback threshold and light level is below light dark threshold
                    _isFallbackDarkness = (darkness, lightLevel) =>
                        darkness >= darknessFallbackThreshold && lightLevel <= lightDarkThreshold;
                            
                            //Condition for calculating pursue speed: if scaling is enabled, interpolate between base speed and scaled speed based on alert, otherwise use scaled speed
                    _calculatePursueSpeed = alert => scaleSpeedWithAlert
                        ? Mathf.Lerp(baseSpeed, baseSpeed * pursueSpeedMultiplier, Mathf.Clamp01(alert))
                        : baseSpeed * pursueSpeedMultiplier;
                            
                            //Condition for calculating search position: use the default search position calculation
                    _calculateSearchPosition = CreateSearchPosition;
                            
                            //Condition for pursuing: automatically pursue, has current target, not in darkness, and alert above threshold
                    _canPursue = () => automaticallyPursue && _hasCurrentTarget && !_isDark && _alert >= alertThreshold;
            
                            //Condition for following: has current target, not in fallback darkness, alert above investigate threshold, and either player is not crouched or we don't respect crouch
                    _canFollow = () => _hasCurrentTarget
                        && !_isFallbackDark
                        && _alert >= investigateAlertThreshold
                        && !(_playerCrouched && respectPlayerCrouch);
                            
                            //Condition for searching: has seen target, no current target, not expired, and search passes below limit
                    _canSearch = () => _hasSeenTarget
                        && !_hasCurrentTarget
                        && Time.time < _lostTargetExpiryTime
                        && _lostTargetSearchPasses < lostTargetSearchPassLimit
                        && _belief < cautionBeliefThreshold;
                            
                            //Condition for idling: no other actions can be performed
                    _canIdle = () => !_canPursue.Invoke()
                    //  && !_canInvestigate.Invoke()
                        && !_canFollow.Invoke()
                        && !_canSearch.Invoke();
                            
                            //Actions for moving and stopping movement
                    _moveTo = MoveTo;
                    _stopMoving = StopMoving;
                
                    //  _canInvestigate = () => !_hasCurrentTarget && !_hasSeenTarget;
                        
                    //    _investigate = position =>
                    //    {
                            
                    //               InvestigateLastKnownPosition();
                                
                            
                    //    };


                    _pursue = position =>
                    {
                        StopScan();
                        SetSpeed(_calculatePursueSpeed.Invoke(_alert));
                        _moveTo.Invoke(position);

                        if (perception._lastTargetDetected)
                        {
                        
                            MoveTo(FillTarget);
                            _hasSeenTarget = true;
                            _hasCurrentTarget = true;
                            _belief += 0.15f;
                            return;
                        }
                    };
            
                    _follow = position =>
                    {
                        StopScan();
                        SetSpeed(baseSpeed);
                        _moveTo.Invoke(position);
                    };
                    
                    _search = InvestigateLastKnownPosition;
                    
                    _idle = () =>
                    {
                        StopScan();
                        _stopMoving.Invoke();
                    };
                }
                //Directly filled by the perception driver, this is the main update loop for the brain.
                public void OnPerceptionUpdate(LibrarianPerceptionDriver.PerceptionSnapshot snapshot)
                {
                    _belief = snapshot.Belief;
                    _playerCrouched = snapshot.PlayerIsCrouching || (perception != null && perception.IsPlayerCrouched());
                    if(perception._lastTargetDetected)
                    {
                    
                        _hasCurrentTarget = true;
                    
                    
                    }
                    float darkness = Mathf.Clamp01(snapshot.Darkness);
                    float halfDarkness = Mathf.Clamp01(snapshot.HalfDarkness);
                    float darknessBlend = 1f - darkness;
                    float fallbackBlend = 1f - halfDarkness;
                    _alert = snapshot.AlertFlag * Mathf.Lerp(
                        darknessAlertMultiplier,
                        1f,
                        Mathf.Clamp01(Mathf.Max(darknessBlend, fallbackBlend)));

                    _isDark = _isDarkness.Invoke(darkness);
                    _isFallbackDark = _isFallbackDarkness.Invoke(darkness, Mathf.Clamp01(snapshot.LightLevel));
                    bool hadCurrentTarget = _hasCurrentTarget;
                    _hasCurrentTarget = snapshot.TargetPosition.HasValue;
                    _targetWasJustLost = hadCurrentTarget && !_hasCurrentTarget;

                    if (_hasCurrentTarget)
                    {
                        _lastKnownTargetPosition = snapshot.TargetPosition.Value;
                        _targetWasJustLost = false;
                        SetTarget(snapshot.TargetPosition.Value);
                        _hasSeenTarget = true;
                        _lostTargetExpiryTime = Time.time + lostTargetInvestigationDuration;
                        _lostTargetSearchPasses = 0;
                    }
                        
                    Think();
                    UpdateAnimation();
                }
                
                private void Think()
                {

                    if (_canPursue.Invoke())
                    {
                        _pursue.Invoke(_lastKnownTargetPosition);
                        return;
                    }

                    if (_canFollow.Invoke())
                    {
                        _follow.Invoke(_lastKnownTargetPosition);
                        return;
                    }

            //   if (_canInvestigate.Invoke())
                //   {
                //       WhilePlayerMissing();
            //   return;
                //    }

                    if (_canSearch.Invoke())
                    {
                        _search.Invoke();
                        return;
                    }

                    if (_canIdle.Invoke()){
                        _idle.Invoke();
                        
                        WhilePlayerMissing();
                        return;
                        }
                }

            
                

                private void InvestigateLastKnownPosition()
                {
                    if (_agent == null || !_agent.isOnNavMesh)
                        return;

                    bool hasReachedSearchPosition = !_agent.pathPending
                        && (!_agent.hasPath || Vector3.Distance(transform.position, _searchPosition) <= _agent.stoppingDistance);
                    bool shouldRefresh = !_hasSearchPosition || hasReachedSearchPosition;

                    if (!shouldRefresh)
                        return;

                    _searchPosition = _calculateSearchPosition.Invoke(_lastKnownTargetPosition);
                    _hasSearchPosition = true;
                    _lostTargetSearchPasses++;
                    //need to make timer longer and coroutine to stop it from jittering.
                    MoveTo(_searchPosition);
                    isSearchingforPlayer = true;
                    _investigationRoutine = StartCoroutine(ScanIKCoroutine());
                    StartScan();
                    if(_lostTargetSearchPasses >= lostTargetSearchPassLimit)
                    {FinishInvestigation();
                    lostTargetInvestigationDuration = 6f;
        isSearchingforPlayer = false;
                    _lostTargetSearchPasses = 0;
                    if (_investigationRoutine != null)
                    {

                        StopCoroutine(_investigationRoutine);
                        _investigationRoutine = null;
                    }
                    }
                
                
                }
            
                private void FinishInvestigation()
                {
                    
                    
                    _hasSeenTarget = false;
                    _hasCurrentTarget = false;
                    _lastKnownTargetPosition = default;
                    
                    _hasSearchPosition = false;
                    _lostTargetSearchPasses = 0;
                    _lostTargetExpiryTime = 0f;
                    _searchTimer = 0f;
                    animatorDriver.canSearch = false;
                    StopScan();
                }

                private Vector3 CreateSearchPosition(Vector3 anchor)
                {
                    Vector3 approach = anchor - transform.position;
                    if (approach.sqrMagnitude < 0.001f)
                        approach = transform.forward;
                    else
                        approach.Normalize();

                    Vector3 side = Vector3.Cross(approach, Vector3.up).normalized;
                    Vector3 nodeA = anchor + approach * investigateSearchDistance * 0.7f + side * investigateSearchDistance * 0.45f;
                    Vector3 nodeB = anchor - approach * investigateSearchDistance * 0.7f - side * investigateSearchDistance * 0.45f;
                    Vector3 nextPosition = _searchSide == 0 ? nodeA : nodeB;
                    _searchSide = 1 - _searchSide;

                    if (NavMesh.SamplePosition(nextPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                        nextPosition = hit.position;
                
                return nextPosition;
                }
private bool CanSearchIsTrue()
{
    return animatorDriver.CanSearchIsTrue;
}

                private void MoveTo(Vector3 position)
                {
                    if (_agent == null || !_agent.isOnNavMesh)
                        return;
   if(CanSearchIsTrue())
   {
        animatorDriver.canSearch = false;
     

   }
                    _repathTimer -= Time.fixedDeltaTime;
                    if (_repathTimer > 0f)
                        return;

                    _agent.SetDestination(position);
                    _repathTimer = repathInterval;
                }

                private void SetSpeed(float speed)
                {
                    if (_agent != null) 
                        _agent.speed = speed;
                }

                private void StopMoving()
                {
                    if (_agent != null && _agent.isOnNavMesh && _agent.hasPath)
                        _agent.ResetPath();
                }

                private void StartScan()
                {
                    if (_scanRoutine != null || perception == null || perception.eye == null)
                        return;

                    _originalEyeRotation ??= perception.eye.localRotation;
                    animatorDriver.canSearch = true;
                    _scanRoutine = StartCoroutine(ScanRoutine());
                }

                private IEnumerator ScanRoutine()
                {
                    if(  isSearchingforPlayer ){
                    Quaternion originalRotation = _originalEyeRotation ?? perception.eye.localRotation;
                    float[] angles = { -135f, 135f, 0f };
                    float[] turnSpeeds = { 3f, 3f, 4f };

                    for (int index = 0; index < angles.Length; index++)
                    {
                        Quaternion targetRotation = originalRotation * Quaternion.Euler(0f, angles[index], 0f);
                        while (perception != null
                            && perception.eye != null
                            && Quaternion.Angle(perception.eye.localRotation, targetRotation) > 0.1f)
                        {
                            perception.eye.localRotation = Quaternion.Slerp(
                                perception.eye.localRotation,
                                targetRotation,
                                Time.deltaTime * turnSpeeds[index]);
                            yield return null;
                        }

                        if (perception == null || perception.eye == null)
                        {
                            _scanRoutine = null;
                            isSearchingforPlayer = false;
                            animatorDriver.canSearch = false;
                            yield break;
                        }

                        perception.eye.localRotation = targetRotation;
                        yield return new WaitForSeconds(0.25f);
                    }

                    if (perception != null && perception.eye != null)
                        perception.eye.localRotation = originalRotation;

                    _scanRoutine = null;
                    }
                    isSearchingforPlayer = false;
                    animatorDriver.canSearch = false;
                }

                private void StopScan()
                {
                    if (_scanRoutine != null)
                        StopCoroutine(_scanRoutine);
        AnimationToken1 = null;
                    _scanRoutine = null;
                    if (_originalEyeRotation.HasValue && perception != null && perception.eye != null)
                        perception.eye.localRotation = _originalEyeRotation.Value;
                }

                private void UpdateAnimation()
                {
                    if (animator == null || string.IsNullOrEmpty(alertParam))
                        return;

                    if (!_alertParamResolved)
                    {
                        _alertParamHash = Animator.StringToHash(alertParam);
                        foreach (AnimatorControllerParameter parameter in animator.parameters)
                        {
                            if (parameter.nameHash == _alertParamHash)
                            {
                                _hasAlertParam = true;
                                _alertParamIsBool = parameter.type == AnimatorControllerParameterType.Bool;
                                break;
                            }
                        }

                        _alertParamResolved = true;
                    }

                    if (!_hasAlertParam)
                        return;

                    if (_alertParamIsBool)
                        animator.SetBool(_alertParamHash, _alert >= alertThreshold);
                    else
                        animator.SetFloat(_alertParamHash, _alert);
                }

                public void SetTarget(Vector3 position)
                {
                    _lastKnownTargetPosition = position;
                    _hasCurrentTarget = true;
                    _targetWasJustLost = false;
                    _hasSeenTarget = true;
                    FillTarget = _lastKnownTargetPosition;
                    GetTarget(FillTarget);
                }
                public Vector3 GetTarget(Vector3 pos) {return pos;}
                public Vector3 FillTarget;
                public void ClearTarget()
                {
                    _targetWasJustLost = _hasCurrentTarget;
                    _hasCurrentTarget = false;
                }

                public bool TargetWasJustLost => _targetWasJustLost;

                public void ForgetTarget()
                {
                    _hasCurrentTarget = false;
                    _targetWasJustLost = false;
                    _hasSeenTarget = false;
                    _hasSearchPosition = false;
        
                }
                private void WhilePlayerMissing()
                {

                InvestigateLastKnownPosition();
                
                    }
                

                }
            }

