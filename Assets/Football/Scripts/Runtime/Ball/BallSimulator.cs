using System.Collections.Generic;
using Football.Core;
using UnityEngine;

namespace Football.Ball
{
    /// <summary>
    /// Deterministic ball physics: gravity, quadratic air drag, Magnus lift, spin decay, bounce with
    /// friction-spin coupling, sliding-to-rolling transition, rolling resistance, goal frame and nets,
    /// and moving capsule bodies. Pure C# so the same code drives the game and the AI's predictions.
    /// </summary>
    public sealed class BallSimulator
    {
        public const float Gravity = 9.81f;

        /// <summary>Slip slower than this at the contact patch counts as pure rolling (m/s).</summary>
        const float RollingSlipTolerance = 0.01f;
        /// <summary>A rolling ball slower than this is considered stopped (m/s).</summary>
        const float StopSpeed = 0.02f;
        /// <summary>Contacts slower than this still resolve but are not reported as events (m/s).</summary>
        const float ReportedImpactSpeed = 0.3f;
        /// <summary>For a thin shell (I = 2/3 m r²) slip speed changes 2.5× faster than linear speed under friction.</summary>
        const float SlipRateFactor = 2.5f;

        static readonly GoalSegment[] GoalFrame = BuildGoalFrame();
        static readonly NetPanel[] Nets = BuildNets();

        public BallPhysicsParams Params { get; }
        public BallState State;

        readonly List<IBallObstacle> obstacles = new List<IBallObstacle>();
        readonly List<BallContact> stepContacts = new List<BallContact>();

        /// <summary>Contacts that happened during the most recent <see cref="Step"/>.</summary>
        public IReadOnlyList<BallContact> LastStepContacts => stepContacts;

        public BallSimulator(BallPhysicsParams parameters, BallState initialState)
        {
            Params = parameters;
            State = initialState;
        }

        public void AddObstacle(IBallObstacle obstacle)
        {
            if (!obstacles.Contains(obstacle))
                obstacles.Add(obstacle);
        }

        public void RemoveObstacle(IBallObstacle obstacle) => obstacles.Remove(obstacle);

        public void Step(float dt)
        {
            stepContacts.Clear();
            Advance(ref State, Params, dt, obstacles, stepContacts);
        }

        /// <summary>
        /// Impulsive contact from a foot, head or body: the ball leaves with exactly this velocity and spin.
        /// Any upward velocity lifts it off the ground.
        /// </summary>
        public void ApplyKick(Vector3 velocity, Vector3 angularVelocity)
        {
            State.Velocity = velocity;
            State.AngularVelocity = angularVelocity;
            if (velocity.y > 0f)
            {
                State.OnGround = false;
            }
            else if (State.OnGround)
            {
                State.Velocity.y = 0f;
            }
        }

        /// <summary>Simulates forward without bodies. Used for AI predictions and tests.</summary>
        public static BallState Predict(BallState state, BallPhysicsParams p, float duration)
        {
            Advance(ref state, p, duration, null, null);
            return state;
        }

        public static void Advance(ref BallState s, BallPhysicsParams p, float dt,
            IReadOnlyList<IBallObstacle> bodies, List<BallContact> contacts)
        {
            if (dt <= 0f)
                return;

            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / p.maxSubStep - 1e-4f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
                SubStep(ref s, p, h, bodies, contacts);
        }

        static void SubStep(ref BallState s, BallPhysicsParams p, float h,
            IReadOnlyList<IBallObstacle> bodies, List<BallContact> contacts)
        {
            Vector3 previousPosition = s.Position;

            if (s.OnGround)
                IntegrateOnGround(ref s, p, h);
            else
                IntegrateInAir(ref s, p, h);

            ResolveGroundImpact(ref s, p, contacts);
            ResolveGoalFrame(ref s, p, contacts);
            ResolveNets(ref s, p, previousPosition, contacts);

            if (bodies != null)
            {
                for (int i = 0; i < bodies.Count; i++)
                    ResolveBody(ref s, p, bodies[i], contacts);
            }
        }

        static float AirDragFactor(BallPhysicsParams p) =>
            0.5f * p.airDensity * p.CrossSectionArea / p.mass;

