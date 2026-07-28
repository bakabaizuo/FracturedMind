using UnityEngine;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.AI;
using FracturedStudios.UI;

        namespace FracturedMind.AI
        {
            /// <summary>
            /// Drives librarian locomotion parameters from motion, so animation stays separate from perception and belief.
            /// </summary>
            [DisallowMultipleComponent]
            [RequireComponent(typeof(Animator))]
            public sealed class LibrarianAnimatorDriver : MonoBehaviour
            {
                [Header("Sources")]
                [SerializeField] private NavMeshAgent agent;

                [Header("Animator Params")]
                [SerializeField] private string moveXParam = "MoveX";
                [SerializeField] private string moveZParam = "MoveZ";
                [SerializeField] private string deltaSphereStrafingParam = "DeltaSphereStrafing";

                [Header("Tuning")]
                [SerializeField, Range(0f, 1f)] private float parameterSmoothing = 0.18f;
                [SerializeField, Min(0.001f)] private float deadZone = 0.02f;

                Animator _animator;
                public LibrarianController _controller;
                public Vector3 _lastWorldPosition;
                public Vector3 _lastForwardPosition;
                public bool _hasLastPosition;
                public bool _hasLastFWPosition;
                int _moveXHash;
                int _moveZHash;
                public int LostPlayerLocAnimHash;//new 7/28/26
                public int ISAnimDoneCounter;//new 7/28/26
                public bool _isAnim_LPLA_Done;//new 7/28/26
                public int _deltaSphereStrafingHash;
                bool _paramsResolved;

                void Awake()
                {
                    _animator = GetComponent<Animator>();
                    if (agent == null)
                        agent = GetComponentInParent<NavMeshAgent>();
                    _controller ??= GetComponent<LibrarianController>();
                    _controller ??= GetComponentInParent<LibrarianController>();
                    _lastWorldPosition = transform.position;
                    _hasLastPosition = false;
                    

                }
                void OnEnable()
                {
                    ResolveParametersOnce();
                }

                void Update()
                {
                    if (_animator == null)
                        return;

                
                    Vector3 worldVelocity = GetWorldVelocity();
                    Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

                    float moveX = Mathf.Abs(localVelocity.x) > deadZone ? Mathf.Clamp(localVelocity.x, -1f, 1f) : 0f;
                    float moveZ = Mathf.Abs(localVelocity.z) > deadZone ? Mathf.Clamp(localVelocity.z, -1f, 1f) : 0f;
                    float deltaSphereStrafing = Mathf.Clamp01(new Vector2(moveX, moveZ).magnitude);

                    float smoothedX = Mathf.Lerp(_animator.GetFloat(_moveXHash), moveX, parameterSmoothing);
                    float smoothedZ = Mathf.Lerp(_animator.GetFloat(_moveZHash), moveZ, parameterSmoothing);
                    float smoothedDelta = Mathf.Lerp(_animator.GetFloat(_deltaSphereStrafingHash), deltaSphereStrafing, parameterSmoothing);

                    _animator.SetFloat(_moveXHash, smoothedX);
                    _animator.SetFloat(_moveZHash, smoothedZ);
                    _animator.SetFloat(_deltaSphereStrafingHash, smoothedDelta);

                    _lastWorldPosition = LibrarianController.RetriveLastPosition();//new 7/28/26
                    _hasLastPosition = true;
                }

                Vector3 GetWorldVelocity()
                {
                    if (agent != null)
                    {
                        Vector3 velocity = agent.velocity;
                        if (velocity.sqrMagnitude > 0.0001f)
                            return velocity;

                        Vector3 desired = agent.desiredVelocity;
                        if (desired.sqrMagnitude > 0.0001f)
                            return desired;
                    }

                    if (!_hasLastPosition)
                        return Vector3.zero;

                    float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                    return (transform.position - _lastWorldPosition) / dt;
                }

                void ResolveParametersOnce()
                {
                    if (_paramsResolved)
                        return;

                    _moveXHash = Animator.StringToHash(moveXParam);
                    _moveZHash = Animator.StringToHash(moveZParam);
                    _deltaSphereStrafingHash = Animator.StringToHash(deltaSphereStrafingParam);
                    _paramsResolved = true;
                }
            }
        }