Shader "Hidden/Wapawapa/BloodFocusRadialGrayscale"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_LinearClamp);
            float2 _Center;
            float _Radius;
            float _Edge;
            float _Strength;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 corrected = uv - float2(0.5, 0.5);
                corrected.x *= _ScreenParams.x / _ScreenParams.y;
                float2 center = _Center - float2(0.5, 0.5);
                center.x *= _ScreenParams.x / _ScreenParams.y;
                float distanceFromHit = distance(corrected, center);
                float mask = (1.0 - smoothstep(max(0.0, _Radius - _Edge), _Radius + _Edge, distanceFromHit)) * _Strength;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half luminance = dot(source.rgb, half3(0.2126, 0.7152, 0.0722));
                source.rgb = lerp(source.rgb, luminance.xxx, saturate(mask));
                return source;
            }
            ENDHLSL
        }
    }
}
