using UnityEngine;
namespace Hometown.Screens
{
    public class IdleMotion : MonoBehaviour
    {
        public float amplitude = 4;
        public bool pulse;
        Vector3 position, scale;
        void Awake() { position = transform.localPosition; scale = transform.localScale; }
        void Update() { float wave = Mathf.Sin(Time.unscaledTime * 2); if (pulse) transform.localScale = scale * (1 + wave * .025f); else transform.localPosition = position + Vector3.up * wave * amplitude; }
        void OnDisable() { transform.localPosition = position; transform.localScale = scale; }
    }
}
