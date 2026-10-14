using Football.Ball;
using Football.Core;
using Football.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Football.Debugging
{
    /// <summary>
    /// Developer tool for checking ball physics by eye. Kicks the ball in the player's facing direction with
    /// fixed, known values. It is NOT the passing or shooting system (that is KickSolver, phase 2).
    /// Keys: 1 short ground pass, 2 driven ground pass, 3 lofted ball with backspin, 4 driven shot,
    /// 5 curled shot, 6 flip curl direction, R ball to the player's feet, G penalty-spot setup.
    /// Attach to the "DebugTools" GameObject.
    /// </summary>
    public sealed class BallTestKicker : MonoBehaviour
    {
        [SerializeField] BallController ball;
        [SerializeField] PlayerMovementController player;

        [Header("Kick values")]
        [SerializeField] float shortPassSpeed = 8f;
        [SerializeField] float drivenPassSpeed = 15f;
        [SerializeField] float loftedSpeed = 22f;
        [SerializeField] float loftedAngle = 35f;
        [SerializeField] float loftedBackspin = 25f;
        [SerializeField] float shotSpeed = 27f;
        [SerializeField] float shotAngle = 6f;
        [SerializeField] float curlSpeed = 25f;
        [SerializeField] float curlAngle = 9f;
        [Tooltip("Sidespin for the curled shot (rad/s). 50 rad/s ≈ 8 rev/s")]
        [SerializeField] float curlSpin = 50f;

        int curlDirection = 1;

        public string LastAction { get; private set; } = "-";
        public int CurlDirection => curlDirection;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || ball == null || player == null)
                return;

            Vector3 direction = player.Motor.Facing;
            Vector3 backspinAxis = Vector3.Cross(direction, Vector3.up);

            if (keyboard.digit1Key.wasPressedThisFrame)
                GroundKick(direction, shortPassSpeed, "Short ground pass");
            else if (keyboard.digit2Key.wasPressedThisFrame)
                GroundKick(direction, drivenPassSpeed, "Driven ground pass");
            else if (keyboard.digit3Key.wasPressedThisFrame)
                AirKick(direction, loftedSpeed, loftedAngle, backspinAxis * loftedBackspin, "Lofted ball, backspin");
            else if (keyboard.digit4Key.wasPressedThisFrame)
                AirKick(direction, shotSpeed, shotAngle, Vector3.zero, "Driven shot");
            else if (keyboard.digit5Key.wasPressedThisFrame)
                AirKick(direction, curlSpeed, curlAngle, Vector3.up * (curlSpin * curlDirection),
                    curlDirection > 0 ? "Curled shot, bends right" : "Curled shot, bends left");
            else if (keyboard.digit6Key.wasPressedThisFrame)
            {
                curlDirection = -curlDirection;
                LastAction = curlDirection > 0 ? "Curl: right" : "Curl: left";
            }
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                ball.PlaceAt(player.Motor.Position + direction * 0.7f);
                LastAction = "Ball to feet";
            }
            else if (keyboard.gKey.wasPressedThisFrame)
            {
                var spot = new Vector3(PitchDimensions.HalfLength - PitchDimensions.PenaltySpotDistance, 0f, 0f);
                player.Teleport(spot - Vector3.right * 1.2f, Vector3.right);
                ball.PlaceAt(spot);
                LastAction = "Penalty spot setup";
            }
        }

        void GroundKick(Vector3 direction, float speed, string label)
        {
            Vector3 velocity = direction * speed;
            ball.Kick(velocity, Vector3.Cross(Vector3.up, velocity) / ball.Radius);
            LastAction = $"{label} {speed:0} m/s";
        }

        void AirKick(Vector3 direction, float speed, float elevationDegrees, Vector3 spin, string label)
        {
            float elevation = elevationDegrees * Mathf.Deg2Rad;
            Vector3 velocity = direction * (Mathf.Cos(elevation) * speed) + Vector3.up * (Mathf.Sin(elevation) * speed);
            ball.Kick(velocity, spin);
            LastAction = $"{label} {speed:0} m/s @ {elevationDegrees:0}°";
        }
    }
}
