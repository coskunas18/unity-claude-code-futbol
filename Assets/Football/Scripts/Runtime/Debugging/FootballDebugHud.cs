using Football.Ball;
using Football.Players;
using UnityEngine;

namespace Football.Debugging
{
    /// <summary>
    /// On-screen readout of movement and ball physics for tuning. Developer overlay, not the match HUD.
    /// Toggle with F1. Attach to the "DebugTools" GameObject.
    /// </summary>
    public sealed class FootballDebugHud : MonoBehaviour
    {
        [SerializeField] PlayerMovementController player;
        [SerializeField] PlayerBallInteraction playerBall;
        [SerializeField] BallController ball;
        [SerializeField] BallEventsMonitor rules;
        [SerializeField] BallTestKicker kicker;
        [SerializeField] bool visible = true;

        string lastContact = "-";
        GUIStyle style;

        void OnEnable()
        {
            if (ball != null)
                ball.Contacted += OnBallContact;
        }

        void OnDisable()
        {
            if (ball != null)
                ball.Contacted -= OnBallContact;
        }

        void OnBallContact(BallContact contact) =>
            lastContact = $"{contact.Type} at {contact.ImpactSpeed:0.0} m/s";

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8)
            };

            var text = new System.Text.StringBuilder();
            if (player != null)
            {
                PlayerMotor motor = player.Motor;
                text.AppendLine("<b>PLAYER</b>");
                text.AppendLine($"Speed {motor.Speed:0.00} m/s  ({motor.Speed * 3.6f:0} km/h)");
                text.AppendLine($"Stamina {motor.Stamina * 100f:0}%   Fatigue x{motor.FatigueFactor():0.00}");
                text.AppendLine($"Sprinting {Flag(motor.IsSprinting)}   Planting {Flag(motor.IsPlanting)}   Ball at feet {Flag(player.HasBall)}");
                if (playerBall != null)
                    text.AppendLine($"Touches {playerBall.TouchCount}   Last {playerBall.LastTouch}");
            }

            if (ball != null)
            {
                BallState state = ball.State;
                text.AppendLine();
                text.AppendLine("<b>BALL</b>");
                text.AppendLine($"Speed {state.Speed:0.0} m/s   Height {state.Height(ball.Radius):0.00} m   {(state.OnGround ? "on grass" : "in air")}");
                text.AppendLine($"Spin {state.AngularVelocity.magnitude / (2f * Mathf.PI):0.0} rev/s");
                text.AppendLine($"Last contact: {lastContact}");
            }

            if (rules != null)
                text.AppendLine($"Last stoppage: {rules.LastResult}   Goals: {rules.Goals}");
            if (kicker != null)
                text.AppendLine($"Test kick: {kicker.LastAction}");

            text.AppendLine();
            text.AppendLine("<b>CONTROLS</b>  WASD move · Shift sprint · Q face ball");
            text.AppendLine("1 short pass · 2 driven pass · 3 lofted · 4 shot · 5 curl · 6 curl side");
            text.AppendLine("R ball to feet · G penalty spot · F1 hide");

            GUI.Box(new Rect(12, 12, 470, 290), text.ToString(), style);
        }

        static string Flag(bool value) => value ? "<color=#7CFC00>yes</color>" : "no";
    }
}
