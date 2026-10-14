using System.Collections.Generic;
using Football.Ball;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests
{
    public class BallSimulatorTests
    {
        const float Dt = 0.02f;
        BallPhysicsParams p;

        [SetUp]
        public void SetUp() => p = new BallPhysicsParams();

        BallState Rolling(Vector3 position, Vector3 velocity) => new BallState
        {
            Position = new Vector3(position.x, p.radius, position.z),
            Velocity = velocity,
            AngularVelocity = Vector3.Cross(Vector3.up, velocity) / p.radius,
            OnGround = true
        };

        BallState Airborne(Vector3 position, Vector3 velocity, Vector3 spin) => new BallState
        {
            Position = position,
            Velocity = velocity,
            AngularVelocity = spin
        };

        /// <summary>Runs until the ball stops or the time runs out; returns the final state.</summary>
        BallState RunUntilStopped(BallState s, float maxSeconds, List<BallContact> contacts = null)
        {
            for (float t = 0f; t < maxSeconds; t += Dt)
            {
                BallSimulator.Advance(ref s, p, Dt, null, contacts);
                if (s.OnGround && s.Velocity.sqrMagnitude < 1e-6f)
                    break;
            }
            return s;
        }

        [TestCase(8f, 12f, 25f)]
        [TestCase(15f, 30f, 55f)]
        public void GroundPass_RollsARealisticDistance(float speed, float minDistance, float maxDistance)
        {
            BallState end = RunUntilStopped(Rolling(Vector3.zero, Vector3.right * speed), 30f);

            Assert.That(end.Velocity.magnitude, Is.LessThan(1e-3f), "ball should come to rest");
            Assert.That(end.Position.x, Is.InRange(minDistance, maxDistance));
            Assert.That(Mathf.Abs(end.Position.z), Is.LessThan(1e-3f), "a pure ground pass must not drift sideways");
        }

        [Test]
        public void RollingBall_SpinMatchesRollingWithoutSlip()
        {
            BallState s = Rolling(Vector3.zero, Vector3.right * 10f);
            BallSimulator.Advance(ref s, p, 0.5f, null, null);

            Vector3 contactVelocity = s.Velocity + Vector3.Cross(s.AngularVelocity, -p.radius * Vector3.up);
            Assert.That(PlayerlessFlat(contactVelocity).magnitude, Is.LessThan(0.02f));
        }

        [Test]
        public void SkiddingBall_PicksUpTopspinAndStartsRolling()
        {
            var s = new BallState
            {
                Position = new Vector3(0f, p.radius, 0f),
                Velocity = Vector3.right * 10f,
                OnGround = true
            };
            BallSimulator.Advance(ref s, p, 1f, null, null);

            Vector3 contactVelocity = s.Velocity + Vector3.Cross(s.AngularVelocity, -p.radius * Vector3.up);
            Assert.That(PlayerlessFlat(contactVelocity).magnitude, Is.LessThan(0.02f), "should be rolling after 1 s");
            Assert.That(s.Velocity.x, Is.LessThan(10f * 0.75f), "sliding friction costs speed");
        }

        [Test]
        public void DroppedBall_BouncesLowerEachTimeThenSettles()
        {
            BallState s = Airborne(new Vector3(0f, 3f, 0f), Vector3.zero, Vector3.zero);
            var apexes = new List<float>();
            float previousVy = 0f;
            for (float t = 0f; t < 10f && !s.OnGround; t += Dt)
            {
                BallSimulator.Advance(ref s, p, Dt, null, null);
                if (previousVy > 0f && s.Velocity.y <= 0f)
                    apexes.Add(s.Position.y);
                previousVy = s.Velocity.y;
            }

            Assert.That(s.OnGround, Is.True, "ball should settle within 10 s");
            Assert.That(apexes.Count, Is.GreaterThanOrEqualTo(3));
            for (int i = 1; i < apexes.Count; i++)
                Assert.That(apexes[i], Is.LessThan(apexes[i - 1]), $"bounce {i} should be lower than bounce {i - 1}");
            Assert.That(apexes[0], Is.InRange(0.8f, 1.6f), "first rebound from 3 m on grass");
        }

        [Test]
        public void Backspin_CarriesFurtherThanNoSpin()
        {
            Vector3 launch = new Vector3(Mathf.Cos(30f * Mathf.Deg2Rad), Mathf.Sin(30f * Mathf.Deg2Rad), 0f) * 22f;
            float carryNoSpin = Carry(Airborne(new Vector3(0f, p.radius, 0f), launch, Vector3.zero));
            float carryBackspin = Carry(Airborne(new Vector3(0f, p.radius, 0f), launch, Vector3.forward * 25f));

            Assert.That(carryBackspin, Is.GreaterThan(carryNoSpin + 1f));
        }

        [Test]
        public void Drag_ShortensFlightComparedToVacuum()
        {
            Vector3 launch = new Vector3(Mathf.Cos(45f * Mathf.Deg2Rad), Mathf.Sin(45f * Mathf.Deg2Rad), 0f) * 25f;
            float carry = Carry(Airborne(new Vector3(0f, p.radius, 0f), launch, Vector3.zero));
            float vacuum = 25f * 25f / BallSimulator.Gravity;

            Assert.That(carry, Is.LessThan(vacuum * 0.9f));
            Assert.That(carry, Is.GreaterThan(vacuum * 0.5f));
        }

        [TestCase(1, -1)]
        [TestCase(-1, 1)]
        public void Sidespin_CurvesTheShot(int spinSign, int expectedZSign)
        {
            // Travelling along +X: right of the ball is -Z. Positive spin about +Y bends it right.
            Vector3 launch = new Vector3(25f, 3f, 0f);
            BallState s = Airborne(new Vector3(0f, p.radius, 0f), launch, Vector3.up * (50f * spinSign));
            while (s.Position.x < 20f)
                BallSimulator.Advance(ref s, p, Dt, null, null);

            Assert.That(Mathf.Sign(s.Position.z), Is.EqualTo(expectedZSign));
            Assert.That(Mathf.Abs(s.Position.z), Is.InRange(0.8f, 6f), "a 20 m curler bends roughly 1–5 m");
        }

        [Test]
        public void ShotAtPost_BouncesBackAndNeverPassesThrough()
        {
            Vector3 post = PitchDimensions.PostBase(1, 1);
            BallState s = Airborne(new Vector3(post.x - 10f, 1f, post.z), new Vector3(30f, 0f, 0f), Vector3.zero);
            var contacts = new List<BallContact>();
            float maxX = float.MinValue;
            for (float t = 0f; t < 1f; t += Dt)
            {
                BallSimulator.Advance(ref s, p, Dt, null, contacts);
                maxX = Mathf.Max(maxX, s.Position.x);
            }

            Assert.That(contacts.Exists(c => c.Type == BallContactType.Post), Is.True, "should hit the post");
            Assert.That(maxX, Is.LessThan(post.x - PitchDimensions.PostRadius - p.radius + 0.01f));
            Assert.That(s.Velocity.x, Is.LessThan(0f), "should rebound into the field");
        }

        [Test]
        public void ShotUnderTheBar_IsStoppedByTheNet()
        {
            BallState s = Airborne(new Vector3(40f, 1.2f, 1f), new Vector3(30f, 1f, 0f), Vector3.zero);
            var contacts = new List<BallContact>();
            BallZone? firstOut = null;
            for (float t = 0f; t < 4f; t += Dt)
            {
                BallSimulator.Advance(ref s, p, Dt, null, contacts);
                BallZoneResult zone = PitchRules.Classify(s.Position, p.radius);
                if (firstOut == null && zone.Zone != BallZone.InPlay)
                    firstOut = zone.Zone;
            }

            Assert.That(firstOut, Is.EqualTo(BallZone.Goal));
            Assert.That(contacts.Exists(c => c.Type == BallContactType.Net), Is.True);
            Assert.That(s.Position.x, Is.LessThanOrEqualTo(PitchDimensions.HalfLength + PitchDimensions.GoalDepth));
        }

        [Test]
        public void BallRollingIntoBody_DeflectsAndDoesNotPassThrough()
        {
            var body = new TestBody(new Vector3(5f, 0f, 0f));
            BallState s = Rolling(Vector3.zero, Vector3.right * 10f);
            var contacts = new List<BallContact>();
            for (float t = 0f; t < 1f; t += Dt)
                BallSimulator.Advance(ref s, p, Dt, new IBallObstacle[] { body }, contacts);

            Assert.That(contacts.Exists(c => c.Type == BallContactType.Body), Is.True);
            Assert.That(s.Position.x, Is.LessThan(5f));
        }

        [Test]
        public void Prediction_MatchesStepping()
        {
            BallState start = Airborne(new Vector3(0f, 0.5f, 0f), new Vector3(18f, 6f, 2f), new Vector3(0f, 20f, 10f));
            BallState stepped = start;
            for (int i = 0; i < 100; i++)
                BallSimulator.Advance(ref stepped, p, Dt, null, null);
            BallState predicted = BallSimulator.Predict(start, p, 100 * Dt);

            Assert.That(Vector3.Distance(stepped.Position, predicted.Position), Is.LessThan(0.05f));
        }

        float Carry(BallState s)
        {
            for (float t = 0f; t < 10f; t += Dt)
            {
                BallSimulator.Advance(ref s, p, Dt, null, null);
                if (s.Position.y <= p.radius + 1e-3f && s.Velocity.y >= 0f && t > 0.1f)
                    return s.Position.x;
            }
            return s.Position.x;
        }

        static Vector3 PlayerlessFlat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        sealed class TestBody : IBallObstacle
        {
            readonly Vector3 position;
            public TestBody(Vector3 position) => this.position = position;
            public Vector3 CapsuleBottom => position + Vector3.up * 0.3f;
            public Vector3 CapsuleTop => position + Vector3.up * 1.5f;
            public float CapsuleRadius => 0.28f;
            public Vector3 Velocity => Vector3.zero;
            public float Restitution => 0.3f;
            public bool BlocksBall => true;
        }
    }
}
