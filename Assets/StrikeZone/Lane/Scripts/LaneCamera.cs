using UnityEngine;

namespace StrikeZone
{
    /// <summary>
    /// Placeholder camera until Cinemachine (spec.md, section 9.2).
    /// Behind the ball while aiming, follows it down the lane, then moves in on the pins.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LaneCamera : MonoBehaviour
    {
        public BowlingLane lane;

        [Header("Behind (aiming)")]
        public Vector3 behindOffset = new Vector3(0.1f, 0.62f, -1.7f);

        [Header("Follow")]
        public float followHeight = 0.6f;
        public float followDistance = 1.9f;
        [Tooltip("The camera stops following once the ball passes this Z, so it can watch the pins.")]
        public float followStopZ = 13.5f;

        [Header("Pin cam")]
        public float pinCamDelay = 1.5f;
        public Vector3 pinCamPosition = new Vector3(0.45f, 0.7f, 16.4f);

        [Header("Smoothing")]
        public float sharpness = 3.5f;

        Vector3 lookPoint;

        void Start()
        {
            if (lane == null) return;
            Target(out var pos, out lookPoint);
            transform.position = pos;
            transform.LookAt(lookPoint);
        }

        void LateUpdate()
        {
            if (lane == null) return;
            Target(out var pos, out var look);
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            lookPoint = Vector3.Lerp(lookPoint, look, k);
            transform.LookAt(lookPoint);
        }

        void Target(out Vector3 pos, out Vector3 look)
        {
            var cfg = lane.config;
            var ball = lane.ball.transform.position;
            var pins = new Vector3(0f, 0.12f, cfg.headPinZ + 0.4f);

            bool pinCam = lane.State == LaneState.Resolving ||
                          (lane.FirstContactTime >= 0f && Time.time - lane.FirstContactTime >= pinCamDelay);

            if (lane.State == LaneState.Ready)
            {
                pos = new Vector3(ball.x, 0f, ball.z) + behindOffset;
                look = new Vector3(0.05f, 0.15f, cfg.headPinZ);
            }
            else if (pinCam)
            {
                pos = pinCamPosition;
                look = pins;
            }
            else
            {
                float fz = Mathf.Min(ball.z, followStopZ);
                pos = new Vector3(ball.x * 0.5f, followHeight, fz - followDistance);
                look = new Vector3(ball.x * 0.3f, 0.15f, Mathf.Min(fz + 7f, cfg.headPinZ + 0.3f));
            }
        }
    }
}