        /// <summary>Deceleration of a ball rolling on the grass at this speed (m/s²): grass resistance plus air drag.</summary>
        public static float RollingDeceleration(BallPhysicsParams p, float speed) =>
            p.rollingResistance * Gravity + p.grassDrag * speed + AirDragFactor(p) * p.dragCoefficient * speed * speed;

        static void IntegrateInAir(ref BallState s, BallPhysicsParams p, float h)
        {
            Vector3 v = s.Velocity;
            Vector3 w = s.AngularVelocity;
            Vector3 acceleration = new Vector3(0f, -Gravity, 0f);

            float speed = v.magnitude;
            if (speed > 1e-4f)
            {
                float k = AirDragFactor(p);
                acceleration -= k * p.dragCoefficient * speed * v;

                float spin = w.magnitude;
                if (spin > 1e-4f)
                {
                    float spinParameter = p.radius * spin / speed;
                    float lift = Mathf.Min(p.maxLiftCoefficient, p.liftPerSpinParameter * spinParameter);
                    Vector3 liftDirection = Vector3.Cross(w, v);
                    if (liftDirection.sqrMagnitude > 1e-10f)
                        acceleration += liftDirection.normalized * (k * lift * speed * speed);
                }
            }

            // Semi-implicit Euler: stable for drag and cheap enough to run 22 predictions per tick.
            v += acceleration * h;
            s.Velocity = v;
            s.Position += v * h;
            s.AngularVelocity = w * Mathf.Exp(-h / p.airSpinDecayTime);
        }

        static void IntegrateOnGround(ref BallState s, BallPhysicsParams p, float h)
        {
            Vector3 v = s.Velocity;
            v.y = 0f;
            Vector3 w = s.AngularVelocity;

            float speed = v.magnitude;
            if (speed > 1e-4f)
                v -= AirDragFactor(p) * p.dragCoefficient * speed * h * v;

            // Velocity of the contact patch relative to the grass.
            Vector3 slip = v + Vector3.Cross(w, -p.radius * Vector3.up);
            slip.y = 0f;
            float slipSpeed = slip.magnitude;
            float slipChange = SlipRateFactor * p.groundFriction * Gravity * h;

            if (slipSpeed > RollingSlipTolerance && slipChange < slipSpeed)
            {
                // Sliding: kinetic friction slows the ball and spins it up toward rolling.
                Vector3 frictionDeltaV = -slip / slipSpeed * (p.groundFriction * Gravity * h);
                v += frictionDeltaV;
                w += p.InverseInertiaFactor * Vector3.Cross(-p.radius * Vector3.up, frictionDeltaV);
            }
            else
            {
                if (slipSpeed > RollingSlipTolerance)
                    v -= slip / SlipRateFactor;

                // Pure rolling: grass resistance grows with speed.
                speed = v.magnitude;
                float deceleration = p.rollingResistance * Gravity + p.grassDrag * speed;
                float newSpeed = Mathf.Max(0f, speed - deceleration * h);
                v = newSpeed < StopSpeed ? Vector3.zero : v * (newSpeed / speed);

                float verticalSpin = w.y;
                w = Vector3.Cross(Vector3.up, v) / p.radius;
                w.y = verticalSpin;
            }

            w.y *= Mathf.Exp(-p.groundSpinDecay * h);

            s.Velocity = v;
            s.AngularVelocity = w;
            s.Position += v * h;
            s.Position.y = p.radius;
        }

