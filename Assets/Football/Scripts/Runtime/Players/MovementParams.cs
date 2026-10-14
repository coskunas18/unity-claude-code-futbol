using System;
using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// Physical movement limits of one player. Defaults describe an average professional outfield player.
    /// Phase 2 derives these from PlayerAttributes (Pace, Acceleration, Agility, Stamina).
    /// </summary>
    [Serializable]
    public class MovementParams
    {
        [Header("Speeds (m/s)")]
        public float walkSpeed = 1.6f;
        public float jogSpeed = 5.2f;
        [Tooltip("Top speed. Elite sprinters in football reach 9.5–10 m/s")]
        public float sprintSpeed = 8.4f;

        [Header("Acceleration (m/s²)")]
        [Tooltip("Acceleration from standstill")]
        public float acceleration = 4.6f;
        [Tooltip("Acceleration lost near top speed: a = acceleration × (1 − falloff × (v / sprintSpeed)²)")]
        [Range(0f, 0.95f)] public float accelerationFalloff = 0.6f;
        public float deceleration = 6.5f;
        [Tooltip("Braking when planting the foot to change direction sharply")]
        public float hardBrakeDeceleration = 9.5f;

        [Header("Turning")]
        [Tooltip("Max rotation of the movement direction when nearly stopped (deg/s)")]
        public float maxTurnRateStanding = 720f;
        [Tooltip("Max rotation of the movement direction at top speed (deg/s)")]
        public float maxTurnRateSprinting = 140f;
        [Tooltip("Sideways grip limit. Turning radius = v² / this (m/s²)")]
        public float maxLateralAcceleration = 7.5f;
        [Tooltip("Turns sharper than this (deg) at speed require braking before turning")]
        public float plantAndTurnAngle = 110f;
        [Tooltip("Below this speed a sharp turn no longer needs a plant (m/s)")]
        public float plantMinSpeed = 2.5f;
        [Tooltip("Target speed multiplier for a turn just below the plant angle")]
        [Range(0.1f, 1f)] public float sharpTurnSpeedFactor = 0.6f;
        [Tooltip("Below this speed the movement direction follows input directly (m/s)")]
        public float freeTurnSpeed = 0.6f;

        [Header("Body orientation")]
        public float bodyTurnRateStanding = 720f;
        public float bodyTurnRateSprinting = 260f;
        [Tooltip("Speed multiplier when moving sideways relative to the body")]
        [Range(0.1f, 1f)] public float strafeSpeedFactor = 0.62f;
        [Tooltip("Speed multiplier when moving backwards relative to the body")]
        [Range(0.1f, 1f)] public float backpedalSpeedFactor = 0.42f;

        [Header("With the ball at feet")]
        [Range(0.5f, 1f)] public float withBallSpeedFactor = 0.9f;
        [Range(0.5f, 1f)] public float withBallAccelerationFactor = 0.85f;
        [Range(0.5f, 1f)] public float withBallTurnFactor = 0.8f;

        [Header("Stamina (0–1)")]
        [Tooltip("Stamina used per second of sprinting")]
        public float sprintStaminaDrain = 0.04f;
        [Tooltip("Stamina regained per second when walking or standing")]
        public float staminaRecovery = 0.02f;
        public float minStaminaToSprint = 0.05f;
        [Tooltip("Below this stamina, top speed and acceleration fall off")]
        public float fatigueThreshold = 0.35f;
        [Range(0.3f, 1f)] public float fatigueMinFactor = 0.8f;

        public MovementParams Clone() => (MovementParams)MemberwiseClone();
    }
}
