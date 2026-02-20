using System;
using UnityEngine;
using FracturedStudios.Components;
using UnityEngine.InputSystem;


namespace FracturedStudios.RigLayers
{
    /// <summary>
    /// Simple IK-like rig layer for driving hand targets for held items (eg. an Orb).
    /// - Supports two modes: Avatar offsets (local-space offsets from a hand transform) or explicit socket Transforms.
    /// - Smoothly blends weight and target position/rotation.
    /// - Intended to be attached to the character (or rig root) and driven by an Item when equipped.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbHandRigLayer : MonoBehaviour
    {
        public enum TargetMode { AvatarOffset, Socket }

        [Header("General")]
        public TargetMode mode = TargetMode.AvatarOffset;
        [Tooltip("If true the layer updates in LateUpdate to better align with animations.")]
        public bool updateInLateUpdate = true;

        [Header("Weight")]
        [Range(0f,1f)] public float weight = 1f;
        [Tooltip("Blend speed for weight changes")]
        public float weightBlendSpeed = 6f;

        [Header("Avatar Offset Mode")]
        [Tooltip("Transform to use as reference for avatar (eg. hand transform or wrist).")]
        public Transform avatarReference;
        [Tooltip("Animator on the avatar/player to apply IK to (optional).")]
        public Animator avatarAnimator;
        [Tooltip("Local-space offset from avatarReference for the target")]
        public Vector3 avatarLocalPosition = Vector3.zero;
        [Tooltip("Local-space rotation offset from avatarReference (euler)")]
        public Vector3 avatarLocalEuler = Vector3.zero;

        [Header("Socket Mode")]
        [Tooltip("Optional explicit socket Transform for the target")]
        public Transform socketTarget;
        [Tooltip("Name of the child GameObject to use/create as an orb socket on the avatar reference")]
        public string orbSocketName = "OrbSocket";
        [Tooltip("If true and no socket is found, the component will create a child GameObject named `orbSocketName` on the avatar reference when SetSocketFromAvatarOffset is called.")]
        public bool autoCreateOrbSocket = true;

        [Header("Smoothing")]
        public float positionSmooth = 12f;
        public float rotationSmooth = 12f;

        [Header("Sockets")]
        [Tooltip("Left hand socket (OrbSocket by default)")]
        public Transform leftHandSocket;
        [Tooltip("Right hand socket")]
        public Transform rightHandSocket;
        [Tooltip("Left target socket (where orb target should be when grabbed)")]
        public Transform leftTargetSocket;
        [Tooltip("Right target socket")]
        public Transform rightTargetSocket;
        [Tooltip("Reference to the OrbSocket GameObject (created/found by SetSocketFromAvatarOffset)")]
        public GameObject OrbSocket;
           private PlayerControlls inputs;
        // runtime
        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private float _currentWeight = 0f;
        // delta-time field (frame delta assigned where needed, not in Update/LateUpdate)
        private float dt;

        // public read-only
        public Vector3 CurrentPosition => _currentPosition;
        public Quaternion CurrentRotation => _currentRotation;
        public float CurrentWeight => _currentWeight;

        // operator state for grabbing the orb (can be toggled via input)
        private bool _grabOrbOperator = false;
        public bool GrabOrb { get => _grabOrbOperator; private set => _grabOrbOperator = value; }

        void Start()
        {
            _currentPosition = transform.position;
            _currentRotation = transform.rotation;
            _currentWeight = 0f;
            // initialize input wrapper once and subscribe to events
            if (inputs == null)
            {
                inputs = new PlayerControlls();
                var action = inputs.Player.OrbLight;
                if (action != null)
                {
                    action.started += OnOrbLightStarted;
                    action.performed += OnOrbLightPerformed;
                    action.canceled += OnOrbLightCanceled;
                }
                inputs.Enable();
            }
        }
        void Reset()
        {
            OrbSocket = null;
             dt = Time.deltaTime;
                _currentWeight = Mathf.MoveTowards(_currentWeight, 0f, weightBlendSpeed * dt);
               
                _currentPosition = Vector3.Lerp(_currentPosition, transform.position, 1f - Mathf.Exp(-positionSmooth * dt * 0.5f));
                _currentRotation = Quaternion.Slerp(_currentRotation, transform.rotation, 1f - Mathf.Exp(-rotationSmooth * dt * 0.5f));
            
            return;
        }
      

        void LateUpdate()
        {
         
        }

        private void GrabOrbSocketTarget(float dt)
        {
            // Use GrabOrb as operator: when true we actively drive the rig towards the grab target.
            if (GrabOrb)
            {
                GrabOrbTarget();
            }
            else
            {
                // when not grabbing, blend weight back to zero and do a light position snap toward rest
                _currentWeight = Mathf.MoveTowards(_currentWeight, 0f, weightBlendSpeed * dt);
                // Optionally, slowly relax position to the component transform
                _currentPosition = Vector3.Lerp(_currentPosition, transform.position, 1f - Mathf.Exp(-positionSmooth * dt * 0.5f));
                _currentRotation = Quaternion.Slerp(_currentRotation, transform.rotation, 1f - Mathf.Exp(-rotationSmooth * dt * 0.5f));
            }
        }

        /// <summary>
        /// Drive the rig toward the current grab target. This contains the previous Tick() logic.
        /// Uses left-hand sockets by convention when available (OrbSocket = leftHandSocket).
        /// </summary>
        public void GrabOrbTarget()
        {
            // target defaults
            dt = Time.deltaTime;
            Vector3 targetPos = transform.position;
            Quaternion targetRot = transform.rotation;

           
            if (leftTargetSocket != null)
            {
                targetPos = leftTargetSocket.position;
                targetRot = leftTargetSocket.rotation;
            }
            else if (rightTargetSocket != null)
            {
                targetPos = rightTargetSocket.position;
                targetRot = rightTargetSocket.rotation;
            }
          
            if (GrabOrb)
            {
                // smooth position/rotation
                _currentPosition = Vector3.Lerp(_currentPosition, targetPos, 1f - Mathf.Exp(-positionSmooth * dt));
                _currentRotation = Quaternion.Slerp(_currentRotation, targetRot, 1f - Mathf.Exp(-rotationSmooth * dt));

                // blend weight towards desired (operator GrabOrb overrides public `weight` desired value)
                float desired = GrabOrb ? 1f : weight;
                _currentWeight = Mathf.MoveTowards(_currentWeight, desired, weightBlendSpeed * dt);
            }
}

        public bool CheckGrabOrbTarget()
        {
            // fallback check using the input action 'triggered' flag (useful when polling)
            if (inputs == null)
            {
                inputs = new PlayerControlls();
                inputs.Enable();
                var a = inputs.Player.OrbLight;
                if (a != null)
                {
                    a.started += OnOrbLightStarted;
                    a.performed += OnOrbLightPerformed;
                    a.canceled += OnOrbLightCanceled;
                }
            }
/*
            var action = inputs.Player?.OrbLight;
            bool triggered = action != null && action.triggered;
            if (triggered)
            {
                // toggle the grab operator on each tap
                GrabOrb = !GrabOrb;
                Debug.Log($"OrbHandRigLayer: GrabOrb toggled to {GrabOrb} (triggered by input action '{action?.name}')");
            }

            return triggered;
            */
            return false; // input handling is now event-driven, so this method can return false or be repurposed for polling if needed.
        }

        // Input callbacks
        private void OnOrbLightStarted(InputAction.CallbackContext ctx)
        {
            // started can be used for hold-to-grab semantics
            GrabOrb = true;
        }

        private void OnOrbLightPerformed(InputAction.CallbackContext ctx)
        {
            // performed is commonly used for button taps; toggle operator
            ToggleGrabOperator();
        }

        private void OnOrbLightCanceled(InputAction.CallbackContext ctx)
        {
            // canceled can end hold-to-grab
            GrabOrb = false;
        }

        private void ToggleGrabOperator()
        {
            GrabOrb = !GrabOrb;
            Debug.Log($"OrbHandRigLayer: GrabOrb toggled to {GrabOrb} (via input event)");
        }
        /// <summary>
        /// Set explicit socket by transform.
        /// </summary>
        public void SetSocket(Transform socket)
        {
            socketTarget = socket;
            mode = (socket != null) ? TargetMode.Socket : mode;
        }

        /// <summary>
        /// Set a hand socket (left==true => leftHandSocket)
        /// </summary>
        public void SetHandSocket(bool left, Transform socket)
        {
            if (left) leftHandSocket = socket; else rightHandSocket = socket;
        }

        /// <summary>
        /// Set a target socket (left==true => leftTargetSocket)
        /// </summary>
        public void SetTargetSocket(bool left, Transform socket)
        {
            if (left) leftTargetSocket = socket; else rightTargetSocket = socket;
        }

        /// <summary>
        /// Set avatar reference and local offset.
        /// </summary>
        public void SetAvatarReference(Transform avatarRef, Vector3 localPos, Vector3 localEuler)
        {
            avatarReference = avatarRef;
            avatarLocalPosition = localPos;
            avatarLocalEuler = localEuler;
            mode = TargetMode.AvatarOffset;
        }

        /// <summary>
        /// Overload to set the avatar via an Animator (preferred when using Unity's IK).
        /// </summary>
        public void SetAvatarReference(Animator animatorRef, Vector3 localPos, Vector3 localEuler)
        {
            avatarAnimator = animatorRef;
            avatarReference = animatorRef != null ? animatorRef.transform : null;
            avatarLocalPosition = localPos;
            avatarLocalEuler = localEuler;
            mode = TargetMode.AvatarOffset;
        }

        void OnDisable()
        {
            if (inputs != null)
            {
                var a = inputs.Player?.OrbLight;
                if (a != null)
                {
                    a.started -= OnOrbLightStarted;
                    a.performed -= OnOrbLightPerformed;
                    a.canceled -= OnOrbLightCanceled;
                }
                inputs.Disable();
            }
        }

        /// <summary>
        /// Apply IK each frame when the Animator calls OnAnimatorIK.
        /// Uses the smoothed `_currentPosition`/`_currentRotation` and `_currentWeight` computed by the rig.
        /// </summary>
        void OnAnimatorIK(int layerIndex)
        {
            if (avatarAnimator == null) return;

            float w = Mathf.Clamp01(_currentWeight);

            // Left hand
            if (leftTargetSocket != null || rightTargetSocket != null)
            {
                // prefer left when available
                avatarAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
                avatarAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, w);
                avatarAnimator.SetIKPosition(AvatarIKGoal.LeftHand, _currentPosition);
                avatarAnimator.SetIKRotation(AvatarIKGoal.LeftHand, _currentRotation);

                // Right hand
                avatarAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
                avatarAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, w);
                avatarAnimator.SetIKPosition(AvatarIKGoal.RightHand, _currentPosition);
                avatarAnimator.SetIKRotation(AvatarIKGoal.RightHand, _currentRotation);
            }
            else
            {
                // clear weights if no targets
                avatarAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                avatarAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                avatarAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                avatarAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            }
        }

        /// <summary>
        /// Find or create a child GameObject named by `orbSocketName` on the `avatarReference`, place it at the configured local offset
        /// and register it as the active socket via `SetSocket`.
        /// Callable from inspector via Context Menu.
        /// </summary>
        [ContextMenu("Set Socket From Avatar Offset")]
        public void SetSocketFromAvatarOffset()
        {
            if (avatarReference == null)
            {
                Debug.LogWarning("OrbHandRigLayer: avatarReference is null - cannot create/find OrbSocket.");
                return;
            }

            // try to find using shared ComponentExtensions helper (searches children/parents/root)
            Transform found = ComponentExtensions.Find<Transform>(OrbSocket.gameObject, orbSocketName);

            if (found == null )
            {
            Debug.LogWarning($"OrbHandRigLayer: OrbSocket named '{orbSocketName}' not found on avatar reference. autoCreateOrbSocket is {(autoCreateOrbSocket ? "enabled" : "disabled")}.");
            }

            if (found != null)
            {
            
                SetSocket(found);
                // keep explicit reference and use as left-target socket
                SetTargetSocket(true, found);
                // store GameObject reference (OrbSocket is a GameObject)
                OrbSocket = found.gameObject;

                // If no explicit leftTargetSocket was assigned, use the OrbSocket transform
                if (leftTargetSocket == null && OrbSocket != null)
                {
                    leftTargetSocket = OrbSocket.transform;
                }

               
            }
            else
            {
                Reset();
                
            }
        }

        /// <summary>
        /// Instantly snap current position/rotation to target.
        /// </summary>
        public void SnapToTarget()
        {
            if (mode == TargetMode.Socket && socketTarget != null)
            {
                _currentPosition = socketTarget.position;
                _currentRotation = socketTarget.rotation;
            }
            else if (mode == TargetMode.AvatarOffset && avatarReference != null)
            {
                _currentPosition = avatarReference.TransformPoint(avatarLocalPosition);
                _currentRotation = avatarReference.rotation * Quaternion.Euler(avatarLocalEuler);
            }
        }
    }
}
