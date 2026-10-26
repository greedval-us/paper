Shader "PaperDefense/NotebookInk"
{
    Properties { _MainTex ("Notebook illustrations", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "NotebookInk"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float3 stroke : TEXCOORD0; float2 atlasUV : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 stroke : TEXCOORD0; float2 atlasUV : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.stroke = input.stroke;
                output.atlasUV = input.atlasUV;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                output.color.rgb = SRGBToLinear(output.color.rgb);
                #endif
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Analytic pixel coverage for ink strokes; filled shapes retain their original alpha.
                float pixelWidth = max(fwidth(input.stroke.y), 0.00001);
                float coverage = saturate((input.stroke.x - abs(input.stroke.y)) / pixelWidth + 0.5);
                input.color.a *= lerp(1, coverage, input.stroke.z);
                // Four samples soften dense pen hatching without using unsafe atlas mips.
                // Bound every tap to one source texel, within the four-pixel crop gutters.
                float2 dx = clamp(ddx(input.atlasUV) * .25, -_MainTex_TexelSize.xy, _MainTex_TexelSize.xy);
                float2 dy = clamp(ddy(input.atlasUV) * .25, -_MainTex_TexelSize.xy, _MainTex_TexelSize.xy);
                half4 illustration = (
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.atlasUV + dx + dy, 0) +
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.atlasUV + dx - dy, 0) +
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.atlasUV - dx + dy, 0) +
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.atlasUV - dx - dy, 0)) * .25;
                return input.color * illustration;
            }
            ENDHLSL
        }
    }
}
