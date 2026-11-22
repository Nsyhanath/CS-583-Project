using System;
using UnityEngine;

namespace StrikeZone
{
    /// <summary>
    /// The one script teammates talk to (spec.md, section 6).
    /// Release meter: PlaceBall(), Throw(), Steer(). Game flow / scoreboard: ThrowResolved, RackAll(), ClearFallen().
    /// </summary>
    public class BowlingLane : MonoBehaviour
    {
        public LaneConfig config;
        public BowlingBall ball;
        public PinSetter pinSetter;
        [Tooltip("Ball used when the scene starts.")]
        public BallSpec startingBall;

        public LaneState State { get; private set; } = LaneState.Ready;
        public int Board { get; private set; }
        public int StandingCount => pinSetter.StandingCount;
        public bool[] StandingMask => pinSetter.StandingMask();
        /// <summary>Time.time of the first ball-to-pin contact this throw, or -1.</summary>
        public float FirstContactTime { get; private set; } = -1f;
        public ThrowResult LastResult { get; private set; }

        public BallSpec CurrentBall
        {
            get => ball.Spec;
            set => ball.ApplySpec(value);
        }

        public event Action BallReleased;
        public event Action BallEnteredGutter;
        public event Action BallHitPins;
        public event Action<ThrowResult> ThrowResolved;

        float releaseTime;
        float ballSlowSince;
        float pinsStillSince;
        int standingBefore;
        bool gutter;
        float impactSpeed;
        float entryAngle;

        [Tooltip("Physics step in seconds. 0.01 = 100 steps per second, so a fast ball can't skip through a pin.")]
        public float physicsStep = 0.01f;

        void Awake()
        {
            Board = config.startBoard;
            State = LaneState.Ready;
            FirstContactTime = -1f;
            // Bowling needs small physics steps. Enforced here so it works even if project settings drift.
            if (Time.fixedDeltaTime > physicsStep) Time.fixedDeltaTime = physicsStep;
        }

        void OnEnable()
        {
            ball.EnteredGutter += OnGutter;
            ball.HitPin += OnHitPin;
        }

        void OnDisable()
        {
            if (ball == null) return;
            ball.EnteredGutter -= OnGutter;
            ball.HitPin -= OnHitPin;
        }

        void Start()
        {
            if (startingBall != null) CurrentBall = startingBall;
            ResetBall();
        }

        // ---------- Before the throw ----------

        /// <summary>Move the ball along the foul line. Board 1 = far right, 39 = far left.</summary>
        public void PlaceBall(int board)
        {
            if (State != LaneState.Ready) return;
            Board = Mathf.Clamp(board, 1, config.boardCount);
            ResetBall();
        }

        /// <summary>Roll the ball. Ignored unless State is Ready.</summary>
        public bool Throw(ThrowParams p)
        {
            if (State != LaneState.Ready) return false;

            standingBefore = pinSetter.StandingCount;
            gutter = false;
            impactSpeed = 0f;
            entryAngle = 0f;
            FirstContactTime = -1f;
            releaseTime = Time.time;
            ballSlowSince = -1f;
            pinsStillSince = -1f;

            ball.Launch(p);
            State = LaneState.Rolling;
            BallReleased?.Invoke();
            return true;
        }

        // ---------- During the roll ----------

        /// <summary>-1 (left) .. +1 (right). Only works in the first part of the lane.</summary>
        public void Steer(float input)
        {
            if (State == LaneState.Rolling) ball.Steer(input);
        }

        // ---------- Pinsetter ----------

        public void RackAll()
        {
            pinSetter.RackAll();
            ResetBall();
            State = LaneState.Ready;
        }

        public void ClearFallen()
        {
            pinSetter.ClearFallen();
            ResetBall();
            State = LaneState.Ready;
        }

        void ResetBall()
        {
            ball.Hold(new Vector3(config.BoardToX(Board), config.ballRadius + 0.001f, config.ballStartZ));
        }

        // ---------- Resolving ----------

        void OnGutter()
        {
            gutter = true;
            BallEnteredGutter?.Invoke();
        }

        void OnHitPin(Pin pin)
        {
            if (State != LaneState.Rolling || FirstContactTime >= 0f) return;
            FirstContactTime = Time.time;
            var v = ball.LastVelocity;
            impactSpeed = v.magnitude;
            entryAngle = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            BallHitPins?.Invoke();
        }

        void FixedUpdate()
        {
            if (State != LaneState.Rolling) return;
            float now = Time.time;

            bool ballSlow = ball.Body.linearVelocity.magnitude < config.ballStopSpeed;
            if (!ballSlow) ballSlowSince = -1f;
            else if (ballSlowSince < 0f) ballSlowSince = now;
            bool ballDone = ball.InPit || ball.transform.position.y < -0.3f ||
                            (ballSlowSince >= 0f && now - ballSlowSince >= config.ballStopSeconds);

            bool pinsStill = pinSetter.AllSettled();
            if (!pinsStill) pinsStillSince = -1f;
            else if (pinsStillSince < 0f) pinsStillSince = now;
            bool pinsDone = pinsStillSince >= 0f && now - pinsStillSince >= config.pinSettleSeconds;

            bool timedOut = (FirstContactTime >= 0f && now - FirstContactTime >= config.timeoutAfterContact) ||
                            now - releaseTime >= config.timeoutAfterRelease;

            if ((ballDone && pinsDone) || timedOut) Resolve();
        }

        void Resolve()
        {
            var mask = pinSetter.StandingMask();
            int standing = 0;
            foreach (var s in mask) if (s) standing++;

            LastResult = new ThrowResult
            {
                knockedThisThrow = Mathf.Max(0, standingBefore - standing),
                standingAfter = standing,
                standingMask = mask,
                gutter = gutter,
                impactSpeed = impactSpeed,
                entryAngleDeg = entryAngle,
            };
            State = LaneState.Resolving;
            ThrowResolved?.Invoke(LastResult);
        }
    }
}
