namespace Football.Core
{
    /// <summary>
    /// FixedUpdate order inside one physics tick: read input, move players and let them touch the ball,
    /// then step the ball, then evaluate rules on the ball's new position.
    /// Camera and visual interpolation run later, in Update/LateUpdate.
    /// </summary>
    public static class ExecutionOrder
    {
        public const int Input = -100;
        public const int Players = -50;
        public const int PlayerBallContact = -40;
        public const int Ball = 0;
        public const int Rules = 50;
    }
}
