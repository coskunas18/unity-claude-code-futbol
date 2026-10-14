using System;
using UnityEngine;

namespace Football.Ball
{
    /// <summary>
    /// Tunable ball physics. Defaults approximate a size-5 match ball on dry, cut grass.
    /// Kept as a plain serializable class so tests can use it without assets.
    /// </summary>
    [Serializable]
    public class BallPhysicsParams
    {
        [Header("Ball")]
        [Tooltip("kg. Laws of the Game: 0.41–0.45")]
        public float mass = 0.43f;
        [Tooltip("m. Circumference 68–70 cm")]
        public float radius = 0.11f;

        [Header("Air")]
        public float airDensity = 1.225f;
        [Tooltip("Drag coefficient. Match balls sit around 0.2–0.3 at kicking speeds")]
        public float dragCoefficient = 0.25f;
        [Tooltip("Lift coefficient per unit of spin parameter S = r|ω| / |v|")]
        public float liftPerSpinParameter = 1f;
        public float maxLiftCoefficient = 0.35f;
        [Tooltip("Seconds for spin to fall to 1/e while airborne")]
        public float airSpinDecayTime = 6f;

        [Header("Ground")]
        [Tooltip("Vertical coefficient of restitution for a soft impact")]
        public float groundRestitution = 0.62f;
        [Tooltip("Restitution lost per m/s of impact speed (hard impacts bounce relatively less)")]
        public float restitutionSpeedFalloff = 0.012f;
        public float minRestitution = 0.35f;
        [Tooltip("Impacts slower than this stop bouncing and the ball settles onto the ground (m/s)")]
        public float settleSpeed = 0.5f;
        [Tooltip("Coulomb friction between ball and grass while sliding or bouncing")]
        public float groundFriction = 0.5f;
        [Tooltip("Rolling resistance as a fraction of g")]
        public float rollingResistance = 0.045f;
        [Tooltip("Speed-proportional grass drag while rolling (1/s)")]
        public float grassDrag = 0.2f;
        [Tooltip("Decay rate of spin about the vertical axis while on the ground (1/s)")]
        public float groundSpinDecay = 3f;

        [Header("Goal frame and bodies")]
        public float postRestitution = 0.7f;
        public float postFriction = 0.1f;
        [Tooltip("Fraction of speed into the net that the net absorbs")]
        public float netAbsorption = 0.85f;
        [Tooltip("Fraction of speed along the net lost on contact")]
        public float netFriction = 0.5f;

        [Header("Integration")]
        [Tooltip("Max sub-step length (s). Small enough that a 35 m/s shot moves < 4 cm per step")]
        public float maxSubStep = 0.005f;

        public float CrossSectionArea => Mathf.PI * radius * radius;

        /// <summary>Spin change per unit of contact impulse/mass for a thin spherical shell, I = 2/3 m r².</summary>
        public float InverseInertiaFactor => 1.5f / (radius * radius);

        public BallPhysicsParams Clone() => (BallPhysicsParams)MemberwiseClone();
    }
}
