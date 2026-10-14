using Football.Ball;
using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// Per-player touch logic: tracks close control, applies the cooldown, draws the execution error and kicks.
    /// Pure C# so headless simulations and tests run exactly what the game runs.
    /// </summary>
    public sealed class BallTouchController
    {
        readonly DribbleParams parameters;
        readonly System.Random random;
        float nextTouchTime;
        float lastCloseControlTime = float.NegativeInfinity;
        float bodyIgnoresBallUntil = float.NegativeInfinity;

        /// <summary>Ball Control attribute, 1–100. Placeholder until PlayerAttributes exists (phase 2).</summary>
        public int BallControl;

        /// <summary>The player's body, if it blocks the ball. Briefly ignored after the player's own touch.</summary>
        public PlayerBody Body;

        /// <summary>Ball at the player's feet right now: slows and stiffens the movement profile.</summary>
        public bool HasBall { get; private set; }

        /// <summary>
        /// The player is carrying the ball: had close control recently and the ball is still near.
        /// While true the legs keep heading for the ball even if a touch pushed it a little out of reach.
        /// </summary>
        public bool IsDribbling { get; private set; }
        public int TouchCount { get; private set; }
        public TouchKind LastTouch { get; private set; }

        public BallTouchController(DribbleParams parameters, int ballControl, int seed)
        {
            this.parameters = parameters;
            BallControl = ballControl;
            random = new System.Random(seed);
        }

        /// <summary>
        /// Turns the controller's desired direction into a running direction while the ball is at the player's feet:
        /// the desired direction says where the BALL should go, the legs head for the spot behind the ball from
        /// which the next touch sends it there. Without close control the input passes through unchanged.
        /// </summary>
        public Vector3 SteerMove(PlayerMotor motor, Vector3 desiredMove, BallSimulator ball)
        {
            Vector3 move = PlayerMotor.Flatten(desiredMove);
            float throttle = Mathf.Clamp01(move.magnitude);
            if (!IsDribbling || throttle < 0.05f)
                return desiredMove;

            Vector3 direction = move / move.magnitude;
            BallState ballState = ball.State;
            Vector3 anticipatedBall = PlayerMotor.Flatten(ballState.Position + ballState.Velocity * parameters.steerLeadTime);
            Vector3 toBall = anticipatedBall - motor.Position;
            toBall.y = 0f;
            Vector3 toTouchSpot = anticipatedBall - direction * parameters.steerBehindDistance - motor.Position;

            // Reversing with the ball ahead: the spot behind the ball is on its far side. Run to the ball first;
            // once it is in reach, turn (the touch model stops it with the sole or drags it back).
            float ballDistance = toBall.magnitude;
            if (ballDistance > 1e-3f && Vector3.Dot(direction, toBall / ballDistance) < -0.5f)
                return ballDistance <= parameters.reach ? desiredMove : toBall / ballDistance * throttle;
            toTouchSpot.y = 0f;
            float distance = toTouchSpot.magnitude;
            if (distance < 0.15f)
                return desiredMove;
            Vector3 toward = toTouchSpot / distance;

            // Slow down to turn tightly when the ball is off the current line, instead of orbiting it.
            Vector3 velocity = PlayerMotor.Flatten(motor.Velocity);
            float speed = velocity.magnitude;
            if (speed > 1f)
            {
                float offLine = Vector3.Angle(velocity, toward);
                throttle *= Mathf.Lerp(1f, parameters.steerTurnThrottle, Mathf.InverseLerp(30f, 90f, offLine));
            }

            // Shorten the stride when arriving at a ball that is slower than the player.
            float ballSpeed = PlayerMotor.Flatten(ballState.Velocity).magnitude;
            if (ballSpeed < speed * 0.5f)
                throttle *= Mathf.Clamp(distance / parameters.steerArriveDistance, 0.3f, 1f);

            return toward * throttle;
        }

        /// <summary>
        /// Call once per fixed tick after the motor has moved and before the ball steps.
        /// Returns the kind of touch made this tick, if any.
        /// </summary>
        public TouchKind Update(float time, PlayerMotor motor, Vector3 desiredMove, BallSimulator ball)
        {
            DribbleParams p = parameters;
            BallState ballState = ball.State;
            float radius = ball.Params.radius;

            Vector3 toBall = PlayerMotor.Flatten(ballState.Position - motor.Position);
            Vector3 relative = PlayerMotor.Flatten(ballState.Velocity - motor.Velocity);
            HasBall = toBall.magnitude < p.closeControlRadius
                && ballState.Height(radius) < p.maxTouchHeight
                && (relative.magnitude < p.closeControlRelativeSpeed
                    || PlayerMotor.Flatten(ballState.Velocity).magnitude < p.closeControlRelativeSpeed);
            if (HasBall)
                lastCloseControlTime = time;
            IsDribbling = HasBall
                || (time - lastCloseControlTime < p.dribbleMemory
                    && toBall.magnitude < p.dribbleRadius
                    && ballState.Height(radius) < p.maxTouchHeight);

            if (Body != null)
                Body.IgnoringBall = time < bodyIgnoresBallUntil;

            if (time < nextTouchTime)
                return TouchKind.None;

            Vector3 move = PlayerMotor.Flatten(desiredMove);
            var input = new TouchInput
            {
                PlayerPosition = motor.Position,
                PlayerVelocity = motor.Velocity,
                PlayerFacing = motor.Facing,
                DesiredDirection = move.sqrMagnitude > 0.0025f ? move.normalized : Vector3.zero,
                BallPosition = ballState.Position,
                BallVelocity = ballState.Velocity,
                BallRadius = radius,
                SprintSpeed = motor.Params.sprintSpeed,
                BallPhysics = ball.Params
            };

            TouchKind kind = DribbleTouchModel.Evaluate(input, p, out Vector3 velocity);
            if (kind == TouchKind.None)
                return kind;

            float speedFraction = Mathf.Clamp01(motor.Speed / motor.Params.sprintSpeed);
            float maxError = DribbleTouchModel.MaxErrorDegrees(p, speedFraction, BallControl, kind);
            float error = (float)(random.NextDouble() * 2.0 - 1.0) * maxError;

            // The foot mis-hits relative to the body's own motion, so the error bends the push the foot adds,
            // not the speed the ball already shares with the running player.
            Vector3 playerVelocity = PlayerMotor.Flatten(motor.Velocity);
            velocity = playerVelocity + Quaternion.AngleAxis(error, Vector3.up) * (velocity - playerVelocity);

            // A foot touch sends the ball along the grass already rolling, so it does not skid.
            ball.ApplyKick(velocity, Vector3.Cross(Vector3.up, velocity) / radius);
            nextTouchTime = time + p.touchCooldown;
            bodyIgnoresBallUntil = time + p.ownTouchBodyIgnore;
            if (Body != null)
                Body.IgnoringBall = true;
            TouchCount++;
            LastTouch = kind;
            return kind;
        }
    }
}
