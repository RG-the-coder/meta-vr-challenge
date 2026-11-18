Shader "Battlefield/Stereo Gauntlet"
{
    Properties { _BaseColor ("Gauntlet color", Color) = (.12,.5,.61,1) }
    SubShader
    {
        // The command UI is Transparent+500, sorting order 500, and writes no depth.
        // Draw hands over it while retaining depth within each hand and the world.
        Tags { "Queue"="Transparent+600" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite On ZTest LEqual Cull Back
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half lighting = .65h + .35h * saturate(dot(normalize(input.normalWS), normalize(half3(.3, .8, -.4))));
                return half4(_BaseColor.rgb * lighting, 1);
            }
            ENDHLSL
        }
    }
}
