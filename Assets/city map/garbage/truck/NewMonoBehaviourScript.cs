using UnityEngine;

public class TruckIndicator : MonoBehaviour
{
    public Renderer indicatorRenderer;
    public float blinkSpeed = 0.5f;

    private Material mat;
    private bool state;

    void Start()
    {
        mat = indicatorRenderer.material;
        mat.EnableKeyword("_EMISSION");

        InvokeRepeating(nameof(Blink), 0f, blinkSpeed);
    }

    void Blink()
    {
        state = !state;

        if (state)
        {
            mat.SetColor("_EmissionColor", Color.yellow * 10f);
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
        }
    }
}