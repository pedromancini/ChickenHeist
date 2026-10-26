Shader "ChickenHeist/PalmViewBackground"
{
    Properties
    {
        _MainTex("Image",2D)="black"{}
        _Linearize("Linearize",Float)=0
        _Shade("Shade",Float)=1
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque"}
        Pass
        {
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;float _Linearize,_Shade;
            CBUFFER_END
            struct Attributes{float4 position:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings vert(Attributes v){Varyings o;o.position=TransformObjectToHClip(v.position.xyz);o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;return o;}
            half4 frag(Varyings i):SV_Target
            {
                half3 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
                half3 decoded=c<=0.04045?c/12.92:pow((c+0.055)/1.055,2.4);
                c=lerp(c,decoded,_Linearize);
                return half4(c*_Shade,1);
            }
            ENDHLSL
        }
    }
}
