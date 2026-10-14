using Football.Ball;
using System;
using UnityEngine;

namespace Football.Players
{
    [Serializable]
    public class DribbleParams
    {
        [Tooltip("Max distance from the player's centre to the ball's centre for a foot touch (m)")]
        public float reach = 0.75f;
        [Tooltip("Inside this distance the ball can be touched from any angle (m)")]
        public float bodyReach = 0.45f;
        [Tooltip("Half-angle of the cone in front of the player where touches are possible (deg)")]
        public float frontHalfAngle = 75f;
        [Tooltip("Ball higher than this above the grass needs a header or chest, not a foot (m)")]
        public float maxTouchHeight = 0.4f;

        [Header("Dribbling")]
        [Tooltip("Ball speed added on top of the player's speed at jogging pace (m/s)")]
        public float closeControlPush = 0.7f;
        [Tooltip("Ball speed added on top of the player's speed at full sprint (m/s)")]
        public float sprintPush = 2.6f;
        [Tooltip("Share of the ball's sideways offset from the running line that a touch removes by the next touch. " +
                 "Without it touch errors add up until the ball drifts out of reach; at 1 or more it overshoots")]
        [Range(0f, 1f)] public float lateralCorrectionGain = 0.7f;
        [Tooltip("No new touch while the ball is still moving away from the player faster than this (m/s)")]
        public float maxRelativeSpeedForTouch = 0.4f;
        [Tooltip("While dribbling the legs run to a point this far behind the ball, seen from the desired direction (m)")]
        public float steerBehindDistance = 0.45f;
        [Tooltip("How far ahead the ball's motion is anticipated when steering toward it (s)")]
        public float steerLeadTime = 0.15f;
        [Tooltip("Throttle used when the touch spot is 90° or more off the current running direction, so the turn is tight")]
        [Range(0.1f, 1f)] public float steerTurnThrottle = 0.35f;
        [Tooltip("Within this distance of a slow ball the player shortens his stride to arrive at it instead of running past (m)")]
        public float steerArriveDistance = 1.2f;
        [Tooltip("Sideways speed added to a drag-back so the ball rolls past the standing leg instead of into the body (m/s)")]
        public float dragBackSideSpeed = 0.9f;
        [Tooltip("A drag-back needs the player almost stopped: max speed toward the ball for it (m/s). " +
                 "Faster than this, a reversing player stops the ball with the sole and steps over it")]
        public float dragBackMaxSpeed = 1.5f;
        [Tooltip("After his own touch the player's body does not block the ball for this long (s)")]
        public float ownTouchBodyIgnore = 0.4f;
        [Tooltip("Minimum time between two touches (s)")]
        public float touchCooldown = 0.25f;

        [Header("Receiving (basic; full first-touch model in phase 2)")]
        [Tooltip("Ball arriving faster than this is cushioned instead of dribbled (m/s)")]
        public float cushionMinSpeed = 3f;
        [Tooltip("Fraction of the arriving relative speed kept after a cushioning touch")]
        [Range(0f, 1f)] public float cushionRetain = 0.2f;
        [Tooltip("Fraction of the ball's speed kept when the player stops it with no movement input")]
        [Range(0f, 1f)] public float trapRetain = 0.1f;

        [Header("Touch error (deg)")]
        public float baseErrorDegrees = 2f;
        public float speedErrorDegrees = 6f;
        public float cushionErrorMultiplier = 2f;

        [Header("Close control state")]
        [Tooltip("Within this distance the player can have the ball at feet (m)")]
        public float closeControlRadius = 1.6f;
        [Tooltip("Ball at feet if it moves with the player within this speed difference, or is itself slower than this (m/s). " +
                 "A fast pass flying past is not under control")]
        public float closeControlRelativeSpeed = 3f;
        [Tooltip("After losing close control the player keeps going for the ball this long (s) ...")]
        public float dribbleMemory = 1f;
        [Tooltip("... as long as it stays within this distance (m)")]
        public float dribbleRadius = 3.5f;
    }

    public enum TouchKind
    {
        None,
        /// <summary>Pushes the ball ahead in the desired direction.</summary>
        Dribble,
        /// <summary>Stops a slow ball when the player wants to stand still.</summary>
        Trap,
        /// <summary>Takes the pace off an arriving ball.</summary>
        Cushion
    }

    public struct TouchInput
    {
        public Vector3 PlayerPosition;
        public Vector3 PlayerVelocity;
        public Vector3 PlayerFacing;
        /// <summary>Unit direction the controller wants to go, or zero to stand still.</summary>
        public Vector3 DesiredDirection;
        public Vector3 BallPosition;
        public Vector3 BallVelocity;
        public float BallRadius;
        public float SprintSpeed;
        /// <summary>Physics of the ball, used to estimate how long the touch lasts before the player catches up.</summary>
        public BallPhysicsParams BallPhysics;
    }

