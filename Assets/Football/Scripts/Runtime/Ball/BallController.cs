using System;
using Football.Core;
using UnityEngine;

namespace Football.Ball
{
    /// <summary>
    /// Scene binding for <see cref="BallSimulator"/>. Steps the simulation on the fixed tick and
    /// interpolates the transform for rendering. The transform is output only: move the ball with
    /// <see cref="Kick"/> or <see cref="PlaceAt"/>, never by setting its position.
    /// Attach to the "Ball" GameObject.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Ball)]
    public sealed class BallController : MonoBehaviour
    {
        [Tooltip("Shared physics settings. Edits apply live in Play mode. Defaults are used when empty.")]
        [SerializeField] BallPhysicsSettings settings;

        BallSimulator simulator;
        Vector3 previousPosition;
        Vector3 currentPosition;
        Quaternion spinRotation = Quaternion.identity;

        /// <summary>Raised for every reported contact (ground bounce, post, net, body).</summary>
        public event Action<BallContact> Contacted;

        public BallState State => Simulator.State;
        public BallPhysicsParams Params => Simulator.Params;
        public float Radius => Simulator.Params.radius;

        public BallSimulator Simulator
        {
            get
            {
                if (simulator == null)
                    CreateSimulator();
                return simulator;
            }
        }

        void Awake()
        {
            if (simulator == null)
                CreateSimulator();
        }

        void CreateSimulator()
        {
            BallPhysicsParams parameters = settings != null ? settings.parameters : new BallPhysicsParams();
            simulator = new BallSimulator(parameters, BallState.AtRest(transform.position, parameters.radius));
            previousPosition = currentPosition = simulator.State.Position;
            transform.position = currentPosition;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            previousPosition = Simulator.State.Position;
            simulator.Step(dt);
            currentPosition = simulator.State.Position;

            Vector3 spin = simulator.State.AngularVelocity;
            float angle = spin.magnitude * dt * Mathf.Rad2Deg;
            if (angle > 1e-4f)
                spinRotation = Quaternion.AngleAxis(angle, spin.normalized) * spinRotation;

            var contacts = simulator.LastStepContacts;
            for (int i = 0; i < contacts.Count; i++)
                Contacted?.Invoke(contacts[i]);
        }

        void Update()
        {
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(previousPosition, currentPosition, alpha), spinRotation);
        }

        /// <summary>The ball leaves with this velocity (m/s) and spin (rad/s) from its current position.</summary>
        public void Kick(Vector3 velocity, Vector3 angularVelocity) =>
            Simulator.ApplyKick(velocity, angularVelocity);

        /// <summary>Puts the ball at rest on the grass at this point, e.g. for a restart.</summary>
        public void PlaceAt(Vector3 groundPoint)
        {
            Simulator.State = BallState.AtRest(groundPoint, Radius);
            previousPosition = currentPosition = simulator.State.Position;
            transform.position = currentPosition;
        }

        public void RegisterObstacle(IBallObstacle obstacle) => Simulator.AddObstacle(obstacle);
        public void UnregisterObstacle(IBallObstacle obstacle) => simulator?.RemoveObstacle(obstacle);

        void OnDrawGizmosSelected()
        {
            if (simulator == null)
                return;
            Gizmos.color = Color.yellow;
            BallState predicted = simulator.State;
            Vector3 last = predicted.Position;
            for (int i = 0; i < 40; i++)
            {
                predicted = BallSimulator.Predict(predicted, simulator.Params, 0.05f);
                Gizmos.DrawLine(last, predicted.Position);
                last = predicted.Position;
            }
        }
    }
}
