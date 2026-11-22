using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StrikeZone
{
    /// <summary>
    /// Goes back to the title screen when the player presses Esc (or Start on a gamepad).
    /// </summary>
    public class ReturnToTitle : MonoBehaviour
    {
        public string titleScene = "TitleScreen";

        void Update()
        {
            if (!BackPressed()) return;

            if (Application.CanStreamedLevelBeLoaded(titleScene))
                SceneManager.LoadScene(titleScene);
            else
                Debug.LogWarning($"Strike Zone: '{titleScene}' isn't in the build scene list, so Esc can't open it.");
        }

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) return true;
            var g = Gamepad.current;
            return g != null && g.startButton.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
