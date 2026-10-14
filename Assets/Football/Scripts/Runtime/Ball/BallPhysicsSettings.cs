using UnityEngine;

namespace Football.Ball
{
    /// <summary>Asset wrapper so ball physics can be tuned in the Inspector, including during Play mode.</summary>
    [CreateAssetMenu(menuName = "Football/Ball Physics Settings", fileName = "BallPhysicsSettings")]
    public sealed class BallPhysicsSettings : ScriptableObject
    {
        public BallPhysicsParams parameters = new BallPhysicsParams();
    }
}
