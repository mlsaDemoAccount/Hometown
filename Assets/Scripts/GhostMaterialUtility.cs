using UnityEngine;
using UnityEngine.Rendering;

/// Creates a transparent INSTANCE copy of a material (never modifies the original asset).
/// Handles both URP Lit/Unlit (_BaseColor, _Surface) and Built-in Standard (_Color, _Mode) -
/// the two most common shader setups. A fully custom shader may not respond to these property
/// names; if a ghost building doesn't visibly go transparent, that's why - check what shader
/// its materials actually use.
public static class GhostMaterialUtility
{
    public static Material MakeTransparentInstance(Material source, float alpha)
    {
        var mat = new Material(source); // instance copy - safe to modify without touching the original asset

        if (mat.HasProperty("_BaseColor"))
        {
            // URP Lit / Unlit
            Color c = mat.GetColor("_BaseColor");
            c.a = alpha;
            mat.SetColor("_BaseColor", c);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // 1 = Transparent
                mat.SetFloat("_Blend", 0f);   // 0 = Alpha blend
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
        }
        else if (mat.HasProperty("_Color"))
        {
            // Built-in Standard
            Color c = mat.GetColor("_Color");
            c.a = alpha;
            mat.SetColor("_Color", c);

            mat.SetFloat("_Mode", 3f); // 3 = Transparent
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        // else: unrecognized shader - color/transparency won't visibly change, but no error either.

        return mat;
    }
}
