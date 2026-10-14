using System;
using UnityEngine;

namespace Football.Players
{
    /// <summary>
    /// Fastest natural speed of the locomotion clips in each body-relative direction (m/s),
    /// i.e. how fast the planted foot slides back under the body. Measured from the clips by the animator builder.
    /// </summary>
    [Serializable]
    public struct LocomotionSpeedLimits
    {
        public float forward;
        public float backward;
        public float left;
        public float right;

        public LocomotionSpeedLimits(float forward, float backward, float left, float right)
        {
            this.forward = forward;
            this.backward = backward;
            this.left = left;
            this.right = right;
        }

        /// <summary>Elliptical interpolation of the four limits for a body-relative direction.</summary>
        public float LimitFor(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-8f)
                return Mathf.Max(forward, 0.01f);
            direction.Normalize();
            float alongZ = direction.y >= 0f ? forward : backward;
            float alongX = direction.x >= 0f ? right : left;
            // Point on the ellipse with semi-axes alongX and alongZ in this direction.
            float denominator = Mathf.Sqrt(direction.x * direction.x * alongZ * alongZ + direction.y * direction.y * alongX * alongX);
            return denominator < 1e-6f ? 0.01f : alongX * alongZ / denominator;
        }
    }

    /// <summary>
    /// Maps the body-relative velocity to blend-tree parameters and an animation playback rate. Within the speed
    /// the clips cover, the blend position equals the velocity, so feet stay planted. Beyond it the position is held
    /// at the edge and the clips play faster in proportion, so stride frequency keeps up instead of the feet sliding.
    /// </summary>
    public static class LocomotionBlend
    {
        public static void Compute(Vector2 localVelocity, LocomotionSpeedLimits limits,
            float minPlaybackRate, float maxPlaybackRate, out Vector2 blendPosition, out float playbackRate)
        {
            float speed = localVelocity.magnitude;
            float limit = limits.LimitFor(localVelocity);
            if (speed <= limit)
            {
                blendPosition = localVelocity;
                playbackRate = 1f;
                return;
            }
            blendPosition = localVelocity * (limit / speed);
            playbackRate = Mathf.Clamp(speed / limit, minPlaybackRate, maxPlaybackRate);
        }

        /// <summary>Velocity expressed in the frame of a body facing <paramref name="facing"/> on the ground: x right, y forward.</summary>
        public static Vector2 ToLocal(Vector3 worldVelocity, Vector3 facing)
        {
            Vector3 forward = new Vector3(facing.x, 0f, facing.z);
            if (forward.sqrMagnitude < 1e-8f)
                forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            return new Vector2(Vector3.Dot(worldVelocity, right), Vector3.Dot(worldVelocity, forward));
        }
    }
}
