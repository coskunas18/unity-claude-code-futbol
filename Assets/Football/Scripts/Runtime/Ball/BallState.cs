using UnityEngine;

namespace Football.Ball
{
    /// <summary>Complete dynamic state of the ball. A value type so predictions can copy it freely.</summary>
    public struct BallState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        /// <summary>Angular velocity in rad/s, world space.</summary>
        public Vector3 AngularVelocity;
        /// <summary>True while the ball is resting or rolling on the grass (not bouncing or airborne).</summary>
        public bool OnGround;

        public float Speed => Velocity.magnitude;
        public float Height(float radius) => Position.y - radius;

        public static BallState AtRest(Vector3 groundPoint, float radius) => new BallState
        {
            Position = new Vector3(groundPoint.x, radius, groundPoint.z),
            OnGround = true
        };
    }

    public enum BallContactType
    {
        GroundBounce,
        Post,
        Crossbar,
        Net,
        Body
    }

    public readonly struct BallContact
    {
        public readonly BallContactType Type;
        public readonly Vector3 Point;
        /// <summary>Approach speed along the contact normal (m/s).</summary>
        public readonly float ImpactSpeed;
        /// <summary>The body that was hit, for <see cref="BallContactType.Body"/>; otherwise null.</summary>
        public readonly IBallObstacle Obstacle;

        public BallContact(BallContactType type, Vector3 point, float impactSpeed, IBallObstacle obstacle = null)
        {
            Type = type;
            Point = point;
            ImpactSpeed = impactSpeed;
            Obstacle = obstacle;
        }
    }

    /// <summary>A moving capsule the ball can bounce off, such as a player's body.</summary>
    public interface IBallObstacle
    {
        Vector3 CapsuleBottom { get; }
        Vector3 CapsuleTop { get; }
        float CapsuleRadius { get; }
        Vector3 Velocity { get; }
        float Restitution { get; }
        bool BlocksBall { get; }
    }
}
