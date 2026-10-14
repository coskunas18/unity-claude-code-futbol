using Football.Players;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests
{
    public class DribbleTouchModelTests
    {
        DribbleParams p;

        [SetUp]
        public void SetUp() => p = new DribbleParams();

        static TouchInput Input(Vector3 playerVelocity, Vector3 desired, Vector3 ballOffset, Vector3 ballVelocity) => new TouchInput
        {
            PlayerPosition = Vector3.zero,
            PlayerVelocity = playerVelocity,
            PlayerFacing = Vector3.right,
            DesiredDirection = desired,
            BallPosition = ballOffset + Vector3.up * 0.11f,
            BallVelocity = ballVelocity,
            BallRadius = 0.11f,
            SprintSpeed = 8.4f
        };

        [Test]
        public void SprintTouch_PushesTheBallFurtherAheadThanJogTouch()
        {
            DribbleTouchModel.Evaluate(Input(Vector3.right * 5f, Vector3.right, Vector3.right * 0.6f, Vector3.right * 4f), p, out Vector3 jog);
            DribbleTouchModel.Evaluate(Input(Vector3.right * 8.4f, Vector3.right, Vector3.right * 0.6f, Vector3.right * 7f), p, out Vector3 sprint);

            Assert.That(jog.magnitude - 5f, Is.LessThan(sprint.magnitude - 8.4f), "ball gets a bigger lead at sprint");
        }

        [Test]
        public void DribbleTouch_SendsBallInTheDesiredDirection()
        {
            TouchKind kind = DribbleTouchModel.Evaluate(
                Input(Vector3.right * 4f, Vector3.forward, new Vector3(0f, 0f, 0.5f), Vector3.zero), p, out Vector3 velocity);

            Assert.That(kind, Is.EqualTo(TouchKind.Dribble));
            Assert.That(Vector3.Angle(velocity, Vector3.forward), Is.LessThan(1f));
        }

        [Test]
        public void BallOffToTheSide_IsSteeredBackOntoTheRunningLine()
        {
            TouchKind kind = DribbleTouchModel.Evaluate(
                Input(Vector3.right * 6f, Vector3.right, new Vector3(0.5f, 0f, 0.4f), Vector3.right * 5f), p, out Vector3 velocity);

            Assert.That(kind, Is.EqualTo(TouchKind.Dribble));
            Assert.That(velocity.z, Is.LessThan(-0.15f), "touch pulls the ball back toward z = 0");
            Assert.That(velocity.x, Is.GreaterThan(6f), "and still pushes it ahead");
        }

        [Test]
        public void BallStillRunningAway_IsNotTouchedAgain()
        {
            TouchKind kind = DribbleTouchModel.Evaluate(
                Input(Vector3.right * 5f, Vector3.right, Vector3.right * 0.6f, Vector3.right * 7f), p, out _);
            Assert.That(kind, Is.EqualTo(TouchKind.None));
        }

        [Test]
        public void BallOutOfReach_IsNotTouched()
        {
            TouchKind kind = DribbleTouchModel.Evaluate(
                Input(Vector3.right * 5f, Vector3.right, Vector3.right * 1.5f, Vector3.zero), p, out _);
            Assert.That(kind, Is.EqualTo(TouchKind.None));
        }

        [Test]
        public void BallBehindThePlayer_IsNotTouched()
        {
            TouchKind kind = DribbleTouchModel.Evaluate(
                Input(Vector3.right * 5f, Vector3.right, Vector3.left * 0.6f, Vector3.zero), p, out _);
            Assert.That(kind, Is.EqualTo(TouchKind.None));
        }

        [Test]
        public void FastArrivingBall_IsCushioned()
        {
            TouchKind kind = DribbleTouchModel.Evaluate(
                Input(Vector3.zero, Vector3.zero, Vector3.right * 0.6f, Vector3.left * 12f), p, out Vector3 velocity);

            Assert.That(kind, Is.EqualTo(TouchKind.Cushion));
            Assert.That(velocity.magnitude, Is.LessThan(12f * 0.3f));
        }

        [Test]
        public void TouchError_ShrinksWithBallControl_AndGrowsWithSpeed()
        {
            float poorSlow = DribbleTouchModel.MaxErrorDegrees(p, 0.2f, 30, TouchKind.Dribble);
            float goodSlow = DribbleTouchModel.MaxErrorDegrees(p, 0.2f, 90, TouchKind.Dribble);
            float goodFast = DribbleTouchModel.MaxErrorDegrees(p, 1f, 90, TouchKind.Dribble);

            Assert.That(goodSlow, Is.LessThan(poorSlow));
            Assert.That(goodFast, Is.GreaterThan(goodSlow));
        }
    }
}
