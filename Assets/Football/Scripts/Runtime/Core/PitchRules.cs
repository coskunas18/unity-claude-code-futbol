using UnityEngine;

namespace Football.Core
{
    public enum BallZone
    {
        InPlay,
        /// <summary>Ball wholly crossed a touchline (throw-in).</summary>
        OverTouchline,
        /// <summary>Ball wholly crossed a goal line outside the goal (goal kick or corner).</summary>
        OverGoalLine,
        /// <summary>Ball wholly crossed the goal line between the posts and under the crossbar.</summary>
        Goal
    }

    public readonly struct BallZoneResult
    {
        public readonly BallZone Zone;
        /// <summary>For goal-line results: +1 east goal line, -1 west. For touchlines: +1 north (+Z), -1 south.</summary>
        public readonly int Side;

        public BallZoneResult(BallZone zone, int side)
        {
            Zone = zone;
            Side = side;
        }

        public override string ToString() => Zone == BallZone.InPlay ? "InPlay" : $"{Zone}({Side:+0;-0})";
    }

    /// <summary>Pure rule checks on ball position. No state, no Unity objects.</summary>
    public static class PitchRules
    {
        /// <summary>
        /// Law 9: the ball is out of play only when it has WHOLLY crossed the goal line or touchline,
        /// on the ground or in the air. Goal line crossings are checked first: a ball can cross both at a corner.
        /// </summary>
        public static BallZoneResult Classify(Vector3 ballCentre, float ballRadius)
        {
            float overGoalLine = Mathf.Abs(ballCentre.x) - (PitchDimensions.HalfLength + ballRadius);
            if (overGoalLine > 0f)
            {
                int side = ballCentre.x > 0f ? 1 : -1;
                bool betweenPosts = Mathf.Abs(ballCentre.z) < PitchDimensions.GoalWidth * 0.5f;
                bool underBar = ballCentre.y < PitchDimensions.GoalHeight;
                return new BallZoneResult(betweenPosts && underBar ? BallZone.Goal : BallZone.OverGoalLine, side);
            }

            if (Mathf.Abs(ballCentre.z) > PitchDimensions.HalfWidth + ballRadius)
                return new BallZoneResult(BallZone.OverTouchline, ballCentre.z > 0f ? 1 : -1);

            return new BallZoneResult(BallZone.InPlay, 0);
        }
    }
}
