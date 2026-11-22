using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StrikeZone
{
    /// <summary>
    /// Runs the title screen menu: wires up the buttons, keeps keyboard/gamepad
    /// navigation alive, and shows a short message ("toast") when a button has
    /// nothing to open yet.
    /// </summary>
    public class TitleScreenController : MonoBehaviour
    {
        [Tooltip("Menu buttons, top to bottom. The first one is selected when the screen opens.")]
        public MenuButton[] buttons;

        [Header("Toast message")]
        public CanvasGroup toast;
        public TMP_Text toastText;
        public float toastSeconds = 2.2f;

        Coroutine toastRoutine;
        GameObject lastSelected;

        void Start()
        {
            foreach (var b in buttons)
            {
                var menuButton = b;
                menuButton.GetComponent<Button>().onClick.AddListener(() => Choose(menuButton));
            }

            if (toast != null) toast.alpha = 0f;

            if (buttons.Length > 0 && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
        }

        void Update()
        {
            var es = EventSystem.current;
            if (es == null || buttons.Length == 0) return;

            if (es.currentSelectedGameObject != null)
            {
                lastSelected = es.currentSelectedGameObject;
                return;
            }

            // Clicking empty space clears the selection; bring it back on the next key press.
            if (NavigationPressed())
                es.SetSelectedGameObject(lastSelected != null ? lastSelected : buttons[0].gameObject);
        }

        static bool NavigationPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                              k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame ||
                              k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
                return true;
            var g = Gamepad.current;
            return g != null && (g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame ||
                                 g.buttonSouth.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                   Input.GetKeyDown(KeyCode.Return);
#endif
        }

        public void Choose(MenuButton button)
        {
            if (!string.IsNullOrEmpty(button.sceneToLoad) && Application.CanStreamedLevelBeLoaded(button.sceneToLoad))
            {
                SceneManager.LoadScene(button.sceneToLoad);
                return;
            }
            ShowToast(button.message);
        }

        public void ShowToast(string message)
        {
            if (toast == null || string.IsNullOrEmpty(message)) return;
            if (toastText != null) toastText.text = message;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            yield return Fade(1f, 0.2f);
            yield return new WaitForSecondsRealtime(toastSeconds);
            yield return Fade(0f, 0.3f);
            toastRoutine = null;
        }

        IEnumerator Fade(float target, float seconds)
        {
            float start = toast.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                toast.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            toast.alpha = target;
        }
    }
}
