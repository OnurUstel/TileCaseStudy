Shader "Horizontal/URP/UVorLumaToGradient_Unlit_Emissive"
{
    Properties
    {
        _MainTex ("Base (RGBA, alpha=mask)", 2D) = "white" {}
        _Tint    ("Global Tint", Color) = (1,1,1,1)

        [Toggle]_UseGradientTex ("Use Gradient Texture (1x256)", Float) = 1
        _GradientTex ("Gradient (1x256, Clamp, Bilinear)", 2D) = "white" {}

        _MapMode ("Map Mode (0=Luma,1=V,2=U)", Range(0,2)) = 1
        _Start   ("Start (0-1)", Range(0,1)) = 0.0
        _End     ("End   (0-1)", Range(0,1)) = 1.0
        [Toggle]_FlipV ("Flip (invert t)", Float) = 0
        _Gamma  ("Luminance Gamma", Range(0.25,4)) = 1.0

        // Gradient dokusu yoksa 3 renk:
        _ColorA ("Low Color",  Color) = (0.45,0.27,0.18,1)
        _ColorB ("Mid Color",  Color) = (1.00,0.72,0.32,1)
        _ColorC ("High Color", Color) = (1.00,0.90,0.60,1)
        _MidAB  ("A↔B Pos (0-1)", Range(0,1)) = 0.25
        _MidBC  ("B↔C Pos (0-1)", Range(0,1)) = 0.80
        _Smooth ("Blend Smoothing", Range(0.0001,0.2)) = 0.04

        [Toggle]_MaskByAlpha ("Mask By MainTex Alpha", Float) = 1
        _AlphaMul ("Alpha Multiplier", Range(0,2)) = 1.0

        // --- PARLAKLIK / BLOOM ---
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 1.0
        [Toggle]_SheenOn ("Sheen (Highlight Band)", Float) = 1
        _SheenPos ("Sheen Position (0=alt,1=üst)", Range(0,1)) = 0.22
        _SheenWidth ("Sheen Width", Range(0.001,0.5)) = 0.10
        _SheenIntensity ("Sheen Intensity", Range(0,4)) = 1.2
        _SheenColor ("Sheen Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags{
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "UniversalMaterialType"="Unlit"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags{ "LightMode"="SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURE2D(_GradientTex); SAMPLER(sampler_GradientTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Tint;
                float  _UseGradientTex;
                float  _MapMode, _Start, _End, _FlipV;
                float  _Gamma;
                float4 _ColorA, _ColorB, _ColorC;
                float  _MidAB, _MidBC, _Smooth;
                float  _MaskByAlpha, _AlphaMul;

                // Bloom/Sheen
                float  _BloomBoost;
                float  _SheenOn, _SheenPos, _SheenWidth, _SheenIntensity;
                float4 _SheenColor;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                OUT.color = IN.color;
                return OUT;
            }

            float luminance(float3 rgb){ return dot(rgb, float3(0.299, 0.587, 0.114)); }

            float3 map3(float t){
                float tAB = smoothstep(_MidAB - _Smooth, _MidAB + _Smooth, t);
                float tBC = smoothstep(_MidBC - _Smooth, _MidBC + _Smooth, t);
                float3 AB = lerp(_ColorA.rgb, _ColorB.rgb, tAB);
                float3 BC = lerp(_ColorB.rgb, _ColorC.rgb, tBC);
                return lerp(AB, BC, step(_MidBC, t));
            }

            half4 frag(Varyings IN):SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // 0..1 t: Luma / Vertical / Horizontal
                float t;
                if (_MapMode < 0.5) {
                    t = saturate(pow(luminance(baseCol.rgb), _Gamma));
                } else if (_MapMode < 1.5) {
                    t = IN.uv.y;
                } else {
                    t = IN.uv.x;
                }

                float denom = max(1e-5, (_End - _Start));
                t = saturate((t - _Start) / denom);
                if (_FlipV > 0.5) t = 1.0 - t;

                float3 grad = (_UseGradientTex > 0.5)
                    ? SAMPLE_TEXTURE2D(_GradientTex, sampler_GradientTex, float2(t, 0.5)).rgb
                    : map3(t);

                // Sheen: gaussian bant (UV eksenine göre)
                float sheen = 0;
                if (_SheenOn > 0.5)
                {
                    float axis = ( _MapMode < 1.5 ? IN.uv.y : IN.uv.x );
                    float x = (axis - _SheenPos) / max(1e-3, _SheenWidth);
                    sheen = exp(-x*x);                      // 0..1
                }

                float3 rgb = grad + _SheenColor.rgb * (_SheenIntensity * sheen);
                rgb *= _Tint.rgb * IN.color.rgb;

                // HDR çıkış: Bloom’u tetiklemek için >1.0
                rgb *= _BloomBoost;

                float a = (_MaskByAlpha > 0.5) ? baseCol.a : 1.0;
                half4 col;
                col.rgb = rgb;
                col.a   = a * _AlphaMul * _Tint.a * IN.color.a;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
