using UnityEngine;

namespace StrikeZone
{
    /// <summary>
    /// Every lane number in one place (spec.md, sections 4 and 7). Meters, seconds, m/s.
    /// Origin = center of the foul line, lane runs along +Z, +X = bowler's right.
    /// </summary>
    [CreateAssetMenu(menuName = "Strike Zone/Lane Config")]
    public class LaneConfig : ScriptableObject
    {
        [Header("Dimensions (real-world)")]
        public float laneWidth = 1.0668f;
        public int boardCount = 39;
        public float headPinZ = 18.288f;
        public float pitEdgeZ = 19.156f;
        public float gutterWidth = 0.235f;
        public float pinSpacing = 0.3048f;
        public float rowSpacing = 0.26397f;
        public float ballRadius = 0.109f;
        public float ballStartZ = -0.3f;
        [Tooltip("Board 10 + straight + hook 0.8 lands near the 1-3 pocket.")]
        [Range(1, 39)] public int startBoard = 10;

        [Header("Hook and steering")]
        [Tooltip("Hook force starts once the ball passes this Z.")]
        public float breakpointZ = 11f;
        [Tooltip("Sideways acceleration at hook = 1, m/s².")]
        public float hookAccel = 0.6f;
        [Tooltip("Steering stops working once the ball passes this Z (about 2/3 down the lane).")]
        public float steerLimitZ = 12.2f;
        [Tooltip("Sideways acceleration at full steer, m/s².")]
        public float steerAccel = 0.8f;

        [Header("Pin down rule")]
        [Tooltip("A pin tilted more than this from upright is down.")]
        public float downTiltDegrees = 20f;

        [Header("Settling")]
        public float pinSettleLinear = 0.02f;
        public float pinSettleAngular = 0.1f;
        public float pinSettleSeconds = 0.75f;
        public float ballStopSpeed = 0.05f;
        public float ballStopSeconds = 0.5f;
        [Tooltip("Resolve anyway this long after the first pin contact.")]
        public float timeoutAfterContact = 4f;
        [Tooltip("Resolve anyway this long after release.")]
        public float timeoutAfterRelease = 8f;

        public float HalfLaneWidth => laneWidth * 0.5f;
        public float BoardWidth => laneWidth / boardCount;

        /// <summary>Board 1 is the far right edge, board 20 the center.</summary>
        public float BoardToX(int board) => HalfLaneWidth - (board - 0.5f) * BoardWidth;

        /// <summary>Home spot of pin index 0..9 (pin number = index + 1), base of the pin at y = 0.</summary>
        public Vector3 PinHome(int index)
        {
            int r = 0;
            while (index > r) { index -= r + 1; r++; }
            return new Vector3((index - r * 0.5f) * pinSpacing, 0f, headPinZ + r * rowSpacing);
        }
    }
}
