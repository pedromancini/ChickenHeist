Shader "ChickenHeist/CrackedMirror"
{
    Properties { _Reflection("Reflection",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Reflection);SAMPLER(sampler_Reflection);
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float4 screen:TEXCOORD0;float2 uv:TEXCOORD1;};
            V vert(A input){V o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.screen=ComputeScreenPos(o.positionCS);o.uv=input.uv;return o;}
            float crackDistance(float2 p,float2 a,float2 b){float2 d=b-a;return length(p-a-d*saturate(dot(p-a,d)/dot(d,d)));}
            half4 frag(V i):SV_Target
            {
                float2 uv=i.screen.xy/i.screen.w;
                half3 color=SAMPLE_TEXTURE2D(_Reflection,sampler_Reflection,uv).rgb;
                float crack=min(crackDistance(i.uv,float2(.08,.9),float2(.32,.62)),crackDistance(i.uv,float2(.32,.62),float2(.24,.28)));
                crack=min(crack,crackDistance(i.uv,float2(.32,.62),float2(.56,.49)));
                crack=min(crack,crackDistance(i.uv,float2(.87,.12),float2(.70,.38)));
                float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
                color=lerp(color*.87,half3(.08,.10,.09),1-smoothstep(.008,.04,edge));
                color=lerp(color,half3(.28,.30,.29),1-smoothstep(.0008,.0025,crack));
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
