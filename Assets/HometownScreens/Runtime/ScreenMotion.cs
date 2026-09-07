using System.Collections;
using UnityEngine;

namespace Hometown.Screens
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenMotion : MonoBehaviour
    {
        public RectTransform content;
        public float duration = .32f;
        CanvasGroup group;
        void OnEnable() { group = GetComponent<CanvasGroup>(); StartCoroutine(Animate(true)); }
        public void Close() { StopAllCoroutines(); StartCoroutine(Animate(false)); }
        IEnumerator Animate(bool opening)
        {
            group.interactable = false; group.blocksRaycasts = true;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0, 1, t / duration);
                float a = opening ? p : 1-p;
                group.alpha = a;
                if (content) content.localScale = Vector3.one * Mathf.Lerp(.93f, 1, a);
                yield return null;
            }
            group.alpha = opening ? 1 : 0;
            if (content) content.localScale = Vector3.one;
            group.interactable = opening;
            group.blocksRaycasts = opening;
            if (!opening) gameObject.SetActive(false);
        }
        void OnDisable() { StopAllCoroutines(); if (content) content.localScale = Vector3.one; }
    }
}
