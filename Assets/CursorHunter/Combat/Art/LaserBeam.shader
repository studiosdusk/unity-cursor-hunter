Shader "CursorHunter/LaserBeam"
{
    Properties { _Feather("Edge feather", Range(0.01, 0.5)) = 0.16 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Feather;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half edge = smoothstep(0, _Feather, input.uv.y) * (1 - smoothstep(1 - _Feather, 1, input.uv.y));
                half endFade = smoothstep(0, 0.025, input.uv.x) * (1 - smoothstep(0.975, 1, input.uv.x));
                return half4(input.color.rgb, input.color.a * edge * endFade);
            }
            ENDHLSL
        }
    }
}
