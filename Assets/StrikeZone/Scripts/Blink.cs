using UnityEngine;
using UnityEngine.UI;

namespace StrikeZone
{
    /// <summary>Blinks a text or image on and off, arcade style.</summary>
    [RequireComponent(typeof(Graphic))]
    public class Blink : MonoBehaviour
    {
        public float onSeconds = 0.6f;
        public float offSeconds = 0.6f;

        Graphic graphic;

        void Awake() => graphic = GetComponent<Graphic>();

        void Update()
        {
            graphic.enabled = Mathf.Repeat(Time.unscaledTime, onSeconds + offSeconds) < onSeconds;
        }
    }
}