        static void ResolveGroundImpact(ref BallState s, BallPhysicsParams p, List<BallContact> contacts)
        {
            if (s.OnGround || s.Position.y >= p.radius)
                return;

            s.Position.y = p.radius;
            float impactSpeed = -s.Velocity.y;
            if (impactSpeed <= 0f)
                return;

            if (impactSpeed < p.settleSpeed)
            {
                s.Velocity.y = 0f;
                s.OnGround = true;
                return;
            }

            float restitution = Mathf.Max(p.minRestitution,
                p.groundRestitution - p.restitutionSpeedFalloff * impactSpeed);

            // Friction impulse at the contact patch, capped by Coulomb's law and by reaching rolling.
            Vector3 horizontal = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            Vector3 slip = horizontal + Vector3.Cross(s.AngularVelocity, -p.radius * Vector3.up);
            slip.y = 0f;
            float slipSpeed = slip.magnitude;
            if (slipSpeed > 1e-4f)
            {
                float coulombLimit = p.groundFriction * (1f + restitution) * impactSpeed;
                float toRolling = slipSpeed / SlipRateFactor;
                Vector3 deltaV = -slip / slipSpeed * Mathf.Min(coulombLimit, toRolling);
                s.Velocity += deltaV;
                s.AngularVelocity += p.InverseInertiaFactor * Vector3.Cross(-p.radius * Vector3.up, deltaV);
            }

            s.Velocity.y = restitution * impactSpeed;
            contacts?.Add(new BallContact(BallContactType.GroundBounce,
                s.Position - p.radius * Vector3.up, impactSpeed));
        }

        static void ResolveGoalFrame(ref BallState s, BallPhysicsParams p, List<BallContact> contacts)
        {
            // Cheap reject: the frame is only near the goal lines.
            if (Mathf.Abs(s.Position.x) < PitchDimensions.HalfLength - 1f)
                return;

            for (int i = 0; i < GoalFrame.Length; i++)
            {
                GoalSegment segment = GoalFrame[i];
                if (ResolveCapsule(ref s, segment.A, segment.B, PitchDimensions.PostRadius, Vector3.zero,
                        p.radius, p.postRestitution, p.postFriction, out Vector3 point, out float impact))
                {
                    if (s.OnGround && s.Velocity.y > 0.01f)
                        s.OnGround = false;
                    if (impact > ReportedImpactSpeed)
                        contacts?.Add(new BallContact(segment.Type, point, impact));
                }
            }
        }

        static void ResolveNets(ref BallState s, BallPhysicsParams p, Vector3 previousPosition,
            List<BallContact> contacts)
        {
            if (Mathf.Abs(s.Position.x) < PitchDimensions.HalfLength - p.radius)
                return;

            for (int i = 0; i < Nets.Length; i++)
            {
                NetPanel net = Nets[i];
                int axis = net.Axis;
                float offset = s.Position[axis] - net.Plane;
                if (Mathf.Abs(offset) >= p.radius || !net.Contains(s.Position))
                    continue;

                float previousOffset = previousPosition[axis] - net.Plane;
                float sideSign = previousOffset != 0f ? Mathf.Sign(previousOffset) : Mathf.Sign(offset);
                if (sideSign == 0f)
                    sideSign = 1f;

                Vector3 position = s.Position;
                position[axis] = net.Plane + sideSign * p.radius;
                s.Position = position;

                Vector3 velocity = s.Velocity;
                float normalSpeed = velocity[axis] * sideSign;
                if (normalSpeed < 0f)
                {
                    float impact = -normalSpeed;
                    Vector3 tangential = velocity;
                    tangential[axis] = 0f;
                    tangential *= 1f - p.netFriction;
                    tangential[axis] = impact * (1f - p.netAbsorption) * sideSign;
                    s.Velocity = tangential;
                    s.AngularVelocity *= 1f - p.netFriction;
                    if (axis != 1 && s.OnGround && s.Velocity.y > 0.01f)
                        s.OnGround = false;
                    if (impact > ReportedImpactSpeed)
                        contacts?.Add(new BallContact(BallContactType.Net, s.Position, impact));
                }
            }
        }

        static void ResolveBody(ref BallState s, BallPhysicsParams p, IBallObstacle body, List<BallContact> contacts)
        {
            if (body == null || !body.BlocksBall)
                return;

            if (ResolveCapsule(ref s, body.CapsuleBottom, body.CapsuleTop, body.CapsuleRadius, body.Velocity,
                    p.radius, body.Restitution, 0.3f, out Vector3 point, out float impact))
            {
                if (s.OnGround && s.Velocity.y > 0.05f)
                    s.OnGround = false;
                if (impact > ReportedImpactSpeed)
                    contacts?.Add(new BallContact(BallContactType.Body, point, impact, body));
            }
        }

