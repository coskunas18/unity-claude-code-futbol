using UnityEngine;

namespace Football.Players
{
    /// <summary>Asset wrapper so movement can be tuned in the Inspector.</summary>
    [CreateAssetMenu(menuName = "Football/Movement Profile", fileName = "MovementProfile")]
    public sealed class MovementProfile : ScriptableObject
    {
        public MovementParams parameters = new MovementParams();
    }
}
