// Blur dual-Kawase untuk efek kaca UI (dipakai GlassBlurFeature lewat Blitter / Render Graph AddBlitPass).
Shader "Hidden/NusantaraAR/GlassBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _BlurOffset;
        float _Saturation;

        half4 Tap(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        }

        half4 UpTent(float2 uv)
        {
            float2 o = _BlitTexture_TexelSize.xy * _BlurOffset;
            half4 s = Tap(uv + float2(-o.x * 2.0, 0.0));
            s += Tap(uv + float2(-o.x, o.y)) * 2.0;
            s += Tap(uv + float2(0.0, o.y * 2.0));
            s += Tap(uv + float2(o.x, o.y)) * 2.0;
            s += Tap(uv + float2(o.x * 2.0, 0.0));
            s += Tap(uv + float2(o.x, -o.y)) * 2.0;
            s += Tap(uv + float2(0.0, -o.y * 2.0));
            s += Tap(uv + float2(-o.x, -o.y)) * 2.0;
            return s / 12.0;
        }
        ENDHLSL

        Pass
        {
            Name "Down"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float2 o = _BlitTexture_TexelSize.xy * _BlurOffset;
                half4 s = Tap(uv) * 4.0;
                s += Tap(uv - o);
                s += Tap(uv + o);
                s += Tap(uv + float2(o.x, -o.y));
                s += Tap(uv - float2(o.x, -o.y));
                return s * 0.125;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Up"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return UpTent(i.texcoord);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Final"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 c = UpTent(i.texcoord).rgb;
                half l = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = max(0.0, lerp(l.xxx, c, _Saturation));
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
