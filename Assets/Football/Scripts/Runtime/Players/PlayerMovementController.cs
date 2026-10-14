using Football.Ball;
using Football.Core;
using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// Scene binding for <see cref="PlayerMotor"/>. Pulls a command from its source each fixed tick,
    /// steps the motor, interpolates the transform for rendering and exposes the body to the ball as a capsule.
    /// Attach to each player's root GameObject. The root must stand on the grass (y = 0).
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Players)]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [Tooltip("Movement limits. Defaults are used when empty.")]
        [SerializeField] MovementProfile profile;
        [Tooltip("Component implementing IMotorCommandSource (FootballInput for the user, AI from phase 2)")]
        [SerializeField] MonoBehaviour commandSource;
        [Tooltip("Ball the body can block")]
        [SerializeField] BallController ball;

        [Header("Body capsule for ball contact")]
        [SerializeField] float bodyRadius = PlayerBody.DefaultRadius;
        [SerializeField] float bodyHeight = PlayerBody.DefaultHeight;
        [SerializeField, Range(0f, 1f)] float bodyRestitution = 0.3f;

        PlayerMotor motor;
        PlayerBody body;
        IMotorCommandSource source;
        IMoveAdjuster moveAdjuster;
        Vector3 previousPosition;
        Vector3 currentPosition;
        Quaternion previousRotation;
        Quaternion currentRotation;

        public PlayerMotor Motor
        {
            get
            {
                if (motor == null)
                    CreateMotor();
                return motor;
            }
        }

        /// <summary>What the command source asked for this tick, before any <see cref="IMoveAdjuster"/>.</summary>
        public MotorCommand LastCommand { get; private set; }

        /// <summary>Set by <see cref="PlayerBallInteraction"/> while the ball is at the player's feet.</summary>
        public bool HasBall { get; set; }

        public MonoBehaviour CommandSource
        {
            get => commandSource;
            set
            {
                commandSource = value;
                source = value as IMotorCommandSource;
            }
        }

        void Awake()
        {
            if (motor == null)
                CreateMotor();
            source = commandSource as IMotorCommandSource;
            moveAdjuster = GetComponent<IMoveAdjuster>();
            if (commandSource != null && source == null)
                Debug.LogError($"{commandSource.GetType().Name} does not implement IMotorCommandSource", this);
        }

        void CreateMotor()
        {
            MovementParams parameters = profile != null ? profile.parameters : new MovementParams();
            Vector3 forward = PlayerMotor.Flatten(transform.forward);
            motor = new PlayerMotor(parameters)
            {
                Position = PlayerMotor.Flatten(transform.position),
                Facing = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward
            };
            body = new PlayerBody(motor) { Radius = bodyRadius, Height = bodyHeight, BodyRestitution = bodyRestitution };
            previousPosition = currentPosition = motor.Position;
            previousRotation = currentRotation = Quaternion.LookRotation(motor.Facing);
        }

        public PlayerBody Body
        {
            get
            {
                if (motor == null)
                    CreateMotor();
                return body;
            }
        }

        void OnEnable()
        {
            if (ball != null)
                ball.RegisterObstacle(Body);
        }

        void OnDisable()
        {
            if (ball != null)
                ball.UnregisterObstacle(Body);
        }

        void FixedUpdate()
        {
            MotorCommand command = source != null ? source.GetCommand(this) : default;
            command.HasBall = HasBall;
            LastCommand = command;

            MotorCommand legs = command;
            if (moveAdjuster != null)
                legs.Move = moveAdjuster.AdjustMove(Motor, command.Move);

            previousPosition = Motor.Position;
            previousRotation = Quaternion.LookRotation(motor.Facing);
            motor.Step(legs, Time.fixedDeltaTime);
            currentPosition = motor.Position;
            currentRotation = Quaternion.LookRotation(motor.Facing);
        }

        void Update()
        {
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(previousPosition, currentPosition, alpha),
                Quaternion.Slerp(previousRotation, currentRotation, alpha));
        }

        /// <summary>Moves the player instantly, e.g. to a restart position. Clears momentum.</summary>
        public void Teleport(Vector3 groundPoint, Vector3 facing)
        {
            Motor.Position = PlayerMotor.Flatten(groundPoint);
            motor.Velocity = Vector3.zero;
            Vector3 flatFacing = PlayerMotor.Flatten(facing);
            if (flatFacing.sqrMagnitude > 1e-4f)
                motor.Facing = flatFacing.normalized;
            previousPosition = currentPosition = motor.Position;
            previousRotation = currentRotation = Quaternion.LookRotation(motor.Facing);
            transform.SetPositionAndRotation(currentPosition, currentRotation);
        }
    }
}
