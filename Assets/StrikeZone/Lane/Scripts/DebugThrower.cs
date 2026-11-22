using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StrikeZone
{
    /// <summary>
    /// Keyboard test thrower until the real release meter exists (spec.md, section 9.1).
    /// Arrows: move board / change speed. Z/X: angle. Q/E: hook. A/D: steer while rolling. Space: throw.
    /// 1/2/3: ball weight. R: rack. C: clear fallen. Disable this once the meter and scoring land.
    /// </summary>
    public class DebugThrower : MonoBehaviour
    {
        public BowlingLane lane;
        public BallSpec[] balls;

        [Header("Throw")]
        public float speed = 7.5f;
        [Range(-6f, 6f)] public float angleDeg = 0f;
        [Range(-1f, 1f)] public float hook = 0.8f;

        [Header("Simple game loop")]
        [Tooltip("After each throw: rack after a strike or the 2nd ball, otherwise clear fallen pins.")]
        public bool autoPinsetter = true;
        public float pinsetterDelay = 1.5f;

        int ballInFrame = 1;
        int frame = 1;
        int lastKnocked = -1;
        string lastLine = "";
        GUIStyle style;

        void OnEnable() { if (lane != null) lane.ThrowResolved += OnResolved; }
        void OnDisable() { if (lane != null) lane.ThrowResolved -= OnResolved; }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null || lane == null) return;

            if (lane.State == LaneState.Ready)
            {
                if (k.leftArrowKey.wasPressedThisFrame) lane.PlaceBall(lane.Board + 1);
                if (k.rightArrowKey.wasPressedThisFrame) lane.PlaceBall(lane.Board - 1);
                if (k.upArrowKey.wasPressedThisFrame) speed += 0.25f;
                if (k.downArrowKey.wasPressedThisFrame) speed -= 0.25f;
                if (k.zKey.wasPressedThisFrame) angleDeg = Mathf.Clamp(angleDeg - 0.2f, -6f, 6f);
                if (k.xKey.wasPressedThisFrame) angleDeg = Mathf.Clamp(angleDeg + 0.2f, -6f, 6f);
                if (k.qKey.wasPressedThisFrame) hook = Mathf.Clamp(hook - 0.1f, -1f, 1f);
                if (k.eKey.wasPressedThisFrame) hook = Mathf.Clamp(hook + 0.1f, -1f, 1f);
                for (int i = 0; i < balls.Length && i < 3; i++)
                    if ((i == 0 && k.digit1Key.wasPressedThisFrame) || (i == 1 && k.digit2Key.wasPressedThisFrame) ||
                        (i == 2 && k.digit3Key.wasPressedThisFrame))
                        lane.CurrentBall = balls[i];
                if (k.spaceKey.wasPressedThisFrame)
                    lane.Throw(new ThrowParams { speed = speed, angleDeg = angleDeg, hook = hook });
            }

            if (lane.State == LaneState.Rolling)
                lane.Steer((k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f));

            if (k.rKey.wasPressedThisFrame) { StopAllCoroutines(); ballInFrame = 1; lane.RackAll(); }
            if (k.cKey.wasPressedThisFrame) { StopAllCoroutines(); lane.ClearFallen(); }
#endif
        }

        void OnResolved(ThrowResult r)
        {
            lastKnocked = r.knockedThisThrow;
            lastLine = r.gutter ? "Gutter" :
                r.standingAfter == 0 ? (ballInFrame == 1 ? "Strike" : "Spare") :
                $"{r.knockedThisThrow} pins";
            if (!autoPinsetter) return;

            bool frameOver = r.standingAfter == 0 || ballInFrame == 2;
            StartCoroutine(Pinsetter(frameOver));
        }

        IEnumerator Pinsetter(bool frameOver)
        {
            yield return new WaitForSeconds(pinsetterDelay);
            if (frameOver)
            {
                lane.RackAll();
                ballInFrame = 1;
                frame = frame % 10 + 1;
            }
            else
            {
                lane.ClearFallen();
                ballInFrame = 2;
            }
        }

        void OnGUI()
        {
            if (lane == null) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
            style.fontSize = Mathf.Max(12, Screen.height / 50);
            style.padding = new RectOffset(12, 12, 10, 10);

            var r = lane.LastResult;
            string text =
                $"<b>Debug thrower</b>   frame {frame} · ball {ballInFrame}\n" +
                $"State: {lane.State}\n" +
                $"Ball: {(lane.CurrentBall != null ? lane.CurrentBall.displayName : "-")}   board {lane.Board}\n" +
                $"Speed {speed:0.00} m/s   angle {angleDeg:0.0}°   hook {hook:+0.0;-0.0;0.0}\n" +
                $"Standing: {lane.StandingCount}\n" +
                (lastKnocked >= 0
                    ? $"Last: <b>{lastLine}</b>   impact {r.impactSpeed:0.0} m/s   entry {r.entryAngleDeg:0.0}°\n"
                    : "") +
                "\n←/→ board   ↑/↓ speed   Z/X angle   Q/E hook\nSpace throw   A/D steer   1/2/3 ball   R rack   C clear";

            float w = Screen.height * 0.5f;
            var size = style.CalcHeight(new GUIContent(text), w);
            GUI.Box(new Rect(Screen.width - w - 16, 16, w, size), text, style);
        }
    }
}
