Shader "ChickenHeist/RuralDirt"
{
    Properties { _BaseMap("Soil", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float3 world:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Input v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.vertex.xyz);
                o.position=TransformWorldToHClip(o.world);
                o.uv=v.uv; o.color=v.color; o.fog=ComputeFogFactor(o.position.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 soil=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                half grain=dot(soil,half3(0.3,0.59,0.11));
                // Retain granular soil detail without a near-black, asphalt-like base.
                half3 albedo=half3(0.36,0.245,0.135)*(0.72+grain*1.7)*i.color.rgb;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 light=SampleSH(half3(0,1,0))+sun.color*saturate(sun.direction.y)*sun.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                for (uint index=0;index<GetAdditionalLightsCount();index++)
                {
                    Light lamp=GetAdditionalLight(index,i.world);
                    light+=lamp.color*saturate(lamp.direction.y)*lamp.distanceAttenuation*lamp.shadowAttenuation;
                }
                #endif
                return half4(MixFog(albedo*light,i.fog),1);
            }
            ENDHLSL
        }
    }
}
