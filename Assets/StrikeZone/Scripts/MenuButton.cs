using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StrikeZone
{
    /// <summary>
    /// One title-screen menu button. Hovering selects it, so mouse and keyboard
    /// share one highlight. The selected button grows a little and shows a bowling-ball marker.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [Tooltip("Scene to open when chosen. Leave empty, or use a scene not in the build list, to show the message instead.")]
        public string sceneToLoad;

        [TextArea] public string message = "Coming soon!";

        [Tooltip("Shown only while this button is selected.")]
        public GameObject marker;

        public float selectedScale = 1.06f;

        bool selected;

        void OnEnable()
        {
            if (marker != null) marker.SetActive(selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (GetComponent<Button>().interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            if (marker != null) marker.SetActive(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            if (marker != null) marker.SetActive(false);
        }

        void Update()
        {
            float target = selected ? selectedScale : 1f;
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, k);
        }
    }
}
