namespace Football.Players
{
    /// <summary>Anything that can drive a player: human input now, PlayerDecisionSystem from phase 2.</summary>
    public interface IMotorCommandSource
    {
        MotorCommand GetCommand(PlayerMovementController player);
    }
}
