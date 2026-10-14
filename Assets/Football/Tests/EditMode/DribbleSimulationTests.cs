using System.Collections.Generic;
using Football.Ball;
using Football.Players;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests
{
    /// <summary>
    /// Headless runs of the full player–ball loop in the same order as the game:
    /// motor step → touch → ball step (with the player's body as an obstacle).
    /// </summary>
    public class DribbleSimulationTests
    {
        const float Dt = 0.02f;

        sealed class Run
        {
            public PlayerMotor Motor;
            public BallTouchController Touches;
            public BallSimulator Ball;
            public float MaxGap;
            public float MaxLateral;
            public int BodyContacts;
            public string FirstLoss;
        }

        static Run Simulate(Vector3 move, bool sprint, float seconds, int ballControl, int seed)
        {
            var motor = new PlayerMotor(new MovementParams()) { Position = new Vector3(-45f, 0f, 0f), Facing = Vector3.right };
            var ballParams = new BallPhysicsParams();
            var ball = new BallSimulator(ballParams, BallState.AtRest(new Vector3(-44.4f, 0f, 0f), ballParams.radius));
            var body = new PlayerBody(motor);
            ball.AddObstacle(body);
            var run = new Run
            {
                Motor = motor,
                Ball = ball,
                Touches = new BallTouchController(new DribbleParams(), ballControl, seed) { Body = body }
            };

            for (float t = 0f; t < seconds; t += Dt)
            {
                Vector3 legs = run.Touches.SteerMove(motor, move, ball);
                motor.Step(new MotorCommand { Move = legs, Sprint = sprint, HasBall = run.Touches.HasBall }, Dt);
                run.Touches.Update(t, motor, move, ball);
                ball.Step(Dt);

                foreach (BallContact contact in ball.LastStepContacts)
                    if (contact.Type == BallContactType.Body)
                        run.BodyContacts++;

                Vector3 offset = PlayerMotor.Flatten(ball.State.Position - motor.Position);
                float gap = offset.magnitude;
                run.MaxGap = Mathf.Max(run.MaxGap, gap);
                run.MaxLateral = Mathf.Max(run.MaxLateral, Mathf.Abs(offset.z));
                if (run.FirstLoss == null && (gap > 3f || Vector3.Dot(offset, move) < -0.3f))
                    run.FirstLoss = $"t={t:0.00} gap={gap:0.00} offset={offset} playerSpeed={motor.Speed:0.00} " +
                                    $"ballV={ball.State.Velocity} touches={run.Touches.TouchCount} body={run.BodyContacts}";
            }
            return run;
        }

        [TestCase(false, 70)]
        [TestCase(true, 70)]
        [TestCase(true, 40)]
        public void StraightDribble_KeepsTheBallForTenSeconds(bool sprint, int ballControl)
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                Run run = Simulate(Vector3.right, sprint, 10f, ballControl, seed);
                Assert.That(run.FirstLoss, Is.Null, $"seed {seed} lost the ball: {run.FirstLoss}");
                Assert.That(run.MaxLateral, Is.LessThan(0.6f), $"seed {seed} ball drifted sideways");
            }
        }

        [Test]
        public void SprintDribble_TouchesLessOftenAndFurtherAheadThanJog()
        {
            Run jog = Simulate(Vector3.right, false, 10f, 70, 1);
            Run sprint = Simulate(Vector3.right, true, 10f, 70, 1);

            Assert.That(sprint.MaxGap, Is.GreaterThan(jog.MaxGap), "sprint touches are knocked further ahead");
            float jogTouchesPerMetre = jog.Touches.TouchCount / (jog.Motor.Position.x + 45f);
            float sprintTouchesPerMetre = sprint.Touches.TouchCount / (sprint.Motor.Position.x + 45f);
            Assert.That(sprintTouchesPerMetre, Is.LessThan(jogTouchesPerMetre));
        }

        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(135f)]
        [TestCase(180f)]
        public void DribbleWithTurn_FollowsTheNewDirection(float turnDegrees)
        {
            Vector3 newDirection = Quaternion.AngleAxis(-turnDegrees, Vector3.up) * Vector3.right;
            var motor = new PlayerMotor(new MovementParams()) { Position = Vector3.zero, Facing = Vector3.right };
            var ballParams = new BallPhysicsParams();
            var ball = new BallSimulator(ballParams, BallState.AtRest(new Vector3(0.6f, 0f, 0f), ballParams.radius));
            var body = new PlayerBody(motor);
            ball.AddObstacle(body);
            var touches = new BallTouchController(new DribbleParams(), 70, 3) { Body = body };

            float t = 0f;
            void Advance(Vector3 move, float seconds)
            {
                for (float end = t + seconds; t < end; t += Dt)
                {
                    Vector3 legs = touches.SteerMove(motor, move, ball);
                    motor.Step(new MotorCommand { Move = legs, HasBall = touches.HasBall }, Dt);
                    touches.Update(t, motor, move, ball);
                    ball.Step(Dt);
                }
            }

            Advance(Vector3.right, 3f);
            Vector3 turnPoint = ball.State.Position;
            Advance(newDirection, 4f);

            Vector3 ballOffset = PlayerMotor.Flatten(ball.State.Position - motor.Position);
            float carried = Vector3.Dot(ball.State.Position - turnPoint, newDirection);
            Assert.That(carried, Is.GreaterThan(8f), "ball carried in the new direction");
            Assert.That(ballOffset.magnitude, Is.LessThan(2.5f), "and still at the player's feet");
        }
    }
}
