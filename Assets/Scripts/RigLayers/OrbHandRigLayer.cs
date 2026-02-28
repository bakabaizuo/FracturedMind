using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;


namespace FracturedStudios.RigLayers
{
    [DisallowMultipleComponent]
    public class OrbHandRigLayer : MonoBehaviour
    {
        public enum DrivenHand { Left, Right }

        private const string OrbSocketName = "OrbSocket";

        [SerializeField] private DrivenHand drivenHand = DrivenHand.Left;
        [SerializeField] private bool updateInLateUpdate = true;
        [SerializeField] private Animator avatarAnimator;
        [SerializeField] private GameObject OrbSocket;
        [SerializeField] [Range(0f, 1f)] private float weight = 1f;
        [SerializeField] private float weightBlendSpeed = 12f;
        [SerializeField] private float positionSmooth = 16f;
        [SerializeField] private float rotationSmooth = 16f;
        [SerializeField] private Vector3 handRotationOffsetEuler = new Vector3(0f, 0f, -180f);
        [SerializeField] private Vector3 handHoldOffsetLocal = new Vector3(0f, 0f, 0.18f);
        [SerializeField] private float moveForwardBiasMax = 0.08f;
        [SerializeField] private float moveSpeedForMaxBias = 4f;

        // if set, instantiate this prefab as the orb light; otherwise a plain GameObject will be created
        [SerializeField] private GameObject orbLightPrefab;
        // Separate light socket assigned in inspector, activated by the flash ability (key 1)
        [SerializeField] private GameObject flashLightObj;
        // How long the flash light stays on after the ability fires
        [SerializeField] private float flashLightDuration = 1.2f;
        // runtime-created orb light helper
        private GameObject orbLightObj;
        // local offset to apply after parenting (so TickRig can reapply it)
        private Vector3 orbLightLocalOffset = Vector3.zero;
        // coroutine handle so we don't overlap flash activations
        private Coroutine flashLightCoroutine;

        private PlayerControlls inputs;
        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private float _currentWeight;
        private CharacterController characterController;
        private bool grabOrb;

        void Start()
        {
            Debug.Log("OrbHandRigLayer.Start() running");
            characterController = GetComponent<CharacterController>();

            if (avatarAnimator == null)
                avatarAnimator = GetComponent<Animator>();

            if (OrbSocket == null)
                OrbSocket = FindDeepChild(transform, OrbSocketName)?.gameObject;

            if (OrbSocket == null)
                Debug.LogWarning("OrbHandRigLayer: OrbSocket not found.");

            // ensure an Orblight object attached to the left hand bone exists
            EnsureOrbLight();

            _currentPosition = OrbSocket != null ? OrbSocket.transform.position : transform.position;
            _currentRotation = OrbSocket != null ? OrbSocket.transform.rotation : transform.rotation;
            _currentWeight = 0f;

            inputs = new PlayerControlls();
            var action = inputs.Player.OrbLight;
            if (action != null)
            {
                // toggle the light each time the action is performed
                action.performed += OnOrbLightPerformed;
                // no longer use canceled at all
            }
            inputs.Enable();
        }

        void LateUpdate()
        {
            if (updateInLateUpdate)
                TickRig(Time.deltaTime);
        }

        void Update()
        {
            if (!updateInLateUpdate)
                TickRig(Time.deltaTime);
        }

        private void TickRig(float deltaTime)
        {
            GrabOrbSocketTarget(deltaTime);

            // keep prefab jointed exactly to hand bone even if animations move it
            if (orbLightObj != null && orbLightObj.transform.parent != null)
            {
                orbLightObj.transform.localPosition = orbLightLocalOffset;
                orbLightObj.transform.localRotation = Quaternion.identity;
            }
        }

        private void GrabOrbSocketTarget(float dt)
        {
            if (grabOrb)
                GrabOrbTarget();
            else
                _currentWeight = Mathf.MoveTowards(_currentWeight, 0f, weightBlendSpeed * dt);
        }

        public void GrabOrbTarget()
        {
            if (OrbSocket == null)
                return;

            float dt = Time.deltaTime;
            Vector3 targetPos = OrbSocket.transform.position;
            Quaternion targetRot = OrbSocket.transform.rotation * Quaternion.Euler(handRotationOffsetEuler);

            targetPos += transform.TransformVector(handHoldOffsetLocal);

            float forwardBias = GetMovementForwardBias();
            if (forwardBias > 0f)
            {
                Vector3 horizontalForward = transform.forward;
                horizontalForward.y = 0f;
                if (horizontalForward.sqrMagnitude > 0.0001f)
                    horizontalForward.Normalize();
                else
                    horizontalForward = Vector3.forward;

                targetPos += horizontalForward * forwardBias;
            }

            _currentPosition = Vector3.Lerp(_currentPosition, targetPos, 1f - Mathf.Exp(-positionSmooth * dt));
            _currentRotation = Quaternion.Slerp(_currentRotation, targetRot, 1f - Mathf.Exp(-rotationSmooth * dt));
            _currentWeight = Mathf.MoveTowards(_currentWeight, weight, weightBlendSpeed * dt);
        }

        private float GetMovementForwardBias()
        {
            if (characterController == null || moveForwardBiasMax <= 0f || moveSpeedForMaxBias <= 0f)
                return 0f;

            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            if (speed <= 0.001f)
                return 0f;

            float factor = Mathf.Clamp01(speed / moveSpeedForMaxBias);
            return moveForwardBiasMax * factor;
        }

        // no longer used (handled in Performed)
        private void OnOrbLightStarted(InputAction.CallbackContext ctx)
        {
        }

