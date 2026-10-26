Shader "ChickenHeist/PalmViewOccluder"
{
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" "RenderType"="Opaque"}
        Pass
        {
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 position:POSITION):SV_POSITION{return TransformObjectToHClip(position.xyz);}
            half4 frag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
