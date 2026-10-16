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
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
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
                return input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.atlasUV);
            }
            ENDHLSL
        }
    }
}
