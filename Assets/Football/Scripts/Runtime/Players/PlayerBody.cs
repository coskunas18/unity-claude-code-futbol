using Football.Ball;
using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// The player's body as the ball sees it: one capsule for legs and torso that moves with the motor.
    /// The lower end sits above the grass so a ball rolling at the feet is reachable for a touch before
    /// the shins block it.
    /// </summary>
    public sealed class PlayerBody : IBallObstacle
    {
        public const float DefaultRadius = 0.28f;
        public const float DefaultHeight = 1.8f;
        const float LowerEndHeight = 0.3f;

        readonly PlayerMotor motor;

        public float Radius = DefaultRadius;
        public float Height = DefaultHeight;
        public float BodyRestitution = 0.3f;
        public bool Enabled = true;
        /// <summary>
        /// Set briefly after the player's own touch: the touching leg is already in contact with the ball and the
        /// single capsule would otherwise shove the ball the player just played.
        /// </summary>
        public bool IgnoringBall;

        public PlayerBody(PlayerMotor motor)
        {
            this.motor = motor;
        }

        public Vector3 CapsuleBottom => motor.Position + Vector3.up * LowerEndHeight;
        public Vector3 CapsuleTop => motor.Position + Vector3.up * (Height - Radius);
        public float CapsuleRadius => Radius;
        public Vector3 Velocity => motor.Velocity;
        public float Restitution => BodyRestitution;
        public bool BlocksBall => Enabled && !IgnoringBall;
    }
}
