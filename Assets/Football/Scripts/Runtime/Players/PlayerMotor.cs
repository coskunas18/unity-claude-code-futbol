using UnityEngine;

namespace Football.Players
{
    /// <summary>What a controller (human input or AI) asks the body to do this tick.</summary>
    public struct MotorCommand
    {
        /// <summary>Desired movement direction on the ground plane. Magnitude 0–1 is the throttle.</summary>
        public Vector3 Move;
        public bool Sprint;
        /// <summary>Optional direction to face (e.g. toward the ball). Zero means face the movement direction.</summary>
        public Vector3 Face;
        /// <summary>Ball at feet: slower, less agile movement profile.</summary>
        public bool HasBall;
    }

    /// <summary>
    /// Kinematic movement model with momentum: acceleration curve, braking, speed-dependent turning radius,
    /// plant-and-turn for sharp direction changes, body orientation (strafe and backpedal) and stamina.
    /// Pure C# so AI can predict arrival times with the same rules the body obeys.
    /// </summary>
    public sealed class PlayerMotor
    {
        public MovementParams Params;

        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Facing = Vector3.forward;
        public float Stamina = 1f;

        public float Speed => Velocity.magnitude;
        public bool IsSprinting { get; private set; }
        /// <summary>True while braking hard before a sharp change of direction.</summary>
        public bool IsPlanting { get; private set; }

        public PlayerMotor(MovementParams parameters)
        {
            Params = parameters;
        }

        public void Step(in MotorCommand command, float dt)
        {
            MovementParams p = Params;
            Vector3 move = Flatten(command.Move);
            float throttle = Mathf.Clamp01(move.magnitude);
            bool hasInput = throttle > 0.05f;
            Vector3 desired = hasInput ? move / move.magnitude : Vector3.zero;

            float speed = Velocity.magnitude;
            float speedFraction = Mathf.Clamp01(speed / p.sprintSpeed);
            float fatigue = FatigueFactor();

            // Body orientation turns toward the requested facing, slower at speed.
            Vector3 faceRequest = Flatten(command.Face);
            Vector3 faceTarget = faceRequest.sqrMagnitude > 1e-4f ? faceRequest.normalized
                : hasInput ? desired : Facing;
            float bodyRate = Mathf.Lerp(p.bodyTurnRateStanding, p.bodyTurnRateSprinting, speedFraction);
            if (command.HasBall)
                bodyRate *= p.withBallTurnFactor;
            Facing = RotateTowards(Facing, faceTarget, bodyRate * Mathf.Deg2Rad * dt);

            // Target speed from throttle, sprint, fatigue, ball and body-relative direction.
            bool sprintRequested = command.Sprint && throttle > 0.5f && Stamina > p.minStaminaToSprint;
            float topSpeed = sprintRequested ? p.sprintSpeed : p.jogSpeed;
            float targetSpeed = hasInput
                ? Mathf.Lerp(p.walkSpeed, topSpeed, Mathf.InverseLerp(0.2f, 1f, throttle))
                : 0f;
            targetSpeed *= fatigue;
            if (command.HasBall)
                targetSpeed *= p.withBallSpeedFactor;
            if (hasInput)
                targetSpeed *= DirectionalSpeedFactor(Vector3.Dot(desired, Facing));

            // Momentum: the current direction of travel only bends toward the desired one.
            Vector3 heading = speed > 0.05f ? Velocity / speed : hasInput ? desired : Facing;
            float turnAngle = hasInput ? Vector3.Angle(heading, desired) : 0f;
            IsPlanting = speed > p.plantMinSpeed && turnAngle > p.plantAndTurnAngle;
            if (IsPlanting)
                targetSpeed = 0f;
            else if (hasInput)
                targetSpeed *= Mathf.Lerp(1f, p.sharpTurnSpeedFactor, turnAngle / p.plantAndTurnAngle);

            float acceleration = p.acceleration * (1f - p.accelerationFalloff * speedFraction * speedFraction) * fatigue;
            if (command.HasBall)
                acceleration *= p.withBallAccelerationFactor;
            float rate = targetSpeed > speed ? acceleration
                : IsPlanting ? p.hardBrakeDeceleration : p.deceleration;
            speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);

            if (hasInput)
            {
                if (speed < p.freeTurnSpeed)
                {
                    heading = desired;
                }
                else if (!IsPlanting)
                {
                    float turnRate = Mathf.Lerp(p.maxTurnRateStanding, p.maxTurnRateSprinting, speedFraction) * Mathf.Deg2Rad;
                    float gripLimitedRate = p.maxLateralAcceleration / speed;
                    if (command.HasBall)
                        turnRate *= p.withBallTurnFactor;
                    heading = RotateTowards(heading, desired, Mathf.Min(turnRate, gripLimitedRate) * dt);
                }
            }

            Velocity = heading * speed;
            Position += Velocity * dt;
            Position.y = 0f;

            IsSprinting = sprintRequested && speed > p.jogSpeed;
            float staminaDelta = IsSprinting ? -p.sprintStaminaDrain
                : speed < p.jogSpeed * 0.6f ? p.staminaRecovery
                : p.staminaRecovery * 0.4f;
            Stamina = Mathf.Clamp01(Stamina + staminaDelta * dt);
        }

        /// <summary>
        /// Throttle (command magnitude) that makes the motor aim for this running speed without sprinting.
        /// Inverse of the throttle-to-speed mapping in <see cref="Step"/>. Below walking pace the motor cannot
        /// creep, so 0 is returned and the caller should stop instead.
        /// </summary>
        public float ThrottleForSpeed(float speed)
        {
            MovementParams p = Params;
            if (speed < p.walkSpeed * 0.5f)
                return 0f;
            return Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(p.walkSpeed, p.jogSpeed, speed));
        }

        /// <summary>1 at or above the fatigue threshold, falling linearly to fatigueMinFactor at zero stamina.</summary>
        public float FatigueFactor()
        {
            MovementParams p = Params;
            if (Stamina >= p.fatigueThreshold)
                return 1f;
            return Mathf.Lerp(p.fatigueMinFactor, 1f, Stamina / p.fatigueThreshold);
        }

        /// <summary>Speed multiplier for moving at an angle to the body: 1 forward, strafe sideways, backpedal backwards.</summary>
        public float DirectionalSpeedFactor(float cosAngleToFacing)
        {
            MovementParams p = Params;
            return cosAngleToFacing >= 0f
                ? Mathf.Lerp(p.strafeSpeedFactor, 1f, cosAngleToFacing)
                : Mathf.Lerp(p.strafeSpeedFactor, p.backpedalSpeedFactor, -cosAngleToFacing);
        }

        public static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 RotateTowards(Vector3 from, Vector3 to, float maxRadians)
        {
            Vector3 result = Vector3.RotateTowards(from, to, maxRadians, 0f);
            result.y = 0f;
            return result.sqrMagnitude > 1e-8f ? result.normalized : to;
        }
    }
}