        private void OnOrbLightPerformed(InputAction.CallbackContext ctx)
        {
            if (orbLightObj != null)
            {
                bool now = !orbLightObj.activeSelf;
                orbLightObj.SetActive(now);
                grabOrb = now; // only grab when visible
            }
        }

        /// <summary>
        /// Flash ability (key 1): activates <see cref="flashLightObj"/> on the hand for <see cref="flashLightDuration"/> seconds.
        /// FlashLightObj is a separate light socket – assign it in the inspector, parented to the hand bone.
        /// </summary>
        public void TriggerFlash()
        {
            if (flashLightObj == null)
            {
                Debug.LogWarning("OrbHandRigLayer.TriggerFlash: flashLightObj is not assigned in inspector");
                return;
            }
            if (flashLightCoroutine != null)
                StopCoroutine(flashLightCoroutine);
            flashLightCoroutine = StartCoroutine(FlashLightRoutine());
        }

        private IEnumerator FlashLightRoutine()
        {
            // Ensure object is active so the Light component updates
            flashLightObj.SetActive(true);
            VerboseLogger.SafeLog($"[OrbLight] flash light on for {flashLightDuration:0.00}s");

            // Try to find a Light component on the object or its children
            Light flashLight = flashLightObj.GetComponentInChildren<Light>();
            if (flashLight == null)
            {
                Debug.LogWarning("OrbHandRigLayer.FlashLightRoutine: no Light component found on flashLightObj");
                // Fallback: simply wait the duration then deactivate
                yield return new WaitForSeconds(flashLightDuration);
                flashLightObj.SetActive(false);
                flashLightCoroutine = null;
                VerboseLogger.SafeLog("[OrbLight] flash light off (no Light component)");
                yield break;
            }

            // Animate light range (radius) from 0 -> 10 over the configured duration
            float startRange = 0f;
            float targetRange = 10f;
            flashLight.range = startRange;

            float elapsed = 0f;
            while (elapsed < flashLightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flashLightDuration);
                // Smooth the interpolation for nicer easing
                float eased = Mathf.SmoothStep(0f, 1f, t);
                flashLight.range = Mathf.Lerp(startRange, targetRange, eased);
                yield return null;
            }

            // Ensure final value
            flashLight.range = targetRange;

            // Optionally keep the light on briefly at full size, then turn off
            yield return new WaitForSeconds(0.05f);

            // Reset and deactivate
            flashLight.range = 0f;
            flashLightObj.SetActive(false);
            flashLightCoroutine = null;
            VerboseLogger.SafeLog("[OrbLight] flash light off");
        }

        // canceled handler no longer used
        private void OnOrbLightCanceled(InputAction.CallbackContext ctx)
        {
        }

        /// <summary>
        /// Finds or creates the Orblight GameObject parented to the left-hand bone.
        /// Object is created inactive so it can be toggled on/off externally.
        /// </summary>
        private void EnsureOrbLight()
        {
            if (orbLightObj != null)
                return;

            // try to find existing by name anywhere under this transform
            var existing = FindDeepChild(transform, "Orblight");
            if (existing != null)
            {
                orbLightObj = existing.gameObject;
                Debug.Log("OrbHandRigLayer: found existing Orblight object, skipping creation.");
                return;
            }

            // locate the hand bone - name contains "hand.L" (case-insensitive)
            Transform handBone = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("hand.l"))
                {
                    handBone = t;
                    break;
                }
            }

            if (handBone == null)
            {
                Debug.LogWarning("OrbHandRigLayer: could not find Hand.L bone to attach Orblight");
                return;
            }

            if (orbLightPrefab != null)
            {
                orbLightObj = Instantiate(orbLightPrefab, handBone, false);
                orbLightObj.name = "Orblight"; // ensure consistent name
                Debug.Log("OrbHandRigLayer: instantiated orbLightPrefab under handBone");
            }
            else
            {
                orbLightObj = new GameObject("Orblight");
                orbLightObj.transform.SetParent(handBone, false);
                orbLightObj.transform.localPosition = Vector3.zero;
                orbLightObj.transform.localRotation = Quaternion.identity;
                Debug.Log("OrbHandRigLayer: created empty Orblight object under handBone");
            }
            // apply requested size and slight offset
            orbLightObj.transform.localScale = Vector3.one * 0.0014f;  // even smaller
            orbLightLocalOffset = new Vector3(0.001f, 0.001f, -0.001f);
            orbLightObj.transform.localPosition = orbLightLocalOffset;
            orbLightObj.SetActive(false); // always start disabled
        }
        void OnDisable()
        {
            if (inputs != null)
            {
                var a = inputs.Player.OrbLight;
                if (a != null)
                {
                    a.started -= OnOrbLightStarted;
                    a.performed -= OnOrbLightPerformed;
                    a.canceled -= OnOrbLightCanceled;
                }
                inputs.Disable();
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (avatarAnimator == null) return;

            float w = Mathf.Clamp01(_currentWeight);

            if (OrbSocket != null)
            {
                if (drivenHand == DrivenHand.Left)
                {
                    avatarAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
                    avatarAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, w);
                    avatarAnimator.SetIKPosition(AvatarIKGoal.LeftHand, _currentPosition);
                    avatarAnimator.SetIKRotation(AvatarIKGoal.LeftHand, _currentRotation);

                    avatarAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                    avatarAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                }
                else
                {
                    avatarAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
                    avatarAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, w);
                    avatarAnimator.SetIKPosition(AvatarIKGoal.RightHand, _currentPosition);
                    avatarAnimator.SetIKRotation(AvatarIKGoal.RightHand, _currentRotation);

                    avatarAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                    avatarAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                }
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

        private Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
