using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests
{
    public class PitchRulesTests
    {
        const float R = 0.11f;
        const float L = PitchDimensions.HalfLength;
        const float W = PitchDimensions.HalfWidth;

        [Test]
        public void BallOnTheTouchline_IsInPlay() =>
            Assert.That(PitchRules.Classify(new Vector3(0f, R, W), R).Zone, Is.EqualTo(BallZone.InPlay));

        [Test]
        public void BallPartlyOverTheTouchline_IsInPlay() =>
            Assert.That(PitchRules.Classify(new Vector3(0f, R, W + R * 0.9f), R).Zone, Is.EqualTo(BallZone.InPlay));

        [Test]
        public void BallWhollyOverTheTouchline_IsOut()
        {
            BallZoneResult result = PitchRules.Classify(new Vector3(10f, R, -(W + R + 0.01f)), R);
            Assert.That(result.Zone, Is.EqualTo(BallZone.OverTouchline));
            Assert.That(result.Side, Is.EqualTo(-1));
        }

        [Test]
        public void BallInTheAirOverTheTouchline_IsOut() =>
            Assert.That(PitchRules.Classify(new Vector3(0f, 5f, W + 1f), R).Zone, Is.EqualTo(BallZone.OverTouchline));

        [Test]
        public void BallWhollyOverTheGoalLineUnderTheBar_IsAGoal()
        {
            BallZoneResult result = PitchRules.Classify(new Vector3(L + R + 0.01f, 1f, 2f), R);
            Assert.That(result.Zone, Is.EqualTo(BallZone.Goal));
            Assert.That(result.Side, Is.EqualTo(1));
        }

        [Test]
        public void BallOnTheGoalLine_IsNotAGoal() =>
            Assert.That(PitchRules.Classify(new Vector3(L + R * 0.5f, 1f, 0f), R).Zone, Is.EqualTo(BallZone.InPlay));

        [Test]
        public void BallOverTheGoalLineWide_IsGoalLineOut() =>
            Assert.That(PitchRules.Classify(new Vector3(-(L + 1f), R, 6f), R).Zone, Is.EqualTo(BallZone.OverGoalLine));

        [Test]
        public void BallOverTheBar_IsGoalLineOut() =>
            Assert.That(PitchRules.Classify(new Vector3(L + 1f, PitchDimensions.GoalHeight + 0.5f, 0f), R).Zone,
                Is.EqualTo(BallZone.OverGoalLine));
    }
}
