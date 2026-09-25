Shader "ChickenHeist/CinematicScreen"
{
    Properties { _BaseMap("Photo",2D)="white" {} _BaseColor("Color",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            struct Input { float4 position:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Output Vert(Input v){Output o;o.position=TransformObjectToHClip(v.position.xyz);o.uv=v.uv;return o;}
            half4 Frag(Output i):SV_Target{return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;}
            ENDHLSL
        }
    }
}
