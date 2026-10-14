using Football.Ball;
using Football.Core;
using Football.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Football.Control
{
    /// <summary>
    /// Human control of one player through the Input System. Bindings are created in code so they live in
    /// one place; a rebinding UI can later override them. Movement is relative to the camera view.
    /// Attach to the "MatchSystems" GameObject and assign it as the player's command source.
    /// Keyboard: WASD/arrows move, Left Shift sprint, Q face the ball (backpedal and strafe).
    /// Gamepad: left stick move, right trigger sprint, left shoulder face the ball.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Input)]
    public sealed class FootballInput : MonoBehaviour, IMotorCommandSource
    {
        [Tooltip("Camera that defines screen-up for movement. Main camera when empty.")]
        [SerializeField] Transform viewCamera;
        [Tooltip("Ball to face while the face-ball control is held")]
        [SerializeField] BallController ball;

        InputAction move;
        InputAction sprint;
        InputAction faceBall;

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/rightTrigger");

            faceBall = new InputAction("FaceBall", InputActionType.Button);
            faceBall.AddBinding("<Keyboard>/q");
            faceBall.AddBinding("<Gamepad>/leftShoulder");
        }

        void OnEnable()
        {
            move.Enable();
            sprint.Enable();
            faceBall.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            sprint.Disable();
            faceBall.Disable();
        }

        void OnDestroy()
        {
            move.Dispose();
            sprint.Dispose();
            faceBall.Dispose();
        }

        public MotorCommand GetCommand(PlayerMovementController player)
        {
            Vector2 stick = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            Transform view = viewCamera != null ? viewCamera : Camera.main != null ? Camera.main.transform : null;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (view != null)
            {
                forward = PlayerMotor.Flatten(view.forward).normalized;
                right = PlayerMotor.Flatten(view.right).normalized;
            }

            var command = new MotorCommand
            {
                Move = forward * stick.y + right * stick.x,
                Sprint = sprint.IsPressed()
            };

            if (faceBall.IsPressed() && ball != null)
                command.Face = PlayerMotor.Flatten(ball.State.Position - player.Motor.Position);

            return command;
        }
    }
}
