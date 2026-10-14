using System;
using Football.Core;
using UnityEngine;

namespace Football.Ball
{
    /// <summary>
    /// Watches the ball against the pitch boundaries and raises an event when it leaves play.
    /// Restarts (throw-in, corner, goal kick, kick-off) belong to MatchStateManager in phase 4;
    /// until then the optional auto reset puts the ball back on the centre spot for testing.
    /// Attach to the "MatchSystems" GameObject.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Rules)]
    public sealed class BallEventsMonitor : MonoBehaviour
    {
        [SerializeField] BallController ball;

        [Tooltip("Phase 1 test helper. Puts the ball on the centre spot after it leaves play.")]
        [SerializeField] bool autoResetWhenOutOfPlay = true;
        [SerializeField, Min(0f)] float resetDelay = 2f;
        [SerializeField] bool logEvents = true;

        BallZone currentZone = BallZone.InPlay;
        float resetAtTime = -1f;

        public event Action<BallZoneResult> BallLeftPlay;

        public BallZoneResult LastResult { get; private set; }
        public int Goals { get; private set; }

        void FixedUpdate()
        {
            if (ball == null)
                return;

            BallZoneResult result = PitchRules.Classify(ball.State.Position, ball.Radius);
            if (currentZone == BallZone.InPlay && result.Zone != BallZone.InPlay)
            {
                LastResult = result;
                if (result.Zone == BallZone.Goal)
                    Goals++;
                if (logEvents)
                    Debug.Log($"[Ball] Left play: {result} at {ball.State.Position:F2}", this);
                BallLeftPlay?.Invoke(result);
                if (autoResetWhenOutOfPlay)
                    resetAtTime = Time.time + resetDelay;
            }
            currentZone = result.Zone;

            if (resetAtTime >= 0f && Time.time >= resetAtTime)
            {
                resetAtTime = -1f;
                ball.PlaceAt(Vector3.zero);
                currentZone = BallZone.InPlay;
            }
        }
    }
}
