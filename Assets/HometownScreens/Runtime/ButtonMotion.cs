using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Hometown.Screens
{
    [RequireComponent(typeof(Button))]
    public class ButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Vector3 original; float target = 1; Button button;
        void Awake() { original = transform.localScale; button = GetComponent<Button>(); button.onClick.AddListener(Click); }
        void Click() { var screen = GetComponentInParent<ReferenceScreen>(); if (screen) screen.PlayClick(); }
        void Update() { transform.localScale = Vector3.Lerp(transform.localScale, original * target, 1-Mathf.Exp(-22 * Time.unscaledDeltaTime)); }
        public void OnPointerEnter(PointerEventData e) { if (button.IsInteractable()) target = 1.035f; }
        public void OnPointerExit(PointerEventData e) { target = 1; }
        public void OnPointerDown(PointerEventData e) { if (button.IsInteractable()) target = .95f; }
        public void OnPointerUp(PointerEventData e) { target = 1; }
        void OnDisable() { target = 1; transform.localScale = original; }
    }
}
