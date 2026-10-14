using Football.Players;
using UnityEngine;

namespace Football.Debugging
{
    /// <summary>
    /// Command source driven by fields instead of a device, for automated Play mode checks and replays.
    /// Assign it to <see cref="PlayerMovementController.CommandSource"/> and set the fields from code.
    /// </summary>
    public sealed class ScriptedCommandSource : MonoBehaviour, IMotorCommandSource
    {
        public Vector3 move;
        public bool sprint;
        public Vector3 face;

        public MotorCommand GetCommand(PlayerMovementController player) =>
            new MotorCommand { Move = move, Sprint = sprint, Face = face };
    }
}
