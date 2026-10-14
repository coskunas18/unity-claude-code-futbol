using Football.Ball;
using Football.Core;
using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// Foot touches for dribbling, stopping and receiving. Runs after the player moved and before the ball steps,
    /// so a touch takes effect in the same tick. The logic lives in <see cref="BallTouchController"/>.
    /// Passing and shooting come in phase 2 (KickSolver). Attach next to <see cref="PlayerMovementController"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.PlayerBallContact)]
    [RequireComponent(typeof(PlayerMovementController))]
    public sealed class PlayerBallInteraction : MonoBehaviour, IMoveAdjuster
    {
        [SerializeField] BallController ball;
        [SerializeField] DribbleParams dribble = new DribbleParams();

        [Tooltip("Placeholder for the Ball Control attribute until PlayerAttributes exists (phase 2)")]
        [SerializeField, Range(1, 100)] int ballControl = 70;
        [Tooltip("Seed for touch error, so a recorded input replays identically")]
        [SerializeField] int randomSeed = 1234;

        PlayerMovementController player;
        BallTouchController touches;

        public int TouchCount => touches?.TouchCount ?? 0;
        public TouchKind LastTouch => touches?.LastTouch ?? TouchKind.None;

        void Awake()
        {
            player = GetComponent<PlayerMovementController>();
            touches = new BallTouchController(dribble, ballControl, randomSeed) { Body = player.Body };
        }

        void FixedUpdate()
        {
            if (ball == null)
                return;
            touches.BallControl = ballControl;
            touches.Update(Time.fixedTime, player.Motor, player.LastCommand.Move, ball.Simulator);
            player.HasBall = touches.HasBall;
        }

        public Vector3 AdjustMove(PlayerMotor motor, Vector3 desiredMove) =>
            ball != null && touches != null ? touches.SteerMove(motor, desiredMove, ball.Simulator) : desiredMove;
    }
}
