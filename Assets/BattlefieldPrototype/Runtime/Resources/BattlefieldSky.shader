Shader "Battlefield/Hazy Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x),
                    lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }
            float cloudNoise(float2 p)
            {
                return noise(p) * .56 + noise(p * 2.03 + 11.7) * .27 + noise(p * 4.09 - 3.4) * .12 + noise(p * 8.13 + 7.1) * .05;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.direction);
                float h = saturate(d.y);
                float3 horizon = float3(.72, .84, .88);
                float3 zenith = float3(.32, .61, .84);
                float3 color = lerp(horizon, zenith, pow(h, .55));
                float3 sun = normalize(float3(.459, .5, -.734));
                float alignment = saturate(dot(d, sun));
                color += float3(.30, .16, .045) * pow(alignment, 9) * .35;
                color += float3(.26, .18, .09) * pow(alignment, 90) * .5;
                // Project the sky onto a high cloud layer. Broad banks and fine streaks
                // retain depth at the horizon without a cubemap or animated noise texture.
                float2 cloudUV = d.xz / max(d.y + .13, .08) * 1.7 + float2(9.3, 3.7);
                float field = cloudNoise(cloudUV * float2(.9, 1.7));
                float density = smoothstep(.48, .72, field) * smoothstep(.015, .14, d.y) * .82;
                float light = saturate(.64 + alignment * .25 + (field - cloudNoise(cloudUV + float2(.18, -.12))) * 2.2);
                float3 cloud = lerp(float3(.78, .85, .91), float3(1, .98, .91), light);
                color = lerp(color, cloud, density);
                float sunDisc = smoothstep(.99960, .99978, alignment) * (1 - density * .85);
                color = lerp(color, float3(1, .94, .79), sunDisc);
                color = lerp(float3(.55, .67, .45), color, smoothstep(-.22, .01, d.y));
                return float4(color, 1);
            }
            ENDCG
        }
    }
}
