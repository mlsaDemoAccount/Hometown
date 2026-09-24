using UnityEngine;

public class TruckIndicator : MonoBehaviour
{
    public Renderer indicatorRenderer;
    public float blinkSpeed = 0.5f;

    private Material mat;
    public int matIndex = 0;
    private bool state;

    void Start()
    {
        if(indicatorRenderer.sharedMaterials.Length > 1)
            mat = indicatorRenderer.sharedMaterials[matIndex];
        else
            mat = indicatorRenderer.material;
        // mat = indicatorRenderer.material[matIndex];
        // mat.EnableKeyword("_EMISSION");

        InvokeRepeating(nameof(Blink), 0f, blinkSpeed);
    }

    void Blink()
    {
        state = !state;

        if (state)
        {
        mat.EnableKeyword("_EMISSION");

            // mat.SetColor("_EmissionColor", Color.yellow * 10f);
        }
        else
        {
        mat.DisableKeyword("_EMISSION");

            // mat.SetColor("_EmissionColor", Color.white);
        }
    }
}