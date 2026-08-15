Shader "ImageToMeshAnim/Vertex Delta"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [IntRange] _AnimationCount("Animation Pose Count", Range(1, 12)) = 12
        _AnimationProgress("Animation Progress", Range(0, 1)) = 0
        _Color("Color", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "VertexDeltaUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _AnimationCount;
                float _AnimationProgress;
                float _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 keyFrame1 : NORMAL;
                float4 keyFrame2And3 : TANGENT;
                float4 uv0AndKeyFrame4 : TEXCOORD0;
                float4 keyFrame5And6 : TEXCOORD1;
                float4 keyFrame7And8 : TEXCOORD2;
                float4 keyFrame9And10 : TEXCOORD3;
                float4 keyFrame11And12 : TEXCOORD4;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                half4 color : COLOR;
                half colorWeight : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS;

                // The initial pose and all stored poses form AnimationCount + 1 equal
                // intervals. The final interval reverses the accumulated deltas so progress
                // 1 reaches the initial pose without storing it as another key frame.
                float animationProgress = saturate(_AnimationProgress) * (_AnimationCount + 1.0);
                float keyFrameProgress = min(animationProgress, _AnimationCount);
                positionOS.xy += input.keyFrame1.xy * saturate(keyFrameProgress);
                positionOS.xy += input.keyFrame2And3.xy * saturate(keyFrameProgress - 1.0);
                positionOS.xy += input.keyFrame2And3.zw * saturate(keyFrameProgress - 2.0);
                positionOS.xy += input.uv0AndKeyFrame4.zw * saturate(keyFrameProgress - 3.0);
                positionOS.xy += input.keyFrame5And6.xy * saturate(keyFrameProgress - 4.0);
                positionOS.xy += input.keyFrame5And6.zw * saturate(keyFrameProgress - 5.0);
                positionOS.xy += input.keyFrame7And8.xy * saturate(keyFrameProgress - 6.0);
                positionOS.xy += input.keyFrame7And8.zw * saturate(keyFrameProgress - 7.0);
                positionOS.xy += input.keyFrame9And10.xy * saturate(keyFrameProgress - 8.0);
                positionOS.xy += input.keyFrame9And10.zw * saturate(keyFrameProgress - 9.0);
                positionOS.xy += input.keyFrame11And12.xy * saturate(keyFrameProgress - 10.0);
                positionOS.xy += input.keyFrame11And12.zw * saturate(keyFrameProgress - 11.0);
                float returnProgress = saturate(animationProgress - _AnimationCount);
                positionOS.xy = lerp(positionOS.xy, input.positionOS.xy, returnProgress);

                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv0 = input.uv0AndKeyFrame4.xy;
                output.color = input.color;
                output.colorWeight = saturate(_Color);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv0);
                half colorWeight = input.colorWeight * input.color.a;
                color.rgb = lerp(color.rgb, input.color.rgb, colorWeight);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
