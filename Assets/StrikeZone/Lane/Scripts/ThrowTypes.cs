using System;

namespace StrikeZone
{
    public enum LaneState
    {
        /// <summary>Ball is on the approach, waiting for a throw.</summary>
        Ready,
        /// <summary>Ball is rolling; pins may be falling.</summary>
        Rolling,
        /// <summary>Throw is finished and reported. Waiting for RackAll() or ClearFallen().</summary>
        Resolving,
    }

    /// <summary>What the release meter hands to BowlingLane.Throw().</summary>
    [Serializable]
    public struct ThrowParams
    {
        /// <summary>Meters per second. Clamped to the current ball's speed range.</summary>
        public float speed;
        /// <summary>Launch direction in degrees. 0 = straight down the lane, positive = to the right.</summary>
        public float angleDeg;
        /// <summary>-1..1. Back-end curve. Positive curves left (a right-hander's hook).</summary>
        public float hook;
    }

    /// <summary>Reported once per throw by BowlingLane.ThrowResolved.</summary>
    public struct ThrowResult
    {
        public int knockedThisThrow;
        public int standingAfter;
        /// <summary>Index 0 = pin 1 ... index 9 = pin 10. True = still standing.</summary>
        public bool[] standingMask;
        public bool gutter;
        /// <summary>Ball speed (m/s) at first pin contact, 0 if it never touched a pin.</summary>
        public float impactSpeed;
        /// <summary>Ball direction at first pin contact, degrees. Negative = moving left.</summary>
        public float entryAngleDeg;
    }
}
