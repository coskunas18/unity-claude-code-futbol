using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// Changes where the legs run without changing what the controller asked for, e.g. running to the ball
    /// while dribbling. The controller's original intent stays in <see cref="PlayerMovementController.LastCommand"/>.
    /// </summary>
    public interface IMoveAdjuster
    {
        Vector3 AdjustMove(PlayerMotor motor, Vector3 desiredMove);
    }
}