    /// <summary>
    /// Decides whether a foot touch happens and what velocity it gives the ball before execution error.
    /// The ball is never attached to the player: every touch is a separate kick.
    /// </summary>
    public static class DribbleTouchModel
    {
        public static TouchKind Evaluate(in TouchInput input, DribbleParams p, out Vector3 ballVelocity)
        {
            ballVelocity = Vector3.zero;

            Vector3 toBall = PlayerMotor.Flatten(input.BallPosition - input.PlayerPosition);
            float distance = toBall.magnitude;
            float ballHeight = input.BallPosition.y - input.BallRadius;
            if (distance > p.reach || ballHeight > p.maxTouchHeight)
                return TouchKind.None;

            // The ball must be in front of the body, or in front of where the player is turning to go:
            // a player changing direction drags the ball across with the inside or outside of the foot.
            bool hasDirection = input.DesiredDirection.sqrMagnitude > 1e-4f;
            if (distance > p.bodyReach && distance > 1e-4f &&
                Vector3.Angle(input.PlayerFacing, toBall) > p.frontHalfAngle &&
                (!hasDirection || Vector3.Angle(input.DesiredDirection, toBall) > p.frontHalfAngle))
                return TouchKind.None;

            Vector3 playerVelocity = PlayerMotor.Flatten(input.PlayerVelocity);
            Vector3 ballVelocityFlat = PlayerMotor.Flatten(input.BallVelocity);
            Vector3 relative = ballVelocityFlat - playerVelocity;

            float approachSpeed = distance > 1e-4f ? -Vector3.Dot(relative, toBall / distance) : relative.magnitude;
            if (approachSpeed > p.cushionMinSpeed)
            {
                Vector3 cushioned = playerVelocity + relative * p.cushionRetain;
                if (hasDirection)
                    cushioned += input.DesiredDirection * p.closeControlPush;
                ballVelocity = cushioned;
                return TouchKind.Cushion;
            }

            if (!hasDirection)
            {
                if (ballVelocityFlat.magnitude < 0.3f)
                    return TouchKind.None;
                ballVelocity = ballVelocityFlat * p.trapRetain;
                return TouchKind.Trap;
            }

            // Wait for the player to catch up with the previous touch: no touch while the ball moves away from him.
            if (distance > 1e-4f && Vector3.Dot(relative, toBall / distance) > p.maxRelativeSpeedForTouch)
                return TouchKind.None;

            float forwardSpeed = Mathf.Max(0f, Vector3.Dot(playerVelocity, input.DesiredDirection));
            float speedFraction = input.SprintSpeed > 0f ? Mathf.Clamp01(forwardSpeed / input.SprintSpeed) : 0f;
            float push = Mathf.Lerp(p.closeControlPush, p.sprintPush, speedFraction * speedFraction);
            // Steer the ball back onto the running line over the time until the player catches it again:
            // the ball runs ahead by `push` and the grass takes that lead away at its rolling deceleration.
            float ballSpeed = forwardSpeed + push;
            float deceleration = input.BallPhysics != null
                ? BallSimulator.RollingDeceleration(input.BallPhysics, ballSpeed)
                : 2f;
            float timeToNextTouch = Mathf.Max(p.touchCooldown, 2f * push / Mathf.Max(0.1f, deceleration));
            Vector3 sideways = toBall - Vector3.Dot(toBall, input.DesiredDirection) * input.DesiredDirection;

            // Drag-back: the ball is sent back past the player, so steer it beside the standing leg, not onto
            // the running line (which goes through the body).
            if (Vector3.Dot(toBall, input.DesiredDirection) < 0f)
            {
                if (distance > 1e-4f && Vector3.Dot(playerVelocity, toBall / distance) > p.dragBackMaxSpeed)
                {
                    // Too fast to drag it back: stop the ball dead with the sole, then turn over it.
                    ballVelocity = Vector3.zero;
                    return TouchKind.Trap;
                }
                Vector3 side = sideways.sqrMagnitude > 1e-4f
                    ? sideways.normalized
                    : Vector3.Cross(Vector3.up, input.DesiredDirection);
                ballVelocity = input.DesiredDirection * ballSpeed + side * p.dragBackSideSpeed;
                return TouchKind.Dribble;
            }

            ballVelocity = input.DesiredDirection * ballSpeed - sideways * (p.lateralCorrectionGain / timeToNextTouch);
            return TouchKind.Dribble;
        }

        /// <summary>Largest error angle for a touch, shrinking with ball control (1–100) and growing with speed.</summary>
        public static float MaxErrorDegrees(DribbleParams p, float speedFraction, int ballControl, TouchKind kind)
        {
            float skill = Mathf.Clamp(ballControl, 1, 100) / 100f;
            float error = (p.baseErrorDegrees + p.speedErrorDegrees * speedFraction) * Mathf.Lerp(1f, 0.25f, skill);
            return kind == TouchKind.Cushion ? error * p.cushionErrorMultiplier : error;
        }
    }
}
