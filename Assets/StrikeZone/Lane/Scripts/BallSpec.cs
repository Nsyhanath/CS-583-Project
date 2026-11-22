using UnityEngine;

namespace StrikeZone
{
    /// <summary>
    /// One bowling ball choice. The design's Force / Speed / Control stats all come from here.
    /// </summary>
    [CreateAssetMenu(menuName = "Strike Zone/Ball Spec")]
    public class BallSpec : ScriptableObject
    {
        public string displayName = "12 lb";
        [Tooltip("Force: heavier balls get deflected less by the pins.")]
        public float massKg = 5.4f;
        [Tooltip("Speed: the slowest and fastest throw this ball allows, m/s.")]
        public Vector2 speedRange = new Vector2(6f, 8.5f);
        [Tooltip("Control: multiplies steering strength. Meters can read it to size the sweet spot.")]
        public float control = 1f;
        public float hookStrength = 1f;
        [Tooltip("Optional. Replaces the ball's look when this ball is chosen.")]
        public Material material;
    }
}
