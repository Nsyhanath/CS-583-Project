using UnityEngine;

namespace StrikeZone
{
    /// <summary>Gently bobs a UI element up and down with a slight tilt.</summary>
    public class TitleBob : MonoBehaviour
    {
        public float amplitude = 10f;
        public float period = 3.2f;
        public float tiltDegrees = -1.5f;

        RectTransform rt;
        Vector2 basePosition;

        void Start()
        {
            rt = (RectTransform)transform;
            basePosition = rt.anchoredPosition;
        }

        void Update()
        {
            float k = (Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / period - Mathf.PI / 2f) + 1f) * 0.5f;
            rt.anchoredPosition = basePosition + Vector2.up * amplitude * k;
            rt.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        }
    }
}
