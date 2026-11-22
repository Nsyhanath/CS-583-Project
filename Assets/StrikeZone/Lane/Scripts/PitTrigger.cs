using UnityEngine;

namespace StrikeZone
{
    /// <summary>Anything that falls in the pit is out of play: pins count as down, the ball is done.</summary>
    [RequireComponent(typeof(Collider))]
    public class PitTrigger : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var pin = other.GetComponentInParent<Pin>();
            if (pin != null) pin.InPit = true;

            var ball = other.GetComponentInParent<BowlingBall>();
            if (ball != null) ball.InPit = true;
        }
    }
}
