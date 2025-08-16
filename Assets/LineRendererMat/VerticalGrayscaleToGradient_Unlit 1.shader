Shader "Horizontal/URP/UVorLumaToGradient_Unlit_EdgeShadow"
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

        // --- İKİ TARAFTAN GÖLGE ---
        [Toggle]_EdgeShadowOn ("Edge Shadow On", Float) = 1
        _EdgeAxis   ("Edge Axis (0=Vertical,1=Horizontal)", Range(0,1)) = 0
        _EdgeWidth  ("Edge Width", Range(0.001,0.5)) = 0.10
        _EdgeInset  ("Edge Inset", Range(0,0.45)) = 0.02
        _EdgeStrength ("Edge Strength", Range(0,1)) = 0.75
        _EdgeTint   ("Edge Tint (multiplier)", Color) = (0.75,0.60,0.50,1)
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
            Tags{ "LightMode"="SRPDefaultUnlit" } // URP 2D Renderer

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
                float  _MapMode, _Start, _End, _FlipV, _Gamma;
                float4 _ColorA, _ColorB, _ColorC;
                float  _MidAB, _MidBC, _Smooth;
                float  _MaskByAlpha, _AlphaMul;

                // Edge shadow
                float  _EdgeShadowOn, _EdgeAxis, _EdgeWidth, _EdgeInset, _EdgeStrength;
                float4 _EdgeTint;
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

                // 0..1 t: Luma / Vertical / Horizontal (renk için)
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

                // Renk/tint
                float3 rgb = grad * _Tint.rgb * IN.color.rgb;

                // --- Kenarlardan gölge ---
                if (_EdgeShadowOn > 0.5)
                {
                    float axis = (_EdgeAxis < 0.5) ? IN.uv.y : IN.uv.x; // 0=V,1=H
                    // inset uygulanmış eksen [0..1]
                    float denomAxis = max(1e-5, 1.0 - 2.0*_EdgeInset);
                    float aN = saturate( (axis - _EdgeInset) / denomAxis );

                    float w = max(1e-3, _EdgeWidth);
                    // iki kenardan gauss benzeri düşüş
                    float e0 = exp( - (aN*aN) / (w*w) );
                    float e1 = exp( - ((1.0 - aN)*(1.0 - aN)) / (w*w) );
                    float edge = saturate(e0 + e1); // 0..1 civarı

                    // tint ve koyulaştırma
                    float3 tinted = rgb * _EdgeTint.rgb;
                    rgb = lerp(rgb, tinted, edge * _EdgeStrength);
                }

                float a = (_MaskByAlpha > 0.5) ? baseCol.a : 1.0;
                return half4(rgb, a * _AlphaMul * _Tint.a * IN.color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
