using UnityEngine;

namespace Hometown.Screens
{
    [ExecuteAlways]
    public class SafeScreen : MonoBehaviour
    {
        public RectTransform safeArea;
        public RectTransform design;
        void LateUpdate()
        {
            if (!safeArea || !design || Screen.width == 0 || Screen.height == 0) return;
            Rect r = Application.isPlaying ? Screen.safeArea : new Rect(0,0,Screen.width,Screen.height);
            safeArea.anchorMin = new Vector2(r.xMin / Screen.width, r.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(r.xMax / Screen.width, r.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            float scale = Mathf.Min(safeArea.rect.width / 1600f, safeArea.rect.height / 900f);
            design.localScale = Vector3.one * Mathf.Max(.01f, scale);
        }
    }
}
