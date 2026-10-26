Shader "PaperDefense/NotebookAtlasUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Notebook atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "NotebookAtlasUI"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float4 _MainTex_ST, _MainTex_TexelSize;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.uv=TRANSFORM_TEX(input.uv,_MainTex);
                output.color=input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                if(_UIVertexColorAlwaysGammaSpace) output.color.rgb=UIGammaToLinear(output.color.rgb);
                #endif
                output.color*=_Color;
                float2 pixelSize=output.vertex.w;
                pixelSize/=abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 clampedRect=clamp(_ClipRect,-2e10,2e10);
                output.mask=float4(input.vertex.xy*2-clampedRect.xy-clampedRect.zw,
                    .25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                // At most two texels plus bilinear's half texel: inside the authored four-texel gutter.
                // Constant white-pixel UVs have zero derivatives, preserving vector ink exactly.
                float2 texel=abs(_MainTex_TexelSize.xy);
                float2 dx=clamp(ddx(input.uv)*.25,-texel,texel);
                float2 dy=clamp(ddy(input.uv)*.25,-texel,texel);
                fixed4 filtered=(tex2Dlod(_MainTex,float4(input.uv+dx+dy,0,0))
                    +tex2Dlod(_MainTex,float4(input.uv+dx-dy,0,0))
                    +tex2Dlod(_MainTex,float4(input.uv-dx+dy,0,0))
                    +tex2Dlod(_MainTex,float4(input.uv-dx-dy,0,0)))*.25;
                input.color.a=round(input.color.a*255)/255;
                fixed4 color=input.color*(filtered+_TextureSampleAdd);
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask=saturate((_ClipRect.zw-_ClipRect.xy-abs(input.mask.xy))*input.mask.zw);
                color.a*=mask.x*mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                color.rgb*=color.a;
                return color;
            }
            ENDCG
        }
    }
}
