using UnityEngine;
using UnityEngine.UI;

namespace StrikeZone
{
    /// <summary>
    /// Rolls a ball down the middle lane of the background image, over and over.
    /// The perspective numbers match the way StrikeZoneBackground.png was drawn
    /// (a 1920x1080 layout), so the ball stays on the lane at any screen size.
    /// </summary>
    public class RollingBall : MonoBehaviour
    {
        [Tooltip("The full-screen background image the ball rolls on.")]
        public RectTransform background;
        public Graphic ball;
        public Graphic reflection;
        public Graphic shadow;

        public float rollSeconds = 3.4f;
        public float pauseSeconds = 0.9f;

        // Layout of the background image, in 1920x1080 reference pixels (y measured from the top).
        const float RefW = 1920f, RefH = 1080f;
        const float VanishY = RefH * 0.215f;
        const float DeckY = RefH * 0.335f;
        static readonly float LaneWidth = Mathf.Max(RefW * 1.05f, RefH * 0.9f);

        void Update()
        {
            if (background == null || ball == null) return;

            float cycle = rollSeconds + pauseSeconds;
            float p = (Time.time % cycle) / rollSeconds;

            if (p > 1f)
            {
                SetAlpha(ball, 0f);
                SetAlpha(reflection, 0f);
                SetAlpha(shadow, 0f);
                return;
            }

            float tDeck = (DeckY - VanishY) / (RefH - VanishY);
            float zDeck = 1f / tDeck - 1f;
            float z = Mathf.Lerp(0.35f, zDeck - 0.15f, p);
            float t = 1f / (1f + z);                         // perspective scale

            float r = LaneWidth * 0.1f * t;                   // ball radius
            float worldX = LaneWidth * (0.22f * Mathf.Pow(1f - p, 1.4f) + 0.04f * p); // gentle hook
            float x = RefW / 2f + worldX * t;
            float surfaceY = VanishY + t * (RefH - VanishY);
            float alpha = p > 0.9f ? Mathf.Clamp01((1f - p) / 0.1f) : Mathf.Clamp01(p / 0.05f);

            float scale = background.rect.width / RefW;
            Place(reflection, x, surfaceY + r, 2f * r, 2f * r, alpha * 0.3f, scale);
            Place(shadow, x, surfaceY, 1.7f * r, 0.24f * r, alpha * 0.45f, scale);
            Place(ball, x, surfaceY - r, 2f * r, 2f * r, alpha, scale);
        }

        static void Place(Graphic g, float x, float y, float w, float h, float alpha, float scale)
        {
            if (g == null) return;
            var rt = g.rectTransform;
            rt.anchoredPosition = new Vector2((x - RefW / 2f) * scale, (RefH / 2f - y) * scale);
            rt.sizeDelta = new Vector2(w * scale, h * scale);
            SetAlpha(g, alpha);
        }

        static void SetAlpha(Graphic g, float alpha)
        {
            if (g == null) return;
            var c = g.color;
            c.a = alpha;
            g.color = c;
        }
    }
}