        /// <summary>Sphere vs moving capsule. Pushes the ball out and reflects its relative velocity.</summary>
        static bool ResolveCapsule(ref BallState s, Vector3 a, Vector3 b, float capsuleRadius, Vector3 capsuleVelocity,
            float ballRadius, float restitution, float friction, out Vector3 contactPoint, out float impactSpeed)
        {
            contactPoint = default;
            impactSpeed = 0f;

            Vector3 closest = ClosestPointOnSegment(a, b, s.Position);
            Vector3 offset = s.Position - closest;
            float minDistance = capsuleRadius + ballRadius;
            float distanceSq = offset.sqrMagnitude;
            if (distanceSq >= minDistance * minDistance || distanceSq < 1e-12f)
                return false;

            float distance = Mathf.Sqrt(distanceSq);
            Vector3 normal = offset / distance;
            s.Position = closest + normal * minDistance;
            contactPoint = closest + normal * capsuleRadius;

            Vector3 relative = s.Velocity - capsuleVelocity;
            float normalSpeed = Vector3.Dot(relative, normal);
            if (normalSpeed >= 0f)
                return true;

            impactSpeed = -normalSpeed;
            Vector3 tangential = relative - normalSpeed * normal;
            s.Velocity = capsuleVelocity + tangential * (1f - friction) - restitution * normalSpeed * normal;
            return true;
        }

        public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-12f)
                return a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return a + ab * t;
        }

        readonly struct GoalSegment
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly BallContactType Type;

            public GoalSegment(Vector3 a, Vector3 b, BallContactType type)
            {
                A = a;
                B = b;
                Type = type;
            }
        }

        /// <summary>Axis-aligned rectangular net panel. Axis is the plane normal: 0 = X, 1 = Y, 2 = Z.</summary>
        readonly struct NetPanel
        {
            public readonly int Axis;
            public readonly float Plane;
            public readonly Vector3 Min;
            public readonly Vector3 Max;

            public NetPanel(int axis, float plane, Vector3 min, Vector3 max)
            {
                Axis = axis;
                Plane = plane;
                Min = min;
                Max = max;
            }

            public bool Contains(Vector3 point)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i == Axis)
                        continue;
                    if (point[i] < Min[i] || point[i] > Max[i])
                        return false;
                }
                return true;
            }
        }

        static GoalSegment[] BuildGoalFrame()
        {
            var segments = new List<GoalSegment>();
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 left = PitchDimensions.PostBase(side, -1);
                Vector3 right = PitchDimensions.PostBase(side, 1);
                Vector3 postTop = Vector3.up * PitchDimensions.PostTopHeight;
                Vector3 barHeight = Vector3.up * PitchDimensions.CrossbarCentreHeight;
                segments.Add(new GoalSegment(left, left + postTop, BallContactType.Post));
                segments.Add(new GoalSegment(right, right + postTop, BallContactType.Post));
                segments.Add(new GoalSegment(left + barHeight, right + barHeight, BallContactType.Crossbar));
            }
            return segments.ToArray();
        }

        static NetPanel[] BuildNets()
        {
            var panels = new List<NetPanel>();
            float halfWidth = PitchDimensions.GoalWidth * 0.5f + PitchDimensions.LineWidth;
            float height = PitchDimensions.GoalHeight + PitchDimensions.LineWidth;
            float front = PitchDimensions.HalfLength;
            float back = PitchDimensions.HalfLength + PitchDimensions.GoalDepth;

            foreach (int side in new[] { -1, 1 })
            {
                float xMin = side > 0 ? front : -back;
                float xMax = side > 0 ? back : -front;

                panels.Add(new NetPanel(0, side * back,
                    new Vector3(0f, 0f, -halfWidth), new Vector3(0f, height, halfWidth)));
                panels.Add(new NetPanel(2, halfWidth,
                    new Vector3(xMin, 0f, 0f), new Vector3(xMax, height, 0f)));
                panels.Add(new NetPanel(2, -halfWidth,
                    new Vector3(xMin, 0f, 0f), new Vector3(xMax, height, 0f)));
                panels.Add(new NetPanel(1, height,
                    new Vector3(xMin, 0f, -halfWidth), new Vector3(xMax, 0f, halfWidth)));
            }
            return panels.ToArray();
        }
    }
}
