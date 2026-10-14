using Football.Players;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests
{
    public class PlayerMotorTests
    {
        const float Dt = 0.02f;
        MovementParams p;
        PlayerMotor motor;

        [SetUp]
        public void SetUp()
        {
            p = new MovementParams();
            motor = new PlayerMotor(p) { Facing = Vector3.right };
        }

        static MotorCommand Run(Vector3 direction, bool sprint = false, Vector3 face = default) =>
            new MotorCommand { Move = direction, Sprint = sprint, Face = face };

        void Simulate(MotorCommand command, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
                motor.Step(command, Dt);
        }

        [Test]
        public void SprintFromStandstill_ReachesNinetyPercentInRealisticTime()
        {
            float time = 0f;
            while (motor.Speed < p.sprintSpeed * 0.9f && time < 10f)
            {
                motor.Step(Run(Vector3.right, sprint: true), Dt);
                time += Dt;
            }
            Assert.That(time, Is.InRange(1.8f, 3.0f));
        }

        [Test]
        public void Speed_NeverExceedsSprintSpeed()
        {
            Simulate(Run(Vector3.right, sprint: true), 8f);
            Assert.That(motor.Speed, Is.LessThanOrEqualTo(p.sprintSpeed + 1e-3f));
            Assert.That(motor.Speed, Is.GreaterThan(p.sprintSpeed * 0.97f));
        }

        [Test]
        public void ReversingAtSprint_BrakesBeforeTurning()
        {
            Simulate(Run(Vector3.right, sprint: true), 4f);

            float stillForwardFor = 0f;
            bool planted = false;
            for (float t = 0f; t < 4f; t += Dt)
            {
                motor.Step(Run(Vector3.left, sprint: true), Dt);
                planted |= motor.IsPlanting;
                if (motor.Velocity.x > 0f)
                    stillForwardFor += Dt;
            }

            Assert.That(planted, Is.True, "a 180° turn at sprint should plant");
            Assert.That(stillForwardFor, Is.GreaterThan(0.4f), "momentum keeps carrying forward");
            Assert.That(motor.Velocity.x, Is.LessThan(-p.jogSpeed * 0.8f), "eventually runs the other way");
        }

        [Test]
        public void TurningAtSprint_IsLimitedByGrip()
        {
            Simulate(Run(Vector3.right, sprint: true), 4f);
            motor.Step(Run(Vector3.forward, sprint: true), Dt);
            Simulate(Run(Vector3.forward, sprint: true), 0.18f);

            float headingChange = Vector3.Angle(Vector3.right, motor.Velocity);
            Assert.That(headingChange, Is.LessThan(45f), "a 90° cut at top speed cannot happen in 0.2 s");
            Assert.That(headingChange, Is.GreaterThan(5f), "but the player does start to turn");
        }

        [Test]
        public void TurningFromJog_IsMuchQuickerThanFromSprint()
        {
            Simulate(Run(Vector3.right), 3f);
            Simulate(Run(Vector3.forward), 0.2f);
            float jogTurn = Vector3.Angle(Vector3.right, motor.Velocity);

            SetUp();
            Simulate(Run(Vector3.right, sprint: true), 4f);
            Simulate(Run(Vector3.forward, sprint: true), 0.2f);
            float sprintTurn = Vector3.Angle(Vector3.right, motor.Velocity);

            Assert.That(jogTurn, Is.GreaterThan(sprintTurn * 1.3f));
        }

        [Test]
        public void Backpedalling_IsSlowerThanStrafing_IsSlowerThanRunningForward()
        {
            Simulate(Run(Vector3.right, face: Vector3.right), 4f);
            float forward = motor.Speed;

            SetUp();
            Simulate(Run(Vector3.forward, face: Vector3.right), 4f);
            float strafe = motor.Speed;

            SetUp();
            Simulate(Run(Vector3.left, face: Vector3.right), 4f);
            float backwards = motor.Speed;

            Assert.That(strafe, Is.LessThan(forward * 0.75f));
            Assert.That(backwards, Is.LessThan(strafe));
            Assert.That(Vector3.Dot(motor.Facing, Vector3.right), Is.GreaterThan(0.99f), "body keeps facing the target");
        }

        [Test]
        public void Sprinting_DrainsStamina_AndFatigueLowersTopSpeed()
        {
            Simulate(Run(Vector3.right, sprint: true), 8f);
            Assert.That(motor.Stamina, Is.LessThan(0.8f));

            motor.Stamina = 0.05f;
            Simulate(Run(Vector3.right, sprint: true), 1f);
            Assert.That(motor.Speed, Is.LessThan(p.jogSpeed + 0.01f), "too tired to sprint");

            motor.Stamina = 0.2f;
            float before = motor.Stamina;
            Simulate(default, 3f);
            Assert.That(motor.Stamina, Is.GreaterThan(before), "recovers while resting");
        }

        [Test]
        public void WithBall_TopSpeedIsLower()
        {
            Simulate(new MotorCommand { Move = Vector3.right, Sprint = true, HasBall = true }, 6f);
            Assert.That(motor.Speed, Is.LessThan(p.sprintSpeed * p.withBallSpeedFactor + 0.01f));
        }

        [Test]
        public void NoInput_PlayerComesToAStop()
        {
            Simulate(Run(Vector3.right), 3f);
            Simulate(default, 2f);
            Assert.That(motor.Speed, Is.LessThan(0.01f));
        }
    }
}
