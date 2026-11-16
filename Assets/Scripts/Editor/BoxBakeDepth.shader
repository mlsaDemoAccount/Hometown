// Editor-only helper for Tools > Performance > Bake Box Building.
// Writes the distance from the camera (normalised to 0..1 between _Offset and _Offset+_Range) as a 16-bit value
// packed into the red + green channels. Used to compute baked ambient occlusion.
Shader "Hidden/BoxBakeDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Depth"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Offset;
                float _Range;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float eye : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 pw = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(pw);
                o.eye = -TransformWorldToView(pw).z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = saturate((i.eye - _Offset) / _Range);
                float hi = floor(d * 255.0) / 255.0;
                float lo = frac(d * 255.0);
                return half4(hi, lo, 0, 1);
            }
            ENDHLSL
        }
    }
}
