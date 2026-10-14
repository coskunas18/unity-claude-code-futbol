using Football.Ball;
using Football.Core;
using UnityEngine;

namespace Football.CameraRig
{
    /// <summary>
    /// TV-style side camera: sits beyond the south touchline, pans along the pitch and tilts toward the play.
    /// The focus blends ball and controlled player and is smoothed so quick touches do not shake the view.
    /// Attach to the Main Camera.
    /// </summary>
    public sealed class BroadcastCamera : MonoBehaviour
    {
        [SerializeField] BallController ball;
        [SerializeField] Transform player;
        [Tooltip("0 = follow the player only, 1 = follow the ball only")]
        [SerializeField, Range(0f, 1f)] float ballWeight = 0.7f;

        [Header("Placement")]
        [SerializeField] float height = 17f;
        [Tooltip("Camera Z when play is on the halfway line. Negative is beyond the south touchline.")]
        [SerializeField] float baseZ = -52f;
        [Tooltip("How far the camera slides along the touchline relative to the focus (0–1)")]
        [SerializeField, Range(0f, 1f)] float panFollow = 0.8f;
        [Tooltip("How far the camera moves toward the far touchline when play goes there (0–1)")]
        [SerializeField, Range(0f, 1f)] float depthFollow = 0.25f;
        [SerializeField, Range(10f, 70f)] float fieldOfView = 32f;

        [Header("Smoothing")]
        [SerializeField, Min(0.01f)] float smoothTime = 0.45f;
        [SerializeField] float maxFocusSpeed = 40f;

        Vector3 focus;
        Vector3 focusVelocity;
        Camera attachedCamera;

        void Start()
        {
            attachedCamera = GetComponent<Camera>();
            focus = TargetFocus();
            Apply();
        }

        void LateUpdate()
        {
            focus = Vector3.SmoothDamp(focus, TargetFocus(), ref focusVelocity, smoothTime, maxFocusSpeed, Time.deltaTime);
            Apply();
        }

        Vector3 TargetFocus()
        {
            Vector3 ballPosition = ball != null ? ball.transform.position : Vector3.zero;
            Vector3 playerPosition = player != null ? player.position : ballPosition;
            Vector3 target = Vector3.Lerp(playerPosition, ballPosition, ballWeight);
            target.x = Mathf.Clamp(target.x, -PitchDimensions.HalfLength - 2f, PitchDimensions.HalfLength + 2f);
            target.z = Mathf.Clamp(target.z, -PitchDimensions.HalfWidth, PitchDimensions.HalfWidth);
            target.y = 0f;
            return target;
        }

        void Apply()
        {
            if (attachedCamera != null)
                attachedCamera.fieldOfView = fieldOfView;
            var position = new Vector3(focus.x * panFollow, height, baseZ + focus.z * depthFollow);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
        }
    }
}
