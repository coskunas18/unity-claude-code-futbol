using UnityEngine;

namespace Football.Core
{
    /// <summary>
    /// Laws of the Game pitch and goal measurements, in metres.
    /// World axes: pitch length runs along X, width along Z, Y is up. Centre spot is the origin.
    /// Boundary values describe the OUTER edge of the line, because lines belong to the area they bound.
    /// </summary>
    public static class PitchDimensions
    {
        public const float Length = 105f;
        public const float Width = 68f;
        public const float HalfLength = Length * 0.5f;
        public const float HalfWidth = Width * 0.5f;

        public const float LineWidth = 0.12f;
        public const float CentreCircleRadius = 9.15f;
        public const float CentreSpotRadius = 0.15f;

        public const float PenaltyAreaDepth = 16.5f;
        public const float PenaltyAreaWidth = 40.32f;
        public const float GoalAreaDepth = 5.5f;
        public const float GoalAreaWidth = 18.32f;
        public const float PenaltySpotDistance = 11f;
        public const float PenaltyArcRadius = 9.15f;
        public const float CornerArcRadius = 1f;

        /// <summary>Distance between the inner edges of the posts.</summary>
        public const float GoalWidth = 7.32f;
        /// <summary>Distance from the ground to the lower edge of the crossbar.</summary>
        public const float GoalHeight = 2.44f;
        public const float GoalDepth = 2f;
        public const float PostRadius = LineWidth * 0.5f;

        /// <summary>Grass outside the lines, for players and the ball to run into.</summary>
        public const float RunOff = 6f;

        /// <summary>X of a goal line's outer edge. side is +1 (east goal) or -1 (west goal).</summary>
        public static float GoalLineX(int side) => side * HalfLength;

        /// <summary>Centre of the left/right post for a goal. lateral is +1 or -1 along Z.</summary>
        public static Vector3 PostBase(int side, int lateral) =>
            new Vector3(side * (HalfLength - PostRadius), 0f, lateral * (GoalWidth * 0.5f + PostRadius));

        public static float CrossbarCentreHeight => GoalHeight + PostRadius;
        public static float PostTopHeight => GoalHeight + 2f * PostRadius;
    }
}
