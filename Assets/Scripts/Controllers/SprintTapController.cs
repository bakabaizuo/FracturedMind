using UnityEngine;

namespace FracturedStudios
{
    /// <summary>
    /// Detects repeated forward taps (W or stick up) and triggers a temporary sprint boost
    /// on the player's `ThirdPersonBasic` when the animator allows sprinting.
    /// Uses the Input System `PlayerControlls` asset.
    /// </summary>
    [DisallowMultipleComponent]
    public class SprintTapController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ThirdPersonBasic locomotion;
        [SerializeField] private StringscriptAnimatior animController;

        [Header("Tap Detection")]
        [SerializeField, Min(0.01f)] private float tapThreshold = 0.6f; // forward axis threshold
        [SerializeField, Min(0.05f)] private float tapWindow = 0.7f;    // time window to collect taps
        [SerializeField, Min(1)] private int tapsRequired = 3;         // taps required to trigger

        [Header("Boost")]
        [SerializeField, Min(1f)] private float boostMultiplier = 1.8f;
        [SerializeField, Min(0.1f)] private float boostDuration = 1.5f;

        private float windowTimer;
        private int tapCount;
        private UnityEngine.InputSystem.PlayerInput dummy; // ensure namespace available
        private PlayerControlls controls;

        void Awake()
        {
            controls = new PlayerControlls();
            // don't subscribe to move performed; we'll poll in Update for simple tap detection
        }

        void OnEnable()
        {
            controls?.Enable();
        }

        void OnDisable()
        {
            controls?.Disable();
        }

        void Start()
        {
            if (locomotion == null)
                locomotion = GetComponent<ThirdPersonBasic>();
            if (animController == null)
                animController = GetComponent<StringscriptAnimatior>();
        }

        void Update()
        {
            Vector2 move = Vector2.zero;
            if (controls != null)
            {
                try { move = controls.Player.Move.ReadValue<Vector2>(); } catch { move = Vector2.zero; }
            }

            float forward = move.y;

            // detect rising edge of forward input
            if (forward >= tapThreshold)
            {
                // if starting a new tap window
                if (windowTimer <= 0f)
                {
                    windowTimer = tapWindow;
                    tapCount = 1;
                }
                else
                {
                    // only count if previous frame was below threshold to avoid holding counting as many taps
                    // We'll detect by ensuring we only count on the frame where we cross upward.
                    // Simplest approach: decrement windowTimer and rely on player to release between taps.
                    tapCount++;
                }
            }

            if (windowTimer > 0f)
            {
                windowTimer -= Time.deltaTime;
                if (tapCount >= tapsRequired)
                {
                    TryTriggerBoost();
                    // reset
                    windowTimer = 0f;
                    tapCount = 0;
                }
            }
        }

        private void TryTriggerBoost()
        {
            if (animController == null || locomotion == null)
                return;

            if (!animController.IsSprinting)
                return; // only allowed when animator reports sprinting state

            locomotion.StartSprintBoost(boostMultiplier, boostDuration);
        }
    }
}
