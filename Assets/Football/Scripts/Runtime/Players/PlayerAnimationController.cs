using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// Drives the character's Animator from the movement model. The motor owns position and rotation (no root motion);
    /// this component only chooses the pose: body-relative velocity feeds a 2D locomotion blend tree, and the playback
    /// rate rises when the player is faster than the clips, see <see cref="LocomotionBlend"/>.
    /// Attach to the player root next to <see cref="PlayerMovementController"/>; the Animator sits on the model child.
    /// </summary>
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        public static readonly int VelocityXId = Animator.StringToHash("VelX");
        public static readonly int VelocityZId = Animator.StringToHash("VelZ");

        [SerializeField] PlayerMovementController movement;
        [SerializeField] Animator animator;

        [Tooltip("Fastest natural speed of the clips per direction, measured by the animator builder (m/s)")]
        [SerializeField] LocomotionSpeedLimits clipSpeedLimits = new LocomotionSpeedLimits(4.96f, 2.1f, 3.85f, 2.95f);
        [SerializeField, Range(0.1f, 1f)] float minPlaybackRate = 0.6f;
        [SerializeField, Range(1f, 3f)] float maxPlaybackRate = 1.8f;
        [Tooltip("Smoothing of the blend parameters (s). Small: the motor already has momentum")]
        [SerializeField, Range(0f, 0.5f)] float parameterDampTime = 0.08f;
        [SerializeField, Range(0f, 0.5f)] float playbackRateDampTime = 0.15f;

        float playbackRate = 1f;
        float playbackRateVelocity;

        public Vector2 LastBlendPosition { get; private set; }
        public float PlaybackRate => playbackRate;

        public void Configure(PlayerMovementController movementController, Animator characterAnimator, LocomotionSpeedLimits limits)
        {
            movement = movementController;
            animator = characterAnimator;
            clipSpeedLimits = limits;
        }

        void Awake()
        {
            if (movement == null)
                movement = GetComponent<PlayerMovementController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.applyRootMotion = false;
        }

        void Update()
        {
            if (movement == null || animator == null)
                return;

            PlayerMotor motor = movement.Motor;
            Vector2 local = LocomotionBlend.ToLocal(motor.Velocity, motor.Facing);
            LocomotionBlend.Compute(local, clipSpeedLimits, minPlaybackRate, maxPlaybackRate,
                out Vector2 blend, out float targetRate);

            float dt = Time.deltaTime;
            animator.SetFloat(VelocityXId, blend.x, parameterDampTime, dt);
            animator.SetFloat(VelocityZId, blend.y, parameterDampTime, dt);
            playbackRate = Mathf.SmoothDamp(playbackRate, targetRate, ref playbackRateVelocity, playbackRateDampTime, Mathf.Infinity, dt);
            animator.speed = playbackRate;
            LastBlendPosition = blend;
        }
    }
}
