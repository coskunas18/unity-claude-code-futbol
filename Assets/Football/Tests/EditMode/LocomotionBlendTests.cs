using Football.Players;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests
{
    public class LocomotionBlendTests
    {
        static readonly LocomotionSpeedLimits Limits = new LocomotionSpeedLimits(5f, 2f, 4f, 3f);

        [Test]
        public void LimitFor_MatchesEachAxis()
        {
            Assert.That(Limits.LimitFor(Vector2.up), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(Limits.LimitFor(Vector2.down), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(Limits.LimitFor(Vector2.left), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(Limits.LimitFor(Vector2.right), Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void LimitFor_DiagonalLiesBetweenItsAxes()
        {
            float diagonal = Limits.LimitFor(new Vector2(1f, 1f));
            Assert.That(diagonal, Is.InRange(3f, 5f));
        }

        [Test]
        public void WithinTheClipRange_BlendEqualsVelocityAtNormalRate()
        {
            LocomotionBlend.Compute(new Vector2(0f, 3f), Limits, 0.5f, 2f, out Vector2 blend, out float rate);
            Assert.That(blend, Is.EqualTo(new Vector2(0f, 3f)));
            Assert.That(rate, Is.EqualTo(1f));
        }

        [Test]
        public void BeyondTheClipRange_BlendIsHeldAndPlaybackSpeedsUp()
        {
            LocomotionBlend.Compute(new Vector2(0f, 8f), Limits, 0.5f, 2f, out Vector2 blend, out float rate);
            Assert.That(blend.y, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(rate, Is.EqualTo(1.6f).Within(1e-4f));
        }

        [Test]
        public void PlaybackRate_IsClamped()
        {
            LocomotionBlend.Compute(new Vector2(0f, -10f), Limits, 0.5f, 2f, out _, out float rate);
            Assert.That(rate, Is.EqualTo(2f));
        }

        [Test]
        public void ToLocal_UsesBodyFacing()
        {
            Vector2 local = LocomotionBlend.ToLocal(new Vector3(0f, 0f, 3f), Vector3.right);
            Assert.That(local.x, Is.EqualTo(-3f).Within(1e-4f), "moving +Z while facing +X is moving to the body's left");
            Assert.That(local.y, Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
