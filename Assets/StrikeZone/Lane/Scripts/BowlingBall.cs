using System;
using UnityEngine;

namespace StrikeZone
{
    /// <summary>
    /// The ball: launch, back-end hook, steering, and gutter detection (spec.md, sections 7.1-7.3).
    /// Teammates shouldn't call this directly; go through BowlingLane.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BowlingBall : MonoBehaviour
    {
        public LaneConfig config;
        [Tooltip("The renderer that shows the ball. BallSpec.material is applied to it.")]
        public Renderer visualRenderer;

        public Rigidbody Body { get; private set; }
        public BallSpec Spec { get; private set; }
        public bool Rolling { get; private set; }
        public bool InGutter { get; private set; }
        public bool InPit { get; set; }
        /// <summary>Velocity at the start of the last physics step (before any collision in that step).</summary>
        public Vector3 LastVelocity { get; private set; }

        public event Action EnteredGutter;
        public event Action<Pin> HitPin;

        float hook;
        float steer;
        int gutterSide;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.maxAngularVelocity = 150f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void ApplySpec(BallSpec spec)
        {
            Spec = spec;
            if (spec == null) return;
            Body.mass = spec.massKg;
            if (visualRenderer != null && spec.material != null) visualRenderer.sharedMaterial = spec.material;
        }

        /// <summary>Hold the ball still at a spot on the approach.</summary>
        public void Hold(Vector3 position)
        {
            Rolling = false;
            InGutter = false;
            InPit = false;
            steer = 0f;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // kinematic-safe
            Body.isKinematic = true;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            Body.position = position;
            Body.rotation = Quaternion.identity;
        }

        public void Launch(ThrowParams p)
        {
            float speed = Spec != null ? Mathf.Clamp(p.speed, Spec.speedRange.x, Spec.speedRange.y) : p.speed;
            Vector3 dir = Quaternion.Euler(0f, p.angleDeg, 0f) * Vector3.forward;

            hook = Mathf.Clamp(p.hook, -1f, 1f);
            steer = 0f;
            InGutter = false;
            InPit = false;
            Rolling = true;

            Body.isKinematic = false;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearVelocity = dir * speed;
            // Start already rolling (no skid): spin axis is up x direction, rate = v / r.
            Body.angularVelocity = Vector3.Cross(Vector3.up, dir) * (speed / config.ballRadius);
            Body.WakeUp();
            LastVelocity = Body.linearVelocity;
        }

        public void Steer(float input) => steer = Mathf.Clamp(input, -1f, 1f);

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            LastVelocity = Body.linearVelocity;
            if (!Rolling || InPit) return;

            Vector3 pos = Body.position;

            if (!InGutter && Mathf.Abs(pos.x) > config.HalfLaneWidth && pos.z < config.headPinZ)
            {
                InGutter = true;
                gutterSide = pos.x > 0f ? 1 : -1;
                EnteredGutter?.Invoke();
            }

            if (InGutter)
            {
                // Keep the ball in the channel so it can't climb back onto the lane.
                float center = gutterSide * (config.HalfLaneWidth + config.gutterWidth * 0.5f);
                var v = Body.linearVelocity;
                v.x = (center - pos.x) * 4f;
                Body.linearVelocity = v;
                return;
            }

            float hookStrength = Spec != null ? Spec.hookStrength : 1f;
            float control = Spec != null ? Spec.control : 1f;
            float accel = 0f;
            if (pos.z > config.breakpointZ && pos.z < config.headPinZ) accel -= hook * config.hookAccel * hookStrength;
            if (pos.z < config.steerLimitZ) accel += steer * config.steerAccel * control;
            if (accel != 0f) Body.AddForce(Vector3.right * accel, ForceMode.Acceleration);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!Rolling) return;
            var pin = collision.collider.GetComponentInParent<Pin>();
            if (pin != null) HitPin?.Invoke(pin);
        }
    }
}
